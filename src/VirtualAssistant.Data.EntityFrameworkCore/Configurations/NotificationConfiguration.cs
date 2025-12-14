using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

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

        // Indexes
        builder.HasIndex(n => n.NotificationStatusId)
            .HasDatabaseName("ix_notifications_status");

        builder.HasIndex(n => n.CreatedAt)
            .HasDatabaseName("ix_notifications_created_at");

        builder.HasIndex(n => n.AgentId)
            .HasDatabaseName("ix_notifications_agent");
    }
}
