using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class UniqueCustomerName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_OrganizationId",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_OrganizationId_Name",
                table: "Customers",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_OrganizationId_Name",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_OrganizationId",
                table: "Customers",
                column: "OrganizationId");
        }
    }
}
