namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for LlmError entity with PostgreSQL snake_case naming.
/// </summary>
public class LlmErrorConfiguration : IEntityTypeConfiguration<LlmError>
{
    public void Configure(EntityTypeBuilder<LlmError> builder)
    {
        builder.ToTable("llm_errors");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.WhisperTranscriptionId)
            .HasColumnName("whisper_transcription_id")
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.DurationMs)
            .HasColumnName("duration_ms")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Foreign key relationship
        builder.HasOne(e => e.WhisperTranscription)
            .WithMany()
            .HasForeignKey(e => e.WhisperTranscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.WhisperTranscriptionId);
        builder.HasIndex(e => e.CreatedAt);
    }
}
