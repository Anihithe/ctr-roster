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
        logger.LogInformation("Base de données initialisée (catalogue vide prêt pour saisie).");
    }
}
