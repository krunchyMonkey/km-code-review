using ReviewOrchestrator.Core.Domain;

namespace ReviewOrchestrator.Core.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 18, 0, 0, TimeSpan.Zero);

    public static PullRequest Pr(
        double ageHours = 1,
        IEnumerable<string>? labels = null,
        IEnumerable<PullRequestFile>? files = null,
        string authorId = "author",
        string repository = "ai-assistant",
        Action<PullRequest>? configure = null)
    {
        var fileList = files?.ToList() ?? [File("src/Feature/Widget.cs", 40, 10)];
        var pr = new PullRequest
        {
            Id = Guid.NewGuid(),
            ExternalId = Random.Shared.Next(1000, 9999).ToString(),
            RepositoryId = repository,
            RepositoryName = repository,
            Title = "Test PR",
            AuthorId = authorId,
            AuthorName = "Test Author",
            CreatedAt = Now.AddHours(-ageHours - 1),
            ReadyForReviewAt = Now.AddHours(-ageHours),
            UpdatedAt = Now.AddHours(-ageHours),
            Status = PullRequestStatus.WaitingForReview,
            Labels = labels?.ToList() ?? [],
            Files = fileList,
            Additions = fileList.Sum(f => f.Additions),
            Deletions = fileList.Sum(f => f.Deletions),
            ChangedFiles = fileList.Count,
        };
        configure?.Invoke(pr);
        return pr;
    }

    public static PullRequestFile File(string path, int additions = 10, int deletions = 0, string? component = null) =>
        new() { Id = Guid.NewGuid(), Path = path, Additions = additions, Deletions = deletions, Component = component };

    /// <summary>Production files with a fixed number of changed lines in total.</summary>
    public static IEnumerable<PullRequestFile> ProductionFiles(int count, int linesEach, string component = "backend") =>
        Enumerable.Range(1, count).Select(i => File($"src/Feature/File{i}.cs", linesEach, 0, component));

    public static Reviewer Reviewer(
        string name,
        IEnumerable<string>? components = null,
        IEnumerable<string>? specialties = null,
        IEnumerable<string>? repos = null,
        bool isActive = true,
        int capacity = 3) =>
        new()
        {
            Id = Guid.NewGuid(),
            ExternalId = name.ToLowerInvariant().Replace(' ', '-'),
            Name = name,
            Components = components?.ToList() ?? [],
            Specialties = specialties?.ToList() ?? [],
            RepositoryHistory = repos?.ToList() ?? [],
            IsActive = isActive,
            MaxConcurrentReviews = capacity,
        };
}

internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}
