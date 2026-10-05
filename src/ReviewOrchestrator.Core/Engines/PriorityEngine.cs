using ReviewOrchestrator.Core.Domain;

namespace ReviewOrchestrator.Core.Engines;

public sealed record PriorityResult(double Score, PriorityBand Band, IReadOnlyList<string> Reasons);

/// <summary>Deterministic priority scoring (spec §9). All numbers are intentionally simple to explain.</summary>
public sealed class PriorityEngine(PriorityOptions options)
{
    public const double MaxAgeContribution = 72;
    public const double ReleaseBlockerBonus = 40;
    public const double ProductionFixBonus = 35;
    public const double BlocksOtherWorkBonus = 25;
    public const double HighRiskBonus = 20;
    public const double MediumRiskBonus = 10;
    public const double LongWaitBonus = 10;
    public const double LongWaitHours = 8; // "> 1 business day"
    public const double SmallPrBonus = 8;
    public const int SmallPrLineThreshold = 150;
    public const double VeryLargePrPenalty = 10;
    public const int VeryLargePrLineThreshold = 1000;

    public PriorityEngine() : this(new PriorityOptions())
    {
    }

    public PriorityResult Calculate(PullRequest pr, ReviewRisk risk, int meaningfulChangedLines, DateTimeOffset now)
    {
        var ageHours = Math.Max(0, (now - pr.ReadyForReviewAt).TotalHours);
        var reasons = new List<string>();

        // Modifiers first so the explanation reads "most important reason first".
        double modifiers = 0;
        void Add(double points, string reason)
        {
            modifiers += points;
            reasons.Add($"{(points >= 0 ? "+" : "")}{points:0} {reason}");
        }

        if (pr.IsReleaseBlocker) Add(ReleaseBlockerBonus, "release blocker");
        if (pr.IsProductionFix) Add(ProductionFixBonus, "production fix");
        if (pr.BlocksOtherWork) Add(BlocksOtherWorkBonus, "blocks other work");

        if (risk == ReviewRisk.High) Add(HighRiskBonus, "high-risk area");
        else if (risk == ReviewRisk.Medium) Add(MediumRiskBonus, "medium-risk area");

        var ageContribution = Math.Min(ageHours, MaxAgeContribution);
        if (ageContribution >= 0.5)
            reasons.Add($"+{ageContribution:0} waiting time{(ageHours > MaxAgeContribution ? " (capped)" : "")}");

        if (ageHours >= LongWaitHours) Add(LongWaitBonus, "waiting > 1 business day");

        if (meaningfulChangedLines <= SmallPrLineThreshold) Add(SmallPrBonus, "small PR");
        if (meaningfulChangedLines > VeryLargePrLineThreshold) Add(-VeryLargePrPenalty, "very large PR");

        var score = Math.Round(ageContribution + modifiers, 1);
        return new PriorityResult(score, BandFor(score), reasons);
    }

    public PriorityBand BandFor(double score) =>
        score >= options.HighBandThreshold ? PriorityBand.High
        : score >= options.MediumBandThreshold ? PriorityBand.Medium
        : PriorityBand.Low;
}
