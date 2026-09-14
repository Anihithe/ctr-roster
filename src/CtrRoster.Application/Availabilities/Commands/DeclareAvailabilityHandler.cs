using System.Text.Json;
using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using CtrRoster.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Availabilities.Commands;

public class DeclareAvailabilityHandler(IAppDbContext db, IDiscordMessageRenderer renderer)
{
    public async Task<string> HandleAsync(
        Guid sessionId,
        ulong userId,
        string username,
        List<string> preferredGames,
        CancellationToken ct = default)
    {
        var session = await db.GameSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new DomainException("Session introuvable.");

        if (session.Status != SessionStatus.Open)
        {
            throw new DomainException("Les inscriptions pour cette session sont closes.");
        }

        var availability = await db.PlayerAvailabilities
            .FirstOrDefaultAsync(a => a.GameSessionId == sessionId && a.DiscordUserId == userId, ct);

        string gamesJson = JsonSerializer.Serialize(preferredGames.Where(g => !string.IsNullOrWhiteSpace(g)).Distinct());

        if (availability == null)
        {
            availability = new PlayerAvailability
            {
                GameSessionId = sessionId,
                DiscordUserId = userId,
                DiscordUsername = username,
                IsAbsent = false,
                PreferredGamesJson = gamesJson,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.PlayerAvailabilities.Add(availability);
        }
        else
        {
            availability.DiscordUsername = username;
            availability.IsAbsent = false;
            availability.PreferredGamesJson = gamesJson;
            availability.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await renderer.QueueMessageUpdateAsync(sessionId, ct);

        return "Tes disponibilités et préférences de jeux ont bien été enregistrées !";
    }
}
