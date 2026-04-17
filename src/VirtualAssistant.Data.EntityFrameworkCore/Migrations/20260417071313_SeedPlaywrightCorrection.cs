using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <summary>
    /// Seeds Whisper transcription corrections for the word "Playwright", which Czech
    /// dictation commonly mis-transcribes as "PlayWrite" / "PlyWrite" (with or without
    /// the missing "a", optionally split by a space). Inserts are idempotent
    /// (WHERE NOT EXISTS) so the migration is safe to re-run.
    /// </summary>
    public partial class SeedPlaywrightCorrection : Migration
    {
        private const string SeedNoteMarker = "Seeded by SeedPlaywrightCorrection (user-reported dictation error 2026-04-17)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Case-insensitive matching (helper hard-codes case_sensitive = false), so one
            // row covers "PlayWrite", "playwrite", "PLAYWRITE" etc. We still need a separate
            // row for the spaced variant ("Play Write") because Replace() is literal.
            InsertCorrectionIfMissing(migrationBuilder, "PlayWrite", "Playwright", 90);
            InsertCorrectionIfMissing(migrationBuilder, "Play Write", "Playwright", 90);
            InsertCorrectionIfMissing(migrationBuilder, "PlyWrite", "Playwright", 90);
            InsertCorrectionIfMissing(migrationBuilder, "Ply Write", "Playwright", 90);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Filter by notes marker so only rows inserted by THIS migration are removed —
            // never any pre-existing row that Up() may have skipped via WHERE NOT EXISTS.
            migrationBuilder.Sql($@"
                DELETE FROM transcription_corrections
                WHERE incorrect_text IN ('PlayWrite', 'Play Write', 'PlyWrite', 'Ply Write')
                  AND correct_text = 'Playwright'
                  AND notes = '{SeedNoteMarker}';
            ");
        }

        private static void InsertCorrectionIfMissing(
            MigrationBuilder migrationBuilder,
            string incorrectText,
            string correctText,
            int priority)
        {
            // Inputs are hard-coded literals in Up() above — no user-controlled data — so
            // embedding them directly into the SQL is safe. Single quotes are doubled per
            // the SQL standard escape.
            var escapedIncorrect = incorrectText.Replace("'", "''");
            var escapedCorrect = correctText.Replace("'", "''");

            migrationBuilder.Sql($@"
                INSERT INTO transcription_corrections
                    (incorrect_text, correct_text, case_sensitive, priority, is_active, notes, created_at, updated_at)
                SELECT
                    '{escapedIncorrect}',
                    '{escapedCorrect}',
                    false,
                    {priority},
                    true,
                    '{SeedNoteMarker}',
                    NOW(),
                    NOW()
                WHERE NOT EXISTS (
                    SELECT 1 FROM transcription_corrections
                    WHERE incorrect_text = '{escapedIncorrect}'
                );
            ");
        }
    }
}
