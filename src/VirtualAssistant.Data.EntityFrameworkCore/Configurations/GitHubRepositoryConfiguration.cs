namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for GitHubRepository entity with PostgreSQL snake_case naming.
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

        builder.Property(r => r.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(201)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnName("description");

        builder.Property(r => r.HtmlUrl)
            .HasColumnName("html_url")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.IsPrivate)
            .HasColumnName("is_private")
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(r => r.SyncedAt)
            .HasColumnName("synced_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Unique constraint on FullName
        builder.HasIndex(r => r.FullName)
            .IsUnique();

        // Relationship with Issues
        builder.HasMany(r => r.Issues)
            .WithOne(i => i.Repository)
            .HasForeignKey(i => i.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
