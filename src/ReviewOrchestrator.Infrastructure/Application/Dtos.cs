using ReviewOrchestrator.Core.Domain;
using ReviewOrchestrator.Core.Engines;

namespace ReviewOrchestrator.Infrastructure.Application;

public sealed record QueueQuery(
    string? Repository = null,
    string? Priority = null,
    string? Risk = null,
    string? Status = null,
    Guid? ReviewerId = null,
    string? AuthorId = null,
    string? AgingState = null,
    string? Search = null,
    string? Sort = null);

public sealed record ReviewerRef(Guid Id, string Name, string AvatarUrl);

public sealed record QueueItemDto(
    Guid Id,
    string ExternalId,
    string Title,
    string RepositoryName,
    string AuthorId,
    string AuthorName,
    DateTimeOffset ReadyForReviewAt,
    int WaitMinutes,
    double PriorityScore,
    PriorityBand PriorityBand,
    IReadOnlyList<string> PriorityReasons,
    ReviewRisk Risk,
    AgingState AgingState,
    PullRequestStatus Status,
    IReadOnlyList<string> Labels,
    int Additions,
    int Deletions,
    int ChangedFiles,
    ReviewerRef? AssignedReviewer,
    ReviewAssignmentStatus? AssignmentStatus,
    bool HasReviewSurfaceWarning,
    bool IsReleaseBlocker,
    bool IsProductionFix,
    bool BlocksOtherWork);

public sealed record QueueSummaryDto(int Total, int High, int Medium, int Low, int Aging);

public sealed record QueueResponse(IReadOnlyList<QueueItemDto> Items, QueueSummaryDto Summary, IReadOnlyList<string> Repositories);

public sealed record PriorityDto(double Score, PriorityBand Band, IReadOnlyList<string> Reasons);

public sealed record RiskDto(ReviewRisk Level, IReadOnlyList<string> Reasons, IReadOnlyList<string> AreasTouched, IReadOnlyList<RiskSignal> Signals);

public sealed record AssignmentDto(
    Guid Id,
    Guid ReviewerId,
    string ReviewerName,
    ReviewAssignmentStatus Status,
    DateTimeOffset AssignedAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset LeaseExpiresAt,
    int? LeaseRemainingMinutes,
    int ReviewerActiveReviews,
    int ReviewerCapacity,
    IReadOnlyList<string> WhyReasons);

public sealed record CandidateDto(
    Guid ReviewerId,
    string Name,
    string AvatarUrl,
    int Score,
    bool Eligible,
    bool QualifiedForRisk,
    int ActiveReviews,
    int Capacity,
    bool IsAssigned,
    IReadOnlyList<string> Reasons);

public sealed record RecommendationDto(int Rank, string Path, string Reason, int? Additions, int? Deletions);

public sealed record BriefDto(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<string> WhatChanged,
    IReadOnlyList<string> HumanAttentionAreas,
    IReadOnlyList<string> MostlyMechanical,
    IReadOnlyList<RecommendationDto> RecommendedReviewOrder);

public sealed record FileDto(
    string Path,
    int Additions,
    int Deletions,
    bool IsGenerated,
    bool IsTestFile,
    bool IsSecuritySensitive,
    string? Component,
    FileCategory Category);

public sealed record PullRequestDetailDto(
    Guid Id,
    string ExternalId,
    string Title,
    string Description,
    string RepositoryName,
    string AuthorId,
    string AuthorName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ReadyForReviewAt,
    DateTimeOffset UpdatedAt,
    int WaitMinutes,
    PullRequestStatus Status,
    AgingState AgingState,
    string SourceBranch,
    string TargetBranch,
    string ExternalUrl,
    string? LinkedWorkItem,
    IReadOnlyList<string> Labels,
    int Additions,
    int Deletions,
    int ChangedFiles,
    int MeaningfulChangedLines,
    int ProductionFiles,
    bool IsReleaseBlocker,
    bool IsProductionFix,
    bool BlocksOtherWork,
    bool IsSecuritySensitive,
    PriorityDto Priority,
    RiskDto Risk,
    AssignmentDto? ActiveAssignment,
    IReadOnlyList<AssignmentDto> AssignmentHistory,
    IReadOnlyList<CandidateDto> Candidates,
    string? RoutingNote,
    BriefDto? Brief,
    IReadOnlyList<FileDto> Files,
    WipWarning? WipWarning,
    ReviewSurfaceWarning? ReviewSurfaceWarning);

public sealed record ReviewerDto(
    Guid Id,
    string ExternalId,
    string Name,
    string AvatarUrl,
    bool IsActive,
    int ActiveReviews,
    int Capacity,
    IReadOnlyList<string> Components,
    IReadOnlyList<string> Specialties,
    IReadOnlyList<string> RepositoryHistory);

public sealed record UtilizationDto(Guid ReviewerId, string ReviewerName, int Active, int Capacity, bool IsActive);

public sealed record AgingBucketDto(AgingState State, int Count);

public sealed record MetricsSummaryDto(
    int WaitingForReview,
    int InReview,
    int ChangesRequested,
    int Aging,
    int AverageWaitMinutes,
    int MedianWaitMinutes,
    int? AverageTimeToFirstReviewMinutes,
    int CompletedReviews,
    IReadOnlyList<UtilizationDto> ReviewerUtilization,
    IReadOnlyList<AgingBucketDto> AgingBuckets);

public sealed record StartReviewRequest(Guid ReviewerId);

public sealed record CompleteReviewRequest(string? Outcome);
