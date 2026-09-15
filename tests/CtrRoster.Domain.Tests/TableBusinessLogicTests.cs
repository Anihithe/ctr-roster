using CtrRoster.Application.Availabilities.Commands;
using CtrRoster.Application.Tables.Commands;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using CtrRoster.Domain.Exceptions;
using CtrRoster.Domain.Tests.TestHelpers;
using CtrRoster.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CtrRoster.Domain.Tests;

public class TableBusinessLogicTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly TestMessageRenderer _renderer;

    public TableBusinessLogicTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();
        AppDbContext.ConfigureSqlitePragmas(_connection, isInMemory: true);

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();

        _renderer = new TestMessageRenderer();
    }

    private (GameSession Session, GameTable Table) SeedSessionAndTable(AppDbContext db, int playerCount = 2)
    {
        var session = new GameSession
        {
            ScheduledDate = DateTime.UtcNow.AddDays(3),
            DiscordChannelId = 111,
            DiscordMessageId = 222,
            Status = SessionStatus.Open
        };

        var table = new GameTable
        {
            GameSession = session,
            GameName = "Warhammer 40k",
            CreatedByDiscordUserId = 1001
        };

        for (int i = 1; i <= playerCount; i++)
        {
            table.Participants.Add(new TableParticipant
            {
                DiscordUserId = (ulong)(1000 + i),
                DiscordUsername = $"Player{i}",
                Role = ParticipantRole.Player
            });
        }

        session.Tables.Add(table);
        db.GameSessions.Add(session);
        db.SaveChanges();

        return (session, table);
    }

    [Fact]
    public async Task LeaveTable_WhenOnePlayerLeavesAndCreatorRemains_KeepsTableOpen()
    {
        // Arrange : Table à 2 joueurs (1001 et 1002)
        using var db = new AppDbContext(_options);
        var (session, table) = SeedSessionAndTable(db, playerCount: 2);
        var handler = new LeaveTableHandler(db, _renderer);

        // Act : Le joueur 1002 quitte la table -> le créateur (1001) reste
        var result = await handler.HandleAsync(table.Id, 1002);

        // Assert : La table DOIT rester active pour le joueur 1001
        using var verifyDb = new AppDbContext(_options);
        var remainingTable = await verifyDb.GameTables
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == table.Id);

        Assert.NotNull(remainingTable);
        Assert.Single(remainingTable.Participants);
        Assert.Equal((ulong)1001, remainingTable.Participants[0].DiscordUserId);
        Assert.DoesNotContain("supprimée", result);
        Assert.Contains(session.Id, _renderer.QueuedSessionIds);
    }

    [Fact]
    public async Task LeaveTable_WhenLastParticipantLeaves_DissolvesTable()
    {
        // Arrange : Table à 1 seul joueur (1001)
        using var db = new AppDbContext(_options);
        var (session, table) = SeedSessionAndTable(db, playerCount: 1);
        var handler = new LeaveTableHandler(db, _renderer);

        // Act : Le dernier joueur 1001 quitte la table
        var result = await handler.HandleAsync(table.Id, 1001);

        // Assert : Comme il n'y a plus personne, la table est supprimée
        using var verifyDb = new AppDbContext(_options);
        var remainingTable = await verifyDb.GameTables.FirstOrDefaultAsync(t => t.Id == table.Id);
        Assert.Null(remainingTable);
        Assert.Contains("supprimée", result);
    }

    [Fact]
    public async Task JoinTable_EnforcesExclusivity_RemovesUserFromPreviousTable()
    {
        // Arrange : Création d'une session avec deux tables
        using var db = new AppDbContext(_options);
        var session = new GameSession
        {
            ScheduledDate = DateTime.UtcNow.AddDays(2),
            DiscordChannelId = 111,
            DiscordMessageId = 222,
            Status = SessionStatus.Open
        };

        var tableA = new GameTable
        {
            GameSession = session,
            GameName = "Table A (3 joueurs)",
            CreatedByDiscordUserId = 1001
        };
        tableA.Participants.Add(new TableParticipant { DiscordUserId = 1001, DiscordUsername = "P1", Role = ParticipantRole.Player });
        tableA.Participants.Add(new TableParticipant { DiscordUserId = 1002, DiscordUsername = "P2", Role = ParticipantRole.Player });
        tableA.Participants.Add(new TableParticipant { DiscordUserId = 1003, DiscordUsername = "P3", Role = ParticipantRole.Player });

        var tableB = new GameTable
        {
            GameSession = session,
            GameName = "Table B",
            CreatedByDiscordUserId = 2001
        };
        tableB.Participants.Add(new TableParticipant { DiscordUserId = 2001, DiscordUsername = "P4", Role = ParticipantRole.Player });

        session.Tables.Add(tableA);
        session.Tables.Add(tableB);
        db.GameSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new JoinTableHandler(db, _renderer);

        // Act : Le joueur 1003 (sur Table A) rejoint la Table B
        await handler.HandleAsync(tableB.Id, 1003, "P3", ParticipantRole.Player);

        // Assert : 1003 est maintenant sur Table B et n'est plus sur Table A
        using var verifyDb = new AppDbContext(_options);
        var loadedTableA = await verifyDb.GameTables.Include(t => t.Participants).FirstAsync(t => t.Id == tableA.Id);
        var loadedTableB = await verifyDb.GameTables.Include(t => t.Participants).FirstAsync(t => t.Id == tableB.Id);

        Assert.DoesNotContain(loadedTableA.Participants, p => p.DiscordUserId == 1003);
        Assert.Contains(loadedTableB.Participants, p => p.DiscordUserId == 1003);
    }

    [Fact]
    public async Task DissolveTable_OnlyAllowedForCreatorOrAdmin()
    {
        // Arrange
        using var db = new AppDbContext(_options);
        var (session, table) = SeedSessionAndTable(db, playerCount: 2);
        var handler = new DissolveTableHandler(db, _renderer);

        // Act & Assert 1 : Un utilisateur lambda (ni créateur ni admin) doit être rejeté
        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(table.Id, requestedByUserId: 9999, isAdmin: false));

        // Act & Assert 2 : Un admin peut dissoudre la table
        var adminResult = await handler.HandleAsync(table.Id, requestedByUserId: 9999, isAdmin: true);
        Assert.Contains("dissoute", adminResult);

        using var verifyDb = new AppDbContext(_options);
        var checkTable = await verifyDb.GameTables.FirstOrDefaultAsync(t => t.Id == table.Id);
        Assert.Null(checkTable);
    }

    [Fact]
    public async Task SetAbsent_RemovesPlayerFromActiveTableAndMarksAbsent()
    {
        // Arrange : Table à 2 joueurs (1001 et 1002)
        using var db = new AppDbContext(_options);
        var (session, table) = SeedSessionAndTable(db, playerCount: 2);
        var handler = new SetAbsentHandler(db, _renderer);

        // Act : Le joueur 1002 se déclare absent
        await handler.HandleAsync(session.Id, 1002, "Player2");

        // Assert : La table avait 2 joueurs, en retirant 1002 il reste le joueur 1001 => table préservée !
        using var verifyDb = new AppDbContext(_options);
        var checkTable = await verifyDb.GameTables
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == table.Id);

        Assert.NotNull(checkTable);
        Assert.Single(checkTable.Participants);
        Assert.Equal((ulong)1001, checkTable.Participants[0].DiscordUserId);

        var availability = await verifyDb.PlayerAvailabilities
            .FirstOrDefaultAsync(a => a.GameSessionId == session.Id && a.DiscordUserId == 1002);
        Assert.NotNull(availability);
        Assert.True(availability.IsAbsent);
    }

    [Fact]
    public async Task SetAbsent_WhenLastPlayerLeaves_RemovesTable()
    {
        // Arrange : Table à 1 seul joueur (1001)
        using var db = new AppDbContext(_options);
        var (session, table) = SeedSessionAndTable(db, playerCount: 1);
        var handler = new SetAbsentHandler(db, _renderer);

        // Act : Le seul joueur 1001 se déclare absent
        await handler.HandleAsync(session.Id, 1001, "Player1");

        // Assert : Comme il n'y a plus personne, la table est supprimée
        using var verifyDb = new AppDbContext(_options);
        var checkTable = await verifyDb.GameTables.FirstOrDefaultAsync(t => t.Id == table.Id);
        Assert.Null(checkTable);
    }

    [Fact]
    public async Task CreateTable_WithAdditionalParticipants_DirectAssignment()
    {
        // Arrange
        using var db = new AppDbContext(_options);
        var session = new GameSession
        {
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            DiscordChannelId = 111,
            DiscordMessageId = 222,
            Status = SessionStatus.Open
        };
        db.GameSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new CreateTableHandler(db, _renderer);
        var additional = new List<ParticipantDto>
        {
            new(5001, "InvitedFriend", ParticipantRole.Player)
        };

        // Act
        var result = await handler.HandleAsync(
            session.Id,
            creatorUserId: 5000,
            creatorUsername: "TableHost",
            gameName: "Dune: Imperium",
            gameId: null,
            creatorRole: ParticipantRole.Player,
            additionalParticipants: additional);

        // Assert
        Assert.NotNull(result.Table);
        Assert.Single(result.DirectAssignedUserIds);
        Assert.Equal((ulong)5001, result.DirectAssignedUserIds[0]);

        using var verifyDb = new AppDbContext(_options);
        var createdTable = await verifyDb.GameTables
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == result.Table.Id);

        Assert.NotNull(createdTable);
        Assert.Equal(2, createdTable.Participants.Count);
        Assert.Equal("Dune: Imperium", createdTable.GameName);
    }

    [Fact]
    public async Task DeclareAvailability_SetsGamesAndResetsAbsent()
    {
        // Arrange
        using var db = new AppDbContext(_options);
        var session = new GameSession
        {
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            DiscordChannelId = 111,
            DiscordMessageId = 222,
            Status = SessionStatus.Open
        };
        db.GameSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new DeclareAvailabilityHandler(db, _renderer);

        // Act
        var response = await handler.HandleAsync(
            session.Id,
            userId: 7777,
            username: "PlayerSeven",
            preferredGames: ["Warhammer 40k", "Catan"]);

        // Assert
        Assert.Contains("enregistrées", response);

        using var verifyDb = new AppDbContext(_options);
        var avail = await verifyDb.PlayerAvailabilities
            .FirstOrDefaultAsync(a => a.GameSessionId == session.Id && a.DiscordUserId == 7777);

        Assert.NotNull(avail);
        Assert.False(avail.IsAbsent);
        Assert.Contains("Warhammer 40k", avail.PreferredGamesJson);
        Assert.Contains("Catan", avail.PreferredGamesJson);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
