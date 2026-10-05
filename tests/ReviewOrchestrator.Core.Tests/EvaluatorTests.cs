using ReviewOrchestrator.Core.Domain;
using ReviewOrchestrator.Core.Engines;
using static ReviewOrchestrator.Core.Tests.TestData;

namespace ReviewOrchestrator.Core.Tests;

public class ReviewSurfaceTests
{
    private readonly ReviewSurfaceEvaluator _evaluator = new();

    [Theory]
    [InlineData("package-lock.json")]
    [InlineData("web/yarn.lock")]
    [InlineData("pnpm-lock.yaml")]
    [InlineData("src/Migrations/AppDbContextModelSnapshot.cs")]
    [InlineData("src/Forms/MainForm.Designer.cs")]
    [InlineData("web/src/__snapshots__/Button.test.tsx.snap")]
    [InlineData("web/dist/app.min.js")]
    public void Generated_and_lock_files_are_excluded(string generatedPath)
    {
        var pr = Pr(files: [File("src/Feature.cs", 100), File(generatedPath, 5000, 2000)]);

        Assert.Equal(100, _evaluator.MeaningfulChangedLines(pr));
        Assert.Equal(1, _evaluator.ProductionFileCount(pr));
        Assert.Null(_evaluator.Evaluate(pr));
    }

    [Fact]
    public void Line_threshold_triggers_warning()
    {
        var atThreshold = Pr(files: ProductionFiles(6, 100));
        var overThreshold = Pr(files: [.. ProductionFiles(6, 100), File("src/Extra.cs", 1)]);

        Assert.Null(_evaluator.Evaluate(atThreshold));
        var warning = _evaluator.Evaluate(overThreshold);
        Assert.NotNull(warning);
        Assert.Equal(601, warning.MeaningfulChangedLines);
        Assert.Equal("Large review surface", warning.Title);
        Assert.Contains("601 meaningful lines changed across 7 production files", warning.Message);
    }

    [Fact]
    public void Production_file_threshold_triggers_warning()
    {
        Assert.Null(_evaluator.Evaluate(Pr(files: ProductionFiles(15, 2))));

        var warning = _evaluator.Evaluate(Pr(files: ProductionFiles(16, 2)));
        Assert.NotNull(warning);
        Assert.Equal(16, warning.ProductionFiles);
    }

    [Fact]
    public void Tests_count_as_meaningful_lines_but_not_production_files()
    {
        var pr = Pr(files: [File("src/A.cs", 10), File("tests/ATests.cs", 20)]);

        Assert.Equal(30, _evaluator.MeaningfulChangedLines(pr));
        Assert.Equal(1, _evaluator.ProductionFileCount(pr));
    }
}

public class AgingAndWipTests
{
    [Theory]
    [InlineData(3.9, AgingState.Fresh)]
    [InlineData(4, AgingState.Waiting)]
    [InlineData(8, AgingState.Aging)]
    [InlineData(24, AgingState.Stale)]
    [InlineData(48, AgingState.Critical)]
    public void Aging_states_follow_thresholds(double hours, AgingState expected) =>
        Assert.Equal(expected, new AgingCalculator().Calculate(Now.AddHours(-hours), Now));

    [Fact]
    public void Wip_warning_triggers_at_threshold_and_counts_only_open_reviews()
    {
        var wip = new WipEvaluator();
        var mine = Enumerable.Range(0, 3).Select(_ => Pr(authorId: "alex")).ToList();
        mine[0].AuthorName = "Alex Chen";
        var approved = Pr(authorId: "alex", configure: p => p.Status = PullRequestStatus.Approved);
        var someoneElse = Pr(authorId: "sam");

        var warning = wip.Evaluate(mine[0], [.. mine, approved, someoneElse]);

        Assert.NotNull(warning);
        Assert.Equal(3, warning.OpenReviewCount);
        Assert.Equal("Alex currently has 3 PRs waiting for or undergoing review.", warning.Message);
        Assert.Null(wip.Evaluate(mine[0], [mine[0], mine[1], approved]));
    }
}

public class LeasePolicyTests
{
    private static ReviewAssignment Assignment(ReviewAssignmentStatus status, DateTimeOffset expires) =>
        new() { Id = Guid.NewGuid(), Status = status, AssignedAt = expires.AddHours(-4), LeaseExpiresAt = expires };

    [Fact]
    public void Default_lease_is_four_hours()
    {
        Assert.Equal(Now.AddHours(4), new LeasePolicy().LeaseExpiry(Now));
    }

    [Fact]
    public void Unacknowledged_assignment_past_lease_is_expired()
    {
        Assert.True(LeasePolicy.IsExpired(Assignment(ReviewAssignmentStatus.Assigned, Now.AddMinutes(-1)), Now));
        Assert.False(LeasePolicy.IsExpired(Assignment(ReviewAssignmentStatus.Assigned, Now.AddMinutes(1)), Now));
    }

    [Theory]
    [InlineData(ReviewAssignmentStatus.Acknowledged)]
    [InlineData(ReviewAssignmentStatus.Completed)]
    [InlineData(ReviewAssignmentStatus.Released)]
    [InlineData(ReviewAssignmentStatus.Expired)]
    public void Only_assigned_status_can_expire(ReviewAssignmentStatus status) =>
        Assert.False(LeasePolicy.IsExpired(Assignment(status, Now.AddHours(-1)), Now));
}
