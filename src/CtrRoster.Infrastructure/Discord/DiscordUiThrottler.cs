using System.Collections.Concurrent;
using System.Threading.Channels;
using CtrRoster.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CtrRoster.Infrastructure.Discord;

/// <summary>
/// Service d'arrière-plan throttlé garantissant le respect strict des rate limits Discord (HTTP 429).
/// Regroupe les demandes d'actualisation de la Card (débouncing de 1.5s) via un Channel non bloquant.
/// </summary>
public class DiscordUiThrottler(
    IServiceScopeFactory scopeFactory,
    ILogger<DiscordUiThrottler> logger) : BackgroundService, IDiscordMessageRenderer
{
    private readonly Channel<Guid> _updateChannel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly ConcurrentDictionary<Guid, DateTime> _pendingSessions = new();
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(1500);

    public Task QueueMessageUpdateAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        _pendingSessions[sessionId] = DateTime.UtcNow;
        _updateChannel.Writer.TryWrite(sessionId);
        logger.LogDebug("Demande d'actualisation enfilée pour la session {SessionId}.", sessionId);
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DiscordUiThrottler démarré.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Attente d'au moins un élément dans le canal
                if (await _updateChannel.Reader.WaitToReadAsync(stoppingToken))
                {
                    // Dépiler les éléments disponibles pour vider la file
                    while (_updateChannel.Reader.TryRead(out var _)) { }

                    // Attente du délai de debounce pour accumuler les clics rapides
                    await Task.Delay(DebounceDelay, stoppingToken);

                    // Traitement de chaque session en attente
                    var sessionsToProcess = _pendingSessions.Keys.ToList();
                    foreach (var sessionId in sessionsToProcess)
                    {
                        _pendingSessions.TryRemove(sessionId, out _);

                        try
                        {
                            using var scope = scopeFactory.CreateScope();
                            var renderer = scope.ServiceProvider.GetRequiredService<DiscordMessageRenderer>();
                            await renderer.RenderSessionMessageAsync(sessionId, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Erreur lors du rafraîchissement throttlé de la session {SessionId}.", sessionId);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur inattendue dans la boucle du DiscordUiThrottler.");
                await Task.Delay(1000, stoppingToken);
            }
        }

        logger.LogInformation("DiscordUiThrottler arrêté.");
    }
}
