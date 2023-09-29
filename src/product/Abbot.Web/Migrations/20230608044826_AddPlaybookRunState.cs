using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Serious.Abbot.Entities;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddPlaybookRunState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaybookRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaybookId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Definition = table.Column<string>(type: "jsonb", nullable: false),
                    Properties = table.Column<PlaybookRunProperties>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybookRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybookRuns_Playbooks_PlaybookId",
                        column: x => x.PlaybookId,
                        principalTable: "Playbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookRuns_CorrelationId",
                table: "PlaybookRuns",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookRuns_PlaybookId",
                table: "PlaybookRuns",
                column: "PlaybookId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaybookRuns");
        }
    }
}
