using Microsoft.EntityFrameworkCore;
using ReviewOrchestrator.Core.Domain;

namespace ReviewOrchestrator.Infrastructure.Persistence;

public sealed class ReviewHubDbContext(DbContextOptions<ReviewHubDbContext> options) : DbContext(options)
{
    public DbSet<PullRequest> PullRequests => Set<PullRequest>();
    public DbSet<PullRequestFile> PullRequestFiles => Set<PullRequestFile>();
    public DbSet<Reviewer> Reviewers => Set<Reviewer>();
    public DbSet<ReviewAssignment> ReviewAssignments => Set<ReviewAssignment>();
    public DbSet<ReviewBrief> ReviewBriefs => Set<ReviewBrief>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PullRequest>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasIndex(p => p.ExternalId).IsUnique();
            b.Property(p => p.Status).HasConversion<string>();
            b.Property(p => p.Risk).HasConversion<string>();
            b.Property(p => p.LastNotifiedAgingState).HasConversion<string>();
            b.Property(p => p.Labels); // primitive collection -> JSON column
            b.HasMany(p => p.Files).WithOne().HasForeignKey(f => f.PullRequestId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Assignments).WithOne().HasForeignKey(a => a.PullRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PullRequestFile>(b => b.HasKey(f => f.Id));

        modelBuilder.Entity<Reviewer>(b =>
        {
            b.HasKey(r => r.Id);
            b.HasIndex(r => r.ExternalId).IsUnique();
        });

        modelBuilder.Entity<ReviewAssignment>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.Status).HasConversion<string>();
            b.HasOne<Reviewer>().WithMany().HasForeignKey(a => a.ReviewerId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.PullRequestId, a.Status });
        });

        modelBuilder.Entity<ReviewBrief>(b =>
        {
            b.HasKey(r => r.Id);
            b.HasIndex(r => r.PullRequestId).IsUnique();
            b.HasOne<PullRequest>().WithOne().HasForeignKey<ReviewBrief>(r => r.PullRequestId).OnDelete(DeleteBehavior.Cascade);
            b.OwnsMany(r => r.RecommendedReviewOrder, o => o.ToJson());
        });
    }
}
