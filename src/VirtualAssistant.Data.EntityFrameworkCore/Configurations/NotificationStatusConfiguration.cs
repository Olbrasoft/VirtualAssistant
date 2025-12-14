using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for NotificationStatus entity.
/// </summary>
public class NotificationStatusConfiguration : IEntityTypeConfiguration<NotificationStatus>
{
    public void Configure(EntityTypeBuilder<NotificationStatus> builder)
    {
        builder.ToTable("notification_statuses");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(s => s.Name)
            .IsUnique()
            .HasDatabaseName("ix_notification_statuses_name_unique");

        // Seed default statuses
        builder.HasData(
            new NotificationStatus { Id = 1, Name = "NewlyReceived" },
            new NotificationStatus { Id = 2, Name = "Announced" },
            new NotificationStatus { Id = 3, Name = "WaitingForPlayback" }
        );
    }
}
