using ReviewOrchestrator.Infrastructure.Application;

namespace ReviewOrchestrator.Api;

/// <summary>Thin HTTP layer: binds input, calls the orchestration service, returns DTOs. No domain logic here.</summary>
public static class Endpoints
{
    public static void MapReviewOrchestratorApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        api.MapGet("/review-queue", (
                string? repository, string? priority, string? risk, string? status, Guid? reviewerId,
                string? authorId, string? agingState, string? search, string? sort,
                ReviewOrchestrationService service, CancellationToken ct) =>
            service.GetQueueAsync(new QueueQuery(repository, priority, risk, status, reviewerId, authorId, agingState, search, sort), ct));

        var prs = api.MapGroup("/pull-requests/{id:guid}");

        prs.MapGet("", (Guid id, ReviewOrchestrationService service, CancellationToken ct) =>
            service.GetDetailAsync(id, ct));

        prs.MapPost("/start-review", (Guid id, StartReviewRequest? body, ReviewOrchestrationService service, CancellationToken ct) =>
            body is null || body.ReviewerId == Guid.Empty
                ? throw OrchestrationException.Invalid("reviewerId is required.")
                : service.StartReviewAsync(id, body.ReviewerId, ct));

        prs.MapPost("/release-review", (Guid id, ReviewOrchestrationService service, CancellationToken ct) =>
            service.ReleaseReviewAsync(id, ct));

        prs.MapPost("/complete-review", (Guid id, CompleteReviewRequest? body, ReviewOrchestrationService service, CancellationToken ct) =>
            service.CompleteReviewAsync(id, body?.Outcome, ct));

        prs.MapPost("/recalculate", (Guid id, ReviewOrchestrationService service, CancellationToken ct) =>
            service.RecalculateAsync(id, ct));

        // Demo helper so lease rerouting can be shown without waiting four hours.
        prs.MapPost("/simulate-lease-expiry", (Guid id, ReviewOrchestrationService service, CancellationToken ct) =>
            service.SimulateLeaseExpiryAsync(id, ct));

        api.MapGet("/reviewers", (ReviewOrchestrationService service, CancellationToken ct) => service.GetReviewersAsync(ct));

        // No auth in the PoC: a fixed demo identity stands in for the signed-in user.
        api.MapGet("/me", (ReviewOrchestrationService service, CancellationToken ct) => service.GetCurrentUserAsync(ct));

        api.MapGet("/metrics/summary", (ReviewOrchestrationService service, CancellationToken ct) => service.GetMetricsAsync(ct));
    }
}
