using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationStatusWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "notification_statuses",
                keyColumn: "id",
                keyValue: 2,
                column: "name",
                value: "Processing");

            migrationBuilder.UpdateData(
                table: "notification_statuses",
                keyColumn: "id",
                keyValue: 3,
                column: "name",
                value: "SentForSummarization");

            migrationBuilder.InsertData(
                table: "notification_statuses",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 4, "Summarized" },
                    { 5, "SentForTranslation" },
                    { 6, "Translated" },
                    { 7, "Announced" },
                    { 8, "WaitingForPlayback" },
                    { 9, "Played" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "notification_statuses",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "notification_statuses",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "notification_statuses",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "notification_statuses",
                keyColumn: "id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "notification_statuses",
                keyColumn: "id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "notification_statuses",
                keyColumn: "id",
                keyValue: 9);

            migrationBuilder.UpdateData(
                table: "notification_statuses",
                keyColumn: "id",
                keyValue: 2,
                column: "name",
                value: "Announced");

            migrationBuilder.UpdateData(
                table: "notification_statuses",
                keyColumn: "id",
                keyValue: 3,
                column: "name",
                value: "WaitingForPlayback");
        }
    }
}
