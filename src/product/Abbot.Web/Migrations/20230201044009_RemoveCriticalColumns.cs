using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class RemoveCriticalColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeToRespond_Critical",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "DefaultTimeToRespond_Critical",
                table: "Organizations");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "TimeToRespond_Critical",
                table: "Rooms",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "DefaultTimeToRespond_Critical",
                table: "Organizations",
                type: "interval",
                nullable: true);
        }
    }
}
