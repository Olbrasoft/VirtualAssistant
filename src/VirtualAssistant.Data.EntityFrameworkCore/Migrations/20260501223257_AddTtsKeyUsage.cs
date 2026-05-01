using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddTtsKeyUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tts_key_usage",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    counter_year = table.Column<int>(type: "integer", nullable: false),
                    counter_month = table.Column<int>(type: "integer", nullable: false),
                    monthly_character_count = table.Column<long>(type: "bigint", nullable: false),
                    total_successes = table.Column<long>(type: "bigint", nullable: false),
                    total_failures = table.Column<long>(type: "bigint", nullable: false),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    last_success_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    state = table.Column<int>(type: "integer", nullable: false),
                    cooldown_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tts_key_usage", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tts_key_usage_key_name_unique",
                table: "tts_key_usage",
                column: "key_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tts_key_usage");
        }
    }
}
