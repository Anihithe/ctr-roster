using CtrRoster.Application.Common;
using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using CtrRoster.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Sessions.Commands;

public class CreateSessionHandler(IAppDbContext db)
{
    public async Task<GameSession> HandleAsync(
        DateTime scheduledDate,
        ulong channelId,
        ulong guildId = 0,
        int? maxTables = null,
        bool force = false,
        CancellationToken ct = default)
    {
        // 1. Vérifier si une session active existe déjà exactement sur ce créneau et ce salon
        var existingSameSlot = await db.GameSessions
            .FirstOrDefaultAsync(s => s.DiscordChannelId == channelId && (s.GuildId == guildId || s.GuildId == 0) && s.Status == SessionStatus.Open && s.ScheduledDate == scheduledDate, ct);

        if (existingSameSlot != null)
        {
            throw new DomainException($"Une session active est déjà ouverte sur ce salon pour le {scheduledDate:dddd dd MMMM yyyy à HH:mm}.");
        }

        // 2. Vérifier les jours d'ouverture si configurés pour ce serveur
        GuildConfig? config = null;
        if (guildId != 0)
        {
            config = await db.GuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId, ct);
            if (config != null && !force && !config.IsDayOpen(scheduledDate.DayOfWeek))
            {
                var openDaysStr = DayParser.FormatDaysFrench(config.GetOpenDays());
                throw new DomainException($"Le lieu est configuré comme fermé le {DayParser.ToFrenchName(scheduledDate.DayOfWeek)}. Jours d'ouverture : {openDaysStr}. (Utilise l'option force:true pour passer outre).");
            }
        }

        // 3. Hériter de la capacité par défaut du serveur si non spécifiée
        int? effectiveMaxTables = maxTables ?? config?.DefaultMaxTables;

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
