using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class SeedGeminiPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed Gemini prompt entry (ID=5, after Default=4)
            // Note: prompt_file_name should NOT include .md extension (HybridPromptLoader adds it)
            // Note: app_id_pattern is CASE-SENSITIVE and matches against Active Window Title
            migrationBuilder.InsertData(
                table: "prompts",
                columns: new[] { "id", "name", "application_name", "app_id_pattern", "prompt_file_name" },
                values: new object[] { 5, "Gemini Correction", "Gemini", "Gemini", "GeminiCorrection" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "prompts",
                keyColumn: "id",
                keyValue: 5);
        }
    }
}
