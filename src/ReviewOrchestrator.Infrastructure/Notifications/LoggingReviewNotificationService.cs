using Microsoft.Extensions.Logging;
using ReviewOrchestrator.Core;
using ReviewOrchestrator.Core.Domain;

namespace ReviewOrchestrator.Infrastructure.Notifications;

/// <summary>Stand-in for Slack/Teams: logs the notification payload that would have been sent.</summary>
public sealed class LoggingReviewNotificationService(ILogger<LoggingReviewNotificationService> logger) : IReviewNotificationService
{
    public Task NotifyAssignedAsync(ReviewAssignment assignment, CancellationToken cancellationToken)
    {
        logger.LogInformation("Notification {NotificationType}: PR {PullRequestId} assigned to reviewer {ReviewerId}, lease until {LeaseExpiresAt}",
            "Assigned", assignment.PullRequestId, assignment.ReviewerId, assignment.LeaseExpiresAt);
        return Task.CompletedTask;
    }

    public Task NotifyAgingAsync(PullRequest pullRequest, CancellationToken cancellationToken)
    {
        logger.LogInformation("Notification {NotificationType}: PR #{ExternalId} in {Repository} entered {AgingState}",
            "Aging", pullRequest.ExternalId, pullRequest.RepositoryName, pullRequest.LastNotifiedAgingState);
        return Task.CompletedTask;
    }

    public Task NotifyReassignedAsync(ReviewAssignment assignment, CancellationToken cancellationToken)
    {
        logger.LogInformation("Notification {NotificationType}: PR {PullRequestId} reassigned to reviewer {ReviewerId}, lease until {LeaseExpiresAt}",
            "Reassigned", assignment.PullRequestId, assignment.ReviewerId, assignment.LeaseExpiresAt);
        return Task.CompletedTask;
    }
}
