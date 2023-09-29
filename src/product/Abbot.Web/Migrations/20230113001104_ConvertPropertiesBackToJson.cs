using Microsoft.EntityFrameworkCore.Migrations;
using Serious.Abbot.Entities;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class ConvertPropertiesBackToJson : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert to JSON, but make sure we're _parsing_ the text as a JSON object, then setting that JSON object as the value.
            // This prevents us from converting `{ "foo": "bar" }` (A JSON object) into `"{ \"foo\": \"bar\" }"` (A JSON string containing a JSON object)
            migrationBuilder.Sql("""ALTER TABLE "AuditEvents" ALTER COLUMN "Properties" TYPE jsonb USING "Properties"::JSONB;""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Manually edited. Don't do anything when moving this migration down.
            // This only exists to correct environments that had ChangePropertiesToString applied.
        }
    }
}
