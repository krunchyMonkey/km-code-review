using ReviewOrchestrator.Core;
using ReviewOrchestrator.Core.Domain;
using ReviewOrchestrator.Core.Engines;

namespace ReviewOrchestrator.Infrastructure.SourceControl;

/// <summary>
/// Returns the demo pull requests from spec §31. Timestamps are relative to the moment the provider was created,
/// so the demo always looks "live" on first run. The provider only describes source-control facts; risk, priority,
/// assignments and briefs are calculated by the orchestrator.
/// </summary>
public sealed class MockSourceControlProvider : ISourceControlProvider
{
    private readonly DateTimeOffset _anchor;

    public MockSourceControlProvider(TimeProvider timeProvider)
    {
        _anchor = timeProvider.GetUtcNow();
    }

    public Task<IReadOnlyList<PullRequest>> GetPullRequestsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PullRequest>>(BuildAll());

    public Task<PullRequest?> GetPullRequestAsync(string externalId, CancellationToken cancellationToken) =>
        Task.FromResult(BuildAll().FirstOrDefault(p => p.ExternalId == externalId));

    private List<PullRequest> BuildAll() =>
    [
        Pr("217", "ai-assistant", "Add pending skill lifecycle handling", "alex-chen", "Alex Chen",
            ago: TimeSpan.FromMinutes(47), additions: 342, deletions: 98,
            labels: ["backend", "skills", "state-management"],
            description: "Introduces a pending state for long-running skills so the assistant can resume them after a restart. " +
                         "The coordinator owns the lifecycle; the endpoint handler now persists pending state with the thread. " +
                         "Needed before #223 (skill retries) can merge.",
            branch: "feature/pending-skill-lifecycle", workItem: "AB#4821",
            configure: p => p.BlocksOtherWork = true,
            files:
            [
                F("src/Skills/PendingSkillCoordinator.cs", 30, 25, "skills"),
                F("src/Endpoints/EndpointHandler.cs", 14, 20, "backend"),
                F("src/Threads/ThreadState.cs", 10, 12, "backend"),
                F("src/Contracts/SkillResponseDto.cs", 3, 1, "backend"),
                F("src/Skills/SkillLifecycleState.cs", 6, 2, "skills"),
                F("src/Skills/SkillExecutionContext.cs", 5, 6, "skills"),
                F("src/Threads/ThreadStore.cs", 6, 10, "backend"),
                F("src/Skills/PendingSkillOptions.cs", 2, 0, "skills"),
                F("src/Contracts/PendingSkillStatus.cs", 2, 0, "backend"),
                F("tests/PendingSkillCoordinatorTests.cs", 16, 4, "skills"),
                F("tests/ThreadStateTests.cs", 5, 2, "backend"),
                F("tests/Fakes/FakeSkillClock.cs", 1, 0, "skills"),
            ]),

        Pr("219", "ai-assistant", "Validate skill manifests before the 4.2 release", "alex-chen", "Alex Chen",
            ago: TimeSpan.FromMinutes(125), additions: 88, deletions: 14,
            labels: ["backend", "skills", "release-blocker"],
            description: "Release 4.2 is blocked: malformed skill manifests crash the registry at startup. " +
                         "Adds a validator that rejects bad manifests with a clear error instead.",
            branch: "fix/skill-manifest-validation", workItem: "AB#4877",
            configure: p => p.IsReleaseBlocker = true,
            files:
            [
                F("src/Skills/SkillManifestValidator.cs", 45, 2, "skills"),
                F("src/Skills/SkillRegistry.cs", 12, 10, "skills"),
                F("src/Contracts/SkillManifestDto.cs", 4, 2, "skills"),
                F("tests/SkillManifestValidatorTests.cs", 22, 0, "skills"),
                F("docs/release-notes/4.2.md", 5, 0, "docs"),
            ]),

        Pr("205", "platform", "Update authentication token refresh logic", "priya-singh", "Priya Singh",
            ago: TimeSpan.FromMinutes(192), additions: 128, deletions: 46,
            labels: ["auth", "security", "infra"],
            description: "Refresh tokens are now rotated on every use and the signing key is loaded from Key Vault. " +
                         "Old refresh tokens are revoked after a 30 second grace window.",
            branch: "feature/token-refresh-rotation", workItem: "AB#4790",
            configure: p => p.IsSecuritySensitive = true,
            files:
            [
                F("src/Auth/TokenRefreshService.cs", 40, 15, "platform"),
                F("src/Auth/RefreshTokenStore.cs", 15, 8, "platform"),
                F("src/Auth/TokenValidationOptions.cs", 6, 2, "platform"),
                F("src/Security/SigningKeyProvider.cs", 12, 9, "platform"),
                F("deploy/terraform/keyvault.tf", 8, 2, "platform"),
                F("src/Contracts/TokenResponse.cs", 4, 1, "platform"),
                F("tests/Auth/TokenRefreshServiceTests.cs", 30, 6, "platform"),
                F("tests/Auth/RefreshTokenStoreTests.cs", 13, 3, "platform"),
            ]),

        Pr("212", "ai-assistant", "Improve threading persistence layer", "marcus-lee", "Marcus Lee",
            ago: TimeSpan.FromMinutes(303), additions: 287, deletions: 121,
            labels: ["backend", "database", "performance"],
            description: "Batches thread writes and moves read queries to compiled queries. " +
                         "Cuts p95 thread-load latency roughly in half in the load test.",
            branch: "perf/thread-persistence",
            files:
            [
                F("src/Persistence/ThreadRepository.cs", 40, 25, "persistence"),
                F("src/Persistence/ThreadDbContext.cs", 10, 6, "persistence"),
                F("src/Persistence/ThreadEntityConfiguration.cs", 8, 4, "persistence"),
                F("src/Persistence/Queries/ThreadQueries.cs", 30, 12, "persistence"),
                F("src/Persistence/BatchWriter.cs", 35, 0, "persistence"),
                F("src/Persistence/ConnectionPoolOptions.cs", 6, 2, "persistence"),
                F("src/Threads/ThreadStore.cs", 18, 22, "persistence"),
                F("src/Threads/ThreadCache.cs", 12, 10, "persistence"),
                F("src/Threads/ThreadState.cs", 6, 4, "persistence"),
                F("src/Contracts/ThreadSummaryDto.cs", 4, 2, "persistence"),
                F("tests/Persistence/ThreadRepositoryTests.cs", 25, 8, "persistence"),
                F("tests/Persistence/BatchWriterTests.cs", 20, 0, "persistence"),
                F("tests/Threads/ThreadCacheTests.cs", 8, 2, "persistence"),
                F("docs/persistence.md", 6, 2, "docs"),
            ]),

        Pr("209", "ai-assistant", "Add analytics events for skill execution", "jordan-park", "Jordan Park",
            ago: TimeSpan.FromMinutes(378), additions: 96, deletions: 12,
            labels: ["telemetry", "analytics", "observability"],
            description: "Emits skill_started / skill_completed / skill_failed events with duration and outcome.",
            branch: "feature/skill-analytics-events",
            files:
            [
                F("src/Telemetry/SkillExecutionEvents.cs", 30, 2, "observability"),
                F("src/Telemetry/TelemetryEventNames.cs", 10, 0, "observability"),
                F("src/Telemetry/SkillTelemetryMiddleware.cs", 22, 8, "observability"),
                F("src/Telemetry/AnalyticsOptions.cs", 6, 0, "observability"),
                F("tests/Telemetry/SkillExecutionEventsTests.cs", 20, 2, "observability"),
                F("docs/telemetry/events.md", 8, 0, "docs"),
            ]),

        Pr("198", "web", "Refactor UI components to new design system", "casey-nguyen", "Casey Nguyen",
            ago: TimeSpan.FromMinutes(521), additions: 421, deletions: 203,
            labels: ["frontend", "ui", "refactor"],
            description: "Moves the shared components onto the new design tokens. Visual changes are intentional; see the Figma link in the work item.",
            branch: "refactor/design-system-components", workItem: "AB#4702",
            files:
            [
                F("src/components/Button.tsx", 30, 18, "web"),
                F("src/components/Card.tsx", 22, 12, "web"),
                F("src/components/Modal.tsx", 34, 16, "web"),
                F("src/components/Tabs.tsx", 26, 14, "web"),
                F("src/components/Badge.tsx", 14, 8, "web"),
                F("src/components/Avatar.tsx", 12, 6, "web"),
                F("src/components/Dropdown.tsx", 36, 20, "web"),
                F("src/components/Tooltip.tsx", 16, 10, "web"),
                F("src/components/Table.tsx", 40, 22, "web"),
                F("src/components/Input.tsx", 18, 10, "web"),
                F("src/components/Select.tsx", 24, 14, "web"),
                F("src/components/Toast.tsx", 16, 8, "web"),
                F("src/components/index.ts", 6, 4, "web"),
                F("src/styles/tokens.css", 48, 20, "web"),
                F("src/styles/theme.ts", 20, 6, "web"),
                F("src/pages/Settings.tsx", 14, 9, "web"),
                F("src/components/Button.test.tsx", 18, 2, "web"),
                F("src/components/__snapshots__/Button.test.tsx.snap", 27, 4, "web"),
            ]),

        Pr("214", "ai-assistant", "Tenant storage isolation for skill artifacts", "alex-chen", "Alex Chen",
            ago: TimeSpan.FromMinutes(570), additions: 1012, deletions: 328,
            labels: ["backend", "storage", "migration"],
            description: "Every skill artifact is now stored under a tenant-scoped container. Includes a migration that adds TenantId " +
                         "to the artifact table and a background backfill job for existing artifacts.",
            branch: "feature/tenant-artifact-isolation", workItem: "AB#4650",
            files:
            [
                F("src/Storage/TenantStorageResolver.cs", 90, 20, "storage"),
                F("src/Storage/ArtifactBlobStore.cs", 80, 40, "storage"),
                F("src/Storage/TenantScopedContainer.cs", 60, 5, "storage"),
                F("src/Storage/StoragePathBuilder.cs", 30, 12, "storage"),
                F("src/Storage/StorageOptions.cs", 12, 2, "storage"),
                F("src/Persistence/ArtifactRepository.cs", 55, 30, "persistence"),
                F("src/Persistence/ArtifactEntityConfiguration.cs", 14, 4, "persistence"),
                F("src/Persistence/Migrations/20261001_TenantArtifactIsolation.cs", 70, 0, "persistence"),
                F("src/Persistence/Migrations/ArtifactDbModelSnapshot.cs", 66, 27, "persistence"),
                F("src/Skills/SkillArtifactService.cs", 60, 35, "skills"),
                F("src/Skills/SkillArtifactUploader.cs", 40, 20, "skills"),
                F("src/Skills/SkillArtifactDownloader.cs", 38, 22, "skills"),
                F("src/Skills/SkillExecutionContext.cs", 10, 6, "skills"),
                F("src/Endpoints/ArtifactEndpoints.cs", 45, 30, "backend"),
                F("src/Endpoints/TenantMiddleware.cs", 25, 4, "backend"),
                F("src/Contracts/ArtifactDto.cs", 12, 6, "backend"),
                F("src/Contracts/ArtifactUploadRequest.cs", 8, 3, "backend"),
                F("src/Tenancy/TenantContext.cs", 22, 2, "backend"),
                F("src/Tenancy/TenantAccessor.cs", 18, 4, "backend"),
                F("src/Tenancy/TenantResolutionMiddleware.cs", 30, 10, "backend"),
                F("src/Background/ArtifactBackfillJob.cs", 75, 0, "storage"),
                F("src/Background/BackfillOptions.cs", 8, 0, "storage"),
                F("src/Configuration/StorageRegistration.cs", 16, 8, "storage"),
                F("tests/Storage/TenantStorageResolverTests.cs", 60, 15, "storage"),
                F("tests/Storage/ArtifactBlobStoreTests.cs", 50, 18, "storage"),
                F("tests/Skills/SkillArtifactServiceTests.cs", 18, 5, "skills"),
            ]),

        Pr("190", "docs", "Update documentation for skill APIs", "sam-rivera", "Sam Rivera",
            ago: TimeSpan.FromMinutes(722), additions: 62, deletions: 8,
            labels: ["docs", "api"],
            description: "Documents the pending-skill endpoints and refreshes the API reference examples.",
            branch: "docs/skill-apis",
            files:
            [
                F("docs/skills/overview.md", 14, 2, "docs"),
                F("docs/skills/pending-skills.md", 28, 0, "docs"),
                F("docs/api/skills-reference.md", 16, 5, "docs"),
                F("docs/changelog.md", 4, 1, "docs"),
            ]),

        Pr("188", "web", "Remove unused dependencies", "riley-kim", "Riley Kim",
            ago: TimeSpan.FromHours(24), additions: 24, deletions: 312,
            labels: ["chore", "dependencies"],
            description: "Drops moment.js, lodash and the legacy polyfill bundle. No runtime behavior change expected.",
            branch: "chore/remove-unused-deps",
            files:
            [
                F("package.json", 4, 22, "web"),
                F("package-lock.json", 20, 130, "web"),
                F("src/legacy/polyfills.ts", 0, 160, "web"),
            ]),

        Pr("183", "platform", "Fix race condition in cache invalidation", "dana-wu", "Dana Wu",
            ago: TimeSpan.FromHours(28), additions: 76, deletions: 31,
            labels: ["bug", "cache", "performance", "production-fix"],
            description: "Permission cache entries could be re-populated with stale data while an invalidation was in flight. " +
                         "Takes a distributed lock around invalidate + reload. Production incident INC-2291.",
            branch: "fix/permission-cache-race", workItem: "INC-2291",
            configure: p => p.IsProductionFix = true,
            files:
            [
                F("src/Permissions/PermissionCache.cs", 22, 14, "platform"),
                F("src/Permissions/CacheInvalidationListener.cs", 12, 9, "platform"),
                F("src/Caching/DistributedCacheLock.cs", 18, 2, "platform"),
                F("src/Caching/CacheKeys.cs", 2, 1, "platform"),
                F("tests/Permissions/PermissionCacheTests.cs", 14, 3, "platform"),
                F("tests/Caching/DistributedCacheLockTests.cs", 6, 0, "platform"),
                F("docs/runbooks/cache-invalidation.md", 2, 2, "docs"),
            ]),

        // --- PRs already in review: these establish realistic reviewer load for the routing demo. ---

        Pr("222", "ai-assistant", "Add retry jitter to skill executor", "alex-chen", "Alex Chen",
            ago: TimeSpan.FromMinutes(420), additions: 64, deletions: 18,
            labels: ["backend", "skills", "resilience"],
            description: "Adds decorrelated jitter to skill retries to avoid thundering-herd retries against downstream tools.",
            branch: "feature/retry-jitter", status: PullRequestStatus.InReview,
            files:
            [
                F("src/Skills/SkillExecutor.cs", 18, 10, "skills"),
                F("src/Skills/RetryPolicy.cs", 14, 8, "skills"),
                F("src/Skills/JitterStrategy.cs", 20, 0, "skills"),
                F("tests/RetryPolicyTests.cs", 12, 0, "skills"),
            ]),

        Pr("215", "ai-assistant", "Consolidate skill registry caching", "marcus-lee", "Marcus Lee",
            ago: TimeSpan.FromMinutes(600), additions: 156, deletions: 201,
            labels: ["backend", "skills", "cache"],
            description: "Replaces three ad-hoc caches in the skill registry with one memory cache and explicit invalidation.",
            branch: "refactor/skill-registry-cache", status: PullRequestStatus.InReview,
            files:
            [
                F("src/Skills/SkillRegistry.cs", 50, 90, "skills"),
                F("src/Skills/SkillRegistryCache.cs", 60, 0, "skills"),
                F("src/Skills/SkillLookup.cs", 10, 40, "skills"),
                F("src/Skills/ManifestCache.cs", 0, 50, "skills"),
                F("src/Configuration/CachingRegistration.cs", 8, 6, "backend"),
                F("tests/SkillRegistryCacheTests.cs", 24, 10, "skills"),
                F("tests/SkillRegistryTests.cs", 4, 5, "skills"),
            ]),

        Pr("210", "ai-assistant", "Split prompt builder into composable stages", "taylor-brooks", "Taylor Brooks",
            ago: TimeSpan.FromHours(20), additions: 233, deletions: 140,
            labels: ["backend", "ai", "refactor"],
            description: "Breaks the 600-line prompt builder into ordered stages so skills can contribute context independently.",
            branch: "refactor/prompt-stages", status: PullRequestStatus.InReview,
            files:
            [
                F("src/Prompting/PromptBuilder.cs", 30, 110, "backend"),
                F("src/Prompting/Stages/SystemPromptStage.cs", 40, 0, "backend"),
                F("src/Prompting/Stages/SkillContextStage.cs", 55, 0, "skills"),
                F("src/Prompting/Stages/HistoryStage.cs", 45, 0, "backend"),
                F("src/Prompting/IPromptStage.cs", 12, 0, "backend"),
                F("src/Prompting/PromptPipeline.cs", 26, 20, "backend"),
                F("src/Configuration/PromptRegistration.cs", 5, 2, "backend"),
                F("tests/Prompting/PromptPipelineTests.cs", 15, 6, "backend"),
                F("tests/Prompting/SkillContextStageTests.cs", 5, 2, "skills"),
            ]),

        Pr("207", "platform", "Rotate service credentials via Key Vault references", "dana-wu", "Dana Wu",
            ago: TimeSpan.FromHours(14), additions: 58, deletions: 40,
            labels: ["security", "infra", "secrets"],
            description: "Replaces connection-string secrets in app settings with Key Vault references and enables rotation.",
            branch: "security/keyvault-references", status: PullRequestStatus.InReview,
            files:
            [
                F("deploy/terraform/app-service.tf", 20, 18, "platform"),
                F("deploy/terraform/keyvault.tf", 16, 4, "platform"),
                F("src/Configuration/SecretRegistration.cs", 14, 12, "platform"),
                F("src/Security/CredentialRotationHandler.cs", 8, 6, "platform"),
                F("docs/runbooks/credential-rotation.md", 0, 0, "docs"),
            ]),

        Pr("203", "platform", "Add rate limiting to public gateway", "riley-kim", "Riley Kim",
            ago: TimeSpan.FromHours(30), additions: 142, deletions: 9,
            labels: ["backend", "gateway", "performance"],
            description: "Adds a token-bucket rate limiter per API key to the public gateway with 429 + Retry-After responses.",
            branch: "feature/gateway-rate-limits", status: PullRequestStatus.InReview,
            files:
            [
                F("src/Gateway/RateLimitingMiddleware.cs", 48, 2, "platform"),
                F("src/Gateway/TokenBucket.cs", 36, 0, "platform"),
                F("src/Gateway/RateLimitOptions.cs", 10, 0, "platform"),
                F("src/Endpoints/GatewayEndpoints.cs", 8, 7, "platform"),
                F("tests/Gateway/TokenBucketTests.cs", 30, 0, "platform"),
                F("tests/Gateway/RateLimitingMiddlewareTests.cs", 10, 0, "platform"),
            ]),

        // --- Completed review (feeds time-to-first-review metrics; not shown in the default queue). ---

        Pr("196", "ai-assistant", "Add skill timeout configuration", "marcus-lee", "Marcus Lee",
            ago: TimeSpan.FromHours(30), additions: 48, deletions: 6,
            labels: ["backend", "skills", "config"],
            description: "Makes per-skill timeouts configurable instead of the hard-coded 30 seconds.",
            branch: "feature/skill-timeouts", status: PullRequestStatus.Approved,
            files:
            [
                F("src/Skills/SkillTimeoutOptions.cs", 14, 0, "skills"),
                F("src/Skills/SkillExecutor.cs", 16, 6, "skills"),
                F("tests/SkillTimeoutTests.cs", 18, 0, "skills"),
            ]),
    ];

    private sealed record FileSpec(string Path, int AddWeight, int DeleteWeight, string? Component);

    private static FileSpec F(string path, int addWeight, int deleteWeight, string? component) =>
        new(path, addWeight, deleteWeight, component);

    private PullRequest Pr(
        string externalId,
        string repository,
        string title,
        string authorId,
        string authorName,
        TimeSpan ago,
        int additions,
        int deletions,
        List<string> labels,
        string description,
        string branch,
        IReadOnlyList<FileSpec> files,
        string? workItem = null,
        PullRequestStatus status = PullRequestStatus.WaitingForReview,
        Action<PullRequest>? configure = null)
    {
        var readyAt = _anchor - ago;
        var pr = new PullRequest
        {
            ExternalId = externalId,
            RepositoryId = repository,
            RepositoryName = repository,
            Title = title,
            Description = description,
            AuthorId = authorId,
            AuthorName = authorName,
            CreatedAt = readyAt - TimeSpan.FromMinutes(25),
            ReadyForReviewAt = readyAt,
            UpdatedAt = readyAt,
            Status = status,
            Additions = additions,
            Deletions = deletions,
            ChangedFiles = files.Count,
            SourceBranch = branch,
            TargetBranch = "main",
            ExternalUrl = $"https://dev.example.com/{repository}/pullrequest/{externalId}",
            LinkedWorkItem = workItem,
            Labels = labels,
        };

        // Spread the PR totals across files proportionally to the weights so the numbers always reconcile.
        var adds = Apportion(additions, files.Select(f => f.AddWeight).ToArray());
        var dels = Apportion(deletions, files.Select(f => f.DeleteWeight).ToArray());
        for (var i = 0; i < files.Count; i++)
        {
            var spec = files[i];
            pr.Files.Add(new PullRequestFile
            {
                Path = spec.Path,
                Additions = adds[i],
                Deletions = dels[i],
                Component = spec.Component,
                IsGenerated = FileClassifier.IsGeneratedPath(spec.Path),
                IsTestFile = FileClassifier.IsTestPath(spec.Path),
                IsSecuritySensitive = FileClassifier.IsAuthPath(spec.Path) && !FileClassifier.IsTestPath(spec.Path),
            });
        }

        configure?.Invoke(pr);
        return pr;
    }

    /// <summary>Largest-remainder apportionment: integer shares of <paramref name="total"/> that sum exactly to it.</summary>
    internal static int[] Apportion(int total, int[] weights)
    {
        var sum = weights.Sum();
        if (sum == 0) return new int[weights.Length];

        var exact = weights.Select(w => (double)total * w / sum).ToArray();
        var result = exact.Select(e => (int)Math.Floor(e)).ToArray();
        var remainder = total - result.Sum();
        foreach (var i in exact.Select((e, i) => (frac: e - Math.Floor(e), i)).OrderByDescending(x => x.frac).ThenBy(x => x.i).Take(remainder).Select(x => x.i))
            result[i]++;
        return result;
    }
}
