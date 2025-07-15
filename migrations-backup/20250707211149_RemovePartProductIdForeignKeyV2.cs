using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemovePartProductIdForeignKeyV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite doesn't support dropping foreign key constraints directly
            // We need to recreate the table without the ProductId foreign key constraint
            
            // Create new Parts table without foreign key constraint
            migrationBuilder.CreateTable(
                name: "Parts_new",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Qty = table.Column<int>(type: "INTEGER", nullable: false),
                    Length = table.Column<decimal>(type: "TEXT", nullable: true),
                    Width = table.Column<decimal>(type: "TEXT", nullable: true),
                    Thickness = table.Column<decimal>(type: "TEXT", nullable: true),
                    Material = table.Column<string>(type: "TEXT", nullable: true),
                    EdgebandingTop = table.Column<string>(type: "TEXT", nullable: true),
                    EdgebandingBottom = table.Column<string>(type: "TEXT", nullable: true),
                    EdgebandingLeft = table.Column<string>(type: "TEXT", nullable: true),
                    EdgebandingRight = table.Column<string>(type: "TEXT", nullable: true),
                    ProductId = table.Column<string>(type: "TEXT", nullable: true), // No FK constraint
                    SubassemblyId = table.Column<string>(type: "TEXT", nullable: true),
                    NestSheetId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusUpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parts_NestSheets_NestSheetId",
                        column: x => x.NestSheetId,
                        principalTable: "NestSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Parts_Subassemblies_SubassemblyId",
                        column: x => x.SubassemblyId,
                        principalTable: "Subassemblies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Copy data from old table to new table
            migrationBuilder.Sql(@"
                INSERT INTO Parts_new (Id, Name, Qty, Length, Width, Thickness, Material, 
                                     EdgebandingTop, EdgebandingBottom, EdgebandingLeft, EdgebandingRight,
                                     ProductId, SubassemblyId, NestSheetId, Status, StatusUpdatedDate, Location)
                SELECT Id, Name, Qty, Length, Width, Thickness, Material, 
                       EdgebandingTop, EdgebandingBottom, EdgebandingLeft, EdgebandingRight,
                       ProductId, SubassemblyId, NestSheetId, Status, StatusUpdatedDate, Location
                FROM Parts;
            ");

            // Drop old table
            migrationBuilder.DropTable(name: "Parts");

            // Rename new table
            migrationBuilder.RenameTable(name: "Parts_new", newName: "Parts");

            // Recreate indexes
            migrationBuilder.CreateIndex(
                name: "IX_Parts_NestSheetId",
                table: "Parts",
                column: "NestSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_SubassemblyId",
                table: "Parts",
                column: "SubassemblyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate the Parts table with the ProductId foreign key constraint
            migrationBuilder.CreateTable(
                name: "Parts_old",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Qty = table.Column<int>(type: "INTEGER", nullable: false),
                    Length = table.Column<decimal>(type: "TEXT", nullable: true),
                    Width = table.Column<decimal>(type: "TEXT", nullable: true),
                    Thickness = table.Column<decimal>(type: "TEXT", nullable: true),
                    Material = table.Column<string>(type: "TEXT", nullable: true),
                    EdgebandingTop = table.Column<string>(type: "TEXT", nullable: true),
                    EdgebandingBottom = table.Column<string>(type: "TEXT", nullable: true),
                    EdgebandingLeft = table.Column<string>(type: "TEXT", nullable: true),
                    EdgebandingRight = table.Column<string>(type: "TEXT", nullable: true),
                    ProductId = table.Column<string>(type: "TEXT", nullable: true),
                    SubassemblyId = table.Column<string>(type: "TEXT", nullable: true),
                    NestSheetId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusUpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Parts_NestSheets_NestSheetId",
                        column: x => x.NestSheetId,
                        principalTable: "NestSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Parts_Subassemblies_SubassemblyId",
                        column: x => x.SubassemblyId,
                        principalTable: "Subassemblies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Copy data back
            migrationBuilder.Sql(@"
                INSERT INTO Parts_old (Id, Name, Qty, Length, Width, Thickness, Material, 
                                     EdgebandingTop, EdgebandingBottom, EdgebandingLeft, EdgebandingRight,
                                     ProductId, SubassemblyId, NestSheetId, Status, StatusUpdatedDate, Location)
                SELECT Id, Name, Qty, Length, Width, Thickness, Material, 
                       EdgebandingTop, EdgebandingBottom, EdgebandingLeft, EdgebandingRight,
                       ProductId, SubassemblyId, NestSheetId, Status, StatusUpdatedDate, Location
                FROM Parts
                WHERE ProductId IN (SELECT Id FROM Products);
            ");

            migrationBuilder.DropTable(name: "Parts");
            migrationBuilder.RenameTable(name: "Parts_old", newName: "Parts");
        }
    }
}
