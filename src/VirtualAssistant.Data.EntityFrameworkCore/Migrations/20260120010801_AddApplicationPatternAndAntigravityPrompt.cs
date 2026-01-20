using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Adds ApplicationPattern column to prompts table and seeds Antigravity prompt.
    /// ApplicationPattern allows matching prompts by desktop application name (e.g., "antigravity.desktop")
    /// which is more reliable than window title matching for apps that change window titles dynamically.
    /// </summary>
    public partial class AddApplicationPatternAndAntigravityPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add ApplicationPattern column for desktop file matching
            migrationBuilder.AddColumn<string>(
                name: "application_pattern",
                table: "prompts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompts_application_pattern",
                table: "prompts",
                column: "application_pattern");

            // Add Antigravity prompt with application_pattern = "antigravity"
            // Antigravity changes window title dynamically but always has "antigravity.desktop" as application
            // Uses GeminiCorrection prompt since Antigravity is built on Gemini
            migrationBuilder.InsertData(
                table: "prompts",
                columns: new[] { "id", "name", "application_name", "app_id_pattern", "application_pattern", "prompt_file_name" },
                values: new object[] { 7, "Antigravity Correction", "Antigravity", "*", "antigravity", "GeminiCorrection" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "prompts",
                keyColumn: "id",
                keyValue: 7);

            migrationBuilder.DropIndex(
                name: "IX_prompts_application_pattern",
                table: "prompts");

            migrationBuilder.DropColumn(
                name: "application_pattern",
                table: "prompts");
        }
    }
}
