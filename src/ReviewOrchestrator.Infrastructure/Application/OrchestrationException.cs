namespace ReviewOrchestrator.Infrastructure.Application;

/// <summary>Expected domain failures. The API maps these to RFC 7807 Problem Details.</summary>
public class OrchestrationException(int statusCode, string title, string detail) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;

    public static OrchestrationException NotFound(string detail) => new(404, "Not found", detail);
    public static OrchestrationException Invalid(string detail) => new(400, "Invalid request", detail);
    public static OrchestrationException Conflict(string detail) => new(409, "Conflict", detail);
}
