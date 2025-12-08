using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RefactorAgentResponseWithTaskLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "started_at",
                table: "agent_tasks");

            migrationBuilder.AddColumn<int>(
                name: "agent_task_id",
                table: "agent_responses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_responses_agent_task_id",
                table: "agent_responses",
                column: "agent_task_id");

            migrationBuilder.AddForeignKey(
                name: "FK_agent_responses_agent_tasks_agent_task_id",
                table: "agent_responses",
                column: "agent_task_id",
                principalTable: "agent_tasks",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_responses_agent_tasks_agent_task_id",
                table: "agent_responses");

            migrationBuilder.DropIndex(
                name: "IX_agent_responses_agent_task_id",
                table: "agent_responses");

            migrationBuilder.DropColumn(
                name: "agent_task_id",
                table: "agent_responses");

            migrationBuilder.AddColumn<DateTime>(
                name: "started_at",
                table: "agent_tasks",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
