using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<GameTable> GameTables => Set<GameTable>();
    public DbSet<TableParticipant> TableParticipants => Set<TableParticipant>();
    public DbSet<PlayerAvailability> PlayerAvailabilities => Set<PlayerAvailability>();
    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <summary>
    /// Configure les PRAGMAs recommandés pour SQLite (mode WAL, clés étrangères, synchronisation).
    /// </summary>
    public static void ConfigureSqlitePragmas(SqliteConnection connection, bool isInMemory = false)
    {
        using var command = connection.CreateCommand();
        if (isInMemory)
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
        }
        else
        {
            command.CommandText = @"
                PRAGMA journal_mode = 'wal';
                PRAGMA synchronous = NORMAL;
                PRAGMA foreign_keys = ON;
            ";
        }
        command.ExecuteNonQuery();
    }
}
