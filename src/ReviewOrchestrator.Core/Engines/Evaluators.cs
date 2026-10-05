using ReviewOrchestrator.Core.Domain;

namespace ReviewOrchestrator.Core.Engines;

public sealed record ReviewSurfaceWarning(int MeaningfulChangedLines, int ProductionFiles, string Title, string Message);

/// <summary>Large-PR / review-surface detection (spec §11). Generated and lock files never count.</summary>
public sealed class ReviewSurfaceEvaluator(ReviewSurfaceOptions options)
{
    public ReviewSurfaceEvaluator() : this(new ReviewSurfaceOptions())
    {
    }

    public int MeaningfulChangedLines(PullRequest pr) =>
        pr.Files.Count == 0
            ? pr.Additions + pr.Deletions
            : pr.Files.Where(f => !FileClassifier.IsGenerated(f)).Sum(f => f.Additions + f.Deletions);

    public int ProductionFileCount(PullRequest pr) => pr.Files.Count(FileClassifier.IsProductionFile);

    public ReviewSurfaceWarning? Evaluate(PullRequest pr)
    {
        var lines = MeaningfulChangedLines(pr);
        var files = ProductionFileCount(pr);
        if (lines <= options.MeaningfulLineThreshold && files <= options.ProductionFileThreshold)
            return null;

        return new ReviewSurfaceWarning(
            lines,
            files,
            "Large review surface",
            $"{lines:N0} meaningful lines changed across {files} production files. " +
            (pr.IntentionallyLarge
                ? "The author marked this change as intentionally large — use the recommended review order as a map."
                : "Consider splitting the change or providing a review map."));
    }
}

public sealed record WipWarning(string AuthorId, string AuthorName, int OpenReviewCount, string Message);

/// <summary>Author work-in-progress signal (spec §15). Informational only — never blocks.</summary>
public sealed class WipEvaluator(ReviewRoutingOptions options)
{
    public WipEvaluator() : this(new ReviewRoutingOptions())
    {
    }

    public int OpenReviewCount(string authorId, IEnumerable<PullRequest> allPullRequests) =>
        allPullRequests.Count(p => p.AuthorId == authorId && p.Status.IsOpenForReview());

    public WipWarning? Evaluate(PullRequest pr, IEnumerable<PullRequest> allPullRequests)
    {
        var count = OpenReviewCount(pr.AuthorId, allPullRequests);
        if (count < options.MaxAuthorOpenReviewsBeforeWarning) return null;

        var firstName = pr.AuthorName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? pr.AuthorName;
        return new WipWarning(pr.AuthorId, pr.AuthorName, count,
            $"{firstName} currently has {count} PRs waiting for or undergoing review.");
    }
}

/// <summary>Aging buckets (spec §14). An additional signal; does not replace priority.</summary>
public sealed class AgingCalculator(QueueOptions options)
{
    public AgingCalculator() : this(new QueueOptions())
    {
    }

    public AgingState Calculate(DateTimeOffset readyForReviewAt, DateTimeOffset now)
    {
        var hours = (now - readyForReviewAt).TotalHours;
        if (hours >= options.CriticalHours) return AgingState.Critical;
        if (hours >= options.StaleHours) return AgingState.Stale;
        if (hours >= options.AgingHours) return AgingState.Aging;
        if (hours >= options.WaitingHours) return AgingState.Waiting;
        return AgingState.Fresh;
    }

    public static bool NeedsAttention(AgingState state) => state >= AgingState.Aging;
}

/// <summary>Reviewer lease rules (spec §13).</summary>
public sealed class LeasePolicy(ReviewRoutingOptions options)
{
    public LeasePolicy() : this(new ReviewRoutingOptions())
    {
    }

    public TimeSpan LeaseDuration => TimeSpan.FromHours(options.DefaultLeaseHours);

    public DateTimeOffset LeaseExpiry(DateTimeOffset assignedAt) => assignedAt + LeaseDuration;

    /// <summary>Only un-acknowledged assignments expire; once a reviewer has started, the lease no longer applies.</summary>
    public static bool IsExpired(ReviewAssignment assignment, DateTimeOffset now) =>
        assignment.Status == ReviewAssignmentStatus.Assigned && assignment.LeaseExpiresAt <= now;

    public static IReadOnlyList<ReviewAssignment> FindExpired(IEnumerable<ReviewAssignment> assignments, DateTimeOffset now) =>
        assignments.Where(a => IsExpired(a, now)).ToList();
}
