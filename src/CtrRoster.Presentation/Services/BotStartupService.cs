using System.Reflection;
using CtrRoster.Presentation.Discord;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CtrRoster.Presentation.Services;

public class BotStartupService(
    DiscordSocketClient client,
    InteractionService interactionService,
    InteractionRouter interactionRouter,
    IServiceProvider serviceProvider,
    IConfiguration config,
    IHostEnvironment env,
    ILogger<BotStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var token = config["Discord:Token"];
        if (string.IsNullOrWhiteSpace(token) || token.Contains("METTRE_VOTRE_TOKEN"))
        {
            logger.LogWarning("⚠️ Aucun token Discord valide n'est configuré dans 'Discord:Token'. Le bot ne se connectera pas au Gateway Discord.");
            logger.LogWarning("Veuillez configurer votre token via 'dotnet user-secrets set \"Discord:Token\" \"...\"' ou dans appsettings.Development.json.");
            return;
        }

        interactionRouter.Initialize();

        client.Log += LogDiscordAsync;
        interactionService.Log += LogDiscordAsync;

        client.InteractionCreated += async interaction =>
        {
            var context = new SocketInteractionContext(client, interaction);
            await interactionService.ExecuteCommandAsync(context, serviceProvider);
        };

        client.Ready += async () =>
        {
            logger.LogInformation("🤖 Bot Discord connecté en tant que {Username}#{Discriminator} ({Id})",
                client.CurrentUser.Username, client.CurrentUser.Discriminator, client.CurrentUser.Id);

            await interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);

            var devGuildId = config.GetValue<ulong>("Discord:DevGuildId");
            if (env.IsDevelopment())
            {
                if (devGuildId != 0)
                {
                    // Enregistrement instantané (0 seconde) sur le serveur de test spécifié
                    await interactionService.RegisterCommandsToGuildAsync(devGuildId);
                    logger.LogInformation("⚡ Slash commands enregistrées instantanément sur le serveur de test Dev {GuildId}", devGuildId);
                }
                else
                {
                    // Enregistrement instantané automatique sur tous les serveurs où le bot est présent
                    foreach (var guild in client.Guilds)
                    {
                        await interactionService.RegisterCommandsToGuildAsync(guild.Id);
                        logger.LogInformation("⚡ Slash commands enregistrées instantanément sur le serveur {GuildName} ({GuildId})", guild.Name, guild.Id);
                    }
                }
            }
            else
            {
                // Enregistrement global pour la production
                await interactionService.RegisterCommandsGloballyAsync();
                logger.LogInformation("🌍 Slash commands enregistrées globalement.");
            }
        };

        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (client.LoginState == LoginState.LoggedIn)
        {
            logger.LogInformation("Arrêt propre du client Discord...");
            await client.StopAsync();
            await client.LogoutAsync();
        }
    }

    private Task LogDiscordAsync(LogMessage log)
    {
        var level = log.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        logger.Log(level, log.Exception, "[Discord.Net] {Message}", log.Message);
        return Task.CompletedTask;
    }
}
