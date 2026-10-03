using CtrRoster.Application.Games.Commands;
using CtrRoster.Application.Games.Queries;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Exceptions;
using CtrRoster.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CtrRoster.Domain.Tests;

public class GameCatalogTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public GameCatalogTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();
        AppDbContext.ConfigureSqlitePragmas(_connection, isInMemory: true);

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task AddGame_ShouldPersist_WhenValid()
    {
        using var db = new AppDbContext(_options);
        var handler = new AddGameHandler(db);

        var game = await handler.HandleAsync(1001, "Ark Nova", 1, 4);

        Assert.NotNull(game);
        Assert.Equal("Ark Nova", game.Name);
        Assert.Equal((ulong)1001, game.GuildId);
        Assert.True(game.IsActive);
        Assert.Single(db.Games);
    }

    [Fact]
    public async Task AddGame_DuplicateOnSameGuild_ShouldThrowDomainException()
    {
        using var db = new AppDbContext(_options);
        var handler = new AddGameHandler(db);
        await handler.HandleAsync(1001, "Catan", 3, 4);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(1001, "catan", 3, 4));
    }

    [Fact]
    public async Task AddGame_SameNameOnDifferentGuilds_ShouldSucceed()
    {
        using var db = new AppDbContext(_options);
        var handler = new AddGameHandler(db);

        var game1 = await handler.HandleAsync(1001, "Catan", 3, 4);
        var game2 = await handler.HandleAsync(2002, "Catan", 3, 4);

        Assert.NotNull(game1);
        Assert.NotNull(game2);
        Assert.Equal((ulong)1001, game1.GuildId);
        Assert.Equal((ulong)2002, game2.GuildId);
        Assert.Equal(2, await db.Games.CountAsync());
    }

    [Fact]
    public async Task GetActiveGames_ShouldFilterStrictlyByGuildId()
    {
        using var db = new AppDbContext(_options);
        var addHandler = new AddGameHandler(db);
        await addHandler.HandleAsync(1001, "Guild1 Game", 2, 4);
        await addHandler.HandleAsync(2002, "Guild2 Game", 2, 4);

        // Add a legacy global game (GuildId = 0)
        db.Games.Add(new Game { GuildId = 0, Name = "Global Legacy Game", IsActive = true });
        await db.SaveChangesAsync();

        var queryHandler = new GetActiveGamesHandler(db);

        var guild1Games = await queryHandler.HandleAsync(1001);
        Assert.Single(guild1Games);
        Assert.Contains(guild1Games, g => g.Name == "Guild1 Game");
        Assert.DoesNotContain(guild1Games, g => g.Name == "Global Legacy Game");
        Assert.DoesNotContain(guild1Games, g => g.Name == "Guild2 Game");

        var guild2Games = await queryHandler.HandleAsync(2002);
        Assert.Single(guild2Games);
        Assert.Contains(guild2Games, g => g.Name == "Guild2 Game");
        Assert.DoesNotContain(guild2Games, g => g.Name == "Global Legacy Game");
        Assert.DoesNotContain(guild2Games, g => g.Name == "Guild1 Game");
    }

    [Fact]
    public async Task RemoveGame_ShouldDeleteFromDatabase()
    {
        using var db = new AppDbContext(_options);
        var addHandler = new AddGameHandler(db);
        await addHandler.HandleAsync(1001, "Nemesis", 1, 5);

        var removeHandler = new RemoveGameHandler(db);
        var removed = await removeHandler.HandleAsync(1001, "nemesis");

        Assert.Equal("Nemesis", removed.Name);
        Assert.Empty(db.Games);
    }

    [Fact]
    public async Task RemoveGame_DifferentGuild_ShouldNotRemoveGameFromOtherGuild()
    {
        using var db = new AppDbContext(_options);
        var addHandler = new AddGameHandler(db);
        await addHandler.HandleAsync(1001, "Root", 2, 4);

        var removeHandler = new RemoveGameHandler(db);

        // Guild 2002 tries to remove Guild 1001's game -> should throw DomainException
        await Assert.ThrowsAsync<DomainException>(() => removeHandler.HandleAsync(2002, "Root"));

        // Game should still exist on Guild 1001
        Assert.Single(db.Games);
    }

    [Fact]
    public async Task RemoveGame_NotFound_ShouldThrowDomainException()
    {
        using var db = new AppDbContext(_options);
        var removeHandler = new RemoveGameHandler(db);

        await Assert.ThrowsAsync<DomainException>(() => removeHandler.HandleAsync(1001, "UnknownGame"));
    }
}
