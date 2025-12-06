using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "body_embedding",
                table: "github_issues",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "embedding_generated_at",
                table: "github_issues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "title_embedding",
                table: "github_issues",
                type: "vector(1536)",
                nullable: true);

            // Create HNSW indexes for fast approximate nearest neighbor search
            // Using cosine distance for semantic similarity
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
            // Drop HNSW indexes first
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_issues_title_embedding;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_issues_body_embedding;");

            migrationBuilder.DropColumn(
                name: "body_embedding",
                table: "github_issues");

            migrationBuilder.DropColumn(
                name: "embedding_generated_at",
                table: "github_issues");

            migrationBuilder.DropColumn(
                name: "title_embedding",
                table: "github_issues");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
