namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for <see cref="TtsKeyUsage"/>.
/// </summary>
public class TtsKeyUsageConfiguration : IEntityTypeConfiguration<TtsKeyUsage>
{
    public void Configure(EntityTypeBuilder<TtsKeyUsage> builder)
    {
        builder.ToTable("tts_key_usage");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.KeyName)
            .HasColumnName("key_name")
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.KeyName)
            .IsUnique()
            .HasDatabaseName("ix_tts_key_usage_key_name_unique");

        builder.Property(x => x.CounterYear).HasColumnName("counter_year");
        builder.Property(x => x.CounterMonth).HasColumnName("counter_month");

        builder.Property(x => x.MonthlyCharacterCount)
            .HasColumnName("monthly_character_count");

        builder.Property(x => x.TotalSuccesses).HasColumnName("total_successes");
        builder.Property(x => x.TotalFailures).HasColumnName("total_failures");
        builder.Property(x => x.ConsecutiveFailures).HasColumnName("consecutive_failures");

        builder.Property(x => x.LastSuccessUtc).HasColumnName("last_success_utc");
        builder.Property(x => x.LastErrorUtc).HasColumnName("last_error_utc");

        builder.Property(x => x.LastErrorReason)
            .HasColumnName("last_error_reason")
            .HasMaxLength(200);

        builder.Property(x => x.State).HasColumnName("state");
        builder.Property(x => x.CooldownUntilUtc).HasColumnName("cooldown_until_utc");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()")
            .IsRequired();
    }
}
