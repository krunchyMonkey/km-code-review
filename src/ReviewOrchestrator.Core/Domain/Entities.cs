namespace ReviewOrchestrator.Core.Domain;

public sealed class PullRequest
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string RepositoryId { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ReadyForReviewAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public PullRequestStatus Status { get; set; }
    public ReviewRisk Risk { get; set; }
    public double PriorityScore { get; set; }

    public int Additions { get; set; }
    public int Deletions { get; set; }
    public int ChangedFiles { get; set; }
    public int MeaningfulChangedLines { get; set; }

    public bool IsReleaseBlocker { get; set; }
    public bool IsProductionFix { get; set; }
    public bool BlocksOtherWork { get; set; }
    public bool IsSecuritySensitive { get; set; }
    public bool IntentionallyLarge { get; set; }

    public string SourceBranch { get; set; } = string.Empty;
    public string TargetBranch { get; set; } = "main";
    public string ExternalUrl { get; set; } = string.Empty;

    /// <summary>Optional linked work item (e.g. "AB#4821"). Not in the original spec model; added for the PR details card.</summary>
    public string? LinkedWorkItem { get; set; }

    /// <summary>Last aging state a notification was sent for. Prevents re-notifying on every polling tick.</summary>
    public AgingState? LastNotifiedAgingState { get; set; }

    public List<string> Labels { get; set; } = [];
    public List<PullRequestFile> Files { get; set; } = [];
    public List<ReviewAssignment> Assignments { get; set; } = [];
}

public sealed class PullRequestFile
{
    public Guid Id { get; set; }
    public Guid PullRequestId { get; set; }
    public string Path { get; set; } = string.Empty;
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public bool IsGenerated { get; set; }
    public bool IsTestFile { get; set; }
    public bool IsSecuritySensitive { get; set; }
    public string? Component { get; set; }
}

public sealed class Reviewer
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int MaxConcurrentReviews { get; set; } = 3;
    public List<string> Components { get; set; } = [];
    public List<string> Specialties { get; set; } = [];

    /// <summary>Repositories this reviewer has reviewed before (mock flag for the "+15 has reviewed the repository" rule).</summary>
    public List<string> RepositoryHistory { get; set; } = [];
}

public sealed class ReviewAssignment
{
    public Guid Id { get; set; }
    public Guid PullRequestId { get; set; }
    public Guid ReviewerId { get; set; }
    public ReviewAssignmentStatus Status { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LeaseExpiresAt { get; set; }

    public bool IsActive => Status is ReviewAssignmentStatus.Assigned or ReviewAssignmentStatus.Acknowledged;
}

public sealed class ReviewBrief
{
    public Guid Id { get; set; }
    public Guid PullRequestId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }

    public List<string> WhatChanged { get; set; } = [];
    public List<string> HumanAttentionAreas { get; set; } = [];
    public List<string> MostlyMechanical { get; set; } = [];
    public List<ReviewFileRecommendation> RecommendedReviewOrder { get; set; } = [];
}

public sealed class ReviewFileRecommendation
{
    public string Path { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int Rank { get; set; }
}

public enum PullRequestStatus
{
    Draft,
    WaitingForReview,
    InReview,
    ChangesRequested,
    Approved,
    Merged,
    Closed
}

public enum ReviewRisk
{
    Low,
    Medium,
    High
}

public enum ReviewAssignmentStatus
{
    Assigned,
    Acknowledged,
    Released,
    Completed,
    Expired
}

public enum AgingState
{
    Fresh,
    Waiting,
    Aging,
    Stale,
    Critical
}

public enum PriorityBand
{
    Low,
    Medium,
    High
}

public static class PullRequestStatusExtensions
{
    /// <summary>Statuses that count as "in the review pipeline" (queue + WIP signal).</summary>
    public static bool IsOpenForReview(this PullRequestStatus status) =>
        status is PullRequestStatus.WaitingForReview or PullRequestStatus.InReview or PullRequestStatus.ChangesRequested;
}
