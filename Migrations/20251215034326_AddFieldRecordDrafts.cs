using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkOrderSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldRecordDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FieldRecordDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EngineerNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldRecordDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldRecordDrafts_AspNetUsers_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldRecordDraftPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FieldRecordDraftId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldRecordDraftPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldRecordDraftPhotos_FieldRecordDrafts_FieldRecordDraftId",
                        column: x => x.FieldRecordDraftId,
                        principalTable: "FieldRecordDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldRecordDraftPhotos_FieldRecordDraftId",
                table: "FieldRecordDraftPhotos",
                column: "FieldRecordDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldRecordDrafts_CreatorId",
                table: "FieldRecordDrafts",
                column: "CreatorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldRecordDraftPhotos");

            migrationBuilder.DropTable(
                name: "FieldRecordDrafts");
        }
    }
}
