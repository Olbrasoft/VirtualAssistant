using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <summary>
    /// Seeds additional Whisper transcription corrections discovered through analysis of
    /// 11,235 voice transcriptions over 3.5 months. Adds 8 missing "Claude Code" variants
    /// (covering ~191 untreated occurrences) and 3 missing "pokračuj" variants. Also raises
    /// the priority of the existing "Cloud Coder" entry so it fires before the new
    /// "Cloud Code" entry, which would otherwise corrupt it via substring replacement.
    /// Inserts are idempotent (WHERE NOT EXISTS) so the migration is safe to re-run.
    /// </summary>
    public partial class SeedClaudeCodeAndPokracujCorrections : Migration
    {
        // Marker stored in the `notes` column for every row inserted by this migration.
        // Down() filters by this marker so that pre-existing rows (potentially skipped by
        // the idempotent Up() insert) are never deleted by a rollback.
        private const string SeedNoteMarker = "Seeded by SeedClaudeCodeAndPokracujCorrections (DB analysis 2026-04-07)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Raise priority of the existing "Cloud Coder → Claude Code" entry from 90 to 95
            // so that it is applied BEFORE the new "Cloud Code" entry. Otherwise the substring
            // matcher in DatabaseCorrectionFilterStrategy would replace "Cloud Code" inside
            // "Cloud Coder" first, leaving "Claude Coder" mangled.
            //
            // Scoped by both `incorrect_text` AND `correct_text` so an unrelated row that
            // happens to share the `incorrect_text` value (different correction target) is
            // not accidentally modified.
            migrationBuilder.Sql(@"
                UPDATE transcription_corrections
                SET priority = 95, updated_at = NOW()
                WHERE incorrect_text = 'Cloud Coder'
                  AND correct_text = 'Claude Code'
                  AND priority < 95;
            ");

            // Insert missing "Claude Code" variants. Priorities are ordered by length (longer
            // = higher priority) so substring matches don't corrupt longer variants.
            // Idempotent: WHERE NOT EXISTS prevents duplicates if migration is re-run.
            InsertCorrectionIfMissing(migrationBuilder, "cloud kóde", "Claude Code", 95);  // 71x
            InsertCorrectionIfMissing(migrationBuilder, "cloud kode", "Claude Code", 95);  // 55x
            InsertCorrectionIfMissing(migrationBuilder, "Cloud kóde", "Claude Code", 95);  //  5x
            InsertCorrectionIfMissing(migrationBuilder, "cloud kód",  "Claude Code", 90);  //  4x
            InsertCorrectionIfMissing(migrationBuilder, "Cloud Code", "Claude Code", 90);  // 43x (with space)
            InsertCorrectionIfMissing(migrationBuilder, "cloud code", "Claude Code", 90);  //  2x
            InsertCorrectionIfMissing(migrationBuilder, "cloudkode",  "Claude Code", 85);  //  5x
            InsertCorrectionIfMissing(migrationBuilder, "Cloud kóda", "Claude Code", 85);  //  1x

            // Insert missing "pokračuj" variants (bonus from same DB analysis).
            InsertCorrectionIfMissing(migrationBuilder, "Pokraciu i prosím", "pokračuj prosím", 10);  // 3x
            InsertCorrectionIfMissing(migrationBuilder, "Pokačuj",            "pokračuj",        10);  // 2x (missing 'r')
            InsertCorrectionIfMissing(migrationBuilder, "Pocaciu i",          "pokračuj",        10);  // 2x
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert priority change for Cloud Coder. Scoped by `incorrect_text` AND
            // `correct_text` so unrelated rows are not affected, and by `priority = 95` so
            // we only revert rows that Up() actually changed.
            migrationBuilder.Sql(@"
                UPDATE transcription_corrections
                SET priority = 90, updated_at = NOW()
                WHERE incorrect_text = 'Cloud Coder'
                  AND correct_text = 'Claude Code'
                  AND priority = 95;
            ");

            // Remove inserted Claude Code variants. Filtered by `notes = SeedNoteMarker`
            // so we only delete rows that THIS migration created — never pre-existing rows
            // that Up() may have skipped via WHERE NOT EXISTS.
            migrationBuilder.Sql($@"
                DELETE FROM transcription_corrections
                WHERE correct_text = 'Claude Code'
                  AND incorrect_text IN (
                      'cloud kóde', 'cloud kode', 'Cloud kóde', 'cloud kód',
                      'Cloud Code', 'cloud code', 'cloudkode', 'Cloud kóda'
                  )
                  AND notes = '{SeedNoteMarker}';
            ");

            // Remove inserted pokračuj variants. Same notes-marker safety as above.
            migrationBuilder.Sql($@"
                DELETE FROM transcription_corrections
                WHERE incorrect_text IN ('Pokraciu i prosím', 'Pokačuj', 'Pocaciu i')
                  AND correct_text IN ('pokračuj', 'pokračuj prosím')
                  AND notes = '{SeedNoteMarker}';
            ");
        }

        private static void InsertCorrectionIfMissing(
            MigrationBuilder migrationBuilder,
            string incorrectText,
            string correctText,
            int priority)
        {
            // MigrationBuilder.Sql executes raw SQL text, so this helper embeds the values
            // directly into the statement and escapes single quotes in string literals by
            // doubling them ('' is the SQL standard escape). Inputs to this helper are
            // hard-coded compile-time literals from the migration body — there is no
            // user-controlled data, so SQL injection is not a concern here.
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
