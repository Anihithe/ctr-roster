using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Games.Commands;

public class ToggleGameActiveHandler(IAppDbContext db)
{
    public async Task<bool> HandleAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct)
            ?? throw new DomainException("Jeu introuvable dans le catalogue.");

        game.IsActive = !game.IsActive;
        await db.SaveChangesAsync(ct);

        return game.IsActive;
    }
}
