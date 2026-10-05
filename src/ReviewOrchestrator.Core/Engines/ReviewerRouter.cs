using ReviewOrchestrator.Core.Domain;

namespace ReviewOrchestrator.Core.Engines;

public sealed record ReviewerCandidate(
    Reviewer Reviewer,
    int Score,
    bool Eligible,
    bool QualifiedForRisk,
    int ActiveReviews,
    IReadOnlyList<string> Reasons,
    bool IsRelevant = false);

public sealed record RoutingResult(Reviewer? Selected, IReadOnlyList<ReviewerCandidate> Candidates, string? Note);

/// <param name="ActiveReviewCounts">
/// Active (Assigned + Acknowledged) review counts per reviewer, <b>excluding</b> any assignment on the PR being routed,
/// so a reviewer's own assignment on this PR never counts against them.
/// </param>
/// <param name="ExcludedReviewerIds">Reviewers that must not be picked (e.g. released or let the lease expire on this PR).</param>
public sealed record RoutingContext(
    PullRequest PullRequest,
    RiskAssessment Risk,
    IReadOnlyList<Reviewer> Reviewers,
    IReadOnlyDictionary<Guid, int> ActiveReviewCounts,
    IReadOnlySet<Guid>? ExcludedReviewerIds = null);

/// <summary>Ownership + specialty + load based reviewer selection (spec §12).</summary>
public sealed class ReviewerRouter
{
    public const int OwnsComponentPoints = 40;
    public const int SpecialtyMatchPoints = 30;
    public const int RepositoryHistoryPoints = 15;
    public const int PerActiveReviewPenalty = 15;
    public const int InactivePenalty = 100;
    public const int AtCapacityPenalty = 100;

    /// <summary>Specialties that qualify someone to review any High-risk PR.</summary>
    public static readonly IReadOnlySet<string> HighRiskSpecialties =
        new HashSet<string>(["security", "auth", "infra", "database"], StringComparer.OrdinalIgnoreCase);

    public const int CandidatesToReturn = 3;

    public static IReadOnlyList<string> ChangedComponents(PullRequest pr)
    {
        var components = pr.Files
            .Select(f => f.Component)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.ToLowerInvariant())
            .Distinct()
            .ToList();
        return components.Count > 0 ? components : [pr.RepositoryName.ToLowerInvariant()];
    }

    public RoutingResult Route(RoutingContext context)
    {
        var pr = context.PullRequest;
        var excluded = context.ExcludedReviewerIds ?? new HashSet<Guid>();
        var components = ChangedComponents(pr);
        var focusAreas = new HashSet<string>(pr.Labels.Concat(context.Risk.RiskAreas), StringComparer.OrdinalIgnoreCase);
        var qualifyingSpecialties = new HashSet<string>(HighRiskSpecialties.Concat(context.Risk.RiskAreas), StringComparer.OrdinalIgnoreCase);
        var isHighRisk = context.Risk.Level == ReviewRisk.High;

        var candidates = new List<ReviewerCandidate>();
        foreach (var reviewer in context.Reviewers)
        {
            var score = 0;
            var eligible = true;
            var reasons = new List<string>();

            var owned = reviewer.Components.Where(c => components.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
            if (owned.Count > 0)
            {
                score += OwnsComponentPoints;
                reasons.Add($"Owns {string.Join(", ", owned)} component");
            }

            var specialties = reviewer.Specialties.Where(focusAreas.Contains).ToList();
            if (specialties.Count > 0)
            {
                score += SpecialtyMatchPoints;
                reasons.Add($"Specialty match: {string.Join(", ", specialties)}");
            }

            if (reviewer.RepositoryHistory.Contains(pr.RepositoryName, StringComparer.OrdinalIgnoreCase))
            {
                score += RepositoryHistoryPoints;
                reasons.Add($"Has reviewed {pr.RepositoryName} before");
            }

            var active = context.ActiveReviewCounts.GetValueOrDefault(reviewer.Id);
            score -= PerActiveReviewPenalty * active;
            reasons.Add(active switch
            {
                0 => "No active reviews",
                1 => "1 active review",
                _ => $"{active} active reviews"
            });

            if (!reviewer.IsActive)
            {
                score -= InactivePenalty;
                eligible = false;
                reasons.Add("Inactive");
            }

            if (active >= reviewer.MaxConcurrentReviews)
            {
                score -= AtCapacityPenalty;
                eligible = false;
                reasons.Add($"At capacity ({active}/{reviewer.MaxConcurrentReviews})");
            }

            if (string.Equals(reviewer.ExternalId, pr.AuthorId, StringComparison.OrdinalIgnoreCase))
            {
                eligible = false;
                reasons.Add("Author of this PR");
            }

            if (excluded.Contains(reviewer.Id))
            {
                eligible = false;
                reasons.Add("Already released or timed out on this PR");
            }

            var qualified = reviewer.Specialties.Any(qualifyingSpecialties.Contains);
            if (isHighRisk)
                reasons.Add(qualified ? "Qualified for high-risk review" : "Not a high-risk specialist");

            var relevant = owned.Count > 0 || specialties.Count > 0;
            candidates.Add(new ReviewerCandidate(reviewer, score, eligible, qualified, active, reasons, relevant));
        }

        var anyQualifiedEligible = isHighRisk && candidates.Any(c => c.Eligible && c.QualifiedForRisk);

        var ordered = candidates
            .OrderByDescending(c => c.Eligible)
            .ThenByDescending(c => anyQualifiedEligible && c.QualifiedForRisk)
            .ThenByDescending(c => c.Score)
            .ThenBy(c => c.ActiveReviews)
            .ThenBy(c => c.Reviewer.Name, StringComparer.Ordinal)
            .ToList();

        var selected = ordered.FirstOrDefault(c => c.Eligible);

        // Make load balancing visible: call it out when the pick carries less load than every other relevant reviewer.
        if (selected is not null && selected.IsRelevant)
        {
            var otherRelevant = candidates.Where(c => c != selected && c.IsRelevant && c.Reviewer.IsActive).ToList();
            if (otherRelevant.Count > 0 && otherRelevant.All(c => c.ActiveReviews > selected.ActiveReviews))
            {
                var withLoad = selected with { Reasons = [.. selected.Reasons, "Lowest current load among qualified reviewers"] };
                ordered[ordered.IndexOf(selected)] = withLoad;
                selected = withLoad;
            }
        }

        string? note = null;
        if (selected is null)
            note = "No eligible reviewer has capacity right now; the PR stays unassigned in the queue.";
        else if (isHighRisk && !anyQualifiedEligible)
            note = "High-risk PR, but no qualified specialist has capacity — assigned the best available reviewer.";
        else if (isHighRisk)
            note = "High-risk PR: restricted to reviewers with a matching security/infra/database specialty.";

        return new RoutingResult(selected?.Reviewer, ordered, note);
    }
}
