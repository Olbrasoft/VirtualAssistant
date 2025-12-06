using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTaskQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_tasks",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    github_issue_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    github_issue_number = table.Column<int>(type: "integer", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: false),
                    created_by_agent_id = table.Column<int>(type: "integer", nullable: true),
                    target_agent_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    result = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_tasks_agents_created_by_agent_id",
                        column: x => x.created_by_agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_agent_tasks_agents_target_agent_id",
                        column: x => x.target_agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "agent_task_sends",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_id = table.Column<int>(type: "integer", nullable: false),
                    agent_id = table.Column<int>(type: "integer", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    delivery_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    response = table.Column<string>(type: "text", nullable: true)
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
                name: "ix_agent_task_sends_agent",
                table: "agent_task_sends",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_task_sends_task",
                table: "agent_task_sends",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_tasks_created_by_agent_id",
                table: "agent_tasks",
                column: "created_by_agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_tasks_issue_number",
                table: "agent_tasks",
                column: "github_issue_number");

            migrationBuilder.CreateIndex(
                name: "ix_agent_tasks_status",
                table: "agent_tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_agent_tasks_target_status",
                table: "agent_tasks",
                columns: new[] { "target_agent_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_agents_is_active",
                table: "agents",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_agents_name_unique",
                table: "agents",
                column: "name",
                unique: true);

            // Seed initial agents
            migrationBuilder.InsertData(
                table: "agents",
                columns: new[] { "name", "label", "is_active", "created_at" },
                values: new object[] { "opencode", "agent:opencode", true, DateTime.UtcNow });

            migrationBuilder.InsertData(
                table: "agents",
                columns: new[] { "name", "label", "is_active", "created_at" },
                values: new object[] { "claude", "agent:claude", true, DateTime.UtcNow });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_task_sends");

            migrationBuilder.DropTable(
                name: "agent_tasks");

            migrationBuilder.DropTable(
                name: "agents");
        }
    }
}
