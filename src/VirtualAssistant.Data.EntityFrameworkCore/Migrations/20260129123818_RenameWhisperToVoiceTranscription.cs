using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RenameWhisperToVoiceTranscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add STT providers (must be done first for FK constraint)
            migrationBuilder.InsertData(
                table: "providers",
                columns: new[] { "id", "created_at", "enabled", "name", "priority", "type" },
                values: new object[,]
                {
                    { 13, new DateTime(2026, 1, 29, 12, 0, 0, 0, DateTimeKind.Utc), true, "Whisper Local", 2, "stt" },
                    { 14, new DateTime(2026, 1, 29, 12, 0, 0, 0, DateTimeKind.Utc), true, "Google Speech-to-Text", 1, "stt" }
                });

            // Step 2: Drop existing FKs from llm_corrections and llm_errors
            migrationBuilder.DropForeignKey(
                name: "FK_llm_corrections_whisper_transcriptions_whisper_transcriptio~",
                table: "llm_corrections");

            migrationBuilder.DropForeignKey(
                name: "FK_llm_errors_whisper_transcriptions_whisper_transcription_id",
                table: "llm_errors");

            // Step 3: Rename FK columns in llm_corrections and llm_errors
            migrationBuilder.RenameColumn(
                name: "whisper_transcription_id",
                table: "llm_errors",
                newName: "voice_transcription_id");

            migrationBuilder.RenameIndex(
                name: "IX_llm_errors_whisper_transcription_id",
                table: "llm_errors",
                newName: "IX_llm_errors_voice_transcription_id");

            migrationBuilder.RenameColumn(
                name: "whisper_transcription_id",
                table: "llm_corrections",
                newName: "voice_transcription_id");

            migrationBuilder.RenameIndex(
                name: "IX_llm_corrections_whisper_transcription_id",
                table: "llm_corrections",
                newName: "IX_llm_corrections_voice_transcription_id");

            // Step 4: Rename table whisper_transcriptions to voice_transcriptions
            migrationBuilder.RenameTable(
                name: "whisper_transcriptions",
                newName: "voice_transcriptions");

            // Step 5: Rename primary key
            migrationBuilder.RenameIndex(
                name: "PK_whisper_transcriptions",
                table: "voice_transcriptions",
                newName: "PK_voice_transcriptions");

            migrationBuilder.RenameIndex(
                name: "IX_whisper_transcriptions_created_at",
                table: "voice_transcriptions",
                newName: "IX_voice_transcriptions_created_at");

            // Step 6: Add provider_id column (nullable first for existing data)
            migrationBuilder.AddColumn<int>(
                name: "provider_id",
                table: "voice_transcriptions",
                type: "integer",
                nullable: true);

            // Step 7: Set default provider_id for existing rows (Whisper Local = 13)
            migrationBuilder.Sql("UPDATE voice_transcriptions SET provider_id = 13 WHERE provider_id IS NULL");

            // Step 8: Make provider_id NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "provider_id",
                table: "voice_transcriptions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // Step 9: Create index on provider_id
            migrationBuilder.CreateIndex(
                name: "ix_voice_transcriptions_provider_id",
                table: "voice_transcriptions",
                column: "provider_id");

            // Step 10: Add FK constraint for provider_id
            migrationBuilder.AddForeignKey(
                name: "FK_voice_transcriptions_providers_provider_id",
                table: "voice_transcriptions",
                column: "provider_id",
                principalTable: "providers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Step 11: Re-add FKs from llm_corrections and llm_errors to voice_transcriptions
            migrationBuilder.AddForeignKey(
                name: "FK_llm_corrections_voice_transcriptions_voice_transcription_id",
                table: "llm_corrections",
                column: "voice_transcription_id",
                principalTable: "voice_transcriptions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_llm_errors_voice_transcriptions_voice_transcription_id",
                table: "llm_errors",
                column: "voice_transcription_id",
                principalTable: "voice_transcriptions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop FKs from llm_corrections and llm_errors
            migrationBuilder.DropForeignKey(
                name: "FK_llm_corrections_voice_transcriptions_voice_transcription_id",
                table: "llm_corrections");

            migrationBuilder.DropForeignKey(
                name: "FK_llm_errors_voice_transcriptions_voice_transcription_id",
                table: "llm_errors");

            // Step 2: Drop FK and index for provider_id
            migrationBuilder.DropForeignKey(
                name: "FK_voice_transcriptions_providers_provider_id",
                table: "voice_transcriptions");

            migrationBuilder.DropIndex(
                name: "ix_voice_transcriptions_provider_id",
                table: "voice_transcriptions");

            // Step 3: Drop provider_id column
            migrationBuilder.DropColumn(
                name: "provider_id",
                table: "voice_transcriptions");

            // Step 4: Rename table back to whisper_transcriptions
            migrationBuilder.RenameTable(
                name: "voice_transcriptions",
                newName: "whisper_transcriptions");

            // Step 5: Rename indexes back
            migrationBuilder.RenameIndex(
                name: "PK_voice_transcriptions",
                table: "whisper_transcriptions",
                newName: "PK_whisper_transcriptions");

            migrationBuilder.RenameIndex(
                name: "IX_voice_transcriptions_created_at",
                table: "whisper_transcriptions",
                newName: "IX_whisper_transcriptions_created_at");

            // Step 6: Rename FK columns back in llm_corrections and llm_errors
            migrationBuilder.RenameColumn(
                name: "voice_transcription_id",
                table: "llm_errors",
                newName: "whisper_transcription_id");

            migrationBuilder.RenameIndex(
                name: "IX_llm_errors_voice_transcription_id",
                table: "llm_errors",
                newName: "IX_llm_errors_whisper_transcription_id");

            migrationBuilder.RenameColumn(
                name: "voice_transcription_id",
                table: "llm_corrections",
                newName: "whisper_transcription_id");

            migrationBuilder.RenameIndex(
                name: "IX_llm_corrections_voice_transcription_id",
                table: "llm_corrections",
                newName: "IX_llm_corrections_whisper_transcription_id");

            // Step 7: Re-add FKs from llm_corrections and llm_errors to whisper_transcriptions
            migrationBuilder.AddForeignKey(
                name: "FK_llm_corrections_whisper_transcriptions_whisper_transcriptio~",
                table: "llm_corrections",
                column: "whisper_transcription_id",
                principalTable: "whisper_transcriptions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_llm_errors_whisper_transcriptions_whisper_transcription_id",
                table: "llm_errors",
                column: "whisper_transcription_id",
                principalTable: "whisper_transcriptions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // Step 8: Delete STT providers
            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: 14);
        }
    }
}
