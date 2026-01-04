namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for TranscriptionCorrection entity with PostgreSQL snake_case naming.
/// </summary>
public class TranscriptionCorrectionConfiguration : IEntityTypeConfiguration<TranscriptionCorrection>
{
    public void Configure(EntityTypeBuilder<TranscriptionCorrection> builder)
    {
        builder.ToTable("transcription_corrections");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.IncorrectText)
            .HasColumnName("incorrect_text")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.CorrectText)
            .HasColumnName("correct_text")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.CaseSensitive)
            .HasColumnName("case_sensitive")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.Priority)
            .HasColumnName("priority")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(t => t.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Indexes for efficient lookups
        builder.HasIndex(t => t.IncorrectText);
        builder.HasIndex(t => t.IsActive);
        builder.HasIndex(t => new { t.IsActive, t.Priority }); // Composite index for active corrections ordered by priority
    }
}
