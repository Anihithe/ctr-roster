namespace CtrRoster.Application.Common;

public static class DayParser
{
    private static readonly Dictionary<string, DayOfWeek> DayAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Français
        { "lundi", DayOfWeek.Monday },
        { "lun", DayOfWeek.Monday },
        { "mardi", DayOfWeek.Tuesday },
        { "mar", DayOfWeek.Tuesday },
        { "mercredi", DayOfWeek.Wednesday },
        { "mer", DayOfWeek.Wednesday },
        { "jeudi", DayOfWeek.Thursday },
        { "jeu", DayOfWeek.Thursday },
        { "vendredi", DayOfWeek.Friday },
        { "ven", DayOfWeek.Friday },
        { "samedi", DayOfWeek.Saturday },
        { "sam", DayOfWeek.Saturday },
        { "dimanche", DayOfWeek.Sunday },
        { "dim", DayOfWeek.Sunday },

        // Anglais
        { "monday", DayOfWeek.Monday },
        { "mon", DayOfWeek.Monday },
        { "tuesday", DayOfWeek.Tuesday },
        { "tue", DayOfWeek.Tuesday },
        { "wednesday", DayOfWeek.Wednesday },
        { "wed", DayOfWeek.Wednesday },
        { "thursday", DayOfWeek.Thursday },
        { "thu", DayOfWeek.Thursday },
        { "friday", DayOfWeek.Friday },
        { "fri", DayOfWeek.Friday },
        { "saturday", DayOfWeek.Saturday },
        { "sat", DayOfWeek.Saturday },
        { "sunday", DayOfWeek.Sunday },
        { "sun", DayOfWeek.Sunday }
    };

    public static List<DayOfWeek> ParseDays(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];

        var tokens = input.Split(new[] { ',', ';', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<DayOfWeek>();

        foreach (var token in tokens)
        {
            if (DayAliases.TryGetValue(token, out var day) && !result.Contains(day))
            {
                result.Add(day);
            }
        }

        return result;
    }

    public static string ToFrenchName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Lundi",
        DayOfWeek.Tuesday => "Mardi",
        DayOfWeek.Wednesday => "Mercredi",
        DayOfWeek.Thursday => "Jeudi",
        DayOfWeek.Friday => "Vendredi",
        DayOfWeek.Saturday => "Samedi",
        DayOfWeek.Sunday => "Dimanche",
        _ => day.ToString()
    };

    public static string FormatDaysFrench(IEnumerable<DayOfWeek> days)
    {
        // Tri dans l'ordre naturel de la semaine : Lundi -> Dimanche
        var ordered = days.OrderBy(d => d == DayOfWeek.Sunday ? 7 : (int)d).ToList();
        return ordered.Count > 0
            ? string.Join(", ", ordered.Select(ToFrenchName))
            : "Tous les jours";
    }
}
