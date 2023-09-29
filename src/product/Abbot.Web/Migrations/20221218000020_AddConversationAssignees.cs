using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddConversationAssignees : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationMember",
                columns: table => new
                {
                    AssignedConversationsId = table.Column<int>(type: "integer", nullable: false),
                    AssigneesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMember", x => new { x.AssignedConversationsId, x.AssigneesId });
                    table.ForeignKey(
                        name: "FK_ConversationMember_Conversations_AssignedConversationsId",
                        column: x => x.AssignedConversationsId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationMember_Members_AssigneesId",
                        column: x => x.AssigneesId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMember_AssigneesId",
                table: "ConversationMember",
                column: "AssigneesId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationMember");
        }
    }
}
