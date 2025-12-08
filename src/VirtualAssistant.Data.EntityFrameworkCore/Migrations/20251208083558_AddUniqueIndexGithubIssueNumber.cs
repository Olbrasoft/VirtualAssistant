using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexGithubIssueNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_agent_tasks_issue_number",
                table: "agent_tasks");

            migrationBuilder.CreateIndex(
                name: "ix_agent_tasks_github_issue_number_unique",
                table: "agent_tasks",
                column: "github_issue_number",
                unique: true,
                filter: "github_issue_number IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_agent_tasks_github_issue_number_unique",
                table: "agent_tasks");

            migrationBuilder.CreateIndex(
                name: "ix_agent_tasks_issue_number",
                table: "agent_tasks",
                column: "github_issue_number");
        }
    }
}
