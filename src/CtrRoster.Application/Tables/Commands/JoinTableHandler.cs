using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using CtrRoster.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Tables.Commands;

public class JoinTableHandler(IAppDbContext db, IDiscordMessageRenderer renderer)
{
    public async Task<string> HandleAsync(
        Guid tableId,
        ulong userId,
        string username,
        ParticipantRole role,
        CancellationToken ct = default)
    {
        var targetTable = await db.GameTables
            .Include(t => t.Participants)
            .Include(t => t.GameSession)
            .FirstOrDefaultAsync(t => t.Id == tableId, ct)
            ?? throw new DomainException("Table introuvable ou déjà fermée.");

        if (targetTable.GameSession.Status != SessionStatus.Open)
        {
            throw new DomainException("Les inscriptions pour cette session sont closes.");
        }

        var sessionId = targetTable.GameSessionId;

        // Règle d'exclusivité : vérifier si l'utilisateur est déjà sur une table de cette session
        var existingTables = await db.GameTables
            .Include(t => t.Participants)
            .Where(t => t.GameSessionId == sessionId)
            .ToListAsync(ct);

        bool roleChanged = false;
        foreach (var tbl in existingTables)
        {
            var p = tbl.Participants.FirstOrDefault(part => part.DiscordUserId == userId);
            if (p != null)
            {
                if (tbl.Id == tableId)
                {
                    if (p.Role == role)
                    {
                        throw new DomainException($"Tu es déjà inscrit comme {role} sur cette table.");
                    }
                    p.Role = role;
                    roleChanged = true;
                }
                else
                {
                    // Retrait de l'ancienne table
                    int remaining = tbl.Participants.Count(part => part.DiscordUserId != userId && part.Role == ParticipantRole.Player);
                    if (remaining < 2)
                    {
                        db.GameTables.Remove(tbl);
                    }
                    else
                    {
                        tbl.Participants.Remove(p);
                    }
                }
            }
        }

        // Si ce n'était pas un simple changement de rôle sur la même table, ajouter à la table cible
        if (!roleChanged)
        {
            targetTable.Participants.Add(new TableParticipant
            {
                GameTableId = targetTable.Id,
                DiscordUserId = userId,
                DiscordUsername = username,
                Role = role
            });
        }

        // Si l'utilisateur était marqué absent dans la session, on le remet présent
        var availability = await db.PlayerAvailabilities
            .FirstOrDefaultAsync(a => a.GameSessionId == sessionId && a.DiscordUserId == userId, ct);

        if (availability != null && availability.IsAbsent)
        {
            availability.IsAbsent = false;
            availability.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await renderer.QueueMessageUpdateAsync(sessionId, ct);

        var roleStr = role == ParticipantRole.Player ? "joueur" : "observateur";
        return $"Tu as rejoint la table de {targetTable.GameName} en tant que {roleStr} !";
    }
}
