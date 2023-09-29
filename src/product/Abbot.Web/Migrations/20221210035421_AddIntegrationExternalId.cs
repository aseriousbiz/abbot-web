using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddIntegrationExternalId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Integrations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_Type_ExternalId",
                table: "Integrations",
                columns: new[] { "Type", "ExternalId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Integrations_Type_ExternalId",
                table: "Integrations");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Integrations");
        }
    }
}
