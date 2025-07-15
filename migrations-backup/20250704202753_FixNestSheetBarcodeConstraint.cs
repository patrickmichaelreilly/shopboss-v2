using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class FixNestSheetBarcodeConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NestSheets_Barcode",
                table: "NestSheets");

            migrationBuilder.DropIndex(
                name: "IX_NestSheets_WorkOrderId",
                table: "NestSheets");

            migrationBuilder.CreateIndex(
                name: "IX_NestSheets_WorkOrderId_Barcode",
                table: "NestSheets",
                columns: new[] { "WorkOrderId", "Barcode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NestSheets_WorkOrderId_Barcode",
                table: "NestSheets");

            migrationBuilder.CreateIndex(
                name: "IX_NestSheets_Barcode",
                table: "NestSheets",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NestSheets_WorkOrderId",
                table: "NestSheets",
                column: "WorkOrderId");
        }
    }
}
