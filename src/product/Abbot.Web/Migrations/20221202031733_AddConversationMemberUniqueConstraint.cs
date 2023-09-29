using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddConversationMemberUniqueConstraint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConversationMembers_ConversationId",
                table: "ConversationMembers");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMembers_ConversationId_MemberId",
                table: "ConversationMembers",
                columns: new[] { "ConversationId", "MemberId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConversationMembers_ConversationId_MemberId",
                table: "ConversationMembers");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMembers_ConversationId",
                table: "ConversationMembers",
                column: "ConversationId");
        }
    }
}
