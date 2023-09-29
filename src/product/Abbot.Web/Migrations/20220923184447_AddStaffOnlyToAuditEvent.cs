using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddStaffOnlyToAuditEvent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "StaffOnly",
                table: "AuditEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StaffOnly",
                table: "AuditEvents");
        }
    }
}
