using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Enums;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CtrRoster.Presentation.Modules.Autocomplete;

/// <summary>
/// Fournit l'autocomplétion des sessions de jeu orphelines (créées en base mais sans message Discord associé).
/// </summary>
public class OrphanSessionAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var guildId = context.Guild?.Id ?? 0;

        var orphanSessions = await db.GameSessions
            .Where(s => (s.GuildId == guildId || s.GuildId == 0) && s.Status == SessionStatus.Open && s.DiscordMessageId == 0)
            .OrderBy(s => s.ScheduledDate)
            .Take(25)
            .ToListAsync();

        var culture = new System.Globalization.CultureInfo("fr-FR");
        var results = orphanSessions.Select(s =>
        {
            var dateStr = s.ScheduledDate.ToString("dddd dd/MM à HH'h'mm", culture);
            // Capitaliser la première lettre (ex: "Mercredi 14/10 à 19h00")
            dateStr = char.ToUpper(dateStr[0]) + dateStr[1..];
            return new AutocompleteResult($"📅 {dateStr} (ID: {s.Id.ToString()[..8]})", s.Id.ToString());
        });

        return AutocompletionResult.FromSuccess(results);
    }
}
