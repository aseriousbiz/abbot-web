using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Serious.Abbot.Entities;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddCustomerTags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Properties = table.Column<CustomerTagProperties>(type: "jsonb", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerTags_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerTags_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerTags_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerTagAssignment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    TagId = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerTagAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerTagAssignment_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerTagAssignment_CustomerTags_TagId",
                        column: x => x.TagId,
                        principalTable: "CustomerTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerTagAssignment_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerTagAssignment_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTagAssignment_CreatorId",
                table: "CustomerTagAssignment",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTagAssignment_CustomerId",
                table: "CustomerTagAssignment",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTagAssignment_ModifiedById",
                table: "CustomerTagAssignment",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTagAssignment_TagId",
                table: "CustomerTagAssignment",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTags_CreatorId",
                table: "CustomerTags",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTags_ModifiedById",
                table: "CustomerTags",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTags_OrganizationId",
                table: "CustomerTags",
                column: "OrganizationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerTagAssignment");

            migrationBuilder.DropTable(
                name: "CustomerTags");
        }
    }
}
