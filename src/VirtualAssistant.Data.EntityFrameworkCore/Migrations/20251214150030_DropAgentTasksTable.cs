using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class DropAgentTasksTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_tasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_tasks",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    created_by_agent_id = table.Column<int>(type: "integer", nullable: true),
                    target_agent_id = table.Column<int>(type: "integer", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    claude_session_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    github_issue_number = table.Column<int>(type: "integer", nullable: true),
                    github_issue_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    result = table.Column<string>(type: "text", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    summary = table.Column<string>(type: "text", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_agent_tasks_created_by_agent_id",
                table: "agent_tasks",
                column: "created_by_agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_tasks_github_issue_number_unique",
                table: "agent_tasks",
                column: "github_issue_number",
                unique: true,
                filter: "github_issue_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_agent_tasks_status",
                table: "agent_tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_agent_tasks_target_status",
                table: "agent_tasks",
                columns: new[] { "target_agent_id", "status" });
        }
    }
}
