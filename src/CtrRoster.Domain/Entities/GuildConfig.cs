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
    /// Intervalle de renouvellement en heures (optionnel, prioritaire sur RenewIntervalDays si renseigné).
    /// </summary>
    public int? RenewIntervalHours { get; set; }

    /// <summary>
    /// Nombre maximum de tables par défaut pour les sessions de ce serveur (null = illimité).
    /// </summary>
    public int? DefaultMaxTables { get; set; }

    /// <summary>
    /// Jours d'ouverture autorisés sous forme JSON (ex: ["Monday", "Friday", ...]).
    /// Si null ou vide, tous les jours sont considérés comme ouverts.
    /// </summary>
    public string? OpenDaysJson { get; set; }

    public TimeSpan GetRenewInterval()
    {
        if (RenewIntervalHours.HasValue && RenewIntervalHours.Value > 0)
        {
            return TimeSpan.FromHours(RenewIntervalHours.Value);
        }
        return TimeSpan.FromDays(RenewIntervalDays > 0 ? RenewIntervalDays : 7);
    }

    public List<DayOfWeek> GetOpenDays()
    {
        if (string.IsNullOrWhiteSpace(OpenDaysJson))
        {
            return GetAllDays();
        }

        try
        {
            var days = System.Text.Json.JsonSerializer.Deserialize<List<string>>(OpenDaysJson);
            if (days == null || days.Count == 0) return GetAllDays();

            var result = new List<DayOfWeek>();
            foreach (var d in days)
            {
                if (Enum.TryParse<DayOfWeek>(d, true, out var parsedDay) && !result.Contains(parsedDay))
                {
                    result.Add(parsedDay);
                }
            }
            return result.Count > 0 ? result : GetAllDays();
        }
        catch
        {
            return GetAllDays();
        }
    }

    public bool IsDayOpen(DayOfWeek day) => GetOpenDays().Contains(day);

    private static List<DayOfWeek> GetAllDays() => [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];
}
