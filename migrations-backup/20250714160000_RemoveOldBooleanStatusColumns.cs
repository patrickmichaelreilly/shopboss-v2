using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOldBooleanStatusColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add StatusUpdatedDate to entities that need it
            
            // Add StatusUpdatedDate to Products (already has Status enum)
            migrationBuilder.AddColumn<DateTime>(
                name: "StatusUpdatedDate",
                table: "Products",
                type: "TEXT",
                nullable: true);

            // Add StatusUpdatedDate to Hardware (already has Status enum)  
            migrationBuilder.AddColumn<DateTime>(
                name: "StatusUpdatedDate",
                table: "Hardware",
                type: "TEXT",
                nullable: true);

            // Add StatusUpdatedDate to DetachedProducts (already has Status enum)
            migrationBuilder.AddColumn<DateTime>(
                name: "StatusUpdatedDate",
                table: "DetachedProducts",
                type: "TEXT",
                nullable: true);

            // Status and StatusUpdatedDate already exist in NestSheets from creation

            // Remove old boolean columns that have been replaced with unified Status system
            
            // Remove IsProcessed and ProcessedDate from NestSheets
            migrationBuilder.DropColumn(
                name: "IsProcessed",
                table: "NestSheets");

            migrationBuilder.DropColumn(
                name: "ProcessedDate",
                table: "NestSheets");


            // Remove IsShipped and ShippedDate from Hardware
            migrationBuilder.DropColumn(
                name: "IsShipped",
                table: "Hardware");

            migrationBuilder.DropColumn(
                name: "ShippedDate",
                table: "Hardware");

            // Remove IsShipped and ShippedDate from DetachedProducts
            migrationBuilder.DropColumn(
                name: "IsShipped",
                table: "DetachedProducts");

            migrationBuilder.DropColumn(
                name: "ShippedDate",
                table: "DetachedProducts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add back old boolean columns for rollback
            
            // Add IsProcessed and ProcessedDate to NestSheets
            migrationBuilder.AddColumn<bool>(
                name: "IsProcessed",
                table: "NestSheets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedDate",
                table: "NestSheets",
                type: "TEXT",
                nullable: true);


            // Add IsShipped and ShippedDate to Hardware
            migrationBuilder.AddColumn<bool>(
                name: "IsShipped",
                table: "Hardware",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippedDate",
                table: "Hardware",
                type: "TEXT",
                nullable: true);

            // Add IsShipped and ShippedDate to DetachedProducts
            migrationBuilder.AddColumn<bool>(
                name: "IsShipped",
                table: "DetachedProducts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippedDate",
                table: "DetachedProducts",
                type: "TEXT",
                nullable: true);

            // Remove StatusUpdatedDate columns
            migrationBuilder.DropColumn(
                name: "StatusUpdatedDate",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StatusUpdatedDate",
                table: "Hardware");

            migrationBuilder.DropColumn(
                name: "StatusUpdatedDate",
                table: "DetachedProducts");

            // Status and StatusUpdatedDate remain in NestSheets (were created during table creation)
        }
    }
}