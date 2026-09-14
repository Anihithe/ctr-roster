using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Tables.Commands;

public class DissolveTableHandler(IAppDbContext db, IDiscordMessageRenderer renderer)
{
    public async Task<string> HandleAsync(
        Guid tableId,
        ulong requestedByUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var table = await db.GameTables
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == tableId, ct)
            ?? throw new DomainException("Table introuvable ou déjà dissoute.");

        // Règle de gouvernance : seul le créateur de la table ou un administrateur peut la dissoudre
        if (table.CreatedByDiscordUserId != requestedByUserId && !isAdmin)
        {
            throw new DomainException("Seul le créateur initial de cette table ou un administrateur peut la dissoudre.");
        }

        var sessionId = table.GameSessionId;
        var gameName = table.GameName;

        db.GameTables.Remove(table);
        await db.SaveChangesAsync(ct);

        await renderer.QueueMessageUpdateAsync(sessionId, ct);

        return $"La table de {gameName} a été dissoute.";
    }
}
