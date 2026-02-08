using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitNest.Migrations
{
    /// <inheritdoc />
    public partial class ChunkStorageUpdated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChunkMetadataFileMetadata_Chunks_ChunksId",
                table: "ChunkMetadataFileMetadata");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Chunks",
                table: "Chunks");

            migrationBuilder.DropIndex(
                name: "IX_Chunks_Checksum",
                table: "Chunks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChunkMetadataFileMetadata",
                table: "ChunkMetadataFileMetadata");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "Checksum",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "ChunksId",
                table: "ChunkMetadataFileMetadata");

            migrationBuilder.AddColumn<byte[]>(
                name: "Hash",
                table: "Chunks",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "ChunksHash",
                table: "ChunkMetadataFileMetadata",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Chunks",
                table: "Chunks",
                column: "Hash");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChunkMetadataFileMetadata",
                table: "ChunkMetadataFileMetadata",
                columns: new[] { "ChunksHash", "FilesId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChunkMetadataFileMetadata_Chunks_ChunksHash",
                table: "ChunkMetadataFileMetadata",
                column: "ChunksHash",
                principalTable: "Chunks",
                principalColumn: "Hash",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChunkMetadataFileMetadata_Chunks_ChunksHash",
                table: "ChunkMetadataFileMetadata");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Chunks",
                table: "Chunks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChunkMetadataFileMetadata",
                table: "ChunkMetadataFileMetadata");

            migrationBuilder.DropColumn(
                name: "Hash",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "ChunksHash",
                table: "ChunkMetadataFileMetadata");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "Chunks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Checksum",
                table: "Chunks",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ChunksId",
                table: "ChunkMetadataFileMetadata",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_Chunks",
                table: "Chunks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChunkMetadataFileMetadata",
                table: "ChunkMetadataFileMetadata",
                columns: new[] { "ChunksId", "FilesId" });

            migrationBuilder.CreateIndex(
                name: "IX_Chunks_Checksum",
                table: "Chunks",
                column: "Checksum",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChunkMetadataFileMetadata_Chunks_ChunksId",
                table: "ChunkMetadataFileMetadata",
                column: "ChunksId",
                principalTable: "Chunks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
