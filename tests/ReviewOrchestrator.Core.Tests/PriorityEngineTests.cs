using ReviewOrchestrator.Core.Domain;
using ReviewOrchestrator.Core.Engines;
using static ReviewOrchestrator.Core.Tests.TestData;

namespace ReviewOrchestrator.Core.Tests;

public class PriorityEngineTests
{
    private readonly PriorityEngine _engine = new();

    private PriorityResult Score(PullRequest pr, ReviewRisk risk = ReviewRisk.Low, int lines = 300) =>
        _engine.Calculate(pr, risk, lines, Now);

    [Fact]
    public void Older_pr_scores_higher_when_otherwise_equal()
    {
        var younger = Score(Pr(ageHours: 2));
        var older = Score(Pr(ageHours: 6));

        Assert.True(older.Score > younger.Score);
        Assert.Equal(4, older.Score - younger.Score, precision: 1);
    }

    [Fact]
    public void Production_fix_receives_bonus()
    {
        var baseline = Score(Pr());
        var fix = Score(Pr(configure: p => p.IsProductionFix = true));

        Assert.Equal(PriorityEngine.ProductionFixBonus, fix.Score - baseline.Score, precision: 1);
        Assert.Contains("+35 production fix", fix.Reasons);
    }

    [Fact]
    public void Release_blocker_receives_bonus()
    {
        var baseline = Score(Pr());
        var blocker = Score(Pr(configure: p => p.IsReleaseBlocker = true));

        Assert.Equal(PriorityEngine.ReleaseBlockerBonus, blocker.Score - baseline.Score, precision: 1);
        Assert.Contains("+40 release blocker", blocker.Reasons);
    }

    [Fact]
    public void Dependency_blocker_receives_bonus()
    {
        var baseline = Score(Pr());
        var blocking = Score(Pr(configure: p => p.BlocksOtherWork = true));

        Assert.Equal(PriorityEngine.BlocksOtherWorkBonus, blocking.Score - baseline.Score, precision: 1);
        Assert.Contains("+25 blocks other work", blocking.Reasons);
    }

    [Fact]
    public void Small_pr_receives_bonus()
    {
        var pr = Pr();
        var medium = Score(pr, lines: 151);
        var small = Score(pr, lines: 150);

        Assert.Equal(PriorityEngine.SmallPrBonus, small.Score - medium.Score, precision: 1);
        Assert.Contains("+8 small PR", small.Reasons);
    }

    [Fact]
    public void Very_large_pr_receives_penalty()
    {
        var pr = Pr();
        var large = Score(pr, lines: 1000);
        var veryLarge = Score(pr, lines: 1001);

        Assert.Equal(-PriorityEngine.VeryLargePrPenalty, veryLarge.Score - large.Score, precision: 1);
        Assert.Contains("-10 very large PR", veryLarge.Reasons);
    }

    [Fact]
    public void Age_contribution_caps_at_72()
    {
        var at72 = Score(Pr(ageHours: 72));
        var at200 = Score(Pr(ageHours: 200));

        Assert.Equal(at72.Score, at200.Score);
        Assert.Equal(72 + PriorityEngine.LongWaitBonus, at200.Score, precision: 1);
        Assert.Contains(at200.Reasons, r => r.Contains("capped"));
    }

    [Fact]
    public void Long_wait_bonus_applies_from_eight_hours()
    {
        var before = Score(Pr(ageHours: 7.9));
        var after = Score(Pr(ageHours: 8));

        Assert.Equal(0.1 + PriorityEngine.LongWaitBonus, after.Score - before.Score, precision: 1);
    }

    [Theory]
    [InlineData(ReviewRisk.High, 20)]
    [InlineData(ReviewRisk.Medium, 10)]
    [InlineData(ReviewRisk.Low, 0)]
    public void Risk_adds_points(ReviewRisk risk, double expected)
    {
        var pr = Pr();
        Assert.Equal(expected, Score(pr, risk).Score - Score(pr, ReviewRisk.Low).Score, precision: 1);
    }

    [Fact]
    public void Matches_spec_example_formula()
    {
        // 14h waiting, production fix, blocks other work, small PR, low risk:
        // 14 + 35 + 25 + 10 (> 1 business day) + 8 = 92
        var pr = Pr(ageHours: 14, configure: p => { p.IsProductionFix = true; p.BlocksOtherWork = true; });
        var result = Score(pr, ReviewRisk.Low, lines: 100);

        Assert.Equal(92, result.Score, precision: 1);
        Assert.Equal(PriorityBand.High, result.Band);
    }

    [Theory]
    [InlineData(10, PriorityBand.Low)]
    [InlineData(20, PriorityBand.Medium)]
    [InlineData(35, PriorityBand.High)]
    public void Bands_follow_thresholds(double score, PriorityBand expected) =>
        Assert.Equal(expected, _engine.BandFor(score));
}
