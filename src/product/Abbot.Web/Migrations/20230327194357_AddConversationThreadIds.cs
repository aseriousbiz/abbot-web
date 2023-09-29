using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddConversationThreadIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "ThreadIds",
                table: "Conversations",
                type: "text[]",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "ThreadId",
                table: "ConversationEvents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ThreadIds",
                table: "Conversations",
                column: "ThreadIds")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conversations_ThreadIds",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ThreadIds",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ThreadId",
                table: "ConversationEvents");
        }
    }
}
