using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddHardwareProductRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductId",
                table: "Hardware",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hardware_ProductId",
                table: "Hardware",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Hardware_Products_ProductId",
                table: "Hardware",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hardware_Products_ProductId",
                table: "Hardware");

            migrationBuilder.DropIndex(
                name: "IX_Hardware_ProductId",
                table: "Hardware");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "Hardware");
        }
    }
}
