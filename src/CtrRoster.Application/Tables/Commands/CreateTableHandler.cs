using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using CtrRoster.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Tables.Commands;

public record ParticipantDto(ulong UserId, string Username, ParticipantRole Role);

public record CreateTableResult(GameTable Table, List<ulong> DirectAssignedUserIds);

public class CreateTableHandler(IAppDbContext db, IDiscordMessageRenderer renderer)
{
    public async Task<CreateTableResult> HandleAsync(
        Guid sessionId,
        ulong creatorUserId,
        string creatorUsername,
        string gameName,
        Guid? gameId,
        ParticipantRole creatorRole,
        List<ParticipantDto> additionalParticipants,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            throw new DomainException("Le nom du jeu ne peut pas être vide.");
        }

        var session = await db.GameSessions
            .Include(s => s.Tables)
            .ThenInclude(t => t.Participants)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new DomainException("Session de jeu introuvable.");

        if (session.Status != SessionStatus.Open)
        {
            throw new DomainException("Les inscriptions pour cette session sont closes.");
        }

        // Constitution de la liste de tous les participants souhaités
        var allParticipants = new List<ParticipantDto>
        {
            new(creatorUserId, creatorUsername, creatorRole)
        };

        foreach (var p in additionalParticipants)
        {
            if (allParticipants.All(existing => existing.UserId != p.UserId))
            {
                allParticipants.Add(p);
            }
        }

        // Règle d'exclusivité : retirer ces participants de leurs éventuelles anciennes tables
        var existingTables = session.Tables.ToList();
        foreach (var tbl in existingTables)
        {
            foreach (var candidate in allParticipants)
            {
                var part = tbl.Participants.FirstOrDefault(p => p.DiscordUserId == candidate.UserId);
                if (part != null)
                {
                    tbl.Participants.Remove(part);
                    int remaining = tbl.Participants.Count(p => p.Role == ParticipantRole.Player);
                    if (remaining < 2)
                    {
                        session.Tables.Remove(tbl);
                        db.GameTables.Remove(tbl);
                    }
                }
            }
        }

        // Vérification de la capacité maximale de tables pour cette session
        if (session.MaxTables.HasValue && session.Tables.Count >= session.MaxTables.Value)
        {
            throw new DomainException($"La capacité maximale de cette session est atteinte ({session.MaxTables.Value} table{(session.MaxTables.Value > 1 ? "s" : "")} max).");
        }

        var newTable = new GameTable
        {
            GameSessionId = sessionId,
            GameSession = session,
            GameName = gameName.Trim(),
            GameId = gameId,
            CreatedByDiscordUserId = creatorUserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        foreach (var p in allParticipants)
        {
            newTable.Participants.Add(new TableParticipant
            {
                GameTable = newTable,
                DiscordUserId = p.UserId,
                DiscordUsername = p.Username,
                Role = p.Role,
                JoinedAtUtc = DateTime.UtcNow
            });
        }

        db.GameTables.Add(newTable);

        // Mettre à jour les présences/absences si nécessaire
        var userIds = allParticipants.Select(p => p.UserId).ToList();
        var availabilities = await db.PlayerAvailabilities
            .Where(a => a.GameSessionId == sessionId && userIds.Contains(a.DiscordUserId))
            .ToListAsync(ct);

        foreach (var a in availabilities)
        {
            if (a.IsAbsent)
            {
                a.IsAbsent = false;
                a.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        await renderer.QueueMessageUpdateAsync(sessionId, ct);

        var directAssignedUserIds = additionalParticipants
            .Where(p => p.UserId != creatorUserId)
            .Select(p => p.UserId)
            .ToList();

        return new CreateTableResult(newTable, directAssignedUserIds);
    }
}
