using CtrRoster.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Application.Common.Interfaces;

/// <summary>
/// Contrat d'accès aux données découplant l'Application de l'implémentation concrète EF Core.
/// </summary>
public interface IAppDbContext
{
    DbSet<GameSession> GameSessions { get; }
    DbSet<GameTable> GameTables { get; }
    DbSet<TableParticipant> TableParticipants { get; }
    DbSet<PlayerAvailability> PlayerAvailabilities { get; }
    DbSet<Game> Games { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
