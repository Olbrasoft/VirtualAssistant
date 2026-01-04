namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for WhisperTranscription entity with PostgreSQL snake_case naming.
/// </summary>
public class WhisperTranscriptionConfiguration : IEntityTypeConfiguration<WhisperTranscription>
{
    public void Configure(EntityTypeBuilder<WhisperTranscription> builder)
    {
        builder.ToTable("whisper_transcriptions");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasColumnName("id");

        builder.Property(w => w.TranscribedText)
            .HasColumnName("transcribed_text")
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(w => w.AudioDurationMs)
            .HasColumnName("audio_duration_ms");

        builder.Property(w => w.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(w => w.CreatedAt);
    }
}
