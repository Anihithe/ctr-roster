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

        // Retrait du participant
        table.Participants.Remove(participant);

        bool dissolved = false;
        // Règle métier : la table n'est supprimée que si plus personne n'est dessus
        if (table.Participants.Count == 0)
        {
            db.GameTables.Remove(table);
            dissolved = true;
        }

        await db.SaveChangesAsync(ct);
        await renderer.QueueMessageUpdateAsync(sessionId, ct);

        return dissolved
            ? $"Tu as quitté la table de **{gameName}**. Comme il n'y avait plus personne, la table a été supprimée."
            : $"Tu as quitté la table de **{gameName}**.";
    }
}
