using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmModelsAndProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "model_id",
                table: "llm_corrections",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "llm_models",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_id = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_models", x => x.id);
                    table.ForeignKey(
                        name: "FK_llm_models_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "providers",
                columns: new[] { "id", "created_at", "enabled", "name", "priority", "type" },
                values: new object[,]
                {
                    { 7, new DateTime(2026, 1, 16, 12, 0, 0, 0, DateTimeKind.Utc), true, "Mistral AI", 1, "llm" },
                    { 8, new DateTime(2026, 1, 16, 12, 0, 0, 0, DateTimeKind.Utc), true, "Zen", 2, "llm" }
                });

            migrationBuilder.InsertData(
                table: "llm_models",
                columns: new[] { "id", "created_at", "is_active", "model_identifier", "name", "provider_id" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 16, 12, 0, 0, 0, DateTimeKind.Utc), true, "mistral-large-latest", "Mistral Large", 7 },
                    { 2, new DateTime(2026, 1, 16, 12, 0, 0, 0, DateTimeKind.Utc), true, "alpha-glm-4.7", "Alpha GLM 4.7", 8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_llm_corrections_model_id",
                table: "llm_corrections",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "ix_llm_models_model_identifier",
                table: "llm_models",
                column: "model_identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_llm_models_provider_id",
                table: "llm_models",
                column: "provider_id");

            // Migrate existing corrections - all were made by Mistral (ID=1)
            migrationBuilder.Sql("UPDATE llm_corrections SET model_id = 1 WHERE model_id IS NULL;");

            migrationBuilder.AddForeignKey(
                name: "FK_llm_corrections_llm_models_model_id",
                table: "llm_corrections",
                column: "model_id",
                principalTable: "llm_models",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_llm_corrections_llm_models_model_id",
                table: "llm_corrections");

            migrationBuilder.DropTable(
                name: "llm_models");

            migrationBuilder.DropIndex(
                name: "IX_llm_corrections_model_id",
                table: "llm_corrections");

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: 8);

            migrationBuilder.DropColumn(
                name: "model_id",
                table: "llm_corrections");
        }
    }
}
