using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class IndexRunGroupCorrelationId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PlaybookRunGroups_CorrelationId",
                table: "PlaybookRunGroups",
                column: "CorrelationId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlaybookRunGroups_CorrelationId",
                table: "PlaybookRunGroups");
        }
    }
}
