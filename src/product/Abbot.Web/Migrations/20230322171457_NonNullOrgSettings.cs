using Microsoft.EntityFrameworkCore.Migrations;
using Serious.Abbot.Entities;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class NonNullOrgSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Verified locally that this works, even when there are 'null' values in the existing column.
            // All currently-null values become '{}' (empty JSON object).
            migrationBuilder.AlterColumn<OrganizationSettings>(
                name: "Settings",
                table: "Organizations",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}",
                oldClrType: typeof(OrganizationSettings),
                oldType: "jsonb",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<OrganizationSettings>(
                name: "Settings",
                table: "Organizations",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(OrganizationSettings),
                oldType: "jsonb");
        }
    }
}
