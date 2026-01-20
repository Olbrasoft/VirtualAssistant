using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmTrackingToNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "llm_model_id",
                table: "notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "llm_provider_id",
                table: "notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "model_provider_mappings",
                columns: table => new
                {
                    model_id = table.Column<int>(type: "integer", nullable: false),
                    provider_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_provider_mappings", x => new { x.model_id, x.provider_id });
                    table.ForeignKey(
                        name: "FK_model_provider_mappings_llm_models_model_id",
                        column: x => x.model_id,
                        principalTable: "llm_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_model_provider_mappings_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "model_provider_mappings",
                columns: new[] { "model_id", "provider_id", "created_at" },
                values: new object[,]
                {
                    { 1, 7, new DateTime(2026, 1, 20, 12, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 8, new DateTime(2026, 1, 20, 12, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_llm_model",
                table: "notifications",
                column: "llm_model_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_llm_provider",
                table: "notifications",
                column: "llm_provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_model_provider_mappings_provider_id",
                table: "model_provider_mappings",
                column: "provider_id");

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_llm_models_llm_model_id",
                table: "notifications",
                column: "llm_model_id",
                principalTable: "llm_models",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_providers_llm_provider_id",
                table: "notifications",
                column: "llm_provider_id",
                principalTable: "providers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notifications_llm_models_llm_model_id",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_providers_llm_provider_id",
                table: "notifications");

            migrationBuilder.DropTable(
                name: "model_provider_mappings");

            migrationBuilder.DropIndex(
                name: "ix_notifications_llm_model",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_llm_provider",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "llm_model_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "llm_provider_id",
                table: "notifications");
        }
    }
}
