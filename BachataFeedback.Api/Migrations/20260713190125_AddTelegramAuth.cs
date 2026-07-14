using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BachataFeedback.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TelegramId",
                table: "AspNetUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramUsername",
                table: "AspNetUsers",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TelegramId_Unique",
                table: "AspNetUsers",
                column: "TelegramId",
                unique: true,
                filter: "[TelegramId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_TelegramId_Unique",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TelegramId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TelegramUsername",
                table: "AspNetUsers");
        }
    }
}
