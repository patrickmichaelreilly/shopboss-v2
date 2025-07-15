using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class StatusStandardization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Status column to Products table
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Add Status column to DetachedProducts table
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "DetachedProducts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Data migration: Set Status = Shipped (4) where IsShipped = true, otherwise Pending (0)
            migrationBuilder.Sql(@"
                UPDATE DetachedProducts 
                SET Status = 4 
                WHERE IsShipped = 1");

            // Hardware already has Status field, ensure consistency with IsShipped
            migrationBuilder.Sql(@"
                UPDATE Hardware 
                SET Status = 4 
                WHERE IsShipped = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove Status column from Products table
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Products");

            // Remove Status column from DetachedProducts table
            migrationBuilder.DropColumn(
                name: "Status",
                table: "DetachedProducts");
        }
    }
}