using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddAttachToHubEvent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HubId",
                table: "ConversationEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationEvents_HubId",
                table: "ConversationEvents",
                column: "HubId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationEvents_Hubs_HubId",
                table: "ConversationEvents",
                column: "HubId",
                principalTable: "Hubs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationEvents_Hubs_HubId",
                table: "ConversationEvents");

            migrationBuilder.DropIndex(
                name: "IX_ConversationEvents_HubId",
                table: "ConversationEvents");

            migrationBuilder.DropColumn(
                name: "HubId",
                table: "ConversationEvents");
        }
    }
}
