using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RevertPromptIdToNotNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Fix any existing NULL values to default (4)
            migrationBuilder.Sql("UPDATE llm_corrections SET prompt_id = 4 WHERE prompt_id IS NULL;");

            // Step 2: Revert prompt_id back to NOT NULL
            // Previous migration made it nullable, which was a mistake - prompt_id must always have a value
            migrationBuilder.AlterColumn<int>(
                name: "prompt_id",
                table: "llm_corrections",
                type: "integer",
                nullable: false,
                defaultValue: 4, // Temporary default for migration safety
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // Step 3: Remove default constraint (runtime always provides explicit value)
            migrationBuilder.Sql("ALTER TABLE llm_corrections ALTER COLUMN prompt_id DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to nullable (for rollback only - not recommended)
            migrationBuilder.AlterColumn<int>(
                name: "prompt_id",
                table: "llm_corrections",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
