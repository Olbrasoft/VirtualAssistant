using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProviderIdFromLlmModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "llm_models_provider_id_fkey",
                table: "llm_models");

            migrationBuilder.DropIndex(
                name: "ix_llm_models_provider_id",
                table: "llm_models");

            migrationBuilder.DropColumn(
                name: "provider_id",
                table: "llm_models");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "provider_id",
                table: "llm_models",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "llm_models",
                keyColumn: "id",
                keyValue: 1,
                column: "provider_id",
                value: 7);

            migrationBuilder.UpdateData(
                table: "llm_models",
                keyColumn: "id",
                keyValue: 2,
                column: "provider_id",
                value: 8);

            migrationBuilder.CreateIndex(
                name: "ix_llm_models_provider_id",
                table: "llm_models",
                column: "provider_id");

            migrationBuilder.AddForeignKey(
                name: "llm_models_provider_id_fkey",
                table: "llm_models",
                column: "provider_id",
                principalTable: "providers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
