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

        builder.Property(l => l.PromptId)
            .HasColumnName("prompt_id")
            .IsRequired(); // NOT NULL - always has a prompt

        builder.Property(l => l.ModelId)
            .HasColumnName("model_id")
            .IsRequired(false); // NULL for legacy corrections before model tracking

        // Foreign key relationships
        builder.HasOne(l => l.WhisperTranscription)
            .WithMany()
            .HasForeignKey(l => l.WhisperTranscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Prompt)
            .WithMany(p => p.LlmCorrections)
            .HasForeignKey(l => l.PromptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Model)
            .WithMany(m => m.LlmCorrections)
            .HasForeignKey(l => l.ModelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.WhisperTranscriptionId);
        builder.HasIndex(l => l.PromptId);
        builder.HasIndex(l => l.ModelId);
        builder.HasIndex(l => l.CreatedAt);
    }
}
