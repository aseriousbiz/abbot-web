using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddSettingAuditEvent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "AuditEvents" SET
                    "Discriminator" = 'SettingAuditEvent',
                    "Reason" = NULL
                WHERE "Discriminator" IN ('AuditEvent','StaffAuditEvent')
                AND "Description" LIKE '% setting `%'
                """);

            // Now unused
            migrationBuilder.DropColumn(
                name: "Enterprise",
                table: "Organizations");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Enterprise",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
