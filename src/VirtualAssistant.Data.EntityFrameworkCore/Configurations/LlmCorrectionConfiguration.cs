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

        builder.Property(l => l.VoiceTranscriptionId)
            .HasColumnName("voice_transcription_id")
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
            .IsRequired(); // Required - every correction must be associated with a model

        // Foreign key relationships
        builder.HasOne(l => l.VoiceTranscription)
            .WithMany()
            .HasForeignKey(l => l.VoiceTranscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Prompt)
            .WithMany(p => p.LlmCorrections)
            .HasForeignKey(l => l.PromptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Model)
            .WithMany(m => m.LlmCorrections)
            .HasForeignKey(l => l.ModelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.VoiceTranscriptionId);
        builder.HasIndex(l => l.PromptId);
        builder.HasIndex(l => l.ModelId);
        builder.HasIndex(l => l.CreatedAt);

        builder.Property(l => l.InputTokens)
            .HasColumnName("input_tokens");

        builder.Property(l => l.OutputTokens)
            .HasColumnName("output_tokens");

        builder.Property(l => l.ReasoningTokens)
            .HasColumnName("reasoning_tokens");
    }
}
