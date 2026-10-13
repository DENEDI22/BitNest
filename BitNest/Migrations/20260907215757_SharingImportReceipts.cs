using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitNest.Migrations
{
    /// <inheritdoc />
    public partial class SharingImportReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IncomingUploadId",
                table: "Files",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UploadSlotOwners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadSlotOwners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadSlotOwners_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Files_IncomingUploadId",
                table: "Files",
                column: "IncomingUploadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadSlotOwners_OwnerUserId",
                table: "UploadSlotOwners",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UploadSlotOwners");

            migrationBuilder.DropIndex(
                name: "IX_Files_IncomingUploadId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "IncomingUploadId",
                table: "Files");
        }
    }
}
