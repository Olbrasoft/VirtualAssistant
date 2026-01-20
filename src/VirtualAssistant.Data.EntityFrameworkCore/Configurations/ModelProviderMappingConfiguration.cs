namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for ModelProviderMapping entity (many-to-many junction table).
/// </summary>
public class ModelProviderMappingConfiguration : IEntityTypeConfiguration<ModelProviderMapping>
{
    public void Configure(EntityTypeBuilder<ModelProviderMapping> builder)
    {
        builder.ToTable("model_provider_mappings");

        // Composite primary key
        builder.HasKey(m => new { m.ModelId, m.ProviderId });

        builder.Property(m => m.ModelId)
            .HasColumnName("model_id");

        builder.Property(m => m.ProviderId)
            .HasColumnName("provider_id");

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Foreign key to LlmModel
        builder.HasOne(m => m.Model)
            .WithMany(lm => lm.ProviderMappings)
            .HasForeignKey(m => m.ModelId)
            .OnDelete(DeleteBehavior.Cascade);

        // Foreign key to Provider
        builder.HasOne(m => m.Provider)
            .WithMany(p => p.ModelMappings)
            .HasForeignKey(m => m.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for querying by provider
        builder.HasIndex(m => m.ProviderId)
            .HasDatabaseName("ix_model_provider_mappings_provider_id");

        // Seed initial data - existing LlmModel relationships
        var seedCreatedAt = new DateTime(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            // Mistral Large (Id=1) -> Mistral AI (ProviderId=7)
            new { ModelId = 1, ProviderId = 7, CreatedAt = seedCreatedAt },
            // Alpha GLM 4.7 (Id=2) -> Zen (ProviderId=8)
            new { ModelId = 2, ProviderId = 8, CreatedAt = seedCreatedAt }
        );
    }
}
