using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReviewOrchestrator.Core;
using ReviewOrchestrator.Core.Domain;
using ReviewOrchestrator.Infrastructure.Application;

namespace ReviewOrchestrator.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>
    /// Drop and re-seed the SQLite database on every start. On by default so demo timestamps ("47m waiting") stay fresh.
    /// </summary>
    public bool ResetOnStartup { get; set; } = true;
}

public static class DatabaseInitializer
{
    /// <summary>
    /// Creates the schema (EnsureCreated — no migrations needed for the PoC) and seeds demo data on first run.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, bool resetOnStartup, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ReviewHubDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (resetOnStartup)
            await db.Database.EnsureDeletedAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);

        if (await db.PullRequests.AnyAsync(ct))
        {
            logger.LogInformation("Database already seeded; skipping seed");
            return;
        }

        await SeedAsync(sp, ct);
        logger.LogInformation("Seeded demo data: {PullRequests} pull requests, {Reviewers} reviewers",
            await db.PullRequests.CountAsync(ct), await db.Reviewers.CountAsync(ct));
    }

    public static async Task SeedAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<ReviewHubDbContext>();
        var provider = sp.GetRequiredService<ISourceControlProvider>();
        var orchestrator = sp.GetRequiredService<ReviewOrchestrationService>();
        var time = sp.GetRequiredService<TimeProvider>();
        var lease = sp.GetRequiredService<OrchestrationEngines>().Lease;

        var reviewers = SeedData.Reviewers();
        db.Reviewers.AddRange(reviewers);
        await db.SaveChangesAsync(ct);

        await orchestrator.ImportAsync(await provider.GetPullRequestsAsync(ct), ct);

        // Pre-existing review state (reviewers already working on some PRs) so routing has realistic load to balance.
        var now = time.GetUtcNow();
        var prs = await db.PullRequests.ToDictionaryAsync(p => p.ExternalId, ct);
        var reviewerIds = reviewers.ToDictionary(r => r.ExternalId, r => r.Id);
        foreach (var seed in SeedData.Assignments())
        {
            if (!prs.TryGetValue(seed.PullRequestExternalId, out var pr)) continue;
            var assignedAt = now - seed.AssignedAgo;
            db.ReviewAssignments.Add(new ReviewAssignment
            {
                Id = Guid.NewGuid(),
                PullRequestId = pr.Id,
                ReviewerId = reviewerIds[seed.ReviewerExternalId],
                Status = seed.Status,
                AssignedAt = assignedAt,
                AcknowledgedAt = now - seed.AcknowledgedAgo,
                CompletedAt = now - seed.CompletedAgo,
                LeaseExpiresAt = lease.LeaseExpiry(assignedAt),
            });
        }
        await db.SaveChangesAsync(ct);

        await orchestrator.ScoreAndRouteAllAsync(ct);
    }
}
