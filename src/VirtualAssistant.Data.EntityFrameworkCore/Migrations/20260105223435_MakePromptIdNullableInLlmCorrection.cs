using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class MakePromptIdNullableInLlmCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make prompt_id nullable to allow fallback cases where prompt selection fails
            migrationBuilder.AlterColumn<int>(
                name: "prompt_id",
                table: "llm_corrections",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to NOT NULL (may fail if NULL values exist)
            migrationBuilder.AlterColumn<int>(
                name: "prompt_id",
                table: "llm_corrections",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
