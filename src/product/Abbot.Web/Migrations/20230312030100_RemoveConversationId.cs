using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class RemoveConversationId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "SkillTriggers");

            migrationBuilder.AlterColumn<string>(
                name: "ConversationId",
                table: "Rooms",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConversationId",
                table: "SkillTriggers",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConversationId",
                table: "Rooms",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
