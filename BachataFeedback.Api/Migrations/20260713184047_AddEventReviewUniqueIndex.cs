using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BachataFeedback.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEventReviewUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Шаг 1: Удаляем дубликаты (оставляем запись с максимальным Id на каждую пару ReviewerId+EventId).
            // Используем подзапрос через временную таблицу (MySQL не поддерживает DELETE ... FROM с подзапросом
            // на ту же таблицу напрямую).
            migrationBuilder.Sql(@"
                DELETE FROM `EventReviews`
                WHERE `Id` NOT IN (
                    SELECT max_id FROM (
                        SELECT MAX(`Id`) AS max_id
                        FROM `EventReviews`
                        GROUP BY `ReviewerId`, `EventId`
                    ) AS keep
                );
            ");

            // Шаг 2: Создаём составной уникальный индекс.
            // В MySQL нельзя удалить индекс, пока он используется FK-ограничением,
            // поэтому создаём новый индекс (покрывающий ReviewerId) ДО удаления старого.
            migrationBuilder.CreateIndex(
                name: "IX_EventReviews_ReviewerId_EventId_Unique",
                table: "EventReviews",
                columns: new[] { "ReviewerId", "EventId" },
                unique: true);

            // Шаг 3: Теперь старый индекс можно удалить — FK уже покрывается новым составным индексом.
            migrationBuilder.DropIndex(
                name: "IX_EventReviews_ReviewerId",
                table: "EventReviews");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventReviews_ReviewerId_EventId_Unique",
                table: "EventReviews");

            migrationBuilder.CreateIndex(
                name: "IX_EventReviews_ReviewerId",
                table: "EventReviews",
                column: "ReviewerId");
        }
    }
}
