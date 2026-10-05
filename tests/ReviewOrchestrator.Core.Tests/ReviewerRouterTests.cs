using ReviewOrchestrator.Core.Domain;
using ReviewOrchestrator.Core.Engines;
using static ReviewOrchestrator.Core.Tests.TestData;

namespace ReviewOrchestrator.Core.Tests;

public class ReviewerRouterTests
{
    private readonly ReviewerRouter _router = new();
    private readonly RiskEngine _risk = new();

    private RoutingResult Route(PullRequest pr, IReadOnlyList<Reviewer> reviewers, Dictionary<Reviewer, int>? load = null, IEnumerable<Reviewer>? excluded = null) =>
        _router.Route(new RoutingContext(
            pr,
            _risk.Assess(pr),
            reviewers,
            (load ?? []).ToDictionary(kv => kv.Key.Id, kv => kv.Value),
            excluded?.Select(r => r.Id).ToHashSet()));

    private static PullRequest BackendPr(string author = "author") =>
        Pr(labels: ["backend"], authorId: author, files: [File("src/Skills/Coordinator.cs", 50, 10, "skills")]);

    [Fact]
    public void Component_owner_is_preferred()
    {
        var owner = Reviewer("Owner", components: ["skills"]);
        var other = Reviewer("Other", specialties: ["frontend"]);

        var result = Route(BackendPr(), [other, owner]);

        Assert.Equal(owner, result.Selected);
        Assert.Contains(result.Candidates[0].Reasons, r => r.Contains("Owns skills"));
        Assert.Equal(ReviewerRouter.OwnsComponentPoints, result.Candidates[0].Score);
    }

    [Fact]
    public void Overloaded_reviewer_is_penalized()
    {
        var busy = Reviewer("Busy", components: ["skills"], capacity: 5);
        var free = Reviewer("Free", components: ["skills"], capacity: 5);

        var result = Route(BackendPr(), [busy, free], new() { [busy] = 2 });

        Assert.Equal(free, result.Selected);
        var busyCandidate = result.Candidates.Single(c => c.Reviewer == busy);
        Assert.Equal(ReviewerRouter.OwnsComponentPoints - 2 * ReviewerRouter.PerActiveReviewPenalty, busyCandidate.Score);
        Assert.Contains("2 active reviews", busyCandidate.Reasons);
        Assert.Contains("Lowest current load among qualified reviewers", result.Candidates[0].Reasons);
    }

    [Fact]
    public void Reviewer_at_capacity_is_not_selected()
    {
        var full = Reviewer("Full", components: ["skills"], specialties: ["backend"], repos: ["ai-assistant"], capacity: 2);
        var weaker = Reviewer("Weaker", repos: ["ai-assistant"]);

        var result = Route(BackendPr(), [full, weaker], new() { [full] = 2 });

        Assert.Equal(weaker, result.Selected);
        var fullCandidate = result.Candidates.Single(c => c.Reviewer == full);
        Assert.False(fullCandidate.Eligible);
        Assert.Contains(fullCandidate.Reasons, r => r.StartsWith("At capacity"));
    }

    [Fact]
    public void Inactive_reviewer_is_not_selected()
    {
        var inactive = Reviewer("Inactive", components: ["skills"], specialties: ["backend"], isActive: false);
        var active = Reviewer("Active");

        var result = Route(BackendPr(), [inactive, active]);

        Assert.Equal(active, result.Selected);
        Assert.False(result.Candidates.Single(c => c.Reviewer == inactive).Eligible);
    }

    [Fact]
    public void Nobody_selected_when_everyone_is_unavailable()
    {
        var full = Reviewer("Full", components: ["skills"], capacity: 1);
        var result = Route(BackendPr(), [full], new() { [full] = 1 });

        Assert.Null(result.Selected);
        Assert.NotNull(result.Note);
    }

    [Fact]
    public void Author_is_never_selected()
    {
        var author = Reviewer("Alex Chen", components: ["skills"]);
        var other = Reviewer("Other");

        var result = Route(BackendPr(author: author.ExternalId), [author, other]);

        Assert.Equal(other, result.Selected);
    }

    [Fact]
    public void Excluded_reviewer_is_not_selected_again()
    {
        var released = Reviewer("Released", components: ["skills"]);
        var next = Reviewer("Next", components: ["skills"]);

        var result = Route(BackendPr(), [released, next], excluded: [released]);

        Assert.Equal(next, result.Selected);
    }

    [Fact]
    public void Security_sensitive_pr_prefers_qualified_security_reviewer()
    {
        var pr = Pr(labels: ["backend"], files: [File("src/Auth/TokenRefreshService.cs", 40, 10, "platform")]);
        // The generalist owns the component and has history, so they out-score the specialist on raw points…
        var generalist = Reviewer("Generalist", components: ["platform"], specialties: ["backend"], repos: ["ai-assistant"]);
        var specialist = Reviewer("Specialist", specialties: ["security"]);

        var result = Route(pr, [generalist, specialist]);

        // …but High risk restricts the pick to someone with a matching security specialty.
        Assert.True(result.Candidates.Single(c => c.Reviewer == generalist).Score > result.Candidates.Single(c => c.Reviewer == specialist).Score);
        Assert.Equal(specialist, result.Selected);
        Assert.True(result.Candidates[0].QualifiedForRisk);
    }

    [Fact]
    public void High_risk_falls_back_to_best_available_when_no_specialist_has_capacity()
    {
        var pr = Pr(files: [File("src/Auth/TokenRefreshService.cs", 40, 10, "platform")]);
        var generalist = Reviewer("Generalist", components: ["platform"]);
        var specialist = Reviewer("Specialist", specialties: ["security"], capacity: 1);

        var result = Route(pr, [generalist, specialist], new() { [specialist] = 1 });

        Assert.Equal(generalist, result.Selected);
        Assert.Contains("no qualified specialist", result.Note);
    }

    [Fact]
    public void Specialty_and_history_add_points()
    {
        var reviewer = Reviewer("Expert", specialties: ["backend"], repos: ["ai-assistant"]);
        var result = Route(BackendPr(), [reviewer]);

        Assert.Equal(ReviewerRouter.SpecialtyMatchPoints + ReviewerRouter.RepositoryHistoryPoints, result.Candidates[0].Score);
    }
}
