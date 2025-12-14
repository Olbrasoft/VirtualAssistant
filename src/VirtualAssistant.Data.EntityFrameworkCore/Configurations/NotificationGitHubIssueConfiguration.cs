using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for NotificationGitHubIssue junction entity.
/// Enables many-to-many relationship between Notification and GitHub issues.
/// </summary>
public class NotificationGitHubIssueConfiguration : IEntityTypeConfiguration<NotificationGitHubIssue>
{
    public void Configure(EntityTypeBuilder<NotificationGitHubIssue> builder)
    {
        builder.ToTable("notification_github_issues");

        // Composite primary key
        builder.HasKey(x => new { x.NotificationId, x.GitHubIssueId });

        builder.Property(x => x.NotificationId)
            .HasColumnName("notification_id");

        builder.Property(x => x.GitHubIssueId)
            .HasColumnName("github_issue_id");

        // Relationship with Notification
        builder.HasOne(x => x.Notification)
            .WithMany(n => n.NotificationGitHubIssues)
            .HasForeignKey(x => x.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on GitHubIssueId for efficient lookups
        builder.HasIndex(x => x.GitHubIssueId)
            .HasDatabaseName("ix_notification_github_issues_github_issue_id");
    }
}
