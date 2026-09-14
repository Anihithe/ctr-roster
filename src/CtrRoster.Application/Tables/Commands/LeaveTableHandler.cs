using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Enums;
using CtrRoster.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Tables.Commands;

public class LeaveTableHandler(IAppDbContext db, IDiscordMessageRenderer renderer)
{
    public async Task<string> HandleAsync(Guid tableId, ulong userId, CancellationToken ct = default)
    {
        var table = await db.GameTables
            .Include(t => t.Participants)
            .Include(t => t.GameSession)
            .FirstOrDefaultAsync(t => t.Id == tableId, ct)
            ?? throw new DomainException("Table introuvable ou déjà dissoute.");

        var participant = table.Participants.FirstOrDefault(p => p.DiscordUserId == userId)
            ?? throw new DomainException("Tu ne fais pas partie de cette table.");

        var sessionId = table.GameSessionId;
        var gameName = table.GameName;

        // Règle métier critique : Si après le départ il reste moins de 2 joueurs => auto-dissolution de la table
        int remainingPlayers = table.Participants.Count(p => p.DiscordUserId != userId && p.Role == ParticipantRole.Player);
        bool dissolved = false;

        if (remainingPlayers < 2)
        {
            db.GameTables.Remove(table);
            dissolved = true;
        }
        else
        {
            table.Participants.Remove(participant);
        }

        await db.SaveChangesAsync(ct);
        await renderer.QueueMessageUpdateAsync(sessionId, ct);

        return dissolved
            ? $"Tu as quitté la table de {gameName}. Il restait moins de 2 joueurs, la table a donc été dissoute."
            : $"Tu as quitté la table de {gameName}.";
    }
}
