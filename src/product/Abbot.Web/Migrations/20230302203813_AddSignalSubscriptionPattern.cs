using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddSignalSubscriptionPattern : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArgumentsPattern",
                table: "SignalSubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArgumentsPatternType",
                table: "SignalSubscriptions",
                type: "text",
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<bool>(
                name: "CaseSensitive",
                table: "SignalSubscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArgumentsPattern",
                table: "SignalSubscriptions");

            migrationBuilder.DropColumn(
                name: "ArgumentsPatternType",
                table: "SignalSubscriptions");

            migrationBuilder.DropColumn(
                name: "CaseSensitive",
                table: "SignalSubscriptions");
        }
    }
}
