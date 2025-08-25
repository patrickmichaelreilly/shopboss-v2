using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopBoss.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create TaskChunks table
            migrationBuilder.CreateTable(
                name: "TaskChunks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsTemplate = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskChunks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Add TaskChunk foreign key columns to ProjectEvents
            migrationBuilder.AddColumn<string>(
                name: "TaskChunkId",
                table: "ProjectEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChunkDisplayOrder",
                table: "ProjectEvents",
                type: "INTEGER",
                nullable: true);

            // Create indexes for TaskChunks
            migrationBuilder.CreateIndex(
                name: "IX_TaskChunks_ProjectId",
                table: "TaskChunks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskChunks_DisplayOrder",
                table: "TaskChunks",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_TaskChunks_ProjectId_DisplayOrder",
                table: "TaskChunks",
                columns: new[] { "ProjectId", "DisplayOrder" });

            // Create index for ProjectEvents TaskChunkId
            migrationBuilder.CreateIndex(
                name: "IX_ProjectEvents_TaskChunkId",
                table: "ProjectEvents",
                column: "TaskChunkId");

            // Create foreign key constraint for ProjectEvents -> TaskChunks
            migrationBuilder.CreateIndex(
                name: "FK_ProjectEvents_TaskChunks_TaskChunkId",
                table: "ProjectEvents",
                column: "TaskChunkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign key constraint and indexes for ProjectEvents
            migrationBuilder.DropIndex(
                name: "FK_ProjectEvents_TaskChunks_TaskChunkId",
                table: "ProjectEvents");

            migrationBuilder.DropIndex(
                name: "IX_ProjectEvents_TaskChunkId",
                table: "ProjectEvents");

            // Drop TaskChunk columns from ProjectEvents
            migrationBuilder.DropColumn(
                name: "ChunkDisplayOrder",
                table: "ProjectEvents");

            migrationBuilder.DropColumn(
                name: "TaskChunkId",
                table: "ProjectEvents");

            // Drop TaskChunks table (this will also drop its indexes and constraints)
            migrationBuilder.DropTable(
                name: "TaskChunks");
        }
    }
}