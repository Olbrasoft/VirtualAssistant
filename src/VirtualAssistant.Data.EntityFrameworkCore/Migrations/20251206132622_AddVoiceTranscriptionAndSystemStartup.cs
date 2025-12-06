using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceTranscriptionAndSystemStartup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_startups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    shutdown_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    shutdown_type = table.Column<string>(type: "text", nullable: true),
                    startup_type = table.Column<string>(type: "text", nullable: false),
                    greeting_spoken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_startups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "voice_transcriptions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    transcribed_text = table.Column<string>(type: "text", nullable: false),
                    source_app = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voice_transcriptions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_startups_started_at",
                table: "system_startups",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "IX_voice_transcriptions_created_at",
                table: "voice_transcriptions",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_startups");

            migrationBuilder.DropTable(
                name: "voice_transcriptions");
        }
    }
}
