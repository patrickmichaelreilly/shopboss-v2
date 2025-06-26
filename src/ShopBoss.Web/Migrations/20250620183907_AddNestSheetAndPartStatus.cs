using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddNestSheetAndPartStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NestSheetId",
                table: "Parts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Parts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusUpdatedDate",
                table: "Parts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NestSheets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkOrderId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Material = table.Column<string>(type: "TEXT", nullable: false),
                    Length = table.Column<decimal>(type: "TEXT", nullable: true),
                    Width = table.Column<decimal>(type: "TEXT", nullable: true),
                    Thickness = table.Column<decimal>(type: "TEXT", nullable: true),
                    Barcode = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsProcessed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NestSheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NestSheets_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parts_NestSheetId",
                table: "Parts",
                column: "NestSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NestSheets_Barcode",
                table: "NestSheets",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NestSheets_WorkOrderId",
                table: "NestSheets",
                column: "WorkOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Parts_NestSheets_NestSheetId",
                table: "Parts",
                column: "NestSheetId",
                principalTable: "NestSheets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Parts_NestSheets_NestSheetId",
                table: "Parts");

            migrationBuilder.DropTable(
                name: "NestSheets");

            migrationBuilder.DropIndex(
                name: "IX_Parts_NestSheetId",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "NestSheetId",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "StatusUpdatedDate",
                table: "Parts");
        }
    }
}
