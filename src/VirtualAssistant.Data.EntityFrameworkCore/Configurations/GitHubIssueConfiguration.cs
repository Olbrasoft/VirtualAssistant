namespace VirtualAssistant.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for GitHubIssue entity with PostgreSQL snake_case naming.
/// </summary>
public class GitHubIssueConfiguration : IEntityTypeConfiguration<GitHubIssue>
{
    public void Configure(EntityTypeBuilder<GitHubIssue> builder)
    {
        builder.ToTable("github_issues");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id");

        builder.Property(i => i.RepositoryId)
            .HasColumnName("repository_id")
            .IsRequired();

        builder.Property(i => i.IssueNumber)
            .HasColumnName("issue_number")
            .IsRequired();

        builder.Property(i => i.Title)
            .HasColumnName("title")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(i => i.Body)
            .HasColumnName("body");

        builder.Property(i => i.State)
            .HasColumnName("state")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.HtmlUrl)
            .HasColumnName("html_url")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(i => i.SyncedAt)
            .HasColumnName("synced_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Unique constraint on (RepositoryId, IssueNumber)
        builder.HasIndex(i => new { i.RepositoryId, i.IssueNumber })
            .IsUnique();

        // Index for faster lookups by state
        builder.HasIndex(i => i.State);

        // Vector columns for semantic search (768 dimensions = nomic-embed-text)
        builder.Property(i => i.TitleEmbedding)
            .HasColumnName("title_embedding")
            .HasColumnType("vector(768)");

        builder.Property(i => i.BodyEmbedding)
            .HasColumnName("body_embedding")
            .HasColumnType("vector(768)");

        builder.Property(i => i.EmbeddingGeneratedAt)
            .HasColumnName("embedding_generated_at");

        // HNSW index for fast approximate nearest neighbor search
        // Note: Index creation is done via migration SQL, not fluent API
    }
}
