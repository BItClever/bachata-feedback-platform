using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BachataFeedback.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPollVoteLogAndRenameBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BalanceLeads",
                table: "Occurrences",
                newName: "BalanceMales");

            migrationBuilder.RenameColumn(
                name: "BalanceFollows",
                table: "Occurrences",
                newName: "BalanceFemales");

            migrationBuilder.CreateTable(
                name: "PollVoteLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    TelegramPollId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OptionIndex = table.Column<int>(type: "int", nullable: true),
                    ActionType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TelegramUsername = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TelegramDisplayName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollVoteLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PollVoteLogs");

            migrationBuilder.RenameColumn(
                name: "BalanceMales",
                table: "Occurrences",
                newName: "BalanceLeads");

            migrationBuilder.RenameColumn(
                name: "BalanceFemales",
                table: "Occurrences",
                newName: "BalanceFollows");
        }
    }
}
