using BitNest.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitNest.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260907000000_WholeFileStorage")]
public partial class WholeFileStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FileChunks");
        migrationBuilder.DropTable(name: "Chunks");
        migrationBuilder.DropColumn(name: "BlobPath", table: "Files");
        migrationBuilder.DropColumn(name: "IsChunked", table: "Files");
        migrationBuilder.AddColumn<string>(
            name: "ContentHash",
            table: "Files",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "");
        migrationBuilder.CreateIndex(
            name: "IX_Files_ContentHash",
            table: "Files",
            column: "ContentHash");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Files_ContentHash", table: "Files");
        migrationBuilder.DropColumn(name: "ContentHash", table: "Files");
        migrationBuilder.AddColumn<string>(
            name: "BlobPath",
            table: "Files",
            type: "text",
            nullable: false,
            defaultValue: "");
        migrationBuilder.AddColumn<bool>(
            name: "IsChunked",
            table: "Files",
            type: "boolean",
            nullable: false,
            defaultValue: false);
        migrationBuilder.CreateTable(
            name: "Chunks",
            columns: table => new
            {
                Hash = table.Column<byte[]>(type: "bytea", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Chunks", x => x.Hash));
        migrationBuilder.CreateTable(
            name: "FileChunks",
            columns: table => new
            {
                Order = table.Column<int>(type: "integer", nullable: false),
                FileId = table.Column<int>(type: "integer", nullable: false),
                ChunkHash = table.Column<byte[]>(type: "bytea", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FileChunks", x => new { x.Order, x.FileId });
                table.ForeignKey("FK_FileChunks_Chunks_ChunkHash", x => x.ChunkHash, "Chunks", "Hash",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_FileChunks_Files_FileId", x => x.FileId, "Files", "Id",
                    onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(name: "IX_FileChunks_ChunkHash", table: "FileChunks", column: "ChunkHash");
        migrationBuilder.CreateIndex(name: "IX_FileChunks_FileId", table: "FileChunks", column: "FileId");
    }
}
