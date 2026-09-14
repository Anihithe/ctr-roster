namespace CtrRoster.Domain.Enums;

/// <summary>
/// Statut du cycle de vie d'une session de jeu.
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// Inscriptions ouvertes, modifications de disponibilités et de tables permises.
    /// </summary>
    Open = 1,

    /// <summary>
    /// Inscriptions verrouillées (ex. quelques heures avant la soirée).
    /// </summary>
    Locked = 2,

    /// <summary>
    /// Session terminée et archivée.
    /// </summary>
    Closed = 3
}
