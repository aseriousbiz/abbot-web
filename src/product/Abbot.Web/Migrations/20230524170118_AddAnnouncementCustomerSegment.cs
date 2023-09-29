using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddAnnouncementCustomerSegment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnnouncementCustomerSegments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnnouncementId = table.Column<int>(type: "integer", nullable: false),
                    CustomerTagId = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementCustomerSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementCustomerSegments_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnnouncementCustomerSegments_CustomerTags_CustomerTagId",
                        column: x => x.CustomerTagId,
                        principalTable: "CustomerTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementCustomerSegments_AnnouncementId_CustomerTagId",
                table: "AnnouncementCustomerSegments",
                columns: new[] { "AnnouncementId", "CustomerTagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementCustomerSegments_CustomerTagId",
                table: "AnnouncementCustomerSegments",
                column: "CustomerTagId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnouncementCustomerSegments");
        }
    }
}
