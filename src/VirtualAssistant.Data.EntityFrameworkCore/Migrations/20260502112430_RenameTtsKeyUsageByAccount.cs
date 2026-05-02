using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RenameTtsKeyUsageByAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename per-key usage rows so deploys are self-contained: a fresh
            // environment that still has the old positional names will end up
            // in the same state as production after this migration runs, and
            // existing monthly counters / lifetime totals are preserved.
            migrationBuilder.Sql(
                "UPDATE tts_key_usage SET key_name = 'GoogleTTS-claudecode' WHERE key_name = 'GoogleTTS-Key2';");
            migrationBuilder.Sql(
                "UPDATE tts_key_usage SET key_name = 'GoogleTTS-olbrasoft'  WHERE key_name = 'GoogleTTS-Key3';");

            // The decommissioned tuma.rsrobot slot has no replacement; remove
            // its row so the dashboard doesn't render a zombie key after deploy.
            migrationBuilder.Sql(
                "DELETE FROM tts_key_usage WHERE key_name = 'GoogleTTS-Key1';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort rollback: re-create the deleted Key1 row would lose
            // its original counters anyway, so we only restore the renames.
            migrationBuilder.Sql(
                "UPDATE tts_key_usage SET key_name = 'GoogleTTS-Key2' WHERE key_name = 'GoogleTTS-claudecode';");
            migrationBuilder.Sql(
                "UPDATE tts_key_usage SET key_name = 'GoogleTTS-Key3' WHERE key_name = 'GoogleTTS-olbrasoft';");
        }
    }
}
