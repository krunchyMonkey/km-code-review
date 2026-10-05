using ReviewOrchestrator.Core.Domain;

namespace ReviewOrchestrator.Core;

/// <summary>
/// Boundary to the system of record for pull requests (Azure DevOps, GitHub, ...).
/// Implementations map provider payloads into the internal <see cref="PullRequest"/> model;
/// calculated fields (risk, priority, assignments, briefs) are owned by the orchestrator.
/// </summary>
public interface ISourceControlProvider
{
    Task<IReadOnlyList<PullRequest>> GetPullRequestsAsync(CancellationToken cancellationToken);
    Task<PullRequest?> GetPullRequestAsync(string externalId, CancellationToken cancellationToken);
}

public interface IReviewBriefProvider
{
    Task<ReviewBrief> GenerateAsync(
        PullRequest pullRequest,
        CancellationToken cancellationToken);
}

public interface IReviewNotificationService
{
    Task NotifyAssignedAsync(ReviewAssignment assignment, CancellationToken cancellationToken);
    Task NotifyAgingAsync(PullRequest pullRequest, CancellationToken cancellationToken);
    Task NotifyReassignedAsync(ReviewAssignment assignment, CancellationToken cancellationToken);
}
