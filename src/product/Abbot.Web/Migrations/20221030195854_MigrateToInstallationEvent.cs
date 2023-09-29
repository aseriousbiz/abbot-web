using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class MigrateToInstallationEvent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "AuditEvents" SET "Discriminator" = 'InstallationEvent'
                WHERE "Discriminator" = 'AdminAuditEvent'
                AND (
                    "Description" LIKE 'Installed Abbot to %'
                    OR
                    "Description" LIKE 'Uninstalled Abbot from %'
                )
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "AuditEvents" SET "Discriminator" = 'AdminAuditEvent'
                WHERE "Discriminator" = 'InstallationEvent'
                AND "Properties" IS NULL -- Legacy event
                """);
        }
    }
}
