namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for GitHubIssue entity.
/// Minimal reference table with (repository_id, issue_number) unique constraint.
/// </summary>
public class GitHubIssueConfiguration : IEntityTypeConfiguration<GitHubIssue>
{
    public void Configure(EntityTypeBuilder<GitHubIssue> builder)
    {
        builder.ToTable("github_issues");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id");

        builder.Property(i => i.RepositoryId)
            .HasColumnName("repository_id")
            .IsRequired();

        builder.Property(i => i.IssueNumber)
            .HasColumnName("issue_number")
            .IsRequired();

        // Unique constraint on (RepositoryId, IssueNumber)
        builder.HasIndex(i => new { i.RepositoryId, i.IssueNumber })
            .IsUnique();
    }
}
