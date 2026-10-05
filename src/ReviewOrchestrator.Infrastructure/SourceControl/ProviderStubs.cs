using ReviewOrchestrator.Core;
using ReviewOrchestrator.Core.Domain;

namespace ReviewOrchestrator.Infrastructure.SourceControl;

/// <summary>
/// Future integration boundary. Would map Azure DevOps PRs (Pull Requests API, iterations/changes API, reviewer votes,
/// work-item links) into the internal <see cref="PullRequest"/> model. The token must come from an environment variable
/// (e.g. SourceControl__AzureDevOps__Token) and never reach the frontend.
/// </summary>
public sealed class AzureDevOpsSourceControlProvider : ISourceControlProvider
{
    public Task<IReadOnlyList<PullRequest>> GetPullRequestsAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("The Azure DevOps provider is not implemented in the PoC. Set SourceControl:Provider=Mock.");

    public Task<PullRequest?> GetPullRequestAsync(string externalId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The Azure DevOps provider is not implemented in the PoC. Set SourceControl:Provider=Mock.");
}

/// <summary>
/// Future integration boundary. Would map GitHub PRs (pulls API, changed files, requested reviewers, CODEOWNERS, labels,
/// check runs) into the internal model.
/// </summary>
public sealed class GitHubSourceControlProvider : ISourceControlProvider
{
    public Task<IReadOnlyList<PullRequest>> GetPullRequestsAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("The GitHub provider is not implemented in the PoC. Set SourceControl:Provider=Mock.");

    public Task<PullRequest?> GetPullRequestAsync(string externalId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The GitHub provider is not implemented in the PoC. Set SourceControl:Provider=Mock.");
}
