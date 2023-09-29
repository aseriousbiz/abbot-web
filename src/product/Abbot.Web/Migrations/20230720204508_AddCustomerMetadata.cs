using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddCustomerMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetadataFields_OrganizationId_Name",
                table: "MetadataFields");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "MetadataFields",
                type: "text",
                nullable: false,
                defaultValue: "Room");

            migrationBuilder.CreateTable(
                name: "CustomerMetadataField",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MetadataFieldId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerMetadataField", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerMetadataField_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerMetadataField_MetadataFields_MetadataFieldId",
                        column: x => x.MetadataFieldId,
                        principalTable: "MetadataFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerMetadataField_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerMetadataField_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetadataFields_OrganizationId_Type_Name",
                table: "MetadataFields",
                columns: new[] { "OrganizationId", "Type", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMetadataField_CreatorId",
                table: "CustomerMetadataField",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMetadataField_CustomerId",
                table: "CustomerMetadataField",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMetadataField_MetadataFieldId",
                table: "CustomerMetadataField",
                column: "MetadataFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMetadataField_ModifiedById",
                table: "CustomerMetadataField",
                column: "ModifiedById");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerMetadataField");

            migrationBuilder.DropIndex(
                name: "IX_MetadataFields_OrganizationId_Type_Name",
                table: "MetadataFields");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "MetadataFields");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataFields_OrganizationId_Name",
                table: "MetadataFields",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }
    }
}
