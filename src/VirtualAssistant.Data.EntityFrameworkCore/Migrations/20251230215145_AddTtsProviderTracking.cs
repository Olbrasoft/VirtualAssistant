using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddTtsProviderTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinalProviderId",
                table: "notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalTtsStatus",
                table: "notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TtsCompletedAt",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Providers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTtsAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotificationId = table.Column<int>(type: "integer", nullable: false),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    AttemptOrder = table.Column<int>(type: "integer", nullable: false),
                    StatusCode = table.Column<string>(type: "text", nullable: false),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTtsAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationTtsAttempts_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationTtsAttempts_notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_FinalProviderId",
                table: "notifications",
                column: "FinalProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTtsAttempts_NotificationId",
                table: "NotificationTtsAttempts",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTtsAttempts_ProviderId",
                table: "NotificationTtsAttempts",
                column: "ProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_Providers_FinalProviderId",
                table: "notifications",
                column: "FinalProviderId",
                principalTable: "Providers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notifications_Providers_FinalProviderId",
                table: "notifications");

            migrationBuilder.DropTable(
                name: "NotificationTtsAttempts");

            migrationBuilder.DropTable(
                name: "Providers");

            migrationBuilder.DropIndex(
                name: "IX_notifications_FinalProviderId",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "FinalProviderId",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "FinalTtsStatus",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "TtsCompletedAt",
                table: "notifications");
        }
    }
}
