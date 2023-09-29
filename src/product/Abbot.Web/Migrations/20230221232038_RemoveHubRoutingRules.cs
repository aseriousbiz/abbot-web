using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class RemoveHubRoutingRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Hubs_RoomId",
                table: "Hubs");

            migrationBuilder.AddColumn<int>(
                name: "HubId",
                table: "Rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_HubId",
                table: "Rooms",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_Hubs_RoomId",
                table: "Hubs",
                column: "RoomId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_Hubs_HubId",
                table: "Rooms",
                column: "HubId",
                principalTable: "Hubs",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_Hubs_HubId",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_HubId",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Hubs_RoomId",
                table: "Hubs");

            migrationBuilder.DropColumn(
                name: "HubId",
                table: "Rooms");

            migrationBuilder.CreateIndex(
                name: "IX_Hubs_RoomId",
                table: "Hubs",
                column: "RoomId");
        }
    }
}
