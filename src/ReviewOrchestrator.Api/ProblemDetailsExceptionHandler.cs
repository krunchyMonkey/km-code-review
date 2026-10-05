using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ReviewOrchestrator.Infrastructure.Application;

namespace ReviewOrchestrator.Api;

/// <summary>Maps expected orchestration failures and bad request bodies to RFC 7807 Problem Details.</summary>
public sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            OrchestrationException e => (e.StatusCode, e.Title, e.Message),
            BadHttpRequestException e => (StatusCodes.Status400BadRequest, "Invalid request", e.Message),
            _ => (0, "", "")
        };
        if (status == 0) return false;

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
