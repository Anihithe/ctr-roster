using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Games.Queries;

public class GetActiveGamesHandler(IAppDbContext db)
{
    public async Task<List<Game>> HandleAsync(ulong guildId, CancellationToken ct = default)
    {
        return await db.Games
            .Where(g => g.GuildId == guildId && g.IsActive)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);
    }
}
