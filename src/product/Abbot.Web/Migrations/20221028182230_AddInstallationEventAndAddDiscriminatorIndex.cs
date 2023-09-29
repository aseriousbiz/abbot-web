using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddInstallationEventAndAddDiscriminatorIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Discriminator",
                table: "AuditEvents",
                column: "Discriminator");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_Discriminator",
                table: "AuditEvents");
        }
    }
}
