using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class RemoveProxyLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProxyLinks");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProxyLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ExternalUrl = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProxyLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProxyLinks_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProxyLinks_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProxyLinks_CreatorId",
                table: "ProxyLinks",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProxyLinks_ModifiedById",
                table: "ProxyLinks",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProxyLinks_Token",
                table: "ProxyLinks",
                column: "Token",
                unique: true);
        }
    }
}
