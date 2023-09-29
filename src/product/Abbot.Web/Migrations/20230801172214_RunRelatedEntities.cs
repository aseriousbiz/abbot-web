using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class RunRelatedEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Related_ConversationId",
                table: "PlaybookRuns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Related_CustomerId",
                table: "PlaybookRuns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Related_RoomId",
                table: "PlaybookRuns",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookRuns_Related_ConversationId",
                table: "PlaybookRuns",
                column: "Related_ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookRuns_Related_CustomerId",
                table: "PlaybookRuns",
                column: "Related_CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookRuns_Related_RoomId",
                table: "PlaybookRuns",
                column: "Related_RoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybookRuns_Conversations_Related_ConversationId",
                table: "PlaybookRuns",
                column: "Related_ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybookRuns_Customers_Related_CustomerId",
                table: "PlaybookRuns",
                column: "Related_CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybookRuns_Rooms_Related_RoomId",
                table: "PlaybookRuns",
                column: "Related_RoomId",
                principalTable: "Rooms",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlaybookRuns_Conversations_Related_ConversationId",
                table: "PlaybookRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybookRuns_Customers_Related_CustomerId",
                table: "PlaybookRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybookRuns_Rooms_Related_RoomId",
                table: "PlaybookRuns");

            migrationBuilder.DropIndex(
                name: "IX_PlaybookRuns_Related_ConversationId",
                table: "PlaybookRuns");

            migrationBuilder.DropIndex(
                name: "IX_PlaybookRuns_Related_CustomerId",
                table: "PlaybookRuns");

            migrationBuilder.DropIndex(
                name: "IX_PlaybookRuns_Related_RoomId",
                table: "PlaybookRuns");

            migrationBuilder.DropColumn(
                name: "Related_ConversationId",
                table: "PlaybookRuns");

            migrationBuilder.DropColumn(
                name: "Related_CustomerId",
                table: "PlaybookRuns");

            migrationBuilder.DropColumn(
                name: "Related_RoomId",
                table: "PlaybookRuns");
        }
    }
}
