using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class SeedAntigravityAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert Antigravity agent with explicit ID matching AgentType enum
            migrationBuilder.InsertData(
                table: "agents",
                columns: new[] { "id", "created_at", "is_active", "label", "name" },
                values: new object[] { 20, new DateTime(2026, 1, 15, 12, 40, 0, 0, DateTimeKind.Utc), true, "agent:antigravity", "antigravity" });

            // Update PostgreSQL sequence to start after highest ID (20)
            migrationBuilder.Sql("SELECT setval('agents_id_seq', 20, true);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Delete Antigravity agent
            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "id",
                keyValue: 20);

            // Reset sequence back to previous highest ID (11)
            migrationBuilder.Sql("SELECT setval('agents_id_seq', 11, true);");
        }
    }
}
