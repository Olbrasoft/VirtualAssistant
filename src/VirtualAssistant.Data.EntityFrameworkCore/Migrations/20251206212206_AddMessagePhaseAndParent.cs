using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualAssistant.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagePhaseAndParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "parent_message_id",
                table: "agent_messages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phase",
                table: "agent_messages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Complete");

            migrationBuilder.CreateIndex(
                name: "ix_agent_messages_parent",
                table: "agent_messages",
                column: "parent_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_messages_source_phase",
                table: "agent_messages",
                columns: new[] { "source_agent", "phase" });

            migrationBuilder.AddForeignKey(
                name: "FK_agent_messages_agent_messages_parent_message_id",
                table: "agent_messages",
                column: "parent_message_id",
                principalTable: "agent_messages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_messages_agent_messages_parent_message_id",
                table: "agent_messages");

            migrationBuilder.DropIndex(
                name: "ix_agent_messages_parent",
                table: "agent_messages");

            migrationBuilder.DropIndex(
                name: "ix_agent_messages_source_phase",
                table: "agent_messages");

            migrationBuilder.DropColumn(
                name: "parent_message_id",
                table: "agent_messages");

            migrationBuilder.DropColumn(
                name: "phase",
                table: "agent_messages");
        }
    }
}
