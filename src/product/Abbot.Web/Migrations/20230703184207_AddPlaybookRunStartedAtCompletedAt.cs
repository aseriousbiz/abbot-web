using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddPlaybookRunStartedAtCompletedAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "PlaybookRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "PlaybookRuns",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "PlaybookRuns");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "PlaybookRuns");
        }
    }
}
