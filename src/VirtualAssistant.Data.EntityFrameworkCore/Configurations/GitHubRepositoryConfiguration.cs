namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for GitHubRepository entity.
/// Minimal reference table with (owner, name) unique constraint.
/// </summary>
public class GitHubRepositoryConfiguration : IEntityTypeConfiguration<GitHubRepository>
{
    public void Configure(EntityTypeBuilder<GitHubRepository> builder)
    {
        builder.ToTable("github_repositories");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id");

        builder.Property(r => r.Owner)
            .HasColumnName("owner")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        // Unique constraint on (owner, name)
        builder.HasIndex(r => new { r.Owner, r.Name })
            .IsUnique();

        // Relationship with Issues
        builder.HasMany(r => r.Issues)
            .WithOne(i => i.Repository)
            .HasForeignKey(i => i.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
