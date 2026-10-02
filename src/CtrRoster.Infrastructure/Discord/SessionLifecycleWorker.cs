using CtrRoster.Application.Common;
using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Application.Sessions.Commands;
using CtrRoster.Domain.Enums;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CtrRoster.Infrastructure.Discord;

/// <summary>
/// Service d'arrière-plan surveillant l'échéance des sessions de jeu.
/// Clôture automatiquement la session arrivée à l'heure H et génère la session suivante si configuré.
/// </summary>
public class SessionLifecycleWorker(
    IServiceScopeFactory scopeFactory,
    DiscordSocketClient discordClient,
    ILogger<SessionLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("🔄 Démarrage du worker de cycle de vie des sessions (surveillance auto-clôture & renouvellement).");

        // Attente que le client Discord soit connecté
        while (!stoppingToken.IsCancellationRequested && discordClient.ConnectionState != ConnectionState.Connected)
        {
            await Task.Delay(1000, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredSessionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur inattendue dans SessionLifecycleWorker.");
            }

            // Vérification toutes les 15 secondes
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task ProcessExpiredSessionsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var renderer = scope.ServiceProvider.GetRequiredService<DiscordMessageRenderer>();
        var createSessionHandler = scope.ServiceProvider.GetRequiredService<CreateSessionHandler>();

        var now = TimeZoneHelper.NowParis;

        // Récupération des sessions ouvertes dont la date et heure prévue est atteinte ou dépassée
        var expiredSessions = await db.GameSessions
            .Where(s => s.Status == SessionStatus.Open && s.ScheduledDate <= now)
            .ToListAsync(ct);

        if (expiredSessions.Count == 0) return;

        foreach (var session in expiredSessions)
        {
            logger.LogInformation("⏰ La session {SessionId} prévue pour le {Date:dd/MM/yyyy à HH:mm} est échue. Clôture automatique en cours...",
                session.Id, session.ScheduledDate);

            // 1. Clôture de la session en base
            session.Status = SessionStatus.Closed;
            await db.SaveChangesAsync(ct);

            // 2. Mise à jour de la Card Discord (affiche 🔴 Session clôturée et désactive les composants)
            try
            {
                await renderer.RenderSessionMessageAsync(session.Id, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de la mise à jour visuelle de la session clôturée {SessionId}.", session.Id);
            }

            // 3. Renouvellement automatique vers la prochaine session
            if (session.DiscordChannelId == 0) continue;

            var channel = discordClient.GetChannel(session.DiscordChannelId) as SocketTextChannel;
            if (channel == null)
            {
                logger.LogWarning("Salon Discord {ChannelId} introuvable pour le renouvellement auto.", session.DiscordChannelId);
                continue;
            }

            var guildId = channel.Guild.Id;
            var guildConfig = await db.GuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId, ct);

            bool shouldAutoRenew = guildConfig?.AutoRenewSessions ?? true;
            if (!shouldAutoRenew)
            {
                logger.LogInformation("Renouvellement automatique désactivé pour le serveur {GuildId}.", guildId);
                continue;
            }

            TimeSpan interval = guildConfig?.GetRenewInterval() ?? TimeSpan.FromDays(7);
            var nextDate = session.ScheduledDate.Add(interval);

            // Sécurité : si le bot a été éteint longtemps, avancer jusqu'à une date future
            while (nextDate <= now)
            {
                nextDate = nextDate.Add(interval);
            }

            // Respect des jours d'ouverture : avancer jusqu'au prochain jour d'ouverture si fermé
            if (guildConfig != null)
            {
                int safety = 0;
                while (!guildConfig.IsDayOpen(nextDate.DayOfWeek) && safety < 14)
                {
                    nextDate = nextDate.AddDays(1);
                    safety++;
                }
            }

            logger.LogInformation("📅 Création automatique de la session suivante pour le {Date:dddd dd MMMM yyyy à HH:mm} sur <#{ChannelId}>...",
                nextDate, channel.Id);

            try
            {
                // Crée la nouvelle session (hérite automatiquement de DefaultMaxTables du serveur)
                var newSession = await createSessionHandler.HandleAsync(nextDate, channel.Id, guildId, ct: ct);

                // Génère et publie la Card Discord sur le salon
                var (embed, components) = renderer.BuildSessionCard(newSession);
                var postedMessage = await channel.SendMessageAsync(embed: embed, components: components);

                newSession.DiscordMessageId = postedMessage.Id;
                await db.SaveChangesAsync(ct);

                logger.LogInformation("✅ Nouvelle session {SessionId} publiée avec succès sur <#{ChannelId}> (MessageId: {MsgId}).",
                    newSession.Id, channel.Id, postedMessage.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de l'ouverture automatique de la prochaine session.");
            }
        }
    }
}
