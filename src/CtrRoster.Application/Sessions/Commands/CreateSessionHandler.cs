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
        ulong guildId = 0,
        int? maxTables = null,
        CancellationToken ct = default)
    {
        // Règle métier : Clôturer automatiquement les sessions précédentes encore ouvertes sur ce canal
        var openSessions = await db.GameSessions
            .Where(s => s.DiscordChannelId == channelId && (s.GuildId == guildId || s.GuildId == 0) && s.Status == SessionStatus.Open)
            .ToListAsync(ct);

        foreach (var oldSession in openSessions)
        {
            oldSession.Status = SessionStatus.Closed;
        }

        // Si maxTables n'est pas spécifié, hériter de la capacité par défaut du serveur
        int? effectiveMaxTables = maxTables;
        if (!effectiveMaxTables.HasValue && guildId != 0)
        {
            var config = await db.GuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId, ct);
            effectiveMaxTables = config?.DefaultMaxTables;
        }

        var newSession = new GameSession
        {
            ScheduledDate = scheduledDate,
            DiscordChannelId = channelId,
            GuildId = guildId,
            MaxTables = effectiveMaxTables,
            Status = SessionStatus.Open,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.GameSessions.Add(newSession);
        await db.SaveChangesAsync(ct);

        return newSession;
    }
}
