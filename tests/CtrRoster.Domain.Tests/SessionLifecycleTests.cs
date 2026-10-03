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

    [Fact]
    public void DailyRenewal_ShouldAdvancePastClosedDays_ToNextOpenDay()
    {
        var config = new GuildConfig
        {
            OpenDaysJson = System.Text.Json.JsonSerializer.Serialize(new[] { "Tuesday", "Wednesday", "Friday" }),
            RenewIntervalDays = 1
        };

        // Supposons une session du mercredi
        var wednesday = new DateTime(2026, 10, 7, 20, 0, 0); // Mercredi 7 Octobre 2026
        Assert.Equal(DayOfWeek.Wednesday, wednesday.DayOfWeek);

        var interval = config.GetRenewInterval();
        var nextDate = wednesday.Add(interval); // Jeudi 8 Octobre

        if (interval <= TimeSpan.FromDays(1))
        {
            int safety = 0;
            while (!config.IsDayOpen(nextDate.DayOfWeek) && safety < 7)
            {
                nextDate = nextDate.AddDays(1);
                safety++;
            }
        }

        // Le jeudi étant fermé, le mode quotidien doit avancer jusqu'au vendredi
        Assert.Equal(DayOfWeek.Friday, nextDate.DayOfWeek);
        Assert.Equal(new DateTime(2026, 10, 9, 20, 0, 0), nextDate);
    }

    [Fact]
    public void WeeklyRenewal_ShouldNotAdvanceToNextDay_WhenDayOfWeekIsClosed()
    {
        var config = new GuildConfig
        {
            // Jeudi a été retiré des jours d'ouverture
            OpenDaysJson = System.Text.Json.JsonSerializer.Serialize(new[] { "Tuesday", "Wednesday", "Friday", "Saturday" }),
            RenewIntervalDays = 7
        };

        var thursday = new DateTime(2026, 10, 8, 20, 0, 0); // Jeudi 8 Octobre 2026
        Assert.Equal(DayOfWeek.Thursday, thursday.DayOfWeek);

        var interval = config.GetRenewInterval();
        var nextDate = thursday.Add(interval); // Jeudi 15 Octobre (+7j)

        bool shouldStopRenewal = false;
        if (interval <= TimeSpan.FromDays(1))
        {
            int safety = 0;
            while (!config.IsDayOpen(nextDate.DayOfWeek) && safety < 7)
            {
                nextDate = nextDate.AddDays(1);
                safety++;
            }
        }
        else
        {
            // En hebdomadaire, si le jour de la semaine est fermé, on stoppe sans déborder sur le vendredi
            if (!config.IsDayOpen(nextDate.DayOfWeek))
            {
                shouldStopRenewal = true;
            }
        }

        Assert.True(shouldStopRenewal);
        // Le jour reste jeudi, pas de glissement vers vendredi
        Assert.Equal(DayOfWeek.Thursday, nextDate.DayOfWeek);
    }
}
