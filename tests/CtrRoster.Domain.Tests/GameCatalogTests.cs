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

        var game = await handler.HandleAsync("Ark Nova", 1, 4);

        Assert.NotNull(game);
        Assert.Equal("Ark Nova", game.Name);
        Assert.True(game.IsActive);
        Assert.Single(db.Games);
    }

    [Fact]
    public async Task AddGame_Duplicate_ShouldThrowDomainException()
    {
        using var db = new AppDbContext(_options);
        var handler = new AddGameHandler(db);
        await handler.HandleAsync("Catan", 3, 4);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync("catan", 3, 4));
    }

    [Fact]
    public async Task RemoveGame_ShouldDeleteFromDatabase()
    {
        using var db = new AppDbContext(_options);
        var addHandler = new AddGameHandler(db);
        await addHandler.HandleAsync("Nemesis", 1, 5);

        var removeHandler = new RemoveGameHandler(db);
        var removed = await removeHandler.HandleAsync("nemesis");

        Assert.Equal("Nemesis", removed.Name);
        Assert.Empty(db.Games);
    }

    [Fact]
    public async Task RemoveGame_NotFound_ShouldThrowDomainException()
    {
        using var db = new AppDbContext(_options);
        var removeHandler = new RemoveGameHandler(db);

        await Assert.ThrowsAsync<DomainException>(() => removeHandler.HandleAsync("UnknownGame"));
    }
}
