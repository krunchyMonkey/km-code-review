using ReviewOrchestrator.Core.Domain;

namespace ReviewOrchestrator.Core.Engines;

public enum FileCategory
{
    Generated,
    Test,
    Documentation,
    Contract,
    Persistence,
    Api,
    Auth,
    Infrastructure,
    Migration,
    Telemetry,
    Frontend,
    Dependency,
    Backend,
    Other
}

/// <summary>
/// Path-based heuristics shared by the risk engine, review-surface evaluator and mock brief provider.
/// Paths are normalized to lower-case with a leading slash so "/auth/" matches "src/auth/x.cs" too.
/// </summary>
public static class FileClassifier
{
    private static readonly string[] GeneratedFileNames =
    [
        "package-lock.json", "yarn.lock", "pnpm-lock.yaml", "packages.lock.json", "npm-shrinkwrap.json",
        "cargo.lock", "poetry.lock", "gemfile.lock", "composer.lock", "go.sum"
    ];

    private static readonly string[] GeneratedSuffixes =
    [
        ".min.js", ".min.css", ".snap", ".designer.cs", ".g.cs", ".generated.cs", "modelsnapshot.cs", ".lock", ".map"
    ];

    private static readonly string[] GeneratedSegments = ["/__snapshots__/", "/dist/", "/generated/", "/node_modules/"];

    private static readonly string[] BackendExtensions = [".cs", ".go", ".java", ".py", ".rs", ".kt", ".rb", ".sql", ".fs"];
    private static readonly string[] FrontendExtensions = [".tsx", ".jsx", ".ts", ".js", ".css", ".scss", ".vue", ".svelte", ".html"];
    private static readonly string[] InfraExtensions = [".tf", ".bicep", ".tfvars"];

    public static string Normalize(string path) => "/" + path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();

    public static string FileName(string path)
    {
        var normalized = path.Replace('\\', '/');
        var index = normalized.LastIndexOf('/');
        return index >= 0 ? normalized[(index + 1)..] : normalized;
    }

    public static bool IsGenerated(PullRequestFile file) => file.IsGenerated || IsGeneratedPath(file.Path);

    public static bool IsGeneratedPath(string path)
    {
        var p = Normalize(path);
        var name = FileName(p);
        return GeneratedFileNames.Contains(name)
               || GeneratedSuffixes.Any(p.EndsWith)
               || GeneratedSegments.Any(p.Contains);
    }

    public static bool IsTest(PullRequestFile file) => file.IsTestFile || IsTestPath(file.Path);

    public static bool IsTestPath(string path)
    {
        var p = Normalize(path);
        var name = FileName(p);
        return p.Contains("/tests/") || p.Contains("/test/") || p.Contains("/__tests__/")
               || name.Contains(".test.") || name.Contains(".spec.")
               || name.EndsWith("tests.cs") || name.EndsWith("test.cs") || name.EndsWith("_test.go") || name.StartsWith("test_");
    }

    public static bool IsDocumentation(string path)
    {
        var p = Normalize(path);
        return p.EndsWith(".md") || p.EndsWith(".mdx") || p.EndsWith(".rst") || p.StartsWith("/docs/") || p.Contains("/docs/");
    }

    /// <summary>A "production" file: something a reviewer must actually read (not generated, not a test, not docs).</summary>
    public static bool IsProductionFile(PullRequestFile file) =>
        !IsGenerated(file) && !IsTest(file) && !IsDocumentation(file.Path);

    public static bool IsAuthPath(string path)
    {
        var p = Normalize(path);
        return p.Contains("/auth/") || p.Contains("/security/") || p.Contains("/permissions/")
               || p.Contains("/authentication/") || p.Contains("/authorization/");
    }

    public static bool IsMigrationPath(string path) => Normalize(path).Contains("/migrations/");

    public static bool IsInfrastructurePath(string path)
    {
        var p = Normalize(path);
        return p.Contains("/terraform/") || p.Contains("/infra/") || p.Contains("/infrastructure/") || p.Contains("/deploy/")
               || p.Contains("/.github/workflows/") || InfraExtensions.Any(p.EndsWith) || FileName(p) == "dockerfile";
    }

    public static bool IsPersistencePath(string path)
    {
        var p = Normalize(path);
        return p.Contains("/persistence/") || p.Contains("/repositories/") || p.Contains("/storage/") || p.Contains("/data/")
               || p.Contains("repository.") || p.Contains("dbcontext") || p.Contains("store.") || p.EndsWith(".sql");
    }

    public static bool IsContractPath(string path)
    {
        var p = Normalize(path);
        var name = FileName(p);
        return p.Contains("/contracts/") || p.Contains("/dtos/") || name.EndsWith("dto.cs") || name.EndsWith("dto.ts")
               || name.EndsWith("request.cs") || name.EndsWith("response.cs");
    }

    public static bool IsApiPath(string path)
    {
        var p = Normalize(path);
        return p.Contains("/endpoints/") || p.Contains("/controllers/") || p.Contains("/api/")
               || FileName(p).EndsWith("controller.cs") || FileName(p).EndsWith("endpoints.cs");
    }

    public static bool IsTelemetryPath(string path)
    {
        var p = Normalize(path);
        return p.Contains("/telemetry/") || p.Contains("/analytics/") || p.Contains("/observability/") || p.Contains("/metrics/");
    }

    public static bool IsDependencyManifest(string path)
    {
        var name = FileName(Normalize(path));
        return name is "package.json" or "directory.packages.props" or "requirements.txt" or "go.mod" or "cargo.toml" or "pyproject.toml"
               || name.EndsWith(".csproj");
    }

    public static bool IsBackendCode(string path) => BackendExtensions.Any(Normalize(path).EndsWith);

    public static bool IsFrontendCode(string path) => FrontendExtensions.Any(Normalize(path).EndsWith);

    public static bool IsSecuritySensitive(PullRequestFile file) => file.IsSecuritySensitive || (IsAuthPath(file.Path) && !IsTest(file));

    /// <summary>Single most relevant category for display and brief generation. Order matters: riskiest first.</summary>
    public static FileCategory Categorize(PullRequestFile file)
    {
        var path = file.Path;
        if (IsGenerated(file)) return FileCategory.Generated;
        if (IsTest(file)) return FileCategory.Test;
        if (IsDocumentation(path)) return FileCategory.Documentation;
        if (IsSecuritySensitive(file)) return FileCategory.Auth;
        if (IsMigrationPath(path)) return FileCategory.Migration;
        if (IsInfrastructurePath(path)) return FileCategory.Infrastructure;
        if (IsContractPath(path)) return FileCategory.Contract;
        if (IsPersistencePath(path)) return FileCategory.Persistence;
        if (IsApiPath(path)) return FileCategory.Api;
        if (IsTelemetryPath(path)) return FileCategory.Telemetry;
        if (IsDependencyManifest(path)) return FileCategory.Dependency;
        if (IsBackendCode(path)) return FileCategory.Backend;
        if (IsFrontendCode(path)) return FileCategory.Frontend;
        return FileCategory.Other;
    }
}
