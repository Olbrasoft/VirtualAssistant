using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for Provider entity.
/// </summary>
public class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ToTable("providers");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .IsRequired();

        builder.Property(p => p.Type)
            .HasColumnName("type")
            .IsRequired();

        builder.Property(p => p.Enabled)
            .HasColumnName("enabled")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.Priority)
            .HasColumnName("priority")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Unique constraint on Name + Type to prevent duplicates
        builder.HasIndex(p => new { p.Name, p.Type })
            .IsUnique()
            .HasDatabaseName("ix_providers_name_type");

        // Index on Type for filtering by provider type
        builder.HasIndex(p => p.Type)
            .HasDatabaseName("ix_providers_type");

        // Seed TTS providers in fallback chain order
        var seedCreatedAt = new DateTime(2024, 12, 30, 22, 51, 45, DateTimeKind.Utc);
        builder.HasData(
            new Provider { Id = 1, Name = "AzureTTS", Type = "tts", Enabled = true, Priority = 1, CreatedAt = seedCreatedAt },
            new Provider { Id = 2, Name = "EdgeTTS-WebSocket", Type = "tts", Enabled = true, Priority = 2, CreatedAt = seedCreatedAt },
            new Provider { Id = 3, Name = "VoiceRSS", Type = "tts", Enabled = true, Priority = 3, CreatedAt = seedCreatedAt },
            new Provider { Id = 4, Name = "GoogleTTS", Type = "tts", Enabled = true, Priority = 4, CreatedAt = seedCreatedAt },
            new Provider { Id = 5, Name = "Piper", Type = "tts", Enabled = true, Priority = 5, CreatedAt = seedCreatedAt },
            new Provider { Id = 6, Name = "cache", Type = "tts", Enabled = true, Priority = 0, CreatedAt = seedCreatedAt }
        );
    }
}
