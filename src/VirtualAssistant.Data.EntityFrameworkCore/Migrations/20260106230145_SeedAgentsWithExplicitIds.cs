using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class SeedAgentsWithExplicitIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete any existing agents seeded by previous migration
            // (these would have auto-generated IDs, likely 1 and 2)
            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "name",
                keyValue: "opencode");

            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "name",
                keyValue: "claude");

            // Insert agents with explicit IDs matching AgentType enum
            migrationBuilder.InsertData(
                table: "agents",
                columns: new[] { "id", "created_at", "is_active", "label", "name" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 12, 6, 23, 11, 18, 0, DateTimeKind.Utc), true, "agent:opencode", "opencode" },
                    { 4, new DateTime(2025, 12, 6, 23, 11, 18, 0, DateTimeKind.Utc), true, "agent:claude-code", "claude-code" },
                    { 11, new DateTime(2026, 1, 6, 22, 43, 47, 0, DateTimeKind.Utc), true, "agent:gemini", "gemini" }
                });

            // Reset PostgreSQL sequence to start after highest ID (11)
            migrationBuilder.Sql("SELECT setval('agents_id_seq', 11, true);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Delete new agents
            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "id",
                keyValue: 11);

            // Restore original agents with auto-generated IDs
            migrationBuilder.InsertData(
                table: "agents",
                columns: new[] { "name", "label", "is_active", "created_at" },
                values: new object[,]
                {
                    { "opencode", "agent:opencode", true, new DateTime(2025, 12, 6, 23, 11, 18, 0, DateTimeKind.Utc) },
                    { "claude", "agent:claude", true, new DateTime(2025, 12, 6, 23, 11, 18, 0, DateTimeKind.Utc) }
                });
        }
    }
}
