namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for GitHubIssueAgent entity with PostgreSQL snake_case naming.
/// Configures composite primary key for many-to-many relationship between issues and agents.
/// </summary>
public class GitHubIssueAgentConfiguration : IEntityTypeConfiguration<GitHubIssueAgent>
{
    public void Configure(EntityTypeBuilder<GitHubIssueAgent> builder)
    {
        builder.ToTable("github_issue_agents");

        // Composite primary key (github_issue_id, agent)
        builder.HasKey(a => new { a.GitHubIssueId, a.Agent });

        builder.Property(a => a.GitHubIssueId)
            .HasColumnName("github_issue_id")
            .IsRequired();

        builder.Property(a => a.Agent)
            .HasColumnName("agent")
            .HasMaxLength(50)
            .IsRequired();

        // Foreign key to GitHubIssue with cascade delete
        builder.HasOne(a => a.GitHubIssue)
            .WithMany(i => i.Agents)
            .HasForeignKey(a => a.GitHubIssueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
