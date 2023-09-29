using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Serious.Abbot.Entities;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddNotificationTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<MemberProperties>(
                name: "Properties",
                table: "Members",
                type: "jsonb",
                defaultValue: "{\"Notifications\":{\"OnExpiration\":true,\"DailyDigest\":false}}",
                nullable: false);

            migrationBuilder.CreateTable(
                name: "PendingMemberNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    DateSentUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingMemberNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingMemberNotifications_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingMemberNotifications_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingMemberNotifications_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingMemberNotifications_ConversationId",
                table: "PendingMemberNotifications",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingMemberNotifications_MemberId",
                table: "PendingMemberNotifications",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingMemberNotifications_OrganizationId",
                table: "PendingMemberNotifications",
                column: "OrganizationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingMemberNotifications");

            migrationBuilder.DropColumn(
                name: "Properties",
                table: "Members");
        }
    }
}
