using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubIssueAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "github_issue_agents",
                columns: table => new
                {
                    github_issue_id = table.Column<int>(type: "integer", nullable: false),
                    agent = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_github_issue_agents", x => new { x.github_issue_id, x.agent });
                    table.ForeignKey(
                        name: "FK_github_issue_agents_github_issues_github_issue_id",
                        column: x => x.github_issue_id,
                        principalTable: "github_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "github_issue_agents");
        }
    }
}
