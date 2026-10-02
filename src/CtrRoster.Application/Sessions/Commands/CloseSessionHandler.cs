using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using CtrRoster.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Sessions.Commands;

/// <summary>
/// Clôture manuellement une session de jeu et actualise sa Card Discord en lecture seule.
/// </summary>
public class CloseSessionHandler(IAppDbContext db, IDiscordMessageRenderer renderer)
{
    public async Task<GameSession> HandleAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.GameSessions
            .Include(s => s.Tables)
            .Include(s => s.Availabilities)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new DomainException("Session de jeu introuvable.");

        if (session.Status == SessionStatus.Closed)
        {
            throw new DomainException("Cette session est déjà clôturée.");
        }

        session.Status = SessionStatus.Closed;
        await db.SaveChangesAsync(ct);

        // Actualise la Card Discord immédiatement (désactive les boutons et affiche 🔴 Session clôturée)
        await renderer.QueueMessageUpdateAsync(sessionId, ct);

        return session;
    }
}
