using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReviewOrchestrator.Core.Domain;
using ReviewOrchestrator.Core.Engines;
using ReviewOrchestrator.Infrastructure.Application;
using ReviewOrchestrator.Infrastructure.Notifications;
using ReviewOrchestrator.Infrastructure.Persistence;
using ReviewOrchestrator.Infrastructure.ReviewBriefs;
using ReviewOrchestrator.Infrastructure.SourceControl;
using static ReviewOrchestrator.Core.Tests.TestData;

namespace ReviewOrchestrator.Core.Tests;

/// <summary>Workflow + lease tests against a real (in-memory) SQLite database.</summary>
public sealed class OrchestrationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ManualTimeProvider _time = new(Now);
    private readonly ServiceProvider _services;

    public OrchestrationServiceTests()
    {
        _connection.Open();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ReviewHubDbContext>(o => o.UseSqlite(_connection));
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(new OrchestrationEngines(new RiskEngine(), new PriorityEngine(), new ReviewerRouter(),
            new ReviewSurfaceEvaluator(), new WipEvaluator(), new AgingCalculator(), new LeasePolicy()));
        services.AddSingleton<ReviewOrchestrator.Core.IReviewBriefProvider>(new MockReviewBriefProvider(_time));
        services.AddSingleton<ReviewOrchestrator.Core.IReviewNotificationService>(
            new LoggingReviewNotificationService(NullLogger<LoggingReviewNotificationService>.Instance));
        services.AddSingleton<ReviewOrchestrator.Core.ISourceControlProvider>(new MockSourceControlProvider(_time));
        services.AddScoped<ReviewOrchestrationService>();
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ReviewHubDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    /// <summary>Each call uses a fresh scope/DbContext, like separate HTTP requests.</summary>
    private async Task<T> Run<T>(Func<ReviewOrchestrationService, ReviewHubDbContext, Task<T>> action)
    {
        using var scope = _services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ReviewOrchestrationService>(),
            scope.ServiceProvider.GetRequiredService<ReviewHubDbContext>());
    }

    private async Task<(Reviewer First, Reviewer Second, Guid PrId)> SeedTwoOwnersAndOnePr()
    {
        var first = Reviewer("Amy Adams", components: ["skills"], repos: ["ai-assistant"]);
        var second = Reviewer("Ben Brown", components: ["skills"]);
        var pr = Pr(ageHours: 1, labels: ["backend"], files: [File("src/Skills/Coordinator.cs", 40, 10, "skills")]);

        await Run(async (service, db) =>
        {
            db.Reviewers.AddRange(first, second);
            await db.SaveChangesAsync();
            await service.ImportAsync([pr], CancellationToken.None);
            await service.ScoreAndRouteAllAsync(CancellationToken.None);
            return 0;
        });
        return (first, second, pr.Id);
    }

    [Fact]
    public async Task New_pr_is_assigned_with_a_lease()
    {
        var (first, _, prId) = await SeedTwoOwnersAndOnePr();

        var detail = await Run((s, _) => s.GetDetailAsync(prId, CancellationToken.None));

        Assert.NotNull(detail.ActiveAssignment);
        Assert.Equal(first.Id, detail.ActiveAssignment.ReviewerId);
        Assert.Equal(ReviewAssignmentStatus.Assigned, detail.ActiveAssignment.Status);
        Assert.Equal(Now.AddHours(4), detail.ActiveAssignment.LeaseExpiresAt);
        Assert.Equal(240, detail.ActiveAssignment.LeaseRemainingMinutes);
        Assert.NotNull(detail.Brief);
    }

    [Fact]
    public async Task Expired_lease_reroutes_to_next_eligible_reviewer()
    {
        var (first, second, prId) = await SeedTwoOwnersAndOnePr();

        _time.Advance(TimeSpan.FromHours(3));
        Assert.Equal(0, await Run((s, _) => s.ExpireLeasesAsync(CancellationToken.None)));

        _time.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        Assert.Equal(1, await Run((s, _) => s.ExpireLeasesAsync(CancellationToken.None)));

        var detail = await Run((s, _) => s.GetDetailAsync(prId, CancellationToken.None));
        Assert.Equal(second.Id, detail.ActiveAssignment?.ReviewerId);
        Assert.Contains(detail.AssignmentHistory, a => a.ReviewerId == first.Id && a.Status == ReviewAssignmentStatus.Expired);
        Assert.Equal(PullRequestStatus.WaitingForReview, detail.Status);
    }

    [Fact]
    public async Task Acknowledged_assignment_does_not_expire()
    {
        var (first, _, prId) = await SeedTwoOwnersAndOnePr();
        await Run((s, _) => s.StartReviewAsync(prId, first.Id, CancellationToken.None));

        _time.Advance(TimeSpan.FromHours(10));

        Assert.Equal(0, await Run((s, _) => s.ExpireLeasesAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task Start_then_approve_moves_pr_out_of_the_open_queue()
    {
        var (first, _, prId) = await SeedTwoOwnersAndOnePr();

        var started = await Run((s, _) => s.StartReviewAsync(prId, first.Id, CancellationToken.None));
        Assert.Equal(PullRequestStatus.InReview, started.Status);
        Assert.Equal(ReviewAssignmentStatus.Acknowledged, started.ActiveAssignment?.Status);
        Assert.NotNull(started.ActiveAssignment?.AcknowledgedAt);

        var approved = await Run((s, _) => s.CompleteReviewAsync(prId, "Approved", CancellationToken.None));
        Assert.Equal(PullRequestStatus.Approved, approved.Status);
        Assert.Null(approved.ActiveAssignment);

        var queue = await Run((s, _) => s.GetQueueAsync(new QueueQuery(), CancellationToken.None));
        Assert.DoesNotContain(queue.Items, i => i.Id == prId);

        var metrics = await Run((s, _) => s.GetMetricsAsync(CancellationToken.None));
        Assert.Equal(0, metrics.InReview);
        Assert.Equal(1, metrics.CompletedReviews);
    }

    [Fact]
    public async Task Request_changes_keeps_pr_in_queue()
    {
        var (first, _, prId) = await SeedTwoOwnersAndOnePr();
        await Run((s, _) => s.StartReviewAsync(prId, first.Id, CancellationToken.None));

        var result = await Run((s, _) => s.CompleteReviewAsync(prId, "ChangesRequested", CancellationToken.None));

        Assert.Equal(PullRequestStatus.ChangesRequested, result.Status);
        var queue = await Run((s, _) => s.GetQueueAsync(new QueueQuery(), CancellationToken.None));
        Assert.Contains(queue.Items, i => i.Id == prId && i.Status == PullRequestStatus.ChangesRequested);
    }

    [Fact]
    public async Task Release_reroutes_to_a_different_reviewer()
    {
        var (first, second, prId) = await SeedTwoOwnersAndOnePr();
        await Run((s, _) => s.StartReviewAsync(prId, first.Id, CancellationToken.None));

        var released = await Run((s, _) => s.ReleaseReviewAsync(prId, CancellationToken.None));

        Assert.Equal(PullRequestStatus.WaitingForReview, released.Status);
        Assert.Equal(second.Id, released.ActiveAssignment?.ReviewerId);
        Assert.Contains(released.AssignmentHistory, a => a.ReviewerId == first.Id && a.Status == ReviewAssignmentStatus.Released);
    }

    [Fact]
    public async Task Invalid_outcome_is_rejected()
    {
        var (_, _, prId) = await SeedTwoOwnersAndOnePr();

        var ex = await Assert.ThrowsAsync<OrchestrationException>(() => Run((s, _) => s.CompleteReviewAsync(prId, "Maybe", CancellationToken.None)));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Starting_a_review_actively_held_by_someone_else_conflicts()
    {
        var (first, second, prId) = await SeedTwoOwnersAndOnePr();
        await Run((s, _) => s.StartReviewAsync(prId, first.Id, CancellationToken.None));

        var ex = await Assert.ThrowsAsync<OrchestrationException>(() => Run((s, _) => s.StartReviewAsync(prId, second.Id, CancellationToken.None)));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Reviewer_at_capacity_cannot_start_a_review()
    {
        var (_, _, prId) = await SeedTwoOwnersAndOnePr();
        var full = Reviewer("Full Reviewer", capacity: 1);
        var other = Pr(files: [File("src/Other.cs")]);
        await Run(async (s, db) =>
        {
            db.Reviewers.Add(full);
            await db.SaveChangesAsync();
            await s.ImportAsync([other], CancellationToken.None);
            return 0;
        });
        await Run((s, _) => s.StartReviewAsync(other.Id, full.Id, CancellationToken.None));

        var ex = await Assert.ThrowsAsync<OrchestrationException>(() => Run((s, _) => s.StartReviewAsync(prId, full.Id, CancellationToken.None)));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("capacity", ex.Message);
    }

    [Fact]
    public async Task Demo_seed_produces_the_spec_scenarios()
    {
        await Run(async (_, _) =>
        {
            using var scope = _services.CreateScope();
            await DatabaseInitializer.SeedAsync(scope.ServiceProvider, CancellationToken.None);
            return 0;
        });

        var queue = await Run((s, _) => s.GetQueueAsync(new QueueQuery(), CancellationToken.None));
        Assert.True(queue.Items.Count >= 10);
        Assert.Equal(queue.Items.OrderByDescending(i => i.PriorityScore).Select(i => i.Id), queue.Items.Select(i => i.Id));

        var pr217 = queue.Items.Single(i => i.ExternalId == "217");
        Assert.Equal((342, 98, 12), (pr217.Additions, pr217.Deletions, pr217.ChangedFiles));
        Assert.Equal(ReviewRisk.Medium, pr217.Risk);
        Assert.Equal(PriorityBand.High, pr217.PriorityBand);
        Assert.Equal("Bob Smith", pr217.AssignedReviewer?.Name);

        var detail217 = await Run((s, _) => s.GetDetailAsync(pr217.Id, CancellationToken.None));
        Assert.NotNull(detail217.WipWarning);
        Assert.Equal(4, detail217.WipWarning.OpenReviewCount);
        Assert.Contains(detail217.Candidates, c => c.Name == "Kent Jones" && !c.Eligible);
        Assert.Equal("src/Skills/PendingSkillCoordinator.cs", detail217.Brief?.RecommendedReviewOrder[0].Path);

        Assert.Equal(ReviewRisk.High, queue.Items.Single(i => i.ExternalId == "183").Risk);
        Assert.Equal(ReviewRisk.High, queue.Items.Single(i => i.ExternalId == "205").Risk);
        Assert.Equal(ReviewRisk.Low, queue.Items.Single(i => i.ExternalId == "190").Risk);
        Assert.Equal(ReviewRisk.Low, queue.Items.Single(i => i.ExternalId == "188").Risk);
        Assert.True(queue.Items.Single(i => i.ExternalId == "214").HasReviewSurfaceWarning);

        // Priya wrote 205, so it must go to the other qualified security reviewer.
        Assert.Equal("Elena Garcia", queue.Items.Single(i => i.ExternalId == "205").AssignedReviewer?.Name);
    }
}
