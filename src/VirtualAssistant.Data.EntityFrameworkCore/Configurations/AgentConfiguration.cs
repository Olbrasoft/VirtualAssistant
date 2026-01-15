namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for Agent entity with PostgreSQL snake_case naming.
/// </summary>
public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("agents");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id");

        builder.Property(a => a.Name)
            .HasColumnName("name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Label)
            .HasColumnName("label")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Unique constraint on name
        builder.HasIndex(a => a.Name)
            .IsUnique()
            .HasDatabaseName("ix_agents_name_unique");

        // Index for querying active agents
        builder.HasIndex(a => a.IsActive)
            .HasDatabaseName("ix_agents_is_active");

        // Seed agents with explicit IDs matching AgentType enum
        builder.HasData(
            new Agent
            {
                Id = 1,
                Name = "opencode",
                Label = "agent:opencode",
                IsActive = true,
                CreatedAt = new DateTime(2025, 12, 6, 23, 11, 18, DateTimeKind.Utc)
            },
            new Agent
            {
                Id = 4,
                Name = "claude-code",
                Label = "agent:claude-code",
                IsActive = true,
                CreatedAt = new DateTime(2025, 12, 6, 23, 11, 18, DateTimeKind.Utc)
            },
            new Agent
            {
                Id = 11,
                Name = "gemini",
                Label = "agent:gemini",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 6, 22, 43, 47, DateTimeKind.Utc)
            },
            new Agent
            {
                Id = 20,
                Name = "antigravity",
                Label = "agent:antigravity",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 15, 12, 40, 0, DateTimeKind.Utc)
            }
        );
    }
}
