namespace CtrRoster.Application.Common.Interfaces;

/// <summary>
/// Contrat pour la mise à jour asynchrone et temporisée (throttled) de la Card Discord d'une session.
/// </summary>
public interface IDiscordMessageRenderer
{
    /// <summary>
    /// Enfile une demande de rafraîchissement visuel pour la session spécifiée.
    /// Les demandes multiples pour une même session sont regroupées (débouncing anti-429).
    /// </summary>
    Task QueueMessageUpdateAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
