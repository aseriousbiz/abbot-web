using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class RemoveLegacyEndpointColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DotNetEndpoint",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "InkEndpoint",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "JavaScriptEndpoint",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PythonEndpoint",
                table: "Organizations");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DotNetEndpoint",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InkEndpoint",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JavaScriptEndpoint",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PythonEndpoint",
                table: "Organizations",
                type: "text",
                nullable: true);
        }
    }
}
