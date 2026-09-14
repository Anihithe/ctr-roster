using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CtrRoster.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Data Source=CtrRosterMem;Mode=Memory;Cache=Shared";

        bool isInMemory = connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase)
                          || connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase);

        if (isInMemory)
        {
            // En mode mémoire partagée, une connexion ouverte singleton doit être conservée
            // pour empêcher SQLite de détruire la base en RAM à chaque fin de scope.
            var keepAliveConnection = new SqliteConnection(connectionString);
            keepAliveConnection.Open();
            AppDbContext.ConfigureSqlitePragmas(keepAliveConnection, isInMemory: true);

            services.AddSingleton(keepAliveConnection);
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                var connection = sp.GetRequiredService<SqliteConnection>();
                options.UseSqlite(connection);
            });
        }
        else
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(connectionString);
            });
        }

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Services de rendu et throttler Discord
        services.AddScoped<Discord.DiscordMessageRenderer>();
        services.AddSingleton<Discord.DiscordUiThrottler>();
        services.AddSingleton<IDiscordMessageRenderer>(sp => sp.GetRequiredService<Discord.DiscordUiThrottler>());
        services.AddHostedService(sp => sp.GetRequiredService<Discord.DiscordUiThrottler>());
        services.AddHostedService<Discord.SessionLifecycleWorker>();

        return services;
    }
}
