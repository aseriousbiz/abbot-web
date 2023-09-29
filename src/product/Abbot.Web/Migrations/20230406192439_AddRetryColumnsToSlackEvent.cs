using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddRetryColumnsToSlackEvent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcessingAttempts",
                table: "SlackEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedProcessing",
                table: "SlackEvents",
                type: "timestamp with time zone",
                nullable: true);

            // ALL tables in Postgres have a hidden column called "xmin" which Npgsql can use for concurrency control.
            // It stores the transaction ID of the last transaction that modified the row.
            // Despite the fact that this looks like it's adding that column to the table, Npgsql will IGNORE it and won't generate DDL to add it.
            // https://github.com/npgsql/efcore.pg/issues/19#issuecomment-272145093
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SlackEvents",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingAttempts",
                table: "SlackEvents");

            migrationBuilder.DropColumn(
                name: "StartedProcessing",
                table: "SlackEvents");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SlackEvents");
        }
    }
}
