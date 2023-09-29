using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddPlaybookEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Playbooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "citext", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Properties = table.Column<PlaybookProperties>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playbooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Playbooks_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaybookVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlaybookId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    Definition = table.Column<PlaybookDefinition>(type: "jsonb", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Properties = table.Column<PlaybookVersionProperties>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybookVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybookVersions_Members_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaybookVersions_Playbooks_PlaybookId",
                        column: x => x.PlaybookId,
                        principalTable: "Playbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Playbooks_OrganizationId_Slug",
                table: "Playbooks",
                columns: new[] { "OrganizationId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookVersions_CreatorId",
                table: "PlaybookVersions",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookVersions_PlaybookId_Version",
                table: "PlaybookVersions",
                columns: new[] { "PlaybookId", "Version" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaybookVersions");

            migrationBuilder.DropTable(
                name: "Playbooks");
        }
    }
}
