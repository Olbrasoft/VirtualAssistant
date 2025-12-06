namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

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
            .IsRequired();

        builder.Property(v => v.SourceApp)
            .HasColumnName("source_app")
            .HasMaxLength(255);

        builder.Property(v => v.DurationMs)
            .HasColumnName("duration_ms");

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(v => v.CreatedAt);
    }
}
