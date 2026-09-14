namespace CtrRoster.Domain.Enums;

/// <summary>
/// Rôle d'un participant autour d'une table de jeu.
/// </summary>
public enum ParticipantRole
{
    /// <summary>
    /// Joueur actif participant à la partie.
    /// </summary>
    Player = 1,

    /// <summary>
    /// Spectateur / Observateur rattaché à la table.
    /// </summary>
    Spectator = 2
}
