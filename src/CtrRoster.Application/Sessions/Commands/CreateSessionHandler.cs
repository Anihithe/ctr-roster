using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Sessions.Commands;

public class CreateSessionHandler(IAppDbContext db)
{
    public async Task<GameSession> HandleAsync(
        DateTime scheduledDate,
        ulong channelId,
        CancellationToken ct = default)
    {
        // Règle métier : Clôturer automatiquement les sessions précédentes encore ouvertes sur ce canal
        var openSessions = await db.GameSessions
            .Where(s => s.DiscordChannelId == channelId && s.Status == SessionStatus.Open)
            .ToListAsync(ct);

        foreach (var oldSession in openSessions)
        {
            oldSession.Status = SessionStatus.Closed;
        }

        var newSession = new GameSession
        {
            ScheduledDate = scheduledDate,
            DiscordChannelId = channelId,
            Status = SessionStatus.Open,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.GameSessions.Add(newSession);
        await db.SaveChangesAsync(ct);

        return newSession;
    }
}
