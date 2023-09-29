using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class AddMetadataFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetadataFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DefaultValue = table.Column<string>(type: "text", nullable: true),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataFields_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetadataFields_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetadataFields_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomMetadataField",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MetadataFieldId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomMetadataField", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomMetadataField_MetadataFields_MetadataFieldId",
                        column: x => x.MetadataFieldId,
                        principalTable: "MetadataFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomMetadataField_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomMetadataField_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomMetadataField_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetadataFields_CreatorId",
                table: "MetadataFields",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataFields_ModifiedById",
                table: "MetadataFields",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataFields_OrganizationId_Name",
                table: "MetadataFields",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomMetadataField_CreatorId",
                table: "RoomMetadataField",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomMetadataField_MetadataFieldId",
                table: "RoomMetadataField",
                column: "MetadataFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomMetadataField_ModifiedById",
                table: "RoomMetadataField",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_RoomMetadataField_RoomId_MetadataFieldId",
                table: "RoomMetadataField",
                columns: new[] { "RoomId", "MetadataFieldId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomMetadataField");

            migrationBuilder.DropTable(
                name: "MetadataFields");
        }
    }
}
