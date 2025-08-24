using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BachataFeedback.Api.Migrations
{
    /// <inheritdoc />
    public partial class reportfix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Reviews_TargetId",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_UserPhotos_TargetId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_TargetId",
                table: "Reports");

            migrationBuilder.AddColumn<int>(
                name: "ReviewId",
                table: "Reports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserPhotoId",
                table: "Reports",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReviewId",
                table: "Reports",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_UserPhotoId",
                table: "Reports",
                column: "UserPhotoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Reviews_ReviewId",
                table: "Reports",
                column: "ReviewId",
                principalTable: "Reviews",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_UserPhotos_UserPhotoId",
                table: "Reports",
                column: "UserPhotoId",
                principalTable: "UserPhotos",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Reviews_ReviewId",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_UserPhotos_UserPhotoId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_ReviewId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_UserPhotoId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ReviewId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "UserPhotoId",
                table: "Reports");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TargetId",
                table: "Reports",
                column: "TargetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Reviews_TargetId",
                table: "Reports",
                column: "TargetId",
                principalTable: "Reviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_UserPhotos_TargetId",
                table: "Reports",
                column: "TargetId",
                principalTable: "UserPhotos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
