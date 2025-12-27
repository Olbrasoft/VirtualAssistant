using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPushToTalkEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transcription_corrections",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    incorrect_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    correct_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    case_sensitive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transcription_corrections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "whisper_transcriptions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    transcribed_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    audio_duration_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whisper_transcriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "llm_corrections",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    whisper_transcription_id = table.Column<int>(type: "integer", nullable: false),
                    corrected_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_corrections", x => x.id);
                    table.ForeignKey(
                        name: "FK_llm_corrections_whisper_transcriptions_whisper_transcriptio~",
                        column: x => x.whisper_transcription_id,
                        principalTable: "whisper_transcriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "llm_errors",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    whisper_transcription_id = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_errors", x => x.id);
                    table.ForeignKey(
                        name: "FK_llm_errors_whisper_transcriptions_whisper_transcription_id",
                        column: x => x.whisper_transcription_id,
                        principalTable: "whisper_transcriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_llm_corrections_created_at",
                table: "llm_corrections",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_llm_corrections_whisper_transcription_id",
                table: "llm_corrections",
                column: "whisper_transcription_id");

            migrationBuilder.CreateIndex(
                name: "IX_llm_errors_created_at",
                table: "llm_errors",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_llm_errors_whisper_transcription_id",
                table: "llm_errors",
                column: "whisper_transcription_id");

            migrationBuilder.CreateIndex(
                name: "IX_transcription_corrections_incorrect_text",
                table: "transcription_corrections",
                column: "incorrect_text");

            migrationBuilder.CreateIndex(
                name: "IX_transcription_corrections_is_active",
                table: "transcription_corrections",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_transcription_corrections_is_active_priority",
                table: "transcription_corrections",
                columns: new[] { "is_active", "priority" });

            migrationBuilder.CreateIndex(
                name: "IX_whisper_transcriptions_created_at",
                table: "whisper_transcriptions",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "llm_corrections");

            migrationBuilder.DropTable(
                name: "llm_errors");

            migrationBuilder.DropTable(
                name: "transcription_corrections");

            migrationBuilder.DropTable(
                name: "whisper_transcriptions");
        }
    }
}
