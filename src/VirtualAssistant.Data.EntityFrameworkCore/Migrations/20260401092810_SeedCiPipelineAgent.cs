using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class SeedCiPipelineAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "agents",
                columns: new[] { "id", "created_at", "is_active", "label", "name" },
                values: new object[] { 30, new DateTime(2026, 4, 1, 10, 0, 0, 0, DateTimeKind.Utc), true, "agent:ci-pipeline", "ci-pipeline" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "id",
                keyValue: 30);
        }
    }
}
