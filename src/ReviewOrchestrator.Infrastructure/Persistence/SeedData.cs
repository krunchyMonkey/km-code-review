using ReviewOrchestrator.Core.Domain;

namespace ReviewOrchestrator.Infrastructure.Persistence;

/// <summary>Demo reviewers (spec §32) and pre-existing review state used to show load-aware routing.</summary>
public static class SeedData
{
    public static List<Reviewer> Reviewers() =>
    [
        R("bob-smith", "Bob Smith", ["backend", "skills"], ["backend", "ai"], ["ai-assistant"]),
        R("kent-jones", "Kent Jones", ["backend", "skills"], ["backend", "architecture"], ["ai-assistant", "platform"]),
        R("priya-singh", "Priya Singh", ["platform"], ["auth", "security", "infra"], ["platform"]),
        R("sarah-miller", "Sarah Miller", ["web", "docs"], ["frontend", "ui"], ["web", "docs"]),
        R("dana-wu", "Dana Wu", ["platform", "persistence", "storage"], ["performance", "database"], ["platform", "ai-assistant"]),
        R("jordan-park", "Jordan Park", ["observability"], ["telemetry", "analytics"], ["ai-assistant"]),
        R("elena-garcia", "Elena Garcia", ["platform"], ["security", "auth", "infra"], ["platform"]),
        R("noah-patel", "Noah Patel", ["web"], ["frontend"], ["web"], isActive: false),
    ];

    /// <summary>The reviewer the UI treats as the signed-in user (no real auth in the PoC).</summary>
    public const string CurrentUserExternalId = "bob-smith";

    public sealed record SeedAssignment(
        string PullRequestExternalId,
        string ReviewerExternalId,
        ReviewAssignmentStatus Status,
        TimeSpan AssignedAgo,
        TimeSpan? AcknowledgedAgo = null,
        TimeSpan? CompletedAgo = null);

    /// <summary>
    /// Existing review state at seed time. In a real integration this would come from reviewer votes / requested reviewers.
    /// PR 190 has an un-acknowledged assignment whose lease has already expired, so the lease service reroutes it on startup.
    /// </summary>
    public static IReadOnlyList<SeedAssignment> Assignments() =>
    [
        new("222", "kent-jones", ReviewAssignmentStatus.Acknowledged, TimeSpan.FromMinutes(260), TimeSpan.FromMinutes(100)),
        new("215", "kent-jones", ReviewAssignmentStatus.Acknowledged, TimeSpan.FromMinutes(400), TimeSpan.FromMinutes(180)),
        new("210", "kent-jones", ReviewAssignmentStatus.Acknowledged, TimeSpan.FromHours(16), TimeSpan.FromHours(5)),
        new("207", "priya-singh", ReviewAssignmentStatus.Acknowledged, TimeSpan.FromHours(9), TimeSpan.FromHours(2)),
        new("203", "dana-wu", ReviewAssignmentStatus.Acknowledged, TimeSpan.FromHours(20), TimeSpan.FromHours(6)),
        new("196", "bob-smith", ReviewAssignmentStatus.Completed, TimeSpan.FromHours(29), TimeSpan.FromHours(26), TimeSpan.FromHours(25)),
        new("190", "sarah-miller", ReviewAssignmentStatus.Assigned, TimeSpan.FromMinutes(270)),
    ];

    private static Reviewer R(string id, string name, List<string> components, List<string> specialties, List<string> repos, bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            ExternalId = id,
            Name = name,
            Components = components,
            Specialties = specialties,
            RepositoryHistory = repos,
            MaxConcurrentReviews = 3,
            IsActive = isActive,
        };
}
