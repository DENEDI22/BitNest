using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitNest.Migrations
{
    /// <inheritdoc />
    public partial class ChunksAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsChunked",
                table: "Files",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Files",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUploaded",
                table: "Files",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Checksum = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chunks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChunkMetadataFileMetadata",
                columns: table => new
                {
                    ChunksId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChunkMetadataFileMetadata", x => new { x.ChunksId, x.FilesId });
                    table.ForeignKey(
                        name: "FK_ChunkMetadataFileMetadata_Chunks_ChunksId",
                        column: x => x.ChunksId,
                        principalTable: "Chunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChunkMetadataFileMetadata_Files_FilesId",
                        column: x => x.FilesId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChunkMetadataFileMetadata_FilesId",
                table: "ChunkMetadataFileMetadata",
                column: "FilesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChunkMetadataFileMetadata");

            migrationBuilder.DropTable(
                name: "Chunks");

            migrationBuilder.DropColumn(
                name: "IsChunked",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "IsUploaded",
                table: "Files");
        }
    }
}
