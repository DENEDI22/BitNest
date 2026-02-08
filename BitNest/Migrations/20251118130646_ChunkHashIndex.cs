using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitNest.Migrations
{
    /// <inheritdoc />
    public partial class ChunkHashIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Chunks_Checksum",
                table: "Chunks",
                column: "Checksum",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Chunks_Checksum",
                table: "Chunks");
        }
    }
}
