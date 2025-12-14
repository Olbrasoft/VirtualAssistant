using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationGitHubIssuesJunctionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_github_issues",
                columns: table => new
                {
                    notification_id = table.Column<int>(type: "integer", nullable: false),
                    github_issue_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_github_issues", x => new { x.notification_id, x.github_issue_id });
                    table.ForeignKey(
                        name: "FK_notification_github_issues_notifications_notification_id",
                        column: x => x.notification_id,
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_github_issues_github_issue_id",
                table: "notification_github_issues",
                column: "github_issue_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_github_issues");
        }
    }
}
