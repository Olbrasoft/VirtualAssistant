namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for NotificationTtsAttempt entity.
/// </summary>
public class NotificationTtsAttemptConfiguration : IEntityTypeConfiguration<NotificationTtsAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationTtsAttempt> builder)
    {
        builder.ToTable("notification_tts_attempts");

        builder.HasKey(nta => nta.Id);

        builder.Property(nta => nta.Id)
            .HasColumnName("id");

        builder.Property(nta => nta.NotificationId)
            .HasColumnName("notification_id")
            .IsRequired();

        builder.Property(nta => nta.ProviderId)
            .HasColumnName("provider_id")
            .IsRequired();

        builder.Property(nta => nta.AttemptOrder)
            .HasColumnName("attempt_order")
            .IsRequired();

        builder.Property(nta => nta.StatusCode)
            .HasColumnName("status_code")
            .IsRequired();

        builder.Property(nta => nta.HttpStatusCode)
            .HasColumnName("http_status_code");

        builder.Property(nta => nta.ErrorMessage)
            .HasColumnName("error_message");

        builder.Property(nta => nta.DurationMs)
            .HasColumnName("duration_ms");

        builder.Property(nta => nta.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // NOTE: Relationship with Notification is configured in NotificationConfiguration.cs

        // Relationship with Provider
        builder.HasOne(nta => nta.Provider)
            .WithMany(p => p.TtsAttempts)
            .HasForeignKey(nta => nta.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(nta => nta.NotificationId)
            .HasDatabaseName("ix_notification_tts_attempts_notification");

        builder.HasIndex(nta => nta.ProviderId)
            .HasDatabaseName("ix_notification_tts_attempts_provider");

        builder.HasIndex(nta => nta.CreatedAt)
            .HasDatabaseName("ix_notification_tts_attempts_created_at");
    }
}
