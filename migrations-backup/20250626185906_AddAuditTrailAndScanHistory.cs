using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditTrailAndScanHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    Station = table.Column<string>(type: "TEXT", nullable: false),
                    WorkOrderId = table.Column<string>(type: "TEXT", nullable: true),
                    Details = table.Column<string>(type: "TEXT", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", nullable: true),
                    IPAddress = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScanHistory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", nullable: false),
                    Station = table.Column<string>(type: "TEXT", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    NestSheetId = table.Column<string>(type: "TEXT", nullable: true),
                    WorkOrderId = table.Column<string>(type: "TEXT", nullable: true),
                    PartsProcessed = table.Column<int>(type: "INTEGER", nullable: true),
                    SessionId = table.Column<string>(type: "TEXT", nullable: true),
                    IPAddress = table.Column<string>(type: "TEXT", nullable: true),
                    Details = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanHistory_NestSheets_NestSheetId",
                        column: x => x.NestSheetId,
                        principalTable: "NestSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScanHistory_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_WorkOrderId",
                table: "AuditLogs",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_Barcode",
                table: "ScanHistory",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_NestSheetId",
                table: "ScanHistory",
                column: "NestSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_Station",
                table: "ScanHistory",
                column: "Station");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_Timestamp",
                table: "ScanHistory",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_WorkOrderId",
                table: "ScanHistory",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ScanHistory");
        }
    }
}
