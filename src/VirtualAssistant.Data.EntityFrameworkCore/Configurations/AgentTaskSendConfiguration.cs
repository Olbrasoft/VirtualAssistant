namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for AgentTaskSend entity with PostgreSQL snake_case naming.
/// </summary>
public class AgentTaskSendConfiguration : IEntityTypeConfiguration<AgentTaskSend>
{
    public void Configure(EntityTypeBuilder<AgentTaskSend> builder)
    {
        builder.ToTable("agent_task_sends");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.TaskId)
            .HasColumnName("task_id")
            .IsRequired();

        builder.Property(s => s.AgentId)
            .HasColumnName("agent_id")
            .IsRequired();

        builder.Property(s => s.SentAt)
            .HasColumnName("sent_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(s => s.DeliveryMethod)
            .HasColumnName("delivery_method")
            .HasMaxLength(50);

        builder.Property(s => s.Response)
            .HasColumnName("response");

        // Relationships
        builder.HasOne(s => s.Task)
            .WithMany(t => t.Sends)
            .HasForeignKey(s => s.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Agent)
            .WithMany(a => a.TaskSends)
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => s.TaskId)
            .HasDatabaseName("ix_agent_task_sends_task");

        builder.HasIndex(s => s.AgentId)
            .HasDatabaseName("ix_agent_task_sends_agent");
    }
}
