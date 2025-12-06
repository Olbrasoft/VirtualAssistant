using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Changes embedding dimensions from 1536 (text-embedding-3-small) to 768 (nomic-embed-text).
    /// Existing embeddings are cleared and must be regenerated.
    /// </remarks>
    public partial class ChangeEmbeddingDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop existing HNSW indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_issues_title_embedding;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_issues_body_embedding;");

            // Clear existing embeddings (wrong dimensions, must regenerate)
            migrationBuilder.Sql(
                "UPDATE github_issues SET title_embedding = NULL, body_embedding = NULL, embedding_generated_at = NULL;");

            migrationBuilder.AlterColumn<Vector>(
                name: "title_embedding",
                table: "github_issues",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Vector>(
                name: "body_embedding",
                table: "github_issues",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);

            // Recreate HNSW indexes for new dimensions
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS idx_issues_title_embedding ON github_issues " +
                "USING hnsw (title_embedding vector_cosine_ops);");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS idx_issues_body_embedding ON github_issues " +
                "USING hnsw (body_embedding vector_cosine_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop HNSW indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_issues_title_embedding;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_issues_body_embedding;");

            // Clear embeddings (dimension mismatch)
            migrationBuilder.Sql(
                "UPDATE github_issues SET title_embedding = NULL, body_embedding = NULL, embedding_generated_at = NULL;");

            migrationBuilder.AlterColumn<Vector>(
                name: "title_embedding",
                table: "github_issues",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(768)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Vector>(
                name: "body_embedding",
                table: "github_issues",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(768)",
                oldNullable: true);

            // Recreate HNSW indexes
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS idx_issues_title_embedding ON github_issues " +
                "USING hnsw (title_embedding vector_cosine_ops);");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS idx_issues_body_embedding ON github_issues " +
                "USING hnsw (body_embedding vector_cosine_ops);");
        }
    }
}
