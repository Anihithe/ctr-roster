using CtrRoster.Application;
using CtrRoster.Infrastructure;
using CtrRoster.Presentation.Discord;
using CtrRoster.Presentation.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Configuration des UserSecrets pour le débug local sécurisé
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// Enregistrement des couches Architecture
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Configuration Discord Socket Client
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds
                     | GatewayIntents.GuildMessages
                     | GatewayIntents.GuildMembers
                     | GatewayIntents.MessageContent,
    AlwaysDownloadUsers = true,
    LogLevel = LogSeverity.Info
}));

// Configuration Discord Interaction Service & Router
builder.Services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>(), new InteractionServiceConfig
{
    LogLevel = LogSeverity.Info,
    DefaultRunMode = RunMode.Async
}));

builder.Services.AddSingleton<InteractionRouter>();

// Service de démarrage du bot
builder.Services.AddHostedService<BotStartupService>();

var env = builder.Environment;
var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Démarrage de CTR-Roster en environnement {Env}", env.EnvironmentName);

// Initialisation du schéma de base de données (In-Memory ou SQLite physique) et seed
await DatabaseInitializationHelper.InitializeDatabaseAsync(app.Services, env.IsDevelopment(), logger);

await app.RunAsync();
