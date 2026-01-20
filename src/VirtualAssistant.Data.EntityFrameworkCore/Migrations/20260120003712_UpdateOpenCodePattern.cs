using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Adds additional OpenCode prompt pattern "OC |".
    /// OpenCode initially shows "OpenCode" but changes window title to "OC | session description"
    /// after loading a session, so we need both patterns to match correctly.
    /// </summary>
    public partial class UpdateOpenCodePattern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // OpenCode changes window title to "OC | <session description>" after session loads
            // Keep original "OpenCode" pattern (id=1) for initial window title
            // Add new "OC |" pattern for session window title
            // Both patterns use the same OpenCodeCorrection prompt file
            migrationBuilder.InsertData(
                table: "prompts",
                columns: new[] { "id", "name", "application_name", "app_id_pattern", "prompt_file_name" },
                values: new object[] { 6, "OpenCode Session Correction", "OpenCode", "OC |", "OpenCodeCorrection" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "prompts",
                keyColumn: "id",
                keyValue: 6);
        }
    }
}
