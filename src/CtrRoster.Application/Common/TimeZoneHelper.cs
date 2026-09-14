namespace CtrRoster.Application.Common;

public static class TimeZoneHelper
{
    private static readonly TimeZoneInfo ParisZone = ResolveParisZone();

    private static TimeZoneInfo ResolveParisZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }
    }

    /// <summary>
    /// Retourne la date et l'heure locale actuelle dans le fuseau horaire de Paris.
    /// Gère automatiquement les passages heure d'été / heure d'hiver.
    /// </summary>
    public static DateTime NowParis => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ParisZone);

    /// <summary>
    /// Convertit une date UTC en heure locale de Paris.
    /// </summary>
    public static DateTime ToParisTime(DateTime utcDateTime) => TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, ParisZone);
}
