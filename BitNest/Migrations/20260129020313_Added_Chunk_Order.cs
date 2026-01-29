using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BitNest.Migrations
{
    /// <inheritdoc />
    public partial class Added_Chunk_Order : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChunkMetadataFileMetadata");

            migrationBuilder.CreateTable(
                name: "FileChunks",
                columns: table => new
                {
                    Order = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChunkHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    FileId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileChunks", x => x.Order);
                    table.ForeignKey(
                        name: "FK_FileChunks_Chunks_ChunkHash",
                        column: x => x.ChunkHash,
                        principalTable: "Chunks",
                        principalColumn: "Hash",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileChunks_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileChunks_ChunkHash",
                table: "FileChunks",
                column: "ChunkHash");

            migrationBuilder.CreateIndex(
                name: "IX_FileChunks_FileId",
                table: "FileChunks",
                column: "FileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileChunks");

            migrationBuilder.CreateTable(
                name: "ChunkMetadataFileMetadata",
                columns: table => new
                {
                    ChunksHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    FilesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChunkMetadataFileMetadata", x => new { x.ChunksHash, x.FilesId });
                    table.ForeignKey(
                        name: "FK_ChunkMetadataFileMetadata_Chunks_ChunksHash",
                        column: x => x.ChunksHash,
                        principalTable: "Chunks",
                        principalColumn: "Hash",
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
    }
}
