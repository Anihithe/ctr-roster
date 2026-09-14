using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CtrRoster.Presentation.Services;

public static class DatabaseInitializationHelper
{
    public static async Task InitializeDatabaseAsync(IServiceProvider services, bool isDevelopment, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var dbContext = (DbContext)db;

        logger.LogInformation("Initialisation de la base de données (Schéma)...");

        // En mode SQLite mémoire ou développement, création ou application des migrations
        await dbContext.Database.EnsureCreatedAsync();

        // Seed du catalogue initial si vide
        if (!await db.Games.AnyAsync())
        {
            logger.LogInformation("Injection des jeux par défaut dans le catalogue...");
            db.Games.AddRange(
                new Game { Name = "Warhammer 40k", MinPlayers = 2, MaxPlayers = 4 },
                new Game { Name = "Le Seigneur des Anneaux (SdA)", MinPlayers = 2, MaxPlayers = 2 },
                new Game { Name = "Dune: Imperium", MinPlayers = 3, MaxPlayers = 4 },
                new Game { Name = "Catan", MinPlayers = 3, MaxPlayers = 4 },
                new Game { Name = "7 Wonders", MinPlayers = 3, MaxPlayers = 7 }
            );

            await db.SaveChangesAsync();
            logger.LogInformation("Catalogue initialisé avec succès (5 jeux).");
        }
    }
}
