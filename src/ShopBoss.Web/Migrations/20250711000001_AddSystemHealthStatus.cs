using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemHealthStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemHealthStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OverallStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    DatabaseStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    MemoryStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    DiskSpaceStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseTimeStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    LastHealthCheck = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActiveWorkOrderCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPartsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DatabaseConnectionTimeMs = table.Column<double>(type: "REAL", nullable: false),
                    MemoryUsagePercentage = table.Column<double>(type: "REAL", nullable: false),
                    AvailableDiskSpaceGB = table.Column<double>(type: "REAL", nullable: false),
                    TotalDiskSpaceGB = table.Column<double>(type: "REAL", nullable: false),
                    AverageResponseTimeMs = table.Column<double>(type: "REAL", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemHealthStatus", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemHealthStatus_LastHealthCheck",
                table: "SystemHealthStatus",
                column: "LastHealthCheck");

            migrationBuilder.CreateIndex(
                name: "IX_SystemHealthStatus_OverallStatus",
                table: "SystemHealthStatus",
                column: "OverallStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemHealthStatus");
        }
    }
}