namespace CtrRoster.Domain.Entities;

/// <summary>
/// Représente la déclaration de présence/absence et les préférences de jeux d'un joueur.
/// </summary>
public class PlayerAvailability
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Identifiant de la session parente.
    /// </summary>
    public Guid GameSessionId { get; set; }

    /// <summary>
    /// Navigation vers la session.
    /// </summary>
    public GameSession GameSession { get; set; } = null!;

    /// <summary>
    /// Identifiant unique Discord du joueur.
    /// </summary>
    public ulong DiscordUserId { get; set; }

    /// <summary>
    /// Pseudo ou DisplayName Discord.
    /// </summary>
    public string DiscordUsername { get; set; } = string.Empty;

    /// <summary>
    /// Indique si le membre s'est déclaré explicitement absent pour cette session.
    /// </summary>
    public bool IsAbsent { get; set; }

    /// <summary>
    /// Liste des jeux souhaités stockée sous forme JSON (ex: ["Warhammer 40k", "Dune"]).
    /// </summary>
    public string PreferredGamesJson { get; set; } = "[]";

    /// <summary>
    /// Date et heure UTC de la dernière mise à jour de disponibilité.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
