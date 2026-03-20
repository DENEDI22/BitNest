using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitNest.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadSlotColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "FileId",
                table: "SharepointLinks",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SharepointLinks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkType",
                table: "SharepointLinks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxFileCount",
                table: "SharepointLinks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UploadCount",
                table: "SharepointLinks",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "SharepointLinks");

            migrationBuilder.DropColumn(
                name: "LinkType",
                table: "SharepointLinks");

            migrationBuilder.DropColumn(
                name: "MaxFileCount",
                table: "SharepointLinks");

            migrationBuilder.DropColumn(
                name: "UploadCount",
                table: "SharepointLinks");

            migrationBuilder.AlterColumn<int>(
                name: "FileId",
                table: "SharepointLinks",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldNullable: true);
        }
    }
}
