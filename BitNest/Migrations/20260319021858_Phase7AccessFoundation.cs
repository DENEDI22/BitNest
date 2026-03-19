using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BitNest.Migrations
{
    /// <inheritdoc />
    public partial class Phase7AccessFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSignInAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerUserId",
                table: "Files",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FileGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    GrantedUserId = table.Column<int>(type: "integer", nullable: false),
                    GrantedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileGrants_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileGrants_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileGrants_Users_GrantedUserId",
                        column: x => x.GrantedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Files_OwnerUserId",
                table: "Files",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FileGrants_FileId_GrantedUserId",
                table: "FileGrants",
                columns: new[] { "FileId", "GrantedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileGrants_GrantedByUserId",
                table: "FileGrants",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FileGrants_GrantedUserId",
                table: "FileGrants",
                column: "GrantedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Users_OwnerUserId",
                table: "Files",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Users_OwnerUserId",
                table: "Files");

            migrationBuilder.DropTable(
                name: "FileGrants");

            migrationBuilder.DropIndex(
                name: "IX_Files_OwnerUserId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastSignInAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Files");
        }
    }
}
