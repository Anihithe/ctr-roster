namespace CtrRoster.Domain.Entities;

/// <summary>
/// Configuration du bot spécifique à un serveur Discord (Guild).
/// </summary>
public class GuildConfig
{
    /// <summary>
    /// Identifiant du serveur Discord (GuildId).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Rôle Discord considéré comme administrateur pour le bot (en plus des Administrateurs Discord).
    /// </summary>
    public ulong? AdminRoleId { get; set; }

    /// <summary>
    /// Salon Discord par défaut où publier les sessions de jeu.
    /// </summary>
    public ulong? DefaultChannelId { get; set; }

    /// <summary>
    /// Salon Discord exclusif autorisé pour les interactions du bot (si restriction activée).
    /// </summary>
    public ulong? AllowedChannelId { get; set; }

    /// <summary>
    /// Indique si les sessions doivent être automatiquement renouvelées pour la semaine suivante à leur clôture.
    /// </summary>
    public bool AutoRenewSessions { get; set; } = true;

    /// <summary>
    /// Nombre de jours d'intervalle pour le renouvellement automatique (7 par défaut).
    /// </summary>
    public int RenewIntervalDays { get; set; } = 7;

    /// <summary>
    /// Nombre maximum de tables par défaut pour les sessions de ce serveur (null = illimité).
    /// </summary>
    public int? DefaultMaxTables { get; set; }
}
