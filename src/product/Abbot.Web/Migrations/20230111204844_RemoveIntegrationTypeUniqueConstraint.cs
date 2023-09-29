using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class RemoveIntegrationTypeUniqueConstraint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Integrations_OrganizationId_Type",
                table: "Integrations");

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_OrganizationId_Type",
                table: "Integrations",
                columns: new[] { "OrganizationId", "Type" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Integrations_OrganizationId_Type",
                table: "Integrations");

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_OrganizationId_Type",
                table: "Integrations",
                columns: new[] { "OrganizationId", "Type" },
                unique: true);
        }
    }
}
