using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class GenericAuditEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActorMemberId",
                table: "AuditEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StaffPerformed",
                table: "AuditEvents",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaffReason",
                table: "AuditEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "AuditEvents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorMemberId",
                table: "AuditEvents",
                column: "ActorMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Type",
                table: "AuditEvents",
                column: "Type");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditEvents_Members_ActorMemberId",
                table: "AuditEvents",
                column: "ActorMemberId",
                principalTable: "Members",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditEvents_Members_ActorMemberId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_ActorMemberId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_Type",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorMemberId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "StaffPerformed",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "StaffReason",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "AuditEvents");
        }
    }
}
