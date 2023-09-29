using Microsoft.EntityFrameworkCore.Migrations;
using Serious.Abbot.Entities;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddSkillProperties : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<SkillProperties>(
                name: "Properties",
                table: "Skills",
                type: "jsonb",
                defaultValue: "{}",
                nullable: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Properties",
                table: "Skills");
        }
    }
}
