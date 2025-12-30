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
            // Create providers table
            migrationBuilder.CreateTable(
                name: "providers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_providers", x => x.id);
                });

            // Create notification_tts_attempts table
            migrationBuilder.CreateTable(
                name: "notification_tts_attempts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    notification_id = table.Column<int>(type: "integer", nullable: false),
                    provider_id = table.Column<int>(type: "integer", nullable: false),
                    attempt_order = table.Column<int>(type: "integer", nullable: false),
                    status_code = table.Column<string>(type: "text", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    http_status_code = table.Column<int>(type: "integer", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_tts_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_notification_tts_attempts_notifications_notification_id",
                        column: x => x.notification_id,
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notification_tts_attempts_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Add TTS tracking columns to notifications table
            migrationBuilder.AddColumn<int>(
                name: "final_provider_id",
                table: "notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "final_tts_status",
                table: "notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "tts_completed_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            // Create indexes
            migrationBuilder.CreateIndex(
                name: "ix_providers_type",
                table: "providers",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "ix_providers_name_type",
                table: "providers",
                columns: new[] { "name", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_tts_attempts_notification",
                table: "notification_tts_attempts",
                column: "notification_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_tts_attempts_provider",
                table: "notification_tts_attempts",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_tts_attempts_created_at",
                table: "notification_tts_attempts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_final_provider",
                table: "notifications",
                column: "final_provider_id");

            // Add foreign key from notifications to providers
            migrationBuilder.AddForeignKey(
                name: "FK_notifications_providers_final_provider_id",
                table: "notifications",
                column: "final_provider_id",
                principalTable: "providers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Insert seed data
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
            // Drop foreign key from notifications to providers
            migrationBuilder.DropForeignKey(
                name: "FK_notifications_providers_final_provider_id",
                table: "notifications");

            // Drop index on notifications.final_provider_id
            migrationBuilder.DropIndex(
                name: "ix_notifications_final_provider",
                table: "notifications");

            // Drop TTS tracking columns from notifications
            migrationBuilder.DropColumn(
                name: "final_provider_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "final_tts_status",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "tts_completed_at",
                table: "notifications");

            // Drop notification_tts_attempts table (this will cascade delete seed data)
            migrationBuilder.DropTable(
                name: "notification_tts_attempts");

            // Drop providers table (this will cascade delete seed data)
            migrationBuilder.DropTable(
                name: "providers");
        }
    }
}
