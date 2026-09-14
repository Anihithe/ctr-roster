using CtrRoster.Application.Common;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using CtrRoster.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CtrRoster.Domain.Tests;

public class SessionLifecycleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SessionLifecycleTests()
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
    public async Task GuildConfig_ShouldPersist_AndRetrieveCorrectly()
    {
        using (var db = new AppDbContext(_options))
        {
            var config = new GuildConfig
            {
                GuildId = 999999,
                AdminRoleId = 888888,
                DefaultChannelId = 777777,
                AutoRenewSessions = true,
                RenewIntervalDays = 7
            };

            db.GuildConfigs.Add(config);
            await db.SaveChangesAsync();
        }

        using (var db = new AppDbContext(_options))
        {
            var config = await db.GuildConfigs.FirstOrDefaultAsync(c => c.GuildId == 999999);
            Assert.NotNull(config);
            Assert.Equal((ulong)888888, config.AdminRoleId);
            Assert.Equal((ulong)777777, config.DefaultChannelId);
            Assert.True(config.AutoRenewSessions);
            Assert.Equal(7, config.RenewIntervalDays);
        }
    }

    [Fact]
    public async Task ExpiredSessions_ShouldBeIdentifiedForClosure()
    {
        using var db = new AppDbContext(_options);

        var pastSession = new GameSession
        {
            ScheduledDate = TimeZoneHelper.NowParis.AddMinutes(-10),
            DiscordChannelId = 12345,
            Status = SessionStatus.Open
        };

        var futureSession = new GameSession
        {
            ScheduledDate = TimeZoneHelper.NowParis.AddDays(3),
            DiscordChannelId = 12345,
            Status = SessionStatus.Open
        };

        db.GameSessions.AddRange(pastSession, futureSession);
        await db.SaveChangesAsync();

        var now = TimeZoneHelper.NowParis;
        var expiredSessions = await db.GameSessions
            .Where(s => s.Status == SessionStatus.Open && s.ScheduledDate <= now)
            .ToListAsync();

        Assert.Single(expiredSessions);
        Assert.Equal(pastSession.Id, expiredSessions[0].Id);
    }
}
