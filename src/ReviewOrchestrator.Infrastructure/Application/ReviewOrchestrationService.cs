using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReviewOrchestrator.Core;
using ReviewOrchestrator.Core.Domain;
using ReviewOrchestrator.Core.Engines;
using ReviewOrchestrator.Infrastructure.Persistence;

namespace ReviewOrchestrator.Infrastructure.Application;

/// <summary>Domain engines used by the orchestrator, grouped to keep the constructor readable.</summary>
public sealed record OrchestrationEngines(
    RiskEngine Risk,
    PriorityEngine Priority,
    ReviewerRouter Router,
    ReviewSurfaceEvaluator Surface,
    WipEvaluator Wip,
    AgingCalculator Aging,
    LeasePolicy Lease);

/// <summary>
/// Application service that applies the deterministic domain engines to persisted state.
/// Endpoint handlers stay thin and call into this class. Data volumes in the PoC are tiny, so PRs are loaded
/// into memory and filtered/sorted there (this also sidesteps SQLite's lack of DateTimeOffset ordering).
/// </summary>
public sealed class ReviewOrchestrationService(
    ReviewHubDbContext db,
    OrchestrationEngines engines,
    IReviewBriefProvider briefProvider,
    IReviewNotificationService notifications,
    TimeProvider timeProvider,
    ILogger<ReviewOrchestrationService> logger)
{
    private sealed record Evaluation(RiskAssessment Risk, PriorityResult Priority, AgingState Aging, int MeaningfulLines, ReviewSurfaceWarning? Surface);

    // ---------------------------------------------------------------- queue

    public async Task<QueueResponse> GetQueueAsync(QueueQuery query, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var prs = await LoadPullRequestsAsync(ct);
        var reviewers = await db.Reviewers.AsNoTracking().ToDictionaryAsync(r => r.Id, ct);

        var statuses = ParseStatuses(query.Status);
        var priorities = ParseEnums<PriorityBand>(query.Priority, "priority");
        var risks = ParseEnums<ReviewRisk>(query.Risk, "risk");
        var agingStates = ParseAgingStates(query.AgingState);
        var search = query.Search?.Trim().TrimStart('#');

        var rows = prs
            .Where(p => statuses.Contains(p.Status))
            .Select(p => (Pr: p, Eval: Evaluate(p, now), Active: p.Assignments.FirstOrDefault(a => a.IsActive)))
            .Where(x => string.IsNullOrWhiteSpace(query.Repository) || string.Equals(x.Pr.RepositoryName, query.Repository, StringComparison.OrdinalIgnoreCase))
            .Where(x => risks.Count == 0 || risks.Contains(x.Eval.Risk.Level))
            .Where(x => query.ReviewerId is null || x.Active?.ReviewerId == query.ReviewerId)
            .Where(x => string.IsNullOrWhiteSpace(query.AuthorId) || string.Equals(x.Pr.AuthorId, query.AuthorId, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(search) || Matches(x.Pr, search))
            .ToList();

        // Tab counts ignore the tab-level filters (priority band / aging) so every tab shows its own count.
        var summary = new QueueSummaryDto(
            rows.Count,
            rows.Count(x => x.Eval.Priority.Band == PriorityBand.High),
            rows.Count(x => x.Eval.Priority.Band == PriorityBand.Medium),
            rows.Count(x => x.Eval.Priority.Band == PriorityBand.Low),
            rows.Count(x => AgingCalculator.NeedsAttention(x.Eval.Aging)));

        var filtered = rows
            .Where(x => priorities.Count == 0 || priorities.Contains(x.Eval.Priority.Band))
            .Where(x => agingStates.Count == 0 || agingStates.Contains(x.Eval.Aging));

        var sorted = (query.Sort?.ToLowerInvariant() ?? "priority-desc") switch
        {
            "oldest" or "age-desc" => filtered.OrderBy(x => x.Pr.ReadyForReviewAt),
            "newest" or "age-asc" => filtered.OrderByDescending(x => x.Pr.ReadyForReviewAt),
            "smallest" or "size-asc" => filtered.OrderBy(x => x.Eval.MeaningfulLines).ThenByDescending(x => x.Eval.Priority.Score),
            "largest" or "size-desc" => filtered.OrderByDescending(x => x.Eval.MeaningfulLines).ThenByDescending(x => x.Eval.Priority.Score),
            "risk-desc" => filtered.OrderByDescending(x => x.Eval.Risk.Level).ThenByDescending(x => x.Eval.Priority.Score),
            "priority-desc" => filtered.OrderByDescending(x => x.Eval.Priority.Score).ThenBy(x => x.Pr.ReadyForReviewAt),
            var other => throw OrchestrationException.Invalid($"Unknown sort '{other}'. Use priority-desc, oldest, newest, smallest, largest or risk-desc.")
        };

        var items = sorted.Select(x =>
        {
            var reviewer = x.Active is not null && reviewers.TryGetValue(x.Active.ReviewerId, out var r) ? r : null;
            return new QueueItemDto(
                x.Pr.Id, x.Pr.ExternalId, x.Pr.Title, x.Pr.RepositoryName, x.Pr.AuthorId, x.Pr.AuthorName,
                x.Pr.ReadyForReviewAt, WaitMinutes(x.Pr, now),
                x.Eval.Priority.Score, x.Eval.Priority.Band, x.Eval.Priority.Reasons,
                x.Eval.Risk.Level, x.Eval.Aging, x.Pr.Status, x.Pr.Labels,
                x.Pr.Additions, x.Pr.Deletions, x.Pr.ChangedFiles,
                reviewer is null ? null : new ReviewerRef(reviewer.Id, reviewer.Name, reviewer.AvatarUrl),
                x.Active?.Status,
                x.Eval.Surface is not null,
                x.Pr.IsReleaseBlocker, x.Pr.IsProductionFix, x.Pr.BlocksOtherWork);
        }).ToList();

        var repositories = prs.Select(p => p.RepositoryName).Distinct().Order().ToList();
        return new QueueResponse(items, summary, repositories);
    }

    // ---------------------------------------------------------------- detail

    public async Task<PullRequestDetailDto> GetDetailAsync(Guid id, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var prs = await LoadPullRequestsAsync(ct);
        var pr = prs.FirstOrDefault(p => p.Id == id) ?? throw OrchestrationException.NotFound($"Pull request {id} was not found.");
        var reviewers = await db.Reviewers.AsNoTracking().ToListAsync(ct);

        var eval = Evaluate(pr, now);
        var routing = engines.Router.Route(BuildRoutingContext(pr, prs, reviewers, eval.Risk));
        var activeCountsAll = ActiveCounts(prs, excludePullRequestId: null);
        var byId = reviewers.ToDictionary(r => r.Id);

        var brief = await db.ReviewBriefs.AsNoTracking().FirstOrDefaultAsync(b => b.PullRequestId == pr.Id, ct)
                    ?? await TryGenerateBriefAsync(pr, ct);

        var active = pr.Assignments.FirstOrDefault(a => a.IsActive);

        AssignmentDto ToDto(ReviewAssignment a)
        {
            var reviewer = byId.GetValueOrDefault(a.ReviewerId);
            var why = a.IsActive
                ? routing.Candidates.FirstOrDefault(c => c.Reviewer.Id == a.ReviewerId)?.Reasons ?? []
                : [];
            int? leaseRemaining = a.Status == ReviewAssignmentStatus.Assigned
                ? (int)Math.Max(0, Math.Ceiling((a.LeaseExpiresAt - now).TotalMinutes))
                : null;
            return new AssignmentDto(a.Id, a.ReviewerId, reviewer?.Name ?? "Unknown reviewer", a.Status, a.AssignedAt, a.AcknowledgedAt,
                a.CompletedAt, a.LeaseExpiresAt, leaseRemaining, activeCountsAll.GetValueOrDefault(a.ReviewerId),
                reviewer?.MaxConcurrentReviews ?? 0, why);
        }

        // Display order: the assignee (or would-be pick), then reviewers who are relevant by ownership/specialty —
        // even when at capacity, so load-aware routing is visible — then everyone else in router order.
        var leadId = active?.ReviewerId ?? routing.Selected?.Id;
        var candidates = routing.Candidates
            .Select((c, index) => (c, index))
            .OrderByDescending(x => x.c.Reviewer.Id == leadId)
            .ThenByDescending(x => x.c.IsRelevant)
            .ThenBy(x => x.index)
            .Select(x => x.c)
            .Take(ReviewerRouter.CandidatesToReturn)
            .Select(c => new CandidateDto(c.Reviewer.Id, c.Reviewer.Name, c.Reviewer.AvatarUrl, c.Score, c.Eligible, c.QualifiedForRisk,
                c.ActiveReviews, c.Reviewer.MaxConcurrentReviews, c.Reviewer.Id == active?.ReviewerId, c.Reasons))
            .ToList();

        var fileLookup = pr.Files.GroupBy(f => f.Path).ToDictionary(g => g.Key, g => g.First());
        var briefDto = brief is null
            ? null
            : new BriefDto(brief.GeneratedAt, brief.WhatChanged, brief.HumanAttentionAreas, brief.MostlyMechanical,
                brief.RecommendedReviewOrder.OrderBy(r => r.Rank).Select(r =>
                {
                    var file = fileLookup.GetValueOrDefault(r.Path);
                    return new RecommendationDto(r.Rank, r.Path, r.Reason, file?.Additions, file?.Deletions);
                }).ToList());

        return new PullRequestDetailDto(
            pr.Id, pr.ExternalId, pr.Title, pr.Description, pr.RepositoryName, pr.AuthorId, pr.AuthorName,
            pr.CreatedAt, pr.ReadyForReviewAt, pr.UpdatedAt, WaitMinutes(pr, now), pr.Status, eval.Aging,
            pr.SourceBranch, pr.TargetBranch, pr.ExternalUrl, pr.LinkedWorkItem, pr.Labels,
            pr.Additions, pr.Deletions, pr.ChangedFiles, eval.MeaningfulLines, engines.Surface.ProductionFileCount(pr),
            pr.IsReleaseBlocker, pr.IsProductionFix, pr.BlocksOtherWork, pr.IsSecuritySensitive,
            new PriorityDto(eval.Priority.Score, eval.Priority.Band, eval.Priority.Reasons),
            new RiskDto(eval.Risk.Level, eval.Risk.Reasons, eval.Risk.AreasTouched, eval.Risk.Signals),
            active is null ? null : ToDto(active),
            pr.Assignments.OrderByDescending(a => a.AssignedAt).Select(ToDto).ToList(),
            candidates,
            active is null || active.Status == ReviewAssignmentStatus.Assigned ? routing.Note : null,
            briefDto,
            pr.Files.OrderByDescending(f => f.Additions + f.Deletions)
                .Select(f => new FileDto(f.Path, f.Additions, f.Deletions, FileClassifier.IsGenerated(f), FileClassifier.IsTest(f),
                    FileClassifier.IsSecuritySensitive(f), f.Component, FileClassifier.Categorize(f)))
                .ToList(),
            engines.Wip.Evaluate(pr, prs),
            eval.Surface);
    }

    // ---------------------------------------------------------------- workflow

    public async Task<PullRequestDetailDto> StartReviewAsync(Guid id, Guid reviewerId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var prs = await LoadPullRequestsAsync(ct, track: true);
        var pr = prs.FirstOrDefault(p => p.Id == id) ?? throw OrchestrationException.NotFound($"Pull request {id} was not found.");
        var reviewer = await db.Reviewers.FirstOrDefaultAsync(r => r.Id == reviewerId, ct)
                       ?? throw OrchestrationException.NotFound($"Reviewer {reviewerId} was not found.");

        if (!pr.Status.IsOpenForReview())
            throw OrchestrationException.Conflict($"PR #{pr.ExternalId} is {pr.Status} and is not open for review.");
        if (string.Equals(reviewer.ExternalId, pr.AuthorId, StringComparison.OrdinalIgnoreCase))
            throw OrchestrationException.Conflict($"{reviewer.Name} is the author of PR #{pr.ExternalId} and cannot review it.");

        var active = pr.Assignments.FirstOrDefault(a => a.IsActive);
        if (active is not null && active.ReviewerId == reviewer.Id)
        {
            if (active.Status == ReviewAssignmentStatus.Assigned)
            {
                active.Status = ReviewAssignmentStatus.Acknowledged;
                active.AcknowledgedAt = now;
            }
        }
        else
        {
            if (active is { Status: ReviewAssignmentStatus.Acknowledged })
            {
                var current = await db.Reviewers.AsNoTracking().FirstAsync(r => r.Id == active.ReviewerId, ct);
                throw OrchestrationException.Conflict($"PR #{pr.ExternalId} is already actively being reviewed by {current.Name}.");
            }

            if (!reviewer.IsActive)
                throw OrchestrationException.Conflict($"{reviewer.Name} is inactive and cannot take reviews.");

            var load = ActiveCounts(prs, excludePullRequestId: pr.Id).GetValueOrDefault(reviewer.Id);
            if (load >= reviewer.MaxConcurrentReviews)
                throw OrchestrationException.Conflict($"{reviewer.Name} is at capacity ({load}/{reviewer.MaxConcurrentReviews} active reviews).");

            if (active is not null)
            {
                // Someone else picked it up before the assigned reviewer acknowledged: hand it over.
                active.Status = ReviewAssignmentStatus.Released;
                active.CompletedAt = now;
            }

            db.ReviewAssignments.Add(new ReviewAssignment
            {
                Id = Guid.NewGuid(),
                PullRequestId = pr.Id,
                ReviewerId = reviewer.Id,
                Status = ReviewAssignmentStatus.Acknowledged,
                AssignedAt = now,
                AcknowledgedAt = now,
                LeaseExpiresAt = engines.Lease.LeaseExpiry(now),
            });
        }

        pr.Status = PullRequestStatus.InReview;
        pr.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(LogEvents.ReviewStarted, "Review started on PR #{ExternalId} by {ReviewerName} ({ReviewerId})",
            pr.ExternalId, reviewer.Name, reviewer.Id);

        return await GetDetailAsync(id, ct);
    }

    public async Task<PullRequestDetailDto> ReleaseReviewAsync(Guid id, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var prs = await LoadPullRequestsAsync(ct, track: true);
        var pr = prs.FirstOrDefault(p => p.Id == id) ?? throw OrchestrationException.NotFound($"Pull request {id} was not found.");
        var active = pr.Assignments.FirstOrDefault(a => a.IsActive)
                     ?? throw OrchestrationException.Conflict($"PR #{pr.ExternalId} has no active review assignment to release.");

        active.Status = ReviewAssignmentStatus.Released;
        active.CompletedAt = now;
        pr.Status = PullRequestStatus.WaitingForReview;
        pr.UpdatedAt = now;

        logger.LogInformation(LogEvents.ReviewReleased, "Review on PR #{ExternalId} released by reviewer {ReviewerId}", pr.ExternalId, active.ReviewerId);

        var reviewers = await db.Reviewers.ToListAsync(ct);
        await AssignNextReviewerAsync(pr, prs, reviewers, now, isReassignment: true, ct);
        await db.SaveChangesAsync(ct);

        return await GetDetailAsync(id, ct);
    }

    public async Task<PullRequestDetailDto> CompleteReviewAsync(Guid id, string? outcome, CancellationToken ct)
    {
        var status = outcome?.Trim().ToLowerInvariant() switch
        {
            "approved" or "approve" => PullRequestStatus.Approved,
            "changesrequested" or "changes-requested" or "requestchanges" => PullRequestStatus.ChangesRequested,
            _ => throw OrchestrationException.Invalid($"Invalid outcome '{outcome}'. Allowed outcomes: Approved, ChangesRequested.")
        };

        var now = timeProvider.GetUtcNow();
        var prs = await LoadPullRequestsAsync(ct, track: true);
        var pr = prs.FirstOrDefault(p => p.Id == id) ?? throw OrchestrationException.NotFound($"Pull request {id} was not found.");
        var active = pr.Assignments.FirstOrDefault(a => a.IsActive)
                     ?? throw OrchestrationException.Conflict($"PR #{pr.ExternalId} has no active review. Start a review first.");

        active.AcknowledgedAt ??= now;
        active.Status = ReviewAssignmentStatus.Completed;
        active.CompletedAt = now;
        pr.Status = status;
        pr.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(LogEvents.ReviewCompleted, "Review on PR #{ExternalId} completed by reviewer {ReviewerId} with outcome {Outcome}",
            pr.ExternalId, active.ReviewerId, status);

        return await GetDetailAsync(id, ct);
    }

    public async Task<PullRequestDetailDto> RecalculateAsync(Guid id, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var prs = await LoadPullRequestsAsync(ct, track: true);
        var pr = prs.FirstOrDefault(p => p.Id == id) ?? throw OrchestrationException.NotFound($"Pull request {id} was not found.");

        Score(pr, now);
        var existing = await db.ReviewBriefs.Where(b => b.PullRequestId == pr.Id).ToListAsync(ct);
        db.ReviewBriefs.RemoveRange(existing);
        await db.SaveChangesAsync(ct);
        await TryGenerateBriefAsync(pr, ct);

        if (pr.Status == PullRequestStatus.WaitingForReview && !pr.Assignments.Any(a => a.IsActive))
        {
            var reviewers = await db.Reviewers.ToListAsync(ct);
            await AssignNextReviewerAsync(pr, prs, reviewers, now, isReassignment: false, ct);
        }

        await db.SaveChangesAsync(ct);
        return await GetDetailAsync(id, ct);
    }

    /// <summary>Demo helper: force the current un-acknowledged lease to expire, then run the lease check.</summary>
    public async Task<PullRequestDetailDto> SimulateLeaseExpiryAsync(Guid id, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var pr = await db.PullRequests.Include(p => p.Assignments).FirstOrDefaultAsync(p => p.Id == id, ct)
                 ?? throw OrchestrationException.NotFound($"Pull request {id} was not found.");
        var active = pr.Assignments.FirstOrDefault(a => a.IsActive)
                     ?? throw OrchestrationException.Conflict($"PR #{pr.ExternalId} has no active assignment.");
        if (active.Status != ReviewAssignmentStatus.Assigned)
            throw OrchestrationException.Conflict("The review has already been started; leases only apply to un-acknowledged assignments.");

        active.LeaseExpiresAt = now.AddSeconds(-1);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        await ExpireLeasesAsync(ct);
        return await GetDetailAsync(id, ct);
    }

    // ---------------------------------------------------------------- background

    /// <summary>Expires un-acknowledged leases, reroutes those PRs, and sends one aging notification per state transition.</summary>
    public async Task<int> ExpireLeasesAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var prs = await LoadPullRequestsAsync(ct, track: true);
        var reviewers = await db.Reviewers.ToListAsync(ct);

        var expired = LeasePolicy.FindExpired(prs.Where(p => p.Status.IsOpenForReview()).SelectMany(p => p.Assignments), now);
        foreach (var assignment in expired)
        {
            var pr = prs.First(p => p.Id == assignment.PullRequestId);
            assignment.Status = ReviewAssignmentStatus.Expired;
            assignment.CompletedAt = now;
            logger.LogInformation(LogEvents.ReviewerLeaseExpired,
                "Lease expired on PR #{ExternalId} for reviewer {ReviewerId} (lease ended {LeaseExpiresAt})",
                pr.ExternalId, assignment.ReviewerId, assignment.LeaseExpiresAt);

            if (pr.Status == PullRequestStatus.WaitingForReview)
                await AssignNextReviewerAsync(pr, prs, reviewers, now, isReassignment: true, ct);
        }

        foreach (var pr in prs.Where(p => p.Status.IsOpenForReview()))
        {
            var state = engines.Aging.Calculate(pr.ReadyForReviewAt, now);
            if (!AgingCalculator.NeedsAttention(state) || pr.LastNotifiedAgingState == state) continue;
            pr.LastNotifiedAgingState = state;
            await notifications.NotifyAgingAsync(pr, ct);
        }

        await db.SaveChangesAsync(ct);
        return expired.Count;
    }

    // ---------------------------------------------------------------- reviewers & metrics

    public async Task<IReadOnlyList<ReviewerDto>> GetReviewersAsync(CancellationToken ct)
    {
        var prs = await LoadPullRequestsAsync(ct);
        var counts = ActiveCounts(prs, excludePullRequestId: null);
        var reviewers = await db.Reviewers.AsNoTracking().ToListAsync(ct);
        return reviewers
            .OrderBy(r => r.Name)
            .Select(r => new ReviewerDto(r.Id, r.ExternalId, r.Name, r.AvatarUrl, r.IsActive, counts.GetValueOrDefault(r.Id),
                r.MaxConcurrentReviews, r.Components, r.Specialties, r.RepositoryHistory))
            .ToList();
    }

    public async Task<ReviewerDto> GetCurrentUserAsync(CancellationToken ct)
    {
        var reviewers = await GetReviewersAsync(ct);
        return reviewers.FirstOrDefault(r => r.ExternalId == SeedData.CurrentUserExternalId) ?? reviewers.First();
    }

    public async Task<MetricsSummaryDto> GetMetricsAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var prs = await LoadPullRequestsAsync(ct);
        var reviewers = await db.Reviewers.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var counts = ActiveCounts(prs, excludePullRequestId: null);

        var open = prs.Where(p => p.Status.IsOpenForReview()).ToList();
        var waiting = open.Where(p => p.Status == PullRequestStatus.WaitingForReview).ToList();
        var waits = waiting.Select(p => WaitMinutes(p, now)).Order().ToList();

        var firstReviewMinutes = prs
            .Select(p => (p.ReadyForReviewAt, First: p.Assignments.Where(a => a.AcknowledgedAt.HasValue).Min(a => a.AcknowledgedAt)))
            .Where(x => x.First.HasValue)
            .Select(x => Math.Max(0, (x.First!.Value - x.ReadyForReviewAt).TotalMinutes))
            .ToList();

        var agingStates = open.Select(p => engines.Aging.Calculate(p.ReadyForReviewAt, now)).ToList();

        return new MetricsSummaryDto(
            waiting.Count,
            open.Count(p => p.Status == PullRequestStatus.InReview),
            open.Count(p => p.Status == PullRequestStatus.ChangesRequested),
            agingStates.Count(AgingCalculator.NeedsAttention),
            waits.Count == 0 ? 0 : (int)Math.Round(waits.Average()),
            Median(waits),
            firstReviewMinutes.Count == 0 ? null : (int)Math.Round(firstReviewMinutes.Average()),
            prs.Count(p => p.Status is PullRequestStatus.Approved or PullRequestStatus.ChangesRequested),
            reviewers.Select(r => new UtilizationDto(r.Id, r.Name, counts.GetValueOrDefault(r.Id), r.MaxConcurrentReviews, r.IsActive)).ToList(),
            Enum.GetValues<AgingState>().Select(s => new AgingBucketDto(s, agingStates.Count(a => a == s))).ToList());
    }

    // ---------------------------------------------------------------- initialization

    /// <summary>Imports PRs from the source-control provider (upsert by external id). Calculated fields are left to the orchestrator.</summary>
    public async Task<int> ImportAsync(IReadOnlyList<PullRequest> incoming, CancellationToken ct)
    {
        var existing = await db.PullRequests.Include(p => p.Files).ToDictionaryAsync(p => p.ExternalId, ct);
        foreach (var source in incoming)
        {
            if (existing.TryGetValue(source.ExternalId, out var pr))
            {
                pr.Title = source.Title;
                pr.Description = source.Description;
                pr.Labels = source.Labels;
                pr.UpdatedAt = source.UpdatedAt;
                continue;
            }

            source.Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id;
            foreach (var file in source.Files)
            {
                file.Id = Guid.NewGuid();
                file.PullRequestId = source.Id;
            }
            db.PullRequests.Add(source);
            logger.LogInformation(LogEvents.PullRequestImported, "Imported PR #{ExternalId} from {Repository}", source.ExternalId, source.RepositoryName);
        }

        return await db.SaveChangesAsync(ct);
    }

    /// <summary>Scores every PR, generates missing briefs and routes unassigned waiting PRs (highest priority picks first).</summary>
    public async Task ScoreAndRouteAllAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var prs = await LoadPullRequestsAsync(ct, track: true);
        var reviewers = await db.Reviewers.ToListAsync(ct);
        var briefed = (await db.ReviewBriefs.Select(b => b.PullRequestId).ToListAsync(ct)).ToHashSet();

        foreach (var pr in prs)
        {
            Score(pr, now);
            if (!briefed.Contains(pr.Id)) await TryGenerateBriefAsync(pr, ct, save: false);
        }

        foreach (var pr in prs
                     .Where(p => p.Status == PullRequestStatus.WaitingForReview && !p.Assignments.Any(a => a.IsActive))
                     .OrderByDescending(p => p.PriorityScore)
                     .ThenBy(p => p.ReadyForReviewAt))
        {
            await AssignNextReviewerAsync(pr, prs, reviewers, now, isReassignment: false, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<List<PullRequest>> LoadPullRequestsAsync(CancellationToken ct, bool track = false)
    {
        var query = db.PullRequests.Include(p => p.Files).Include(p => p.Assignments).AsSplitQuery();
        return track ? await query.ToListAsync(ct) : await query.AsNoTracking().ToListAsync(ct);
    }

    private Evaluation Evaluate(PullRequest pr, DateTimeOffset now)
    {
        var risk = engines.Risk.Assess(pr);
        var lines = engines.Surface.MeaningfulChangedLines(pr);
        var priority = engines.Priority.Calculate(pr, risk.Level, lines, now);
        return new Evaluation(risk, priority, engines.Aging.Calculate(pr.ReadyForReviewAt, now), lines, engines.Surface.Evaluate(pr));
    }

    /// <summary>Persists the calculated fields on the entity (the queue still recomputes live because age changes).</summary>
    private Evaluation Score(PullRequest pr, DateTimeOffset now)
    {
        var eval = Evaluate(pr, now);
        pr.Risk = eval.Risk.Level;
        pr.PriorityScore = eval.Priority.Score;
        pr.MeaningfulChangedLines = eval.MeaningfulLines;

        logger.LogInformation(LogEvents.RiskCalculated, "Risk for PR #{ExternalId}: {Risk} ({ReasonCount} reasons)",
            pr.ExternalId, eval.Risk.Level, eval.Risk.Reasons.Count);
        logger.LogInformation(LogEvents.PullRequestScored, "PR #{ExternalId} scored {PriorityScore} ({PriorityBand})",
            pr.ExternalId, eval.Priority.Score, eval.Priority.Band);
        return eval;
    }

    private RoutingContext BuildRoutingContext(PullRequest pr, IReadOnlyList<PullRequest> all, IReadOnlyList<Reviewer> reviewers, RiskAssessment risk)
    {
        var excluded = pr.Assignments
            .Where(a => a.Status is ReviewAssignmentStatus.Released or ReviewAssignmentStatus.Expired)
            .Select(a => a.ReviewerId)
            .ToHashSet();
        return new RoutingContext(pr, risk, reviewers, ActiveCounts(all, pr.Id), excluded);
    }

    private async Task AssignNextReviewerAsync(
        PullRequest pr,
        IReadOnlyList<PullRequest> all,
        IReadOnlyList<Reviewer> reviewers,
        DateTimeOffset now,
        bool isReassignment,
        CancellationToken ct)
    {
        var risk = engines.Risk.Assess(pr);
        var result = engines.Router.Route(BuildRoutingContext(pr, all, reviewers, risk));
        if (result.Selected is null)
        {
            logger.LogWarning(LogEvents.ReviewerUnavailable, "No eligible reviewer for PR #{ExternalId}: {Note}", pr.ExternalId, result.Note);
            return;
        }

        var assignment = new ReviewAssignment
        {
            Id = Guid.NewGuid(),
            PullRequestId = pr.Id,
            ReviewerId = result.Selected.Id,
            Status = ReviewAssignmentStatus.Assigned,
            AssignedAt = now,
            LeaseExpiresAt = engines.Lease.LeaseExpiry(now),
        };
        pr.Assignments.Add(assignment); // keeps in-memory active counts correct while routing several PRs in a row
        db.ReviewAssignments.Add(assignment);

        var top = result.Candidates[0];
        logger.LogInformation(LogEvents.ReviewerAssigned,
            "Assigned PR #{ExternalId} to {ReviewerName} (score {Score}, reassignment: {IsReassignment})",
            pr.ExternalId, result.Selected.Name, top.Score, isReassignment);

        if (isReassignment) await notifications.NotifyReassignedAsync(assignment, ct);
        else await notifications.NotifyAssignedAsync(assignment, ct);
    }

    private async Task<ReviewBrief?> TryGenerateBriefAsync(PullRequest pr, CancellationToken ct, bool save = true)
    {
        try
        {
            var brief = await briefProvider.GenerateAsync(pr, ct);
            brief.Id = brief.Id == Guid.Empty ? Guid.NewGuid() : brief.Id;
            brief.PullRequestId = pr.Id;
            db.ReviewBriefs.Add(brief);
            if (save) await db.SaveChangesAsync(ct);

            logger.LogInformation(LogEvents.ReviewBriefGenerated, "Review brief generated for PR #{ExternalId}: {AttentionCount} attention areas, {FileCount} files in review order",
                pr.ExternalId, brief.HumanAttentionAreas.Count, brief.RecommendedReviewOrder.Count);
            return brief;
        }
        catch (NotSupportedException ex)
        {
            logger.LogWarning(ex, "Review brief provider is not available for PR #{ExternalId}", pr.ExternalId);
            return null;
        }
    }

    private static Dictionary<Guid, int> ActiveCounts(IEnumerable<PullRequest> prs, Guid? excludePullRequestId) =>
        prs.Where(p => p.Id != excludePullRequestId)
            .SelectMany(p => p.Assignments)
            .Where(a => a.IsActive)
            .GroupBy(a => a.ReviewerId)
            .ToDictionary(g => g.Key, g => g.Count());

    private static int WaitMinutes(PullRequest pr, DateTimeOffset now) => (int)Math.Max(0, (now - pr.ReadyForReviewAt).TotalMinutes);

    private static int Median(IReadOnlyList<int> sorted) =>
        sorted.Count == 0 ? 0
        : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2]
        : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;

    private static bool Matches(PullRequest pr, string search) =>
        pr.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
        || pr.ExternalId.Equals(search, StringComparison.OrdinalIgnoreCase)
        || pr.AuthorName.Contains(search, StringComparison.OrdinalIgnoreCase)
        || pr.RepositoryName.Contains(search, StringComparison.OrdinalIgnoreCase)
        || pr.Labels.Any(l => l.Contains(search, StringComparison.OrdinalIgnoreCase));

    private static HashSet<PullRequestStatus> ParseStatuses(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("open", StringComparison.OrdinalIgnoreCase))
            return [PullRequestStatus.WaitingForReview, PullRequestStatus.InReview, PullRequestStatus.ChangesRequested];
        if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
            return [.. Enum.GetValues<PullRequestStatus>()];
        return ParseEnums<PullRequestStatus>(value, "status");
    }

    private static HashSet<AgingState> ParseAgingStates(string? value)
    {
        // "attention" (or "aging" in lower case from the Aging tab) means Aging, Stale or Critical.
        if (value is "attention" or "aging")
            return [AgingState.Aging, AgingState.Stale, AgingState.Critical];
        return ParseEnums<AgingState>(value, "agingState");
    }

    private static HashSet<T> ParseEnums<T>(string? value, string parameter) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var result = new HashSet<T>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<T>(part, ignoreCase: true, out var parsed))
                throw OrchestrationException.Invalid($"Invalid {parameter} '{part}'. Allowed: {string.Join(", ", Enum.GetNames<T>())}.");
            result.Add(parsed);
        }
        return result;
    }
}
