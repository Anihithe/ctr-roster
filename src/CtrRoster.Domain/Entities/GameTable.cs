namespace CtrRoster.Domain.Entities;

/// <summary>
/// Représente une table physique de jeu constituée au cours d'une session.
/// </summary>
public class GameTable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Identifiant de la session parente.
    /// </summary>
    public Guid GameSessionId { get; set; }

    /// <summary>
    /// Navigation vers la session parente.
    /// </summary>
    public GameSession GameSession { get; set; } = null!;

    /// <summary>
    /// Nom du jeu joué sur cette table (soit issu du catalogue, soit saisie libre « Autre »).
    /// </summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>
    /// Référence optionnelle vers le jeu du catalogue si applicable.
    /// </summary>
    public Guid? GameId { get; set; }

    /// <summary>
    /// Identifiant Discord du membre ayant créé la table.
    /// </summary>
    public ulong CreatedByDiscordUserId { get; set; }

    /// <summary>
    /// Date et heure UTC de création de la table.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Participants inscrits à cette table (joueurs et observateurs).
    /// </summary>
    public List<TableParticipant> Participants { get; set; } = [];
}
