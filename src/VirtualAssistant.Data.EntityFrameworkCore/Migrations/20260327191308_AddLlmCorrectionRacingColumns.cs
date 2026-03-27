using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmCorrectionRacingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_winner",
                table: "llm_corrections",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "race_group_id",
                table: "llm_corrections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_llm_corrections_race_group_id",
                table: "llm_corrections",
                column: "race_group_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_llm_corrections_race_group_id",
                table: "llm_corrections");

            migrationBuilder.DropColumn(
                name: "is_winner",
                table: "llm_corrections");

            migrationBuilder.DropColumn(
                name: "race_group_id",
                table: "llm_corrections");
        }
    }
}
