using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create prompts table
            migrationBuilder.CreateTable(
                name: "prompts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    application_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    app_id_pattern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    prompt_file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prompts_app_id_pattern",
                table: "prompts",
                column: "app_id_pattern");

            // Step 2: Seed default prompts (created_at will use database default NOW())
            // Note: prompt_file_name should NOT include .md extension (HybridPromptLoader adds it)
            // Note: app_id_pattern is CASE-SENSITIVE and matches against Active Window Title
            migrationBuilder.InsertData(
                table: "prompts",
                columns: new[] { "id", "name", "application_name", "app_id_pattern", "prompt_file_name" },
                values: new object[,]
                {
                    { 1, "OpenCode Correction", "OpenCode", "OpenCode", "OpenCodeCorrection" },
                    { 2, "Claude Code Correction", "Claude Code", "Claude Code", "ClaudeCodeCorrection" },
                    { 3, "Ferdium Correction", "Ferdium", "Ferdium", "FerdiumCorrection" },
                    { 4, "Default Correction", "Default", "*", "DefaultCorrection" }
                });

            // Step 3: Add prompt_id column with temporary DEFAULT 4 (for existing records)
            migrationBuilder.AddColumn<int>(
                name: "prompt_id",
                table: "llm_corrections",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            // Step 4: Remove DEFAULT constraint (runtime ALWAYS provides explicit PromptId)
            // Use explicit SQL to ensure default is properly removed
            migrationBuilder.Sql("ALTER TABLE llm_corrections ALTER COLUMN prompt_id DROP DEFAULT;");

            // Step 5: Add FK constraint and index
            migrationBuilder.CreateIndex(
                name: "IX_llm_corrections_prompt_id",
                table: "llm_corrections",
                column: "prompt_id");

            migrationBuilder.AddForeignKey(
                name: "FK_llm_corrections_prompts_prompt_id",
                table: "llm_corrections",
                column: "prompt_id",
                principalTable: "prompts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Correct order: FK → Index → Column → Table
            migrationBuilder.DropForeignKey(
                name: "FK_llm_corrections_prompts_prompt_id",
                table: "llm_corrections");

            migrationBuilder.DropIndex(
                name: "IX_llm_corrections_prompt_id",
                table: "llm_corrections");

            migrationBuilder.DropColumn(
                name: "prompt_id",
                table: "llm_corrections");

            migrationBuilder.DropTable(
                name: "prompts");
        }
    }
}
