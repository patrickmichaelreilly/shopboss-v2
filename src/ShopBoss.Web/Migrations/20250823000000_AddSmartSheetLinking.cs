using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartSheetLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SmartSheetId",
                table: "Projects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SmartSheetLastSync",
                table: "Projects",
                type: "TEXT",
                nullable: true);
                
            migrationBuilder.CreateIndex(
                name: "IX_Projects_SmartSheetId",
                table: "Projects",
                column: "SmartSheetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_SmartSheetId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SmartSheetLastSync",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SmartSheetId",
                table: "Projects");
        }
    }
}