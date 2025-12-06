namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for SystemStartup entity with PostgreSQL snake_case naming.
/// </summary>
public class SystemStartupConfiguration : IEntityTypeConfiguration<SystemStartup>
{
    public void Configure(EntityTypeBuilder<SystemStartup> builder)
    {
        builder.ToTable("system_startups");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.StartedAt)
            .HasColumnName("started_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(s => s.ShutdownAt)
            .HasColumnName("shutdown_at");

        builder.Property(s => s.ShutdownType)
            .HasColumnName("shutdown_type")
            .HasConversion<string>();

        builder.Property(s => s.StartupType)
            .HasColumnName("startup_type")
            .HasConversion<string>();

        builder.Property(s => s.GreetingSpoken)
            .HasColumnName("greeting_spoken")
            .HasMaxLength(500);

        builder.HasIndex(s => s.StartedAt);
    }
}
