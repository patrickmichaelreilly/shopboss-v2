using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =================================
            // PHASE 1: CREATE ALL TABLES (PRIMARY KEYS ONLY)
            // =================================
            
            // StorageRacks - CREATE FIRST
            migrationBuilder.CreateTable(
                name: "StorageRacks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Rows = table.Column<int>(type: "INTEGER", nullable: false),
                    Columns = table.Column<int>(type: "INTEGER", nullable: false),
                    Length = table.Column<decimal>(type: "TEXT", nullable: true),
                    Width = table.Column<decimal>(type: "TEXT", nullable: true),
                    Height = table.Column<decimal>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPortable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageRacks", x => x.Id);
                });

            // WorkOrders
            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ArchivedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                });

            // Products
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ProductNumber = table.Column<string>(type: "TEXT", nullable: false),
                    WorkOrderId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Qty = table.Column<int>(type: "INTEGER", nullable: false),
                    Length = table.Column<decimal>(type: "TEXT", nullable: true),
                    Width = table.Column<decimal>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    StatusUpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            // DetachedProducts
            migrationBuilder.CreateTable(
                name: "DetachedProducts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ProductNumber = table.Column<string>(type: "TEXT", nullable: false),
                    WorkOrderId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Qty = table.Column<int>(type: "INTEGER", nullable: false),
                    Length = table.Column<decimal>(type: "TEXT", nullable: true),
                    Width = table.Column<decimal>(type: "TEXT", nullable: true),
                    Thickness = table.Column<decimal>(type: "TEXT", nullable: true),
                    Material = table.Column<string>(type: "TEXT", nullable: false),
                    EdgebandingTop = table.Column<string>(type: "TEXT", nullable: false),
                    EdgebandingBottom = table.Column<string>(type: "TEXT", nullable: false),
                    EdgebandingLeft = table.Column<string>(type: "TEXT", nullable: false),
                    EdgebandingRight = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    StatusUpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetachedProducts", x => x.Id);
                });

            // Hardware
            migrationBuilder.CreateTable(
                name: "Hardware",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkOrderId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Qty = table.Column<int>(type: "INTEGER", nullable: false),
                    MicrovellumId = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    StatusUpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hardware", x => x.Id);
                });

            // NestSheets
            migrationBuilder.CreateTable(
                name: "NestSheets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkOrderId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Material = table.Column<string>(type: "TEXT", nullable: false),
                    Length = table.Column<decimal>(type: "TEXT", nullable: true),
                    Width = table.Column<decimal>(type: "TEXT", nullable: true),
                    Thickness = table.Column<decimal>(type: "TEXT", nullable: true),
                    Barcode = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    StatusUpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NestSheets", x => x.Id);
                });

            // Subassemblies
            migrationBuilder.CreateTable(
                name: "Subassemblies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: true),
                    ParentSubassemblyId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Qty = table.Column<int>(type: "INTEGER", nullable: false),
                    Length = table.Column<decimal>(type: "TEXT", nullable: true),
                    Width = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subassemblies", x => x.Id);
                });

            // Parts
            migrationBuilder.CreateTable(
                name: "Parts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: true),
                    SubassemblyId = table.Column<string>(type: "TEXT", nullable: true),
                    NestSheetId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Qty = table.Column<int>(type: "INTEGER", nullable: false),
                    Length = table.Column<decimal>(type: "TEXT", nullable: true),
                    Width = table.Column<decimal>(type: "TEXT", nullable: true),
                    Thickness = table.Column<decimal>(type: "TEXT", nullable: true),
                    Material = table.Column<string>(type: "TEXT", nullable: false),
                    EdgebandingTop = table.Column<string>(type: "TEXT", nullable: false),
                    EdgebandingBottom = table.Column<string>(type: "TEXT", nullable: false),
                    EdgebandingLeft = table.Column<string>(type: "TEXT", nullable: false),
                    EdgebandingRight = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    StatusUpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.Id);
                });


            // Bins
            migrationBuilder.CreateTable(
                name: "Bins",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    StorageRackId = table.Column<string>(type: "TEXT", nullable: false),
                    Row = table.Column<int>(type: "INTEGER", nullable: false),
                    Column = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<string>(type: "TEXT", nullable: true),
                    ProductId = table.Column<string>(type: "TEXT", nullable: true),
                    WorkOrderId = table.Column<string>(type: "TEXT", nullable: true),
                    Contents = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PartsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bins", x => x.Id);
                });

            // AuditLogs
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

            // ScanHistory
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
                });

            // BackupConfigurations
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

            // BackupStatuses
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

            // SystemHealthStatus
            migrationBuilder.CreateTable(
                name: "SystemHealthStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OverallStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    DatabaseStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    DiskSpaceStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    MemoryStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseTimeStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    AvailableDiskSpaceGB = table.Column<double>(type: "REAL", nullable: false),
                    TotalDiskSpaceGB = table.Column<double>(type: "REAL", nullable: false),
                    MemoryUsagePercentage = table.Column<double>(type: "REAL", nullable: false),
                    AverageResponseTimeMs = table.Column<double>(type: "REAL", nullable: false),
                    DatabaseConnectionTimeMs = table.Column<double>(type: "REAL", nullable: false),
                    ActiveWorkOrderCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPartsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastHealthCheck = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemHealthStatus", x => x.Id);
                });

            // =================================
            // PHASE 2: ADD ALL FOREIGN KEY CONSTRAINTS
            // =================================

            // Products → WorkOrders
            migrationBuilder.AddForeignKey(
                name: "FK_Products_WorkOrders_WorkOrderId",
                table: "Products",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // DetachedProducts → WorkOrders
            migrationBuilder.AddForeignKey(
                name: "FK_DetachedProducts_WorkOrders_WorkOrderId",
                table: "DetachedProducts",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Hardware → WorkOrders
            migrationBuilder.AddForeignKey(
                name: "FK_Hardware_WorkOrders_WorkOrderId",
                table: "Hardware",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Hardware → Products
            migrationBuilder.AddForeignKey(
                name: "FK_Hardware_Products_ProductId",
                table: "Hardware",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // NestSheets → WorkOrders
            migrationBuilder.AddForeignKey(
                name: "FK_NestSheets_WorkOrders_WorkOrderId",
                table: "NestSheets",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Subassemblies → Products
            migrationBuilder.AddForeignKey(
                name: "FK_Subassemblies_Products_ProductId",
                table: "Subassemblies",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Subassemblies → Subassemblies (self-reference)
            migrationBuilder.AddForeignKey(
                name: "FK_Subassemblies_Subassemblies_ParentSubassemblyId",
                table: "Subassemblies",
                column: "ParentSubassemblyId",
                principalTable: "Subassemblies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Parts → Subassemblies
            migrationBuilder.AddForeignKey(
                name: "FK_Parts_Subassemblies_SubassemblyId",
                table: "Parts",
                column: "SubassemblyId",
                principalTable: "Subassemblies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Parts → NestSheets
            migrationBuilder.AddForeignKey(
                name: "FK_Parts_NestSheets_NestSheetId",
                table: "Parts",
                column: "NestSheetId",
                principalTable: "NestSheets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Bins → StorageRacks
            migrationBuilder.AddForeignKey(
                name: "FK_Bins_StorageRacks_StorageRackId",
                table: "Bins",
                column: "StorageRackId",
                principalTable: "StorageRacks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Bins → Parts
            migrationBuilder.AddForeignKey(
                name: "FK_Bins_Parts_PartId",
                table: "Bins",
                column: "PartId",
                principalTable: "Parts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Bins → Products
            migrationBuilder.AddForeignKey(
                name: "FK_Bins_Products_ProductId",
                table: "Bins",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Bins → WorkOrders
            migrationBuilder.AddForeignKey(
                name: "FK_Bins_WorkOrders_WorkOrderId",
                table: "Bins",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ScanHistory → NestSheets
            migrationBuilder.AddForeignKey(
                name: "FK_ScanHistory_NestSheets_NestSheetId",
                table: "ScanHistory",
                column: "NestSheetId",
                principalTable: "NestSheets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ScanHistory → WorkOrders
            migrationBuilder.AddForeignKey(
                name: "FK_ScanHistory_WorkOrders_WorkOrderId",
                table: "ScanHistory",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // =================================
            // PHASE 3: CREATE ALL INDEXES
            // =================================

            migrationBuilder.CreateIndex(
                name: "IX_Products_WorkOrderId",
                table: "Products",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DetachedProducts_WorkOrderId",
                table: "DetachedProducts",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Hardware_WorkOrderId",
                table: "Hardware",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Hardware_ProductId",
                table: "Hardware",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_NestSheets_WorkOrderId_Barcode",
                table: "NestSheets",
                columns: new[] { "WorkOrderId", "Barcode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subassemblies_ProductId",
                table: "Subassemblies",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Subassemblies_ParentSubassemblyId",
                table: "Subassemblies",
                column: "ParentSubassemblyId");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_ProductId",
                table: "Parts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_SubassemblyId",
                table: "Parts",
                column: "SubassemblyId");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_NestSheetId",
                table: "Parts",
                column: "NestSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageRacks_Name",
                table: "StorageRacks",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_StorageRacks_Type",
                table: "StorageRacks",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_StorageRacks_IsActive",
                table: "StorageRacks",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Bins_StorageRackId",
                table: "Bins",
                column: "StorageRackId");

            migrationBuilder.CreateIndex(
                name: "IX_Bins_StorageRackId_Row_Column",
                table: "Bins",
                columns: new[] { "StorageRackId", "Row", "Column" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bins_Status",
                table: "Bins",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Bins_PartId",
                table: "Bins",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_Bins_ProductId",
                table: "Bins",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Bins_WorkOrderId",
                table: "Bins",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_WorkOrderId",
                table: "AuditLogs",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_Timestamp",
                table: "ScanHistory",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_Barcode",
                table: "ScanHistory",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_Station",
                table: "ScanHistory",
                column: "Station");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_NestSheetId",
                table: "ScanHistory",
                column: "NestSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_WorkOrderId",
                table: "ScanHistory",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupStatuses_CreatedDate",
                table: "BackupStatuses",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_BackupStatuses_BackupType",
                table: "BackupStatuses",
                column: "BackupType");

            migrationBuilder.CreateIndex(
                name: "IX_BackupStatuses_IsSuccessful",
                table: "BackupStatuses",
                column: "IsSuccessful");

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
            // Drop all tables in reverse order of dependencies
            migrationBuilder.DropTable(name: "AuditLogs");
            migrationBuilder.DropTable(name: "ScanHistory");
            migrationBuilder.DropTable(name: "Bins");
            migrationBuilder.DropTable(name: "Parts");
            migrationBuilder.DropTable(name: "Hardware");
            migrationBuilder.DropTable(name: "DetachedProducts");
            migrationBuilder.DropTable(name: "Subassemblies");
            migrationBuilder.DropTable(name: "NestSheets");
            migrationBuilder.DropTable(name: "Products");
            migrationBuilder.DropTable(name: "StorageRacks");
            migrationBuilder.DropTable(name: "BackupConfigurations");
            migrationBuilder.DropTable(name: "BackupStatuses");
            migrationBuilder.DropTable(name: "SystemHealthStatus");
            migrationBuilder.DropTable(name: "WorkOrders");
        }
    }
}