namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for LlmModel entity with PostgreSQL snake_case naming.
/// Provider relationship is managed via ModelProviderMapping (many-to-many).
/// </summary>
public class LlmModelConfiguration : IEntityTypeConfiguration<LlmModel>
{
    public void Configure(EntityTypeBuilder<LlmModel> builder)
    {
        builder.ToTable("llm_models");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id");

        builder.Property(m => m.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.ModelIdentifier)
            .HasColumnName("model_identifier")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Unique constraint on ModelIdentifier
        builder.HasIndex(m => m.ModelIdentifier)
            .IsUnique()
            .HasDatabaseName("ix_llm_models_model_identifier");

        // Seed LLM models (provider relationship via ModelProviderMapping)
        var seedCreatedAt = new DateTime(2026, 1, 16, 12, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new { Id = 1, Name = "Mistral Large", ModelIdentifier = "mistral-large-latest", IsActive = true, CreatedAt = seedCreatedAt },
            new { Id = 2, Name = "Alpha GLM 4.7", ModelIdentifier = "alpha-glm-4.7", IsActive = true, CreatedAt = seedCreatedAt }
        );
    }
}
