using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class ReduceGitHubSchemaToMinimal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "github_issue_agents");

            migrationBuilder.DropIndex(
                name: "IX_github_repositories_full_name",
                table: "github_repositories");

            migrationBuilder.DropIndex(
                name: "IX_github_issues_state",
                table: "github_issues");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "github_repositories");

            migrationBuilder.DropColumn(
                name: "description",
                table: "github_repositories");

            migrationBuilder.DropColumn(
                name: "full_name",
                table: "github_repositories");

            migrationBuilder.DropColumn(
                name: "html_url",
                table: "github_repositories");

            migrationBuilder.DropColumn(
                name: "is_private",
                table: "github_repositories");

            migrationBuilder.DropColumn(
                name: "synced_at",
                table: "github_repositories");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "github_repositories");

            migrationBuilder.DropColumn(
                name: "body",
                table: "github_issues");

            migrationBuilder.DropColumn(
                name: "body_embedding",
                table: "github_issues");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "github_issues");

            migrationBuilder.DropColumn(
                name: "embedding_generated_at",
                table: "github_issues");

            migrationBuilder.DropColumn(
                name: "html_url",
                table: "github_issues");

            migrationBuilder.DropColumn(
                name: "state",
                table: "github_issues");

            migrationBuilder.DropColumn(
                name: "synced_at",
                table: "github_issues");

            migrationBuilder.DropColumn(
                name: "title",
                table: "github_issues");

            migrationBuilder.DropColumn(
                name: "title_embedding",
                table: "github_issues");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "github_issues");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_github_repositories_owner_name",
                table: "github_repositories",
                columns: new[] { "owner", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_github_repositories_owner_name",
                table: "github_repositories");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "github_repositories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "github_repositories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "full_name",
                table: "github_repositories",
                type: "character varying(201)",
                maxLength: 201,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "html_url",
                table: "github_repositories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_private",
                table: "github_repositories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "synced_at",
                table: "github_repositories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "github_repositories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "body",
                table: "github_issues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "body_embedding",
                table: "github_issues",
                type: "vector(768)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "github_issues",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "embedding_generated_at",
                table: "github_issues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "html_url",
                table: "github_issues",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "github_issues",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "synced_at",
                table: "github_issues",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "github_issues",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Vector>(
                name: "title_embedding",
                table: "github_issues",
                type: "vector(768)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "github_issues",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "github_issue_agents",
                columns: table => new
                {
                    github_issue_id = table.Column<int>(type: "integer", nullable: false),
                    agent = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_github_issue_agents", x => new { x.github_issue_id, x.agent });
                    table.ForeignKey(
                        name: "FK_github_issue_agents_github_issues_github_issue_id",
                        column: x => x.github_issue_id,
                        principalTable: "github_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_github_repositories_full_name",
                table: "github_repositories",
                column: "full_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_github_issues_state",
                table: "github_issues",
                column: "state");
        }
    }
}
