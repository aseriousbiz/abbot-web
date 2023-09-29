using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddIndexToSlackEventsCreated : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SlackEventsRollups_Date",
                table: "SlackEventsRollups",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_SlackEvents_Created",
                table: "SlackEvents",
                column: "Created");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlackEventsRollups_Date",
                table: "SlackEventsRollups");

            migrationBuilder.DropIndex(
                name: "IX_SlackEvents_Created",
                table: "SlackEvents");
        }
    }
}
