namespace CtrRoster.Domain.Entities;

/// <summary>
/// Représente un jeu du catalogue de l'association.
/// </summary>
public class Game
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Nom officiel ou usuel du jeu (ex: Warhammer 40k, Le Seigneur des Anneaux, Catan).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Nombre minimum de joueurs recommandés.
    /// </summary>
    public int? MinPlayers { get; set; }

    /// <summary>
    /// Nombre maximum de joueurs possibles sur une table.
    /// </summary>
    public int? MaxPlayers { get; set; }

    /// <summary>
    /// Indique si le jeu est actuellement actif et proposé dans le sélecteur Discord.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
