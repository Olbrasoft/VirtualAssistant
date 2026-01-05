namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for Prompt entity with PostgreSQL snake_case naming.
/// </summary>
public class PromptConfiguration : IEntityTypeConfiguration<Prompt>
{
    public void Configure(EntityTypeBuilder<Prompt> builder)
    {
        builder.ToTable("prompts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.ApplicationName)
            .HasColumnName("application_name")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.AppIdPattern)
            .HasColumnName("app_id_pattern")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.PromptFileName)
            .HasColumnName("prompt_file_name")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Relationship: Prompt 1:N LlmCorrections
        builder.HasMany(p => p.LlmCorrections)
            .WithOne(l => l.Prompt)
            .HasForeignKey(l => l.PromptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.AppIdPattern);
    }
}
