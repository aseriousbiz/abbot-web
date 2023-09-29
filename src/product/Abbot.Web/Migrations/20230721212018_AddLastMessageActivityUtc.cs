using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddLastMessageActivityUtc : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "Rooms");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMessageActivityUtc",
                table: "Rooms",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMessageActivityUtc",
                table: "Rooms");

            migrationBuilder.AddColumn<string>(
                name: "ConversationId",
                table: "Rooms",
                type: "text",
                nullable: true);
        }
    }
}
