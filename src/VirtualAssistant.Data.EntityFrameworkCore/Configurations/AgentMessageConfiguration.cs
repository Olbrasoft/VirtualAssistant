using VirtualAssistant.Data.Enums;

namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for AgentMessage entity with PostgreSQL snake_case naming.
/// </summary>
public class AgentMessageConfiguration : IEntityTypeConfiguration<AgentMessage>
{
    public void Configure(EntityTypeBuilder<AgentMessage> builder)
    {
        builder.ToTable("agent_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id");

        builder.Property(m => m.SourceAgent)
            .HasColumnName("source_agent")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.TargetAgent)
            .HasColumnName("target_agent")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.MessageType)
            .HasColumnName("message_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(m => m.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("pending");

        builder.Property(m => m.RequiresApproval)
            .HasColumnName("requires_approval")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(m => m.ApprovedAt)
            .HasColumnName("approved_at");

        builder.Property(m => m.DeliveredAt)
            .HasColumnName("delivered_at");

        builder.Property(m => m.ProcessedAt)
            .HasColumnName("processed_at");

        builder.Property(m => m.Phase)
            .HasColumnName("phase")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(MessagePhase.Complete);

        builder.Property(m => m.ParentMessageId)
            .HasColumnName("parent_message_id");

        // Self-referencing relationship for task tracking
        builder.HasOne(m => m.ParentMessage)
            .WithMany(m => m.ChildMessages)
            .HasForeignKey(m => m.ParentMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        // Index for querying pending messages for a target agent
        builder.HasIndex(m => new { m.TargetAgent, m.Status })
            .HasDatabaseName("ix_agent_messages_target_status");

        // Index for querying by creation time (for cleanup, audit)
        builder.HasIndex(m => m.CreatedAt)
            .HasDatabaseName("ix_agent_messages_created_at");

        // Index for querying messages requiring approval
        builder.HasIndex(m => new { m.RequiresApproval, m.Status })
            .HasDatabaseName("ix_agent_messages_approval_status")
            .HasFilter("requires_approval = true AND status = 'pending'");

        // Index for querying child messages by parent
        builder.HasIndex(m => m.ParentMessageId)
            .HasDatabaseName("ix_agent_messages_parent");

        // Index for finding active tasks (Start phase without Complete)
        builder.HasIndex(m => new { m.SourceAgent, m.Phase })
            .HasDatabaseName("ix_agent_messages_source_phase");
    }
}
