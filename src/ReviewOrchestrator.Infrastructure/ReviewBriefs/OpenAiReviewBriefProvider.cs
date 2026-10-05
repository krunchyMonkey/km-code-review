using ReviewOrchestrator.Core;
using ReviewOrchestrator.Core.Domain;

namespace ReviewOrchestrator.Infrastructure.ReviewBriefs;

/// <summary>
/// Future LLM-backed brief provider. Intentionally not implemented in the PoC.
/// When implemented: read the API key from an environment variable, treat all repository content and PR text as
/// untrusted input (wrap it as data, never as instructions), cap the diff size, and validate the structured output.
/// </summary>
public sealed class OpenAiReviewBriefProvider : IReviewBriefProvider
{
    public Task<ReviewBrief> GenerateAsync(PullRequest pullRequest, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The LLM review brief provider is not implemented in the PoC. Set ReviewBrief:Provider=Mock.");
}
