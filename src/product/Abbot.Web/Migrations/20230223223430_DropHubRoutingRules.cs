using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class DropHubRoutingRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HubRoutingRules");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HubRoutingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HubId = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    RoomId = table.Column<int>(type: "integer", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Emoji = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HubRoutingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HubRoutingRules_Hubs_HubId",
                        column: x => x.HubId,
                        principalTable: "Hubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HubRoutingRules_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HubRoutingRules_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_HubRoutingRules_HubId",
                table: "HubRoutingRules",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_HubRoutingRules_OrganizationId",
                table: "HubRoutingRules",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_HubRoutingRules_RoomId",
                table: "HubRoutingRules",
                column: "RoomId");
        }
    }
}
