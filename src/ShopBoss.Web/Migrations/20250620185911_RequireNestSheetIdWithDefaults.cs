using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class RequireNestSheetIdWithDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, create default nest sheets for each work order that has parts without nest sheets
            migrationBuilder.Sql(@"
                INSERT INTO NestSheets (Id, WorkOrderId, Name, Material, Barcode, CreatedDate, IsProcessed)
                SELECT 
                    'default-' || wo.Id,
                    wo.Id,
                    'Default Nest Sheet',
                    'Mixed Materials',
                    'DEFAULT-' || wo.Id,
                    datetime('now'),
                    0
                FROM WorkOrders wo
                WHERE EXISTS (
                    SELECT 1 FROM Parts p 
                    WHERE (p.ProductId IN (SELECT pr.Id FROM Products pr WHERE pr.WorkOrderId = wo.Id) OR
                           p.SubassemblyId IN (SELECT s.Id FROM Subassemblies s 
                                               JOIN Products pr ON s.ProductId = pr.Id 
                                               WHERE pr.WorkOrderId = wo.Id))
                    AND p.NestSheetId IS NULL
                )
            ");

            // Update all parts without nest sheets to use the default nest sheet for their work order
            migrationBuilder.Sql(@"
                UPDATE Parts 
                SET NestSheetId = 'default-' || (
                    CASE 
                        WHEN ProductId IS NOT NULL THEN (
                            SELECT pr.WorkOrderId FROM Products pr WHERE pr.Id = Parts.ProductId
                        )
                        WHEN SubassemblyId IS NOT NULL THEN (
                            SELECT pr.WorkOrderId 
                            FROM Products pr 
                            JOIN Subassemblies s ON pr.Id = s.ProductId 
                            WHERE s.Id = Parts.SubassemblyId
                        )
                    END
                )
                WHERE NestSheetId IS NULL OR NestSheetId = ''
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_Parts_NestSheets_NestSheetId",
                table: "Parts");

            migrationBuilder.AlterColumn<string>(
                name: "NestSheetId",
                table: "Parts",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Parts_NestSheets_NestSheetId",
                table: "Parts",
                column: "NestSheetId",
                principalTable: "NestSheets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Parts_NestSheets_NestSheetId",
                table: "Parts");

            migrationBuilder.AlterColumn<string>(
                name: "NestSheetId",
                table: "Parts",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddForeignKey(
                name: "FK_Parts_NestSheets_NestSheetId",
                table: "Parts",
                column: "NestSheetId",
                principalTable: "NestSheets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
