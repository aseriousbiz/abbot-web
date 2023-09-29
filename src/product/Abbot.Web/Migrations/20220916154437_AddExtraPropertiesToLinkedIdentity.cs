using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddExtraPropertiesToLinkedIdentity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalMetadata",
                table: "LinkedIdentities",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalName",
                table: "LinkedIdentities",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalMetadata",
                table: "LinkedIdentities");

            migrationBuilder.DropColumn(
                name: "ExternalName",
                table: "LinkedIdentities");
        }
    }
}
