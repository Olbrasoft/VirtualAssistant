using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddTtsProviderTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "providers",
                columns: new[] { "id", "created_at", "enabled", "name", "priority", "type" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 12, 30, 22, 51, 45, 0, DateTimeKind.Utc), true, "AzureTTS", 1, "tts" },
                    { 2, new DateTime(2024, 12, 30, 22, 51, 45, 0, DateTimeKind.Utc), true, "EdgeTTS-WebSocket", 2, "tts" },
                    { 3, new DateTime(2024, 12, 30, 22, 51, 45, 0, DateTimeKind.Utc), true, "VoiceRSS", 3, "tts" },
                    { 4, new DateTime(2024, 12, 30, 22, 51, 45, 0, DateTimeKind.Utc), true, "GoogleTTS", 4, "tts" },
                    { 5, new DateTime(2024, 12, 30, 22, 51, 45, 0, DateTimeKind.Utc), true, "Piper", 5, "tts" },
                    { 6, new DateTime(2024, 12, 30, 22, 51, 45, 0, DateTimeKind.Utc), true, "cache", 0, "tts" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: 6);
        }
    }
}
