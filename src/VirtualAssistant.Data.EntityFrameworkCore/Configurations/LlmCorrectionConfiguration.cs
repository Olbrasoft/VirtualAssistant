namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for LlmCorrection entity with PostgreSQL snake_case naming.
/// </summary>
public class LlmCorrectionConfiguration : IEntityTypeConfiguration<LlmCorrection>
{
    public void Configure(EntityTypeBuilder<LlmCorrection> builder)
    {
        builder.ToTable("llm_corrections");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id");

        builder.Property(l => l.WhisperTranscriptionId)
            .HasColumnName("whisper_transcription_id")
            .IsRequired();

        builder.Property(l => l.CorrectedText)
            .HasColumnName("corrected_text")
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(l => l.DurationMs)
            .HasColumnName("duration_ms")
            .IsRequired();

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Foreign key relationship
        builder.HasOne(l => l.WhisperTranscription)
            .WithMany()
            .HasForeignKey(l => l.WhisperTranscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.WhisperTranscriptionId);
        builder.HasIndex(l => l.CreatedAt);
    }
}
