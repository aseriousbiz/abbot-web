using Microsoft.EntityFrameworkCore.Migrations;
using Serious.Abbot.Entities;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class ChangePropertiesToString : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration failed in prod. So we're nulling it out, and ConvertPropertiesBackToJson fixes existing environments.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This migration failed in prod. So we're nulling it out, and ConvertPropertiesBackToJson fixes existing environments.
        }
    }
}
