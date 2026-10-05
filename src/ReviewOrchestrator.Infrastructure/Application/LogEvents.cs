using Microsoft.Extensions.Logging;

namespace ReviewOrchestrator.Infrastructure.Application;

/// <summary>Named orchestration events (spec §37) so they can be filtered in structured logs.</summary>
public static class LogEvents
{
    public static readonly EventId PullRequestScored = new(1001, nameof(PullRequestScored));
    public static readonly EventId RiskCalculated = new(1002, nameof(RiskCalculated));
    public static readonly EventId ReviewerAssigned = new(1003, nameof(ReviewerAssigned));
    public static readonly EventId ReviewerLeaseExpired = new(1004, nameof(ReviewerLeaseExpired));
    public static readonly EventId ReviewStarted = new(1005, nameof(ReviewStarted));
    public static readonly EventId ReviewReleased = new(1006, nameof(ReviewReleased));
    public static readonly EventId ReviewCompleted = new(1007, nameof(ReviewCompleted));
    public static readonly EventId ReviewBriefGenerated = new(1008, nameof(ReviewBriefGenerated));
    public static readonly EventId ReviewerUnavailable = new(1009, nameof(ReviewerUnavailable));
    public static readonly EventId PullRequestImported = new(1010, nameof(PullRequestImported));
}
