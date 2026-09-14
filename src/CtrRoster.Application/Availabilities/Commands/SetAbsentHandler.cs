using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using CtrRoster.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Availabilities.Commands;

public class SetAbsentHandler(IAppDbContext db, IDiscordMessageRenderer renderer)
{
    public async Task<string> HandleAsync(
        Guid sessionId,
        ulong userId,
        string username,
        CancellationToken ct = default)
    {
        var session = await db.GameSessions
            .Include(s => s.Tables)
            .ThenInclude(t => t.Participants)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new DomainException("Session introuvable.");

        if (session.Status != SessionStatus.Open)
        {
            throw new DomainException("Cette session est déjà fermée.");
        }

        // Si le joueur est actuellement assigné à une table de cette session, on le retire
        foreach (var table in session.Tables.ToList())
        {
            var participant = table.Participants.FirstOrDefault(p => p.DiscordUserId == userId);
            if (participant != null)
            {
                int remainingPlayers = table.Participants.Count(p => p.DiscordUserId != userId && p.Role == ParticipantRole.Player);
                if (remainingPlayers < 2)
                {
                    db.GameTables.Remove(table);
                }
                else
                {
                    table.Participants.Remove(participant);
                }
            }
        }

        // Mise à jour de l'enregistrement de disponibilité
        var availability = await db.PlayerAvailabilities
            .FirstOrDefaultAsync(a => a.GameSessionId == sessionId && a.DiscordUserId == userId, ct);

        if (availability == null)
        {
            availability = new PlayerAvailability
            {
                GameSessionId = sessionId,
                DiscordUserId = userId,
                DiscordUsername = username,
                IsAbsent = true,
                PreferredGamesJson = "[]",
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.PlayerAvailabilities.Add(availability);
        }
        else
        {
            availability.DiscordUsername = username;
            availability.IsAbsent = true;
            availability.PreferredGamesJson = "[]";
            availability.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await renderer.QueueMessageUpdateAsync(sessionId, ct);

        return "Tu as été marqué comme absent pour cette session (et retiré de toute table éventuelle).";
    }
}
