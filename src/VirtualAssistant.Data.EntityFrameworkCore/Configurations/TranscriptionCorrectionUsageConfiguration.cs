namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// Entity Framework Core configuration for TranscriptionCorrectionUsage entity.
/// </summary>
public class TranscriptionCorrectionUsageConfiguration : IEntityTypeConfiguration<TranscriptionCorrectionUsage>
{
    public void Configure(EntityTypeBuilder<TranscriptionCorrectionUsage> builder)
    {
        builder.ToTable("transcription_correction_usage");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id");

        builder.Property(u => u.CorrectionId)
            .HasColumnName("correction_id")
            .IsRequired();

        builder.Property(u => u.UsedAt)
            .HasColumnName("used_at")
            .IsRequired();

        builder.Property(u => u.Context)
            .HasColumnName("context")
            .HasMaxLength(100);

        // Foreign key relationship
        builder.HasOne(u => u.Correction)
            .WithMany()
            .HasForeignKey(u => u.CorrectionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for querying by correction
        builder.HasIndex(u => u.CorrectionId)
            .HasDatabaseName("ix_transcription_correction_usage_correction_id");

        // Index for time-based queries
        builder.HasIndex(u => u.UsedAt)
            .HasDatabaseName("ix_transcription_correction_usage_used_at");
    }
}
