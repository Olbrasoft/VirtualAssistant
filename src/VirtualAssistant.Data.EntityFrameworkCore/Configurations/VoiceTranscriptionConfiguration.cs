namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for VoiceTranscription entity with PostgreSQL snake_case naming.
/// </summary>
public class VoiceTranscriptionConfiguration : IEntityTypeConfiguration<VoiceTranscription>
{
    public void Configure(EntityTypeBuilder<VoiceTranscription> builder)
    {
        builder.ToTable("voice_transcriptions");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("id");

        builder.Property(v => v.TranscribedText)
            .HasColumnName("transcribed_text")
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(v => v.AudioDurationMs)
            .HasColumnName("audio_duration_ms");

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(v => v.ProviderId)
            .HasColumnName("provider_id")
            .IsRequired();

        // FK to providers table
        builder.HasOne(v => v.Provider)
            .WithMany(p => p.VoiceTranscriptions)
            .HasForeignKey(v => v.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => v.CreatedAt);

        builder.HasIndex(v => v.ProviderId)
            .HasDatabaseName("ix_voice_transcriptions_provider_id");
    }
}
