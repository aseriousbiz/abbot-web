using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class MigrateCriticalToDeadline : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "TimeToRespond_Deadline",
                table: "Rooms",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "DefaultTimeToRespond_Deadline",
                table: "Organizations",
                type: "interval",
                nullable: true);

            migrationBuilder.Sql("""
UPDATE "Rooms" SET "TimeToRespond_Deadline" = "TimeToRespond_Critical"
""");
            migrationBuilder.Sql("""
UPDATE "Organizations" SET "DefaultTimeToRespond_Deadline" = "DefaultTimeToRespond_Critical"
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
UPDATE "Rooms" SET "TimeToRespond_Critical" = "TimeToRespond_Deadline"
""");
            migrationBuilder.Sql("""
UPDATE "Organizations" SET "DefaultTimeToRespond_Critical" = "DefaultTimeToRespond_Deadline"
""");

            migrationBuilder.DropColumn(
                name: "TimeToRespond_Deadline",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "DefaultTimeToRespond_Deadline",
                table: "Organizations");
        }
    }
}
