using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for Notification entity.
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .HasColumnName("id");

        builder.Property(n => n.Text)
            .HasColumnName("text")
            .IsRequired();

        builder.Property(n => n.AgentId)
            .HasColumnName("agent_id")
            .IsRequired();

        // Relationship with Agent
        builder.HasOne(n => n.Agent)
            .WithMany(a => a.Notifications)
            .HasForeignKey(n => n.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(n => n.NotificationStatusId)
            .HasColumnName("notification_status_id")
            .IsRequired()
            .HasDefaultValue(1); // NewlyReceived

        // Relationship with NotificationStatus
        builder.HasOne(n => n.Status)
            .WithMany(s => s.Notifications)
            .HasForeignKey(n => n.NotificationStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // TTS tracking fields
        builder.Property(n => n.FinalProviderId)
            .HasColumnName("final_provider_id");

        builder.Property(n => n.FinalTtsStatus)
            .HasColumnName("final_tts_status");

        builder.Property(n => n.TtsCompletedAt)
            .HasColumnName("tts_completed_at");

        // Relationship with Provider (TTS tracking - final provider used)
        builder.HasOne(n => n.FinalProvider)
            .WithMany()
            .HasForeignKey(n => n.FinalProviderId)
            .OnDelete(DeleteBehavior.SetNull);

        // Relationship with TTS attempts (one notification has many TTS attempts)
        builder.HasMany(n => n.TtsAttempts)
            .WithOne(nta => nta.Notification)
            .HasForeignKey(nta => nta.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(n => n.NotificationStatusId)
            .HasDatabaseName("ix_notifications_status");

        builder.HasIndex(n => n.CreatedAt)
            .HasDatabaseName("ix_notifications_created_at");

        builder.HasIndex(n => n.AgentId)
            .HasDatabaseName("ix_notifications_agent");

        builder.HasIndex(n => n.FinalProviderId)
            .HasDatabaseName("ix_notifications_final_provider");
    }
}
