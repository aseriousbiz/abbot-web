using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class NestedAuditEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTopLevel",
                table: "AuditEvents",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ParentIdentifier",
                table: "AuditEvents",
                column: "ParentIdentifier");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_ParentIdentifier",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "IsTopLevel",
                table: "AuditEvents");
        }
    }
}
