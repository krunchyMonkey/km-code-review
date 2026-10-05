using ReviewOrchestrator.Core.Domain;

namespace ReviewOrchestrator.Core.Engines;

public sealed record RiskSignal(bool IsWarning, string Text);

public sealed record RiskAssessment(
    ReviewRisk Level,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> AreasTouched,
    IReadOnlyList<RiskSignal> Signals,
    IReadOnlyList<string> RiskAreas);

/// <summary>Deterministic risk tiering (spec §10).</summary>
public sealed class RiskEngine(ReviewSurfaceEvaluator surfaceEvaluator)
{
    private static readonly string[] HighRiskLabels = ["security", "auth", "permission", "infra", "migration"];
    private static readonly string[] PersistenceLabels = ["database", "persistence", "storage"];

    public const int MediumRiskLineThreshold = 500;
    public const int MediumRiskProductionFileThreshold = 10;

    public RiskEngine() : this(new ReviewSurfaceEvaluator())
    {
    }

    public RiskAssessment Assess(PullRequest pr)
    {
        var high = new List<string>();
        var medium = new List<string>();
        var riskAreas = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        var labels = pr.Labels.Select(l => l.ToLowerInvariant()).ToList();
        foreach (var keyword in HighRiskLabels)
        {
            var label = labels.FirstOrDefault(l => l.Contains(keyword));
            if (label is null) continue;
            high.Add($"Label '{label}' indicates a {keyword}-sensitive change");
            riskAreas.Add(MapRiskArea(keyword));
        }

        var production = pr.Files.Where(FileClassifier.IsProductionFile).ToList();

        var authFiles = pr.Files.Where(f => !FileClassifier.IsTest(f) && FileClassifier.IsSecuritySensitive(f)).ToList();
        if (authFiles.Count > 0)
        {
            high.Add($"Touches security-sensitive paths ({Describe(authFiles)})");
            foreach (var f in authFiles)
                riskAreas.Add(FileClassifier.Normalize(f.Path).Contains("/permissions/") ? "permissions" : "auth");
            riskAreas.Add("security");
        }

        var migrationFiles = pr.Files.Where(f => FileClassifier.IsMigrationPath(f.Path)).ToList();
        if (migrationFiles.Count > 0)
        {
            high.Add($"Includes data migrations ({Describe(migrationFiles)})");
            riskAreas.Add("database");
        }

        var infraFiles = pr.Files.Where(f => FileClassifier.IsInfrastructurePath(f.Path)).ToList();
        if (infraFiles.Any(f => FileClassifier.Normalize(f.Path).Contains("/terraform/")))
        {
            high.Add($"Changes Terraform infrastructure ({Describe(infraFiles)})");
            riskAreas.Add("infra");
        }

        if (pr.IsSecuritySensitive)
        {
            high.Add("PR is flagged as security-sensitive");
            riskAreas.Add("security");
        }

        // Medium signals.
        var persistenceFiles = production.Where(f => FileClassifier.IsPersistencePath(f.Path)).ToList();
        var hasPersistence = persistenceFiles.Count > 0 || labels.Any(l => PersistenceLabels.Contains(l));
        if (hasPersistence)
        {
            medium.Add(persistenceFiles.Count > 0
                ? $"Changes persistence behavior ({Describe(persistenceFiles)})"
                : "Labelled as a persistence/database change");
            riskAreas.Add("database");
        }

        var contractFiles = production.Where(f => FileClassifier.IsContractPath(f.Path) || FileClassifier.IsApiPath(f.Path)).ToList();
        var hasApi = contractFiles.Count > 0 || labels.Contains("api-change") || labels.Contains("breaking-change");
        if (hasApi)
            medium.Add(contractFiles.Count > 0 ? $"Changes the API contract ({Describe(contractFiles)})" : "Labelled as an API contract change");

        var backendFiles = production
            .Where(f => FileClassifier.IsBackendCode(f.Path) && !FileClassifier.IsTelemetryPath(f.Path) && !FileClassifier.IsContractPath(f.Path))
            .ToList();
        var hasBackend = backendFiles.Count > 0 || labels.Contains("backend");
        if (hasBackend)
            medium.Add(backendFiles.Count > 0 ? $"Changes backend behavior in {backendFiles.Count} file(s)" : "Labelled as a backend change");

        var meaningful = surfaceEvaluator.MeaningfulChangedLines(pr);
        if (meaningful > MediumRiskLineThreshold)
            medium.Add($"{meaningful:N0} meaningful lines changed (> {MediumRiskLineThreshold})");

        if (production.Count > MediumRiskProductionFileThreshold)
            medium.Add($"{production.Count} production files changed (> {MediumRiskProductionFileThreshold})");

        var level = high.Count > 0 ? ReviewRisk.High : medium.Count > 0 ? ReviewRisk.Medium : ReviewRisk.Low;
        var reasons = level switch
        {
            ReviewRisk.High => high.Concat(medium).ToList(),
            ReviewRisk.Medium => medium,
            _ => [LowRiskReason(pr)]
        };

        var areas = AreasTouched(pr, hasBackend, hasApi, hasPersistence, authFiles.Count > 0 || labels.Any(l => l.Contains("auth") || l.Contains("security")),
            migrationFiles.Count > 0, infraFiles.Count > 0 || labels.Any(l => l.Contains("infra")));

        var signals = new List<RiskSignal>
        {
            authFiles.Count == 0 && !labels.Any(l => l.Contains("auth"))
                ? new RiskSignal(false, "No authentication changes")
                : new RiskSignal(true, "Authentication / authorization code changed"),
            authFiles.Count == 0 && !pr.IsSecuritySensitive
                ? new RiskSignal(false, "No security-sensitive files")
                : new RiskSignal(true, "Security-sensitive files require a qualified reviewer"),
        };
        if (migrationFiles.Count > 0) signals.Add(new RiskSignal(true, "Schema migration — verify rollback path"));
        else if (!hasPersistence) signals.Add(new RiskSignal(false, "No persistence changes"));
        if (level != ReviewRisk.Low) signals.Add(new RiskSignal(true, "Requires component-owner review"));
        if (production.Count == 0) signals.Add(new RiskSignal(false, "No production code changed"));
        if (pr.Files.Any(FileClassifier.IsTest) && production.Count > 0) signals.Add(new RiskSignal(false, "Includes test changes"));

        return new RiskAssessment(level, reasons, areas, signals, riskAreas.ToList());
    }

    private static string LowRiskReason(PullRequest pr)
    {
        var production = pr.Files.Where(FileClassifier.IsProductionFile).ToList();
        if (pr.Files.Count > 0 && pr.Files.All(f => FileClassifier.IsTest(f) || FileClassifier.IsGenerated(f)))
            return "Tests / generated files only";
        if (pr.Files.Count > 0 && pr.Files.All(f => FileClassifier.IsDocumentation(f.Path) || FileClassifier.IsGenerated(f)))
            return "Documentation only";
        if (production.Count > 0 && production.All(f => FileClassifier.IsTelemetryPath(f.Path)))
            return "Telemetry-only change";
        return "No high- or medium-risk signals detected";
    }

    private static List<string> AreasTouched(PullRequest pr, bool backend, bool api, bool persistence, bool auth, bool migration, bool infra)
    {
        var areas = new List<string>();
        if (auth) areas.Add("Authentication & Security");
        if (migration) areas.Add("Data Migrations");
        if (infra) areas.Add("Infrastructure");
        if (backend) areas.Add("Backend Logic");
        if (api) areas.Add("API Changes");
        if (persistence) areas.Add("Data Persistence");
        if (pr.Files.Any(f => FileClassifier.IsProductionFile(f) && FileClassifier.Categorize(f) == FileCategory.Frontend)) areas.Add("Frontend / UI");
        if (pr.Files.Any(f => FileClassifier.IsTelemetryPath(f.Path))) areas.Add("Telemetry");
        if (pr.Files.Any(f => FileClassifier.IsDependencyManifest(f.Path))) areas.Add("Dependencies");
        if (pr.Files.Any(f => FileClassifier.IsDocumentation(f.Path))) areas.Add("Documentation");
        if (pr.Files.Any(FileClassifier.IsTest)) areas.Add("Tests");
        return areas;
    }

    private static string MapRiskArea(string keyword) => keyword switch
    {
        "permission" => "permissions",
        "migration" => "database",
        _ => keyword
    };

    private static string Describe(IReadOnlyCollection<PullRequestFile> files)
    {
        var names = files.Take(2).Select(f => FileClassifier.FileName(f.Path)).ToList();
        return files.Count > 2 ? $"{string.Join(", ", names)} +{files.Count - 2} more" : string.Join(", ", names);
    }
}
