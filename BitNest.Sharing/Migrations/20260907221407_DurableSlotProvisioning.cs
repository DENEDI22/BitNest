using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitNest.Sharing.Migrations
{
    /// <inheritdoc />
    public partial class DurableSlotProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerSlotId",
                table: "Links",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerSlotId",
                table: "Incoming",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Links_OwnerSlotId",
                table: "Links",
                column: "OwnerSlotId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Links_OwnerSlotId",
                table: "Links");

            migrationBuilder.DropColumn(
                name: "OwnerSlotId",
                table: "Links");

            migrationBuilder.DropColumn(
                name: "OwnerSlotId",
                table: "Incoming");
        }
    }
}
