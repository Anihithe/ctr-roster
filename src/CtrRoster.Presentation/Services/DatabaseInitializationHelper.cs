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

        // Application automatique des ajouts de colonnes pour les bases SQLite existantes
        await UpgradeSchemaIfNeededAsync(dbContext, logger);

        logger.LogInformation("Base de données initialisée et schéma vérifié.");
    }

    private static async Task UpgradeSchemaIfNeededAsync(DbContext dbContext, ILogger logger)
    {
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using var cmd = connection.CreateCommand();

            // 1. Vérifier si GuildId existe sur Games
            cmd.CommandText = "PRAGMA table_info(Games);";
            var hasGamesGuildId = false;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1).Equals("GuildId", StringComparison.OrdinalIgnoreCase))
                    {
                        hasGamesGuildId = true;
                        break;
                    }
                }
            }

            if (!hasGamesGuildId)
            {
                logger.LogInformation("Migration schéma SQLite : Ajout de la colonne GuildId sur la table Games.");
                using var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Games ADD COLUMN GuildId INTEGER NOT NULL DEFAULT 0;";
                await alterCmd.ExecuteNonQueryAsync();
            }

            // 2. Vérifier si GuildId existe sur GameSessions
            cmd.CommandText = "PRAGMA table_info(GameSessions);";
            var hasSessionsGuildId = false;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1).Equals("GuildId", StringComparison.OrdinalIgnoreCase))
                    {
                        hasSessionsGuildId = true;
                        break;
                    }
                }
            }

            if (!hasSessionsGuildId)
            {
                logger.LogInformation("Migration schéma SQLite : Ajout de la colonne GuildId sur la table GameSessions.");
                using var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE GameSessions ADD COLUMN GuildId INTEGER NOT NULL DEFAULT 0;";
                await alterCmd.ExecuteNonQueryAsync();
            }

            // 3. Vérifier si AllowedChannelId existe sur GuildConfigs
            cmd.CommandText = "PRAGMA table_info(GuildConfigs);";
            var hasAllowedChannel = false;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1).Equals("AllowedChannelId", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllowedChannel = true;
                        break;
                    }
                }
            }

            if (!hasAllowedChannel)
            {
                logger.LogInformation("Migration schéma SQLite : Ajout de la colonne AllowedChannelId sur la table GuildConfigs.");
                using var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE GuildConfigs ADD COLUMN AllowedChannelId INTEGER NULL;";
                await alterCmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Remarque lors de la vérification de schéma SQLite (peut être normal sur base neuve).");
        }
    }
}
