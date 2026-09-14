using CtrRoster.Domain.Enums;

namespace CtrRoster.Domain.Entities;

/// <summary>
/// Représente l'inscription d'un membre à une table donnée.
/// </summary>
public class TableParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Identifiant de la table de jeu.
    /// </summary>
    public Guid GameTableId { get; set; }

    /// <summary>
    /// Navigation vers la table.
    /// </summary>
    public GameTable GameTable { get; set; } = null!;

    /// <summary>
    /// Identifiant unique Discord de l'utilisateur.
    /// </summary>
    public ulong DiscordUserId { get; set; }

    /// <summary>
    /// Pseudo ou DisplayName Discord du membre au moment de l'inscription.
    /// </summary>
    public string DiscordUsername { get; set; } = string.Empty;

    /// <summary>
    /// Rôle du membre au sein de la table (Joueur ou Observateur).
    /// </summary>
    public ParticipantRole Role { get; set; } = ParticipantRole.Player;

    /// <summary>
    /// Date et heure UTC d'inscription à la table.
    /// </summary>
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
