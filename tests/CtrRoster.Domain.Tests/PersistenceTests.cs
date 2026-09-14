using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using CtrRoster.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CtrRoster.Domain.Tests;

public class PersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public PersistenceTests()
    {
        // Utilisation exclusive d'une base SQLite éphémère en mémoire vive
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();

        AppDbContext.ConfigureSqlitePragmas(_connection, isInMemory: true);

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Création du schéma EF Core en mémoire vive
        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CanCreateSession_WithTablesAndParticipants_InMemory()
    {
        // Arrange
        using var context = new AppDbContext(_options);
        var session = new GameSession
        {
            ScheduledDate = DateTime.UtcNow.AddDays(4),
            DiscordChannelId = 111222333444555666,
            DiscordMessageId = 999888777666555444,
            Status = SessionStatus.Open
        };

        var table = new GameTable
        {
            GameSession = session,
            GameName = "Warhammer 40k",
            CreatedByDiscordUserId = 123456789
        };

        table.Participants.Add(new TableParticipant
        {
            DiscordUserId = 123456789,
            DiscordUsername = "Player1",
            Role = ParticipantRole.Player
        });

        table.Participants.Add(new TableParticipant
        {
            DiscordUserId = 987654321,
            DiscordUsername = "Player2",
            Role = ParticipantRole.Player
        });

        session.Tables.Add(table);
        context.GameSessions.Add(session);
        await context.SaveChangesAsync();

        // Act
        using var verifyContext = new AppDbContext(_options);
        var loadedSession = await verifyContext.GameSessions
            .Include(s => s.Tables)
            .ThenInclude(t => t.Participants)
            .FirstOrDefaultAsync(s => s.Id == session.Id);

        // Assert
        Assert.NotNull(loadedSession);
        Assert.Single(loadedSession.Tables);
        var loadedTable = loadedSession.Tables[0];
        Assert.Equal("Warhammer 40k", loadedTable.GameName);
        Assert.Equal(2, loadedTable.Participants.Count);
    }

    [Fact]
    public async Task CascadeDelete_WhenSessionIsDeleted_DeletesTablesAndParticipants()
    {
        // Arrange
        using var context = new AppDbContext(_options);
        var session = new GameSession
        {
            ScheduledDate = DateTime.UtcNow.AddDays(2),
            DiscordChannelId = 111,
            DiscordMessageId = 222
        };

        var table = new GameTable
        {
            GameSession = session,
            GameName = "Catan",
            CreatedByDiscordUserId = 123
        };

        table.Participants.Add(new TableParticipant
        {
            DiscordUserId = 123,
            DiscordUsername = "CatanLover",
            Role = ParticipantRole.Player
        });

        session.Tables.Add(table);
        context.GameSessions.Add(session);
        await context.SaveChangesAsync();

        var tableId = table.Id;

        // Act
        using var deleteContext = new AppDbContext(_options);
        var sessionToDelete = await deleteContext.GameSessions.FindAsync(session.Id);
        Assert.NotNull(sessionToDelete);
        deleteContext.GameSessions.Remove(sessionToDelete);
        await deleteContext.SaveChangesAsync();

        // Assert
        using var checkContext = new AppDbContext(_options);
        var remainingTables = await checkContext.GameTables.Where(t => t.Id == tableId).ToListAsync();
        var remainingParticipants = await checkContext.TableParticipants.Where(p => p.GameTableId == tableId).ToListAsync();

        Assert.Empty(remainingTables);
        Assert.Empty(remainingParticipants);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
