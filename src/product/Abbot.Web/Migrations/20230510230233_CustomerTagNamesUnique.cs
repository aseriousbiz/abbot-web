using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class CustomerTagNamesUnique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerTags_OrganizationId",
                table: "CustomerTags");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTags_OrganizationId_Name",
                table: "CustomerTags",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerTags_OrganizationId_Name",
                table: "CustomerTags");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTags_OrganizationId",
                table: "CustomerTags",
                column: "OrganizationId");
        }
    }
}
