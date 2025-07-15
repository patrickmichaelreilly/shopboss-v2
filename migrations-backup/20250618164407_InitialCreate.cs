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
            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                });

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
                    EdgebandingRight = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetachedProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetachedProducts_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hardware",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkOrderId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Qty = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hardware", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hardware_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    Width = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    table.ForeignKey(
                        name: "FK_Subassemblies_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Subassemblies_Subassemblies_ParentSubassemblyId",
                        column: x => x.ParentSubassemblyId,
                        principalTable: "Subassemblies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Parts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: true),
                    SubassemblyId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Qty = table.Column<int>(type: "INTEGER", nullable: false),
                    Length = table.Column<decimal>(type: "TEXT", nullable: true),
                    Width = table.Column<decimal>(type: "TEXT", nullable: true),
                    Thickness = table.Column<decimal>(type: "TEXT", nullable: true),
                    Material = table.Column<string>(type: "TEXT", nullable: false),
                    EdgebandingTop = table.Column<string>(type: "TEXT", nullable: false),
                    EdgebandingBottom = table.Column<string>(type: "TEXT", nullable: false),
                    EdgebandingLeft = table.Column<string>(type: "TEXT", nullable: false),
                    EdgebandingRight = table.Column<string>(type: "TEXT", nullable: false)
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
                        name: "FK_Parts_Subassemblies_SubassemblyId",
                        column: x => x.SubassemblyId,
                        principalTable: "Subassemblies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetachedProducts_WorkOrderId",
                table: "DetachedProducts",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Hardware_WorkOrderId",
                table: "Hardware",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_ProductId",
                table: "Parts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_SubassemblyId",
                table: "Parts",
                column: "SubassemblyId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_WorkOrderId",
                table: "Products",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Subassemblies_ParentSubassemblyId",
                table: "Subassemblies",
                column: "ParentSubassemblyId");

            migrationBuilder.CreateIndex(
                name: "IX_Subassemblies_ProductId",
                table: "Subassemblies",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetachedProducts");

            migrationBuilder.DropTable(
                name: "Hardware");

            migrationBuilder.DropTable(
                name: "Parts");

            migrationBuilder.DropTable(
                name: "Subassemblies");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "WorkOrders");
        }
    }
}
