using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddPlaybookRunGroupCreatedById : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedById",
                table: "PlaybookRunGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookRunGroups_CreatedById",
                table: "PlaybookRunGroups",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybookRunGroups_Members_CreatedById",
                table: "PlaybookRunGroups",
                column: "CreatedById",
                principalTable: "Members",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlaybookRunGroups_Members_CreatedById",
                table: "PlaybookRunGroups");

            migrationBuilder.DropIndex(
                name: "IX_PlaybookRunGroups_CreatedById",
                table: "PlaybookRunGroups");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PlaybookRunGroups");
        }
    }
}
