using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BachataFeedback.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPerfIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Не удаляем старые индексы — они используются внешними ключами (FK),
            // и InnoDB не позволяет их дропнуть без дропа самих FK.
            // Просто добавляем новые составные индексы для производительности.

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_RevieweeId_CreatedAt",
                table: "Reviews",
                columns: new[] { "RevieweeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewerId_CreatedAt",
                table: "Reviews",
                columns: new[] { "ReviewerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TargetType_TargetId",
                table: "Reports",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventReviews_EventId_CreatedAt",
                table: "EventReviews",
                columns: new[] { "EventId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_RevieweeId_CreatedAt",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ReviewerId_CreatedAt",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reports_TargetType_TargetId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_EventReviews_EventId_CreatedAt",
                table: "EventReviews");

            // Не восстанавливаем старые индексы — они не удалялись в Up.
        }
    }
}