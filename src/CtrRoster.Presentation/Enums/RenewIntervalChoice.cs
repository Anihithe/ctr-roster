using Discord.Interactions;

namespace CtrRoster.Presentation.Enums;

/// <summary>
/// Choix guidé pour la fréquence de renouvellement automatique des sessions.
/// </summary>
public enum RenewIntervalChoice
{
    [ChoiceDisplay("Hebdomadaire (tous les 7 jours)")]
    Weekly = 7,

    [ChoiceDisplay("Quotidien (chaque jour ouvert)")]
    Daily = 1
}
