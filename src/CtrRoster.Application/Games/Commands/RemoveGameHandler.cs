using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Games.Commands;

public class RemoveGameHandler(IAppDbContext db)
{
    public async Task<Game> HandleAsync(ulong guildId, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Le nom du jeu ne peut pas être vide.");
        }

        string trimmedName = name.Trim();
        var game = await db.Games.FirstOrDefaultAsync(
            g => g.GuildId == guildId && g.Name.ToLower() == trimmedName.ToLower(), ct);

        if (game == null)
        {
            throw new DomainException($"Le jeu '{trimmedName}' est introuvable dans le catalogue de ce serveur.");
        }

        db.Games.Remove(game);
        await db.SaveChangesAsync(ct);

        return game;
    }
}
