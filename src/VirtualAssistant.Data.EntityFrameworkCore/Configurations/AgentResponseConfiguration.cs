namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for AgentResponse entity with PostgreSQL snake_case naming.
/// </summary>
public class AgentResponseConfiguration : IEntityTypeConfiguration<AgentResponse>
{
    public void Configure(EntityTypeBuilder<AgentResponse> builder)
    {
        builder.ToTable("agent_responses");

        builder.HasKey(ar => ar.Id);

        builder.Property(ar => ar.Id)
            .HasColumnName("id");

        builder.Property(ar => ar.AgentName)
            .HasColumnName("agent_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ar => ar.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(ar => ar.StartedAt)
            .HasColumnName("started_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(ar => ar.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(ar => ar.AgentTaskId)
            .HasColumnName("agent_task_id");

        // Relationship to AgentTask
        builder.HasOne(ar => ar.AgentTask)
            .WithMany()
            .HasForeignKey(ar => ar.AgentTaskId)
            .OnDelete(DeleteBehavior.SetNull);

        // Index for querying by agent name
        builder.HasIndex(ar => ar.AgentName)
            .HasDatabaseName("ix_agent_responses_agent_name");

        // Index for querying by status
        builder.HasIndex(ar => ar.Status)
            .HasDatabaseName("ix_agent_responses_status");

        // Composite index for common query: last response by agent
        builder.HasIndex(ar => new { ar.AgentName, ar.StartedAt })
            .HasDatabaseName("ix_agent_responses_agent_name_started_at");
    }
}
