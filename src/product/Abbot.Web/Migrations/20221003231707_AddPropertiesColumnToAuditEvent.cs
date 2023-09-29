using Microsoft.EntityFrameworkCore.Migrations;
using Serious.Abbot.Entities;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddPropertiesColumnToAuditEvent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<RespondersInfo>(
                name: "Properties",
                table: "AuditEvents",
                type: "jsonb",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Properties",
                table: "AuditEvents");
        }
    }
}
