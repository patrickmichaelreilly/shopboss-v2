using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingStatusToHardwareAndDetachedProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsShipped",
                table: "Hardware");

            migrationBuilder.DropColumn(
                name: "ShippedDate",
                table: "Hardware");

            migrationBuilder.DropColumn(
                name: "IsShipped",
                table: "DetachedProducts");

            migrationBuilder.DropColumn(
                name: "ShippedDate",
                table: "DetachedProducts");
        }
    }
}
