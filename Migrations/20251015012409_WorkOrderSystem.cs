using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkOrderSystem.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkLogHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkLogId = table.Column<int>(type: "int", nullable: false),
                    ArchivedContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EditedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkLogHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkLogHistories_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkLogHistories_WorkLogs_WorkLogId",
                        column: x => x.WorkLogId,
                        principalTable: "WorkLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkLogHistories_EditedById",
                table: "WorkLogHistories",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkLogHistories_WorkLogId",
                table: "WorkLogHistories",
                column: "WorkLogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkLogHistories");
        }
    }
}
