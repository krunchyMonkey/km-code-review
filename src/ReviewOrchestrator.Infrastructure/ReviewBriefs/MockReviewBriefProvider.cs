using ReviewOrchestrator.Core;
using ReviewOrchestrator.Core.Domain;
using ReviewOrchestrator.Core.Engines;

namespace ReviewOrchestrator.Infrastructure.ReviewBriefs;

/// <summary>
/// Deterministic stand-in for an LLM-generated review brief. Uses only file paths, churn, labels and PR flags,
/// so the same PR always yields the same brief and no API key is needed.
/// </summary>
public sealed class MockReviewBriefProvider(TimeProvider timeProvider) : IReviewBriefProvider
{
    private const int MaxRecommendedFiles = 5;

    public Task<ReviewBrief> GenerateAsync(PullRequest pullRequest, CancellationToken cancellationToken)
    {
        var files = pullRequest.Files.Select(f => (File: f, Category: FileClassifier.Categorize(f))).ToList();
        var production = files.Where(x => x.Category is not (FileCategory.Generated or FileCategory.Test or FileCategory.Documentation)).ToList();

        var brief = new ReviewBrief
        {
            Id = Guid.NewGuid(),
            PullRequestId = pullRequest.Id,
            GeneratedAt = timeProvider.GetUtcNow(),
            WhatChanged = WhatChanged(pullRequest, files, production),
            HumanAttentionAreas = AttentionAreas(pullRequest, files, production),
            MostlyMechanical = Mechanical(files),
            RecommendedReviewOrder = ReviewOrder(files),
        };
        return Task.FromResult(brief);
    }

    private static List<string> WhatChanged(
        PullRequest pr,
        List<(PullRequestFile File, FileCategory Category)> files,
        List<(PullRequestFile File, FileCategory Category)> production)
    {
        var items = new List<string> { $"{TrimTitle(pr.Title)}." };

        var core = production
            .Where(x => x.Category is FileCategory.Backend or FileCategory.Frontend or FileCategory.Other)
            .OrderByDescending(x => Churn(x.File))
            .FirstOrDefault();
        if (core.File is not null)
            items.Add($"Main logic change in {Name(core.File)} ({Delta(core.File)}).");

        AddIfAny(items, files, FileCategory.Auth, n => $"Modified authentication / security code in {n}.");
        AddIfAny(items, files, FileCategory.Migration, n => $"Added or changed database migrations ({n}).");
        AddIfAny(items, files, FileCategory.Infrastructure, n => $"Updated infrastructure definitions ({n}).");
        AddIfAny(items, files, FileCategory.Persistence, n => $"Modified persistence logic in {n}.");
        AddIfAny(items, files, FileCategory.Api, n => $"Changed request handling in {n}.");

        var contracts = files.Where(x => x.Category == FileCategory.Contract).ToList();
        if (contracts.Count > 0)
            items.Add(contracts.Count == 1
                ? $"Updated one API contract type ({Name(contracts[0].File)})."
                : $"Updated {contracts.Count} API contract types.");

        if (pr.Labels.Any(l => l.Contains("state", StringComparison.OrdinalIgnoreCase)))
            items.Add("Updated state management for the affected flow.");

        AddIfAny(items, files, FileCategory.Telemetry, n => $"Added or changed telemetry in {n}.");
        AddIfAny(items, files, FileCategory.Dependency, n => $"Changed dependency manifests ({n}).");

        var frontend = files.Count(x => x.Category == FileCategory.Frontend);
        if (frontend > 3) items.Add($"Touched {frontend} UI files.");

        var docs = files.Count(x => x.Category == FileCategory.Documentation);
        if (docs > 0 && docs == files.Count) items.Add($"Documentation-only change across {docs} pages.");

        var tests = files.Count(x => x.Category == FileCategory.Test);
        if (tests > 0) items.Add($"Added or updated {tests} test file{(tests == 1 ? "" : "s")}.");

        return items.Distinct().Take(6).ToList();
    }

    private static List<string> AttentionAreas(
        PullRequest pr,
        List<(PullRequestFile File, FileCategory Category)> files,
        List<(PullRequestFile File, FileCategory Category)> production)
    {
        var items = new List<string>();

        if (pr.IsReleaseBlocker)
            items.Add("Release blocker — confirm the fix is minimal and safe to ship in the release branch.");
        if (pr.IsProductionFix)
            items.Add("Production fix — check that it addresses the root cause and has a regression test.");

        foreach (var (file, _) in files.Where(x => x.Category == FileCategory.Auth).Take(2))
            items.Add($"Security-sensitive change in {Name(file)} — verify token, permission and error handling paths.");

        if (files.Any(x => x.Category == FileCategory.Migration))
            items.Add("Schema migration — verify backward compatibility, rollback and data backfill.");

        foreach (var (file, _) in files.Where(x => x.Category == FileCategory.Infrastructure).Take(1))
            items.Add($"Infrastructure change in {Name(file)} — confirm environment parity and secret handling.");

        var persistence = files.Where(x => x.Category == FileCategory.Persistence).OrderByDescending(x => Churn(x.File)).FirstOrDefault();
        if (persistence.File is not null)
            items.Add($"Changes persistence behavior in {Name(persistence.File)} — check transactions and concurrent writes.");

        var api = files.Where(x => x.Category == FileCategory.Api).OrderByDescending(x => Churn(x.File)).FirstOrDefault();
        if (api.File is not null)
            items.Add($"Request handling changed in {Name(api.File)} — check failure handling and retries.");

        if (pr.Labels.Any(l => l.Contains("state", StringComparison.OrdinalIgnoreCase)))
            items.Add("State can now change through more than one path — check every place that sets or clears it.");

        if (pr.Labels.Any(l => l.Contains("cache", StringComparison.OrdinalIgnoreCase)))
            items.Add("Cache behavior changed — look for stale reads and invalidation ordering.");

        if (pr.Labels.Any(l => l.Contains("performance", StringComparison.OrdinalIgnoreCase)))
            items.Add("Performance-motivated change — confirm behavior is unchanged under concurrency.");

        if (files.Any(x => x.Category == FileCategory.Contract))
            items.Add("API contract changed — confirm clients tolerate the new or changed fields.");

        var largest = production.OrderByDescending(x => Churn(x.File)).FirstOrDefault();
        if (largest.File is not null && Churn(largest.File) >= 80 && items.Count < 5)
            items.Add($"Most of the logic is concentrated in {Name(largest.File)} ({Delta(largest.File)}).");

        if (production.Count > 0 && !files.Any(x => x.Category == FileCategory.Test))
            items.Add("No test changes accompany the production changes.");

        if (items.Count == 0)
            items.Add("No high-attention areas detected — a quick read should be enough.");

        return items.Distinct().Take(5).ToList();
    }

    private static List<string> Mechanical(List<(PullRequestFile File, FileCategory Category)> files)
    {
        var items = new List<string>();

        var contracts = files.Where(x => x.Category == FileCategory.Contract).ToList();
        if (contracts.Count > 0) items.Add($"DTO / contract changes ({Names(contracts)})");

        var tests = files.Where(x => x.Category == FileCategory.Test).ToList();
        if (tests.Count > 0) items.Add($"Test updates ({tests.Count} file{(tests.Count == 1 ? "" : "s")})");

        var options = files.Where(x => FileClassifier.FileName(x.File.Path).EndsWith("Options.cs", StringComparison.OrdinalIgnoreCase)
                                       || x.File.Path.Contains("/Configuration/", StringComparison.OrdinalIgnoreCase)).ToList();
        if (options.Count > 0) items.Add($"Options / DI registration ({Names(options)})");

        var generated = files.Where(x => x.Category == FileCategory.Generated).ToList();
        if (generated.Count > 0) items.Add($"Generated or lock files ({Names(generated)}) — excluded from review surface");

        var docs = files.Where(x => x.Category == FileCategory.Documentation).ToList();
        if (docs.Count > 0) items.Add($"Documentation ({docs.Count} file{(docs.Count == 1 ? "" : "s")})");

        var deletionsOnly = files.Where(x => x.File.Additions == 0 && x.File.Deletions > 0 && x.Category != FileCategory.Generated).ToList();
        if (deletionsOnly.Count > 0) items.Add($"Pure deletions ({Names(deletionsOnly)})");

        if (files.Count(x => x.Category == FileCategory.Frontend) > 5) items.Add("Styling and import updates across components");

        if (items.Count == 0) items.Add("Nothing identified as purely mechanical");
        return items;
    }

    private static List<ReviewFileRecommendation> ReviewOrder(List<(PullRequestFile File, FileCategory Category)> files)
    {
        // Security-sensitive files always make the list; the rest are ranked by category weight then churn.
        var ranked = files
            .Where(x => x.Category is not (FileCategory.Generated or FileCategory.Test or FileCategory.Documentation or FileCategory.Contract)
                        || FileClassifier.IsSecuritySensitive(x.File))
            .OrderByDescending(x => CategoryWeight(x.Category))
            .ThenByDescending(x => Churn(x.File))
            .ThenBy(x => x.File.Path, StringComparer.Ordinal)
            .ToList();

        var mustInclude = ranked.Where(x => x.Category == FileCategory.Auth).ToList();
        var selected = mustInclude.Concat(ranked.Except(mustInclude)).Take(Math.Max(MaxRecommendedFiles, mustInclude.Count)).ToList();

        // If the PR has no production files (docs-only, tests-only), recommend the largest files instead.
        if (selected.Count == 0)
            selected = files.Where(x => x.Category != FileCategory.Generated).OrderByDescending(x => Churn(x.File)).Take(3).ToList();

        return selected
            .Select((x, i) => new ReviewFileRecommendation { Rank = i + 1, Path = x.File.Path, Reason = ReasonFor(x.File, x.Category) })
            .ToList();
    }

    /// <summary>High-risk categories are pinned to the top; everything else is ordered purely by churn.</summary>
    private static int CategoryWeight(FileCategory category) => category switch
    {
        FileCategory.Auth => 3,
        FileCategory.Migration => 2,
        FileCategory.Infrastructure => 1,
        _ => 0
    };

    private static string ReasonFor(PullRequestFile file, FileCategory category)
    {
        var name = FileClassifier.FileName(file.Path);
        var stem = Path.GetFileNameWithoutExtension(name);
        return category switch
        {
            FileCategory.Auth => "Security-sensitive: authentication / authorization logic",
            FileCategory.Migration => "Schema migration — verify rollback and data impact",
            FileCategory.Infrastructure => "Infrastructure definition",
            FileCategory.Persistence => "Changes to persistence behavior",
            FileCategory.Api => "Request handling and API surface",
            FileCategory.Telemetry => "Telemetry emission",
            FileCategory.Dependency => "Dependency manifest",
            FileCategory.Documentation => "Documentation content",
            FileCategory.Test => "Test coverage for the change",
            _ when stem.EndsWith("Coordinator") || stem.EndsWith("Service") || stem.EndsWith("Executor") || stem.EndsWith("Resolver") =>
                $"Core logic for {Humanize(stem)}",
            _ when stem.Contains("State") => "State-management updates",
            _ when stem.EndsWith("Middleware") => "Request pipeline behavior",
            _ when stem.EndsWith("Validator") => "Validation rules",
            _ when category == FileCategory.Frontend => "UI component change",
            _ => $"Largest remaining production change ({Delta(file)})"
        };
    }

    private static void AddIfAny(List<string> items, List<(PullRequestFile File, FileCategory Category)> files, FileCategory category, Func<string, string> text)
    {
        var matching = files.Where(x => x.Category == category).ToList();
        if (matching.Count > 0) items.Add(text(Names(matching)));
    }

    private static string Names(List<(PullRequestFile File, FileCategory Category)> files)
    {
        var names = files.OrderByDescending(x => Churn(x.File)).Take(2).Select(x => Name(x.File)).ToList();
        return files.Count > 2 ? $"{string.Join(", ", names)} +{files.Count - 2} more" : string.Join(", ", names);
    }

    private static string Name(PullRequestFile file) => FileClassifier.FileName(file.Path);
    private static int Churn(PullRequestFile file) => file.Additions + file.Deletions;
    private static string Delta(PullRequestFile file) => $"+{file.Additions} / −{file.Deletions}";
    private static string TrimTitle(string title) => title.TrimEnd('.', ' ');

    private static string Humanize(string pascal)
    {
        var words = System.Text.RegularExpressions.Regex.Split(pascal, "(?<!^)(?=[A-Z])");
        return string.Join(' ', words).ToLowerInvariant();
    }
}
