using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class TrackedPlaybookVersionPlusPublishedAt : Migration
    {
        static void PurgePlaybookVersions(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                DELETE FROM "PlaybookRuns";
                DELETE FROM "PlaybookVersions";
                """);

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            PurgePlaybookVersions(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybookVersions_Members_CreatorId",
                table: "PlaybookVersions");

            migrationBuilder.DropIndex(
                name: "IX_PlaybookVersions_CreatorId",
                table: "PlaybookVersions");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "PlaybookVersions");

            migrationBuilder.DropColumn(
                name: "Published",
                table: "PlaybookVersions");

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "PlaybookVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatorId",
                table: "PlaybookVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "Modified",
                table: "PlaybookVersions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedById",
                table: "PlaybookVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookVersions_CreatorId",
                table: "PlaybookVersions",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookVersions_ModifiedById",
                table: "PlaybookVersions",
                column: "ModifiedById");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybookVersions_Users_CreatorId",
                table: "PlaybookVersions",
                column: "CreatorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybookVersions_Users_ModifiedById",
                table: "PlaybookVersions",
                column: "ModifiedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            PurgePlaybookVersions(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybookVersions_Users_CreatorId",
                table: "PlaybookVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybookVersions_Users_ModifiedById",
                table: "PlaybookVersions");

            migrationBuilder.DropIndex(
                name: "IX_PlaybookVersions_CreatorId",
                table: "PlaybookVersions");

            migrationBuilder.DropIndex(
                name: "IX_PlaybookVersions_ModifiedById",
                table: "PlaybookVersions");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "PlaybookVersions");

            migrationBuilder.DropColumn(
                name: "Modified",
                table: "PlaybookVersions");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "PlaybookVersions");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "PlaybookVersions");

            migrationBuilder.AddColumn<bool>(
                name: "Published",
                table: "PlaybookVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CreatorId",
                table: "PlaybookVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookVersions_CreatorId",
                table: "PlaybookVersions",
                column: "CreatorId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybookVersions_Members_CreatorId",
                table: "PlaybookVersions",
                column: "CreatorId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
