using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BachataFeedback.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEventImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventReviews_Events_EventId1",
                table: "EventReviews");

            migrationBuilder.DropIndex(
                name: "IX_EventReviews_EventId1",
                table: "EventReviews");

            migrationBuilder.DropColumn(
                name: "EventId1",
                table: "EventReviews");

            migrationBuilder.AddColumn<string>(
                name: "CoverImagePath",
                table: "Events",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImagePath",
                table: "Events");

            migrationBuilder.AddColumn<int>(
                name: "EventId1",
                table: "EventReviews",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventReviews_EventId1",
                table: "EventReviews",
                column: "EventId1");

            migrationBuilder.AddForeignKey(
                name: "FK_EventReviews_Events_EventId1",
                table: "EventReviews",
                column: "EventId1",
                principalTable: "Events",
                principalColumn: "Id");
        }
    }
}
