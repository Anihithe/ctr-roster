using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Games.Commands;

public class AddGameHandler(IAppDbContext db)
{
    public async Task<Game> HandleAsync(
        string name,
        int? minPlayers,
        int? maxPlayers,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Le nom du jeu ne peut pas être vide.");
        }

        string trimmedName = name.Trim();
        var exists = await db.Games.AnyAsync(g => g.Name.ToLower() == trimmedName.ToLower(), ct);
        if (exists)
        {
            throw new DomainException($"Le jeu '{trimmedName}' existe déjà dans le catalogue.");
        }

        var game = new Game
        {
            Name = trimmedName,
            MinPlayers = minPlayers,
            MaxPlayers = maxPlayers,
            IsActive = true
        };

        db.Games.Add(game);
        await db.SaveChangesAsync(ct);

        return game;
    }
}
