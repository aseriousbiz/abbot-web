using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class DropPlaybookTriggers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaybookTriggers");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaybookTriggers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlaybookId = table.Column<int>(type: "integer", nullable: false),
                    ApiToken = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Properties = table.Column<string>(type: "jsonb", nullable: false),
                    StepId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybookTriggers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybookTriggers_Playbooks_PlaybookId",
                        column: x => x.PlaybookId,
                        principalTable: "Playbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookTriggers_ApiToken",
                table: "PlaybookTriggers",
                column: "ApiToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookTriggers_PlaybookId_StepId",
                table: "PlaybookTriggers",
                columns: new[] { "PlaybookId", "StepId" },
                unique: true);
        }
    }
}
