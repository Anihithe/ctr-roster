using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Sessions.Commands;

/// <summary>
/// Définit ou modifie unitairement la capacité maximale de tables pour une session de jeu.
/// </summary>
public class SetSessionCapacityHandler(IAppDbContext db, IDiscordMessageRenderer renderer)
{
    public async Task<GameSession> HandleAsync(
        Guid sessionId,
        int? maxTables,
        CancellationToken ct = default)
    {
        if (maxTables.HasValue && maxTables.Value < 1)
        {
            throw new DomainException("La capacité maximale de tables doit être au minimum de 1 (ou vide pour illimité).");
        }

        var session = await db.GameSessions
            .Include(s => s.Tables)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new DomainException("Session de jeu introuvable.");

        session.MaxTables = maxTables;
        await db.SaveChangesAsync(ct);

        // Actualise la Card Discord immédiatement
        await renderer.QueueMessageUpdateAsync(sessionId, ct);

        return session;
    }
}
