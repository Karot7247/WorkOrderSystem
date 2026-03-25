using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkOrderSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryAndNoteToWorkOrderPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UploadedAt",
                table: "WorkOrderPhotos");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "WorkOrderPhotos",
                newName: "CustomNote");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "WorkOrderPhotos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "WorkOrderPhotos");

            migrationBuilder.RenameColumn(
                name: "CustomNote",
                table: "WorkOrderPhotos",
                newName: "Description");

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAt",
                table: "WorkOrderPhotos",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
