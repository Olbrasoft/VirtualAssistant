using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.Enums;

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

        // Seed statuses matching NotificationStatusEnum
        builder.HasData(
            new NotificationStatus { Id = (int)NotificationStatusEnum.NewlyReceived, Name = nameof(NotificationStatusEnum.NewlyReceived) },
            new NotificationStatus { Id = (int)NotificationStatusEnum.Processing, Name = nameof(NotificationStatusEnum.Processing) },
            new NotificationStatus { Id = (int)NotificationStatusEnum.SentForSummarization, Name = nameof(NotificationStatusEnum.SentForSummarization) },
            new NotificationStatus { Id = (int)NotificationStatusEnum.Summarized, Name = nameof(NotificationStatusEnum.Summarized) },
            new NotificationStatus { Id = (int)NotificationStatusEnum.SentForTranslation, Name = nameof(NotificationStatusEnum.SentForTranslation) },
            new NotificationStatus { Id = (int)NotificationStatusEnum.Translated, Name = nameof(NotificationStatusEnum.Translated) },
            new NotificationStatus { Id = (int)NotificationStatusEnum.Announced, Name = nameof(NotificationStatusEnum.Announced) },
            new NotificationStatus { Id = (int)NotificationStatusEnum.WaitingForPlayback, Name = nameof(NotificationStatusEnum.WaitingForPlayback) },
            new NotificationStatus { Id = (int)NotificationStatusEnum.Played, Name = nameof(NotificationStatusEnum.Played) }
        );
    }
}
