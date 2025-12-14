using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class DropAgentMessagingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_message_logs");

            migrationBuilder.DropTable(
                name: "agent_messages");

            migrationBuilder.DropTable(
                name: "agent_responses");

            migrationBuilder.DropTable(
                name: "agent_task_sends");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_message_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    context = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    source_agent = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_message_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_messages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parent_message_id = table.Column<int>(type: "integer", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    message_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    session_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_agent = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    target_agent = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_messages_agent_messages_parent_message_id",
                        column: x => x.parent_message_id,
                        principalTable: "agent_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "agent_responses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agent_task_id = table.Column<int>(type: "integer", nullable: true),
                    agent_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_responses", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_responses_agent_tasks_agent_task_id",
                        column: x => x.agent_task_id,
                        principalTable: "agent_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "agent_task_sends",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agent_id = table.Column<int>(type: "integer", nullable: false),
                    task_id = table.Column<int>(type: "integer", nullable: false),
                    delivery_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    response = table.Column<string>(type: "text", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_task_sends", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_task_sends_agent_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "agent_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_task_sends_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_message_logs_created",
                table: "agent_message_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_agent_message_logs_level",
                table: "agent_message_logs",
                column: "level");

            migrationBuilder.CreateIndex(
                name: "ix_agent_messages_approval_status",
                table: "agent_messages",
                columns: new[] { "requires_approval", "status" },
                filter: "requires_approval = true AND status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_agent_messages_created_at",
                table: "agent_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_agent_messages_parent",
                table: "agent_messages",
                column: "parent_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_messages_session",
                table: "agent_messages",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_messages_source_phase",
                table: "agent_messages",
                columns: new[] { "source_agent", "phase" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_messages_target_status",
                table: "agent_messages",
                columns: new[] { "target_agent", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_responses_agent_name",
                table: "agent_responses",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_agent_responses_agent_name_started_at",
                table: "agent_responses",
                columns: new[] { "agent_name", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_responses_agent_task_id",
                table: "agent_responses",
                column: "agent_task_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_responses_status",
                table: "agent_responses",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_agent_task_sends_agent",
                table: "agent_task_sends",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_task_sends_task",
                table: "agent_task_sends",
                column: "task_id");
        }
    }
}
