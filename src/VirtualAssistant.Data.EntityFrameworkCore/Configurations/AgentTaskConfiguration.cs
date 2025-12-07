namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for AgentTask entity with PostgreSQL snake_case naming.
/// </summary>
public class AgentTaskConfiguration : IEntityTypeConfiguration<AgentTask>
{
    public void Configure(EntityTypeBuilder<AgentTask> builder)
    {
        builder.ToTable("agent_tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.GithubIssueUrl)
            .HasColumnName("github_issue_url")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.GithubIssueNumber)
            .HasColumnName("github_issue_number");

        builder.Property(t => t.Summary)
            .HasColumnName("summary")
            .IsRequired();

        builder.Property(t => t.CreatedByAgentId)
            .HasColumnName("created_by_agent_id");

        builder.Property(t => t.TargetAgentId)
            .HasColumnName("target_agent_id");

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("pending");

        builder.Property(t => t.RequiresApproval)
            .HasColumnName("requires_approval")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.Result)
            .HasColumnName("result");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(t => t.ApprovedAt)
            .HasColumnName("approved_at");

        builder.Property(t => t.SentAt)
            .HasColumnName("sent_at");

        builder.Property(t => t.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(t => t.NotifiedAt)
            .HasColumnName("notified_at");

        // Relationships
        builder.HasOne(t => t.CreatedByAgent)
            .WithMany(a => a.CreatedTasks)
            .HasForeignKey(t => t.CreatedByAgentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.TargetAgent)
            .WithMany(a => a.AssignedTasks)
            .HasForeignKey(t => t.TargetAgentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(t => t.Status)
            .HasDatabaseName("ix_agent_tasks_status");

        builder.HasIndex(t => new { t.TargetAgentId, t.Status })
            .HasDatabaseName("ix_agent_tasks_target_status");

        builder.HasIndex(t => t.GithubIssueNumber)
            .HasDatabaseName("ix_agent_tasks_issue_number");
    }
}
