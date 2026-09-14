using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrRoster.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MinPlayers = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxPlayers = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DiscordChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    DiscordMessageId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedByDiscordUserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameTables_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerAvailabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiscordUserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    DiscordUsername = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsAbsent = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreferredGamesJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerAvailabilities_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TableParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameTableId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiscordUserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    DiscordUsername = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TableParticipants_GameTables_GameTableId",
                        column: x => x.GameTableId,
                        principalTable: "GameTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_IsActive",
                table: "Games",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Name",
                table: "Games",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_ScheduledDate",
                table: "GameSessions",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_Status",
                table: "GameSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GameTables_GameSessionId",
                table: "GameTables",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAvailabilities_GameSessionId_DiscordUserId",
                table: "PlayerAvailabilities",
                columns: new[] { "GameSessionId", "DiscordUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TableParticipants_GameTableId_DiscordUserId",
                table: "TableParticipants",
                columns: new[] { "GameTableId", "DiscordUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "PlayerAvailabilities");

            migrationBuilder.DropTable(
                name: "TableParticipants");

            migrationBuilder.DropTable(
                name: "GameTables");

            migrationBuilder.DropTable(
                name: "GameSessions");
        }
    }
}
