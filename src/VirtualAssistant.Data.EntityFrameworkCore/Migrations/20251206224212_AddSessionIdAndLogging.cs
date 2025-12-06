using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionIdAndLogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "phase",
                table: "agent_messages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Complete");

            migrationBuilder.AddColumn<string>(
                name: "session_id",
                table: "agent_messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agent_message_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_agent = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    context = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_message_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_messages_session",
                table: "agent_messages",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_message_logs_created",
                table: "agent_message_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_agent_message_logs_level",
                table: "agent_message_logs",
                column: "level");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_message_logs");

            migrationBuilder.DropIndex(
                name: "ix_agent_messages_session",
                table: "agent_messages");

            migrationBuilder.DropColumn(
                name: "session_id",
                table: "agent_messages");

            migrationBuilder.AlterColumn<string>(
                name: "phase",
                table: "agent_messages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Complete",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }
    }
}
