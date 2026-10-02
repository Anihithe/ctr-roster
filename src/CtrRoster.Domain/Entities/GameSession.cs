using CtrRoster.Domain.Enums;

namespace CtrRoster.Domain.Entities;

/// <summary>
/// Représente une soirée ou un après-midi de jeu organisé par l'association.
/// </summary>
public class GameSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Identifiant du serveur Discord (GuildId) organisant la session.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Date et heure UTC de création de la session.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date prévue de la session (ex: vendredi 20h).
    /// </summary>
    public DateTime ScheduledDate { get; set; }

    /// <summary>
    /// Identifiant du salon Discord où le message récapitulatif est posté.
    /// </summary>
    public ulong DiscordChannelId { get; set; }

    /// <summary>
    /// Identifiant du message Discord (la Card / l'Embed) mis à jour dynamiquement.
    /// </summary>
    public ulong DiscordMessageId { get; set; }

    /// <summary>
    /// Statut actuel de la session.
    /// </summary>
    public SessionStatus Status { get; set; } = SessionStatus.Open;

    /// <summary>
    /// Liste des déclarations de disponibilité des joueurs pour cette session.
    /// </summary>
    public List<PlayerAvailability> Availabilities { get; set; } = [];

    /// <summary>
    /// Liste des tables constituées pour cette session.
    /// </summary>
    public List<GameTable> Tables { get; set; } = [];
}
