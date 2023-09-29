using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddEnterpriseGridId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnterpriseGridId",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Organizations" SET "EnterpriseGridId" = "PlatformId"
                WHERE "PlatformId" LIKE 'E%' AND "PlatformType" = 1
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnterpriseGridId",
                table: "Organizations");
        }
    }
}
