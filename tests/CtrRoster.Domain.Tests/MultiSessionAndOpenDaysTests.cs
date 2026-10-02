using System.Text.Json;
using CtrRoster.Application.Common;
using CtrRoster.Application.Sessions.Commands;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using CtrRoster.Domain.Exceptions;
using CtrRoster.Domain.Tests.TestHelpers;
using CtrRoster.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CtrRoster.Domain.Tests;

public class MultiSessionAndOpenDaysTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly TestMessageRenderer _renderer;

    public MultiSessionAndOpenDaysTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();
        AppDbContext.ConfigureSqlitePragmas(_connection, isInMemory: true);

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();

        _renderer = new TestMessageRenderer();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    // --- DayParser Tests ---
    [Fact]
    public void DayParser_ShouldParseFrenchAndEnglishDays()
    {
        var days = DayParser.ParseDays("mardi, jeudi; samedi, sunday");
        Assert.Equal(4, days.Count);
        Assert.Contains(DayOfWeek.Tuesday, days);
        Assert.Contains(DayOfWeek.Thursday, days);
        Assert.Contains(DayOfWeek.Saturday, days);
        Assert.Contains(DayOfWeek.Sunday, days);
    }

    [Fact]
    public void DayParser_FormatDaysFrench_ShouldSortChronologically()
    {
        var days = new List<DayOfWeek> { DayOfWeek.Sunday, DayOfWeek.Tuesday, DayOfWeek.Friday };
        var formatted = DayParser.FormatDaysFrench(days);
        Assert.Equal("Mardi, Vendredi, Dimanche", formatted);
    }

    // --- GuildConfig Tests ---
    [Fact]
    public void GuildConfig_GetRenewInterval_ShouldRespectHoursAndDays()
    {
        var config = new GuildConfig { RenewIntervalDays = 7, RenewIntervalHours = null };
        Assert.Equal(TimeSpan.FromDays(7), config.GetRenewInterval());

        config.RenewIntervalHours = 24;
        Assert.Equal(TimeSpan.FromHours(24), config.GetRenewInterval());

        config.RenewIntervalHours = 48;
        Assert.Equal(TimeSpan.FromHours(48), config.GetRenewInterval());
    }

    [Fact]
    public void GuildConfig_GetOpenDays_ShouldDefaultToAllDays_WhenNullOrEmpty()
    {
        var config = new GuildConfig { OpenDaysJson = null };
        var openDays = config.GetOpenDays();
        Assert.Equal(7, openDays.Count);
        Assert.True(config.IsDayOpen(DayOfWeek.Monday));
        Assert.True(config.IsDayOpen(DayOfWeek.Sunday));
    }

    [Fact]
    public void GuildConfig_OpenDays_ShouldFilterProperly_WhenConfigured()
    {
        var configuredDays = new List<string> { "Tuesday", "Wednesday", "Friday" };
        var config = new GuildConfig
        {
            OpenDaysJson = JsonSerializer.Serialize(configuredDays)
        };

        Assert.True(config.IsDayOpen(DayOfWeek.Tuesday));
        Assert.True(config.IsDayOpen(DayOfWeek.Wednesday));
        Assert.True(config.IsDayOpen(DayOfWeek.Friday));
        Assert.False(config.IsDayOpen(DayOfWeek.Monday));
        Assert.False(config.IsDayOpen(DayOfWeek.Sunday));
    }

    // --- Multi-Sessions & CreateSessionHandler Tests ---
    [Fact]
    public async Task CreateSession_ShouldAllowMultipleParallelOpenSessions_OnDifferentSlots()
    {
        using var db = new AppDbContext(_options);
        var handler = new CreateSessionHandler(db);

        var date1 = new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc);
        var date2 = new DateTime(2026, 10, 11, 20, 0, 0, DateTimeKind.Utc);

        var session1 = await handler.HandleAsync(date1, channelId: 12345, guildId: 100);
        var session2 = await handler.HandleAsync(date2, channelId: 12345, guildId: 100);

        Assert.NotEqual(session1.Id, session2.Id);

        var activeSessions = await db.GameSessions
            .Where(s => s.DiscordChannelId == 12345 && s.Status == SessionStatus.Open)
            .ToListAsync();

        Assert.Equal(2, activeSessions.Count);
    }

    [Fact]
    public async Task CreateSession_ShouldRejectDuplicateSlot_OnSameChannel()
    {
        using var db = new AppDbContext(_options);
        var handler = new CreateSessionHandler(db);

        var date = new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc);

        await handler.HandleAsync(date, channelId: 12345, guildId: 100);

        // Tentative de créer une session exactement au même moment sur le même salon
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(date, channelId: 12345, guildId: 100));

        Assert.Contains("déjà ouverte", ex.Message);
    }

    [Fact]
    public async Task CreateSession_ShouldRejectClosedDay_UnlessForced()
    {
        using var db = new AppDbContext(_options);
        var config = new GuildConfig
        {
            GuildId = 100,
            OpenDaysJson = JsonSerializer.Serialize(new List<string> { "Friday", "Saturday" })
        };
        db.GuildConfigs.Add(config);
        await db.SaveChangesAsync();

        var handler = new CreateSessionHandler(db);

        // 2026-10-12 is Monday (closed day)
        var mondayDate = new DateTime(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc);
        Assert.Equal(DayOfWeek.Monday, mondayDate.DayOfWeek);

        // Doit lever une DomainException sans force
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(mondayDate, channelId: 12345, guildId: 100, force: false));

        Assert.Contains("fermé", ex.Message);

        // Doit passer avec force: true
        var session = await handler.HandleAsync(mondayDate, channelId: 12345, guildId: 100, force: true);
        Assert.NotNull(session);
        Assert.Equal(SessionStatus.Open, session.Status);
    }

    // --- CloseSessionHandler Tests ---
    [Fact]
    public async Task CloseSession_ShouldMarkSessionClosed_AndQueueUpdate()
    {
        using var db = new AppDbContext(_options);
        var session = new GameSession
        {
            ScheduledDate = DateTime.UtcNow.AddDays(1),
            DiscordChannelId = 12345,
            DiscordMessageId = 67890,
            Status = SessionStatus.Open
        };
        db.GameSessions.Add(session);
        await db.SaveChangesAsync();

        var closeHandler = new CloseSessionHandler(db, _renderer);
        var closedSession = await closeHandler.HandleAsync(session.Id);

        Assert.Equal(SessionStatus.Closed, closedSession.Status);
        Assert.Contains(session.Id, _renderer.QueuedSessionIds);

        // Re-clôturer doit lever une exception
        await Assert.ThrowsAsync<DomainException>(() => closeHandler.HandleAsync(session.Id));
    }
}
