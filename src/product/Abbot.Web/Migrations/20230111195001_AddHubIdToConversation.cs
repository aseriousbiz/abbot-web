using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddHubIdToConversation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HubId",
                table: "Conversations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HubThreadId",
                table: "Conversations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_HubId",
                table: "Conversations",
                column: "HubId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Hubs_HubId",
                table: "Conversations",
                column: "HubId",
                principalTable: "Hubs",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Hubs_HubId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_HubId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "HubId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "HubThreadId",
                table: "Conversations");
        }
    }
}
