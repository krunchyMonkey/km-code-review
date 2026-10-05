namespace ReviewOrchestrator.Core;

public sealed class ReviewRoutingOptions
{
    public const string Section = "ReviewRouting";

    public double DefaultLeaseHours { get; set; } = 4;
    public int MaxAuthorOpenReviewsBeforeWarning { get; set; } = 3;
    public int LeaseCheckIntervalSeconds { get; set; } = 60;
}

public sealed class QueueOptions
{
    public const string Section = "Queue";

    public double WaitingHours { get; set; } = 4;
    public double AgingHours { get; set; } = 8;
    public double StaleHours { get; set; } = 24;
    public double CriticalHours { get; set; } = 48;
}

public sealed class PriorityOptions
{
    public const string Section = "Priority";

    /// <summary>Scores at or above this are shown as the High priority band.</summary>
    public double HighBandThreshold { get; set; } = 35;

    /// <summary>Scores at or above this (and below High) are shown as the Medium priority band.</summary>
    public double MediumBandThreshold { get; set; } = 20;
}

public sealed class ReviewSurfaceOptions
{
    public const string Section = "ReviewSurface";

    public int MeaningfulLineThreshold { get; set; } = 600;
    public int ProductionFileThreshold { get; set; } = 15;
}

public sealed class SourceControlOptions
{
    public const string Section = "SourceControl";

    public string Provider { get; set; } = "Mock";
}

public sealed class ReviewBriefOptions
{
    public const string Section = "ReviewBrief";

    public string Provider { get; set; } = "Mock";
}
