using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BackupIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxBackupRetention = table.Column<int>(type: "INTEGER", nullable: false),
                    EnableCompression = table.Column<bool>(type: "INTEGER", nullable: false),
                    BackupDirectoryPath = table.Column<string>(type: "TEXT", nullable: false),
                    EnableAutomaticBackups = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BackupType = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalSize = table.Column<long>(type: "INTEGER", nullable: false),
                    BackupSize = table.Column<long>(type: "INTEGER", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupStatuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupStatuses_BackupType",
                table: "BackupStatuses",
                column: "BackupType");

            migrationBuilder.CreateIndex(
                name: "IX_BackupStatuses_CreatedDate",
                table: "BackupStatuses",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_BackupStatuses_IsSuccessful",
                table: "BackupStatuses",
                column: "IsSuccessful");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupConfigurations");

            migrationBuilder.DropTable(
                name: "BackupStatuses");
        }
    }
}
