using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddMercury2ProviderAndTokenTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "input_tokens",
                table: "llm_corrections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "output_tokens",
                table: "llm_corrections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reasoning_tokens",
                table: "llm_corrections",
                type: "integer",
                nullable: true);

            // Seed Inception Labs provider and Mercury 2 model using raw SQL.
            // Uses INSERT ... ON CONFLICT to be idempotent (safe to re-run).
            // Does not hardcode IDs - uses sequences for auto-increment.
            migrationBuilder.Sql(@"
                INSERT INTO providers (name, type, enabled, priority, created_at)
                SELECT 'Inception Labs', 'llm', true, 10, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM providers WHERE name = 'Inception Labs' AND type = 'llm');

                INSERT INTO llm_models (name, model_identifier, is_active, created_at)
                SELECT 'Mercury 2', 'mercury-2', true, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM llm_models WHERE model_identifier = 'mercury-2');

                INSERT INTO model_provider_mappings (model_id, provider_id, created_at)
                SELECT m.id, p.id, NOW()
                FROM llm_models m, providers p
                WHERE m.model_identifier = 'mercury-2' AND p.name = 'Inception Labs' AND p.type = 'llm'
                AND NOT EXISTS (
                    SELECT 1 FROM model_provider_mappings mp WHERE mp.model_id = m.id AND mp.provider_id = p.id
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM model_provider_mappings
                WHERE model_id = (SELECT id FROM llm_models WHERE model_identifier = 'mercury-2')
                  AND provider_id = (SELECT id FROM providers WHERE name = 'Inception Labs' AND type = 'llm');
                DELETE FROM llm_models WHERE model_identifier = 'mercury-2';
                DELETE FROM providers WHERE name = 'Inception Labs' AND type = 'llm';
            ");

            migrationBuilder.DropColumn(
                name: "input_tokens",
                table: "llm_corrections");

            migrationBuilder.DropColumn(
                name: "output_tokens",
                table: "llm_corrections");

            migrationBuilder.DropColumn(
                name: "reasoning_tokens",
                table: "llm_corrections");
        }
    }
}
