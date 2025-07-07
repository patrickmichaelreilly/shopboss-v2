using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemovePartProductIdForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Parts_Products_ProductId",
                table: "Parts");

            migrationBuilder.AddForeignKey(
                name: "FK_Parts_Products_ProductId",
                table: "Parts",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Parts_Products_ProductId",
                table: "Parts");

            migrationBuilder.AddForeignKey(
                name: "FK_Parts_Products_ProductId",
                table: "Parts",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
