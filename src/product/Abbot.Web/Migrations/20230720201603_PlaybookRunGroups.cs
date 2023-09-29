using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Serious.Abbot.Entities;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class PlaybookRunGroups : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "PlaybookRuns",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlaybookRunGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlaybookId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Properties = table.Column<PlaybookRunGroupProperties>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybookRunGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybookRunGroups_Playbooks_PlaybookId",
                        column: x => x.PlaybookId,
                        principalTable: "Playbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookRuns_GroupId",
                table: "PlaybookRuns",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookRunGroups_PlaybookId",
                table: "PlaybookRunGroups",
                column: "PlaybookId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybookRuns_PlaybookRunGroups_GroupId",
                table: "PlaybookRuns",
                column: "GroupId",
                principalTable: "PlaybookRunGroups",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlaybookRuns_PlaybookRunGroups_GroupId",
                table: "PlaybookRuns");

            migrationBuilder.DropTable(
                name: "PlaybookRunGroups");

            migrationBuilder.DropIndex(
                name: "IX_PlaybookRuns_GroupId",
                table: "PlaybookRuns");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "PlaybookRuns");
        }
    }
}
