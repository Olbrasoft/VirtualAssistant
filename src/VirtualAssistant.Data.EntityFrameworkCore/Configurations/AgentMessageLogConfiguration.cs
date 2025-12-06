namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for AgentMessageLog entity with PostgreSQL snake_case naming.
/// </summary>
public class AgentMessageLogConfiguration : IEntityTypeConfiguration<AgentMessageLog>
{
    public void Configure(EntityTypeBuilder<AgentMessageLog> builder)
    {
        builder.ToTable("agent_message_logs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id");

        builder.Property(l => l.SourceAgent)
            .HasColumnName("source_agent")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.Level)
            .HasColumnName("level")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.Message)
            .HasColumnName("message")
            .IsRequired();

        builder.Property(l => l.Context)
            .HasColumnName("context")
            .HasColumnType("jsonb");

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Index for querying by creation time (for cleanup, audit)
        builder.HasIndex(l => l.CreatedAt)
            .HasDatabaseName("ix_agent_message_logs_created");

        // Index for filtering by log level
        builder.HasIndex(l => l.Level)
            .HasDatabaseName("ix_agent_message_logs_level");
    }
}
