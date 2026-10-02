using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Enums;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace CtrRoster.Presentation.Modules;

public class UserSlashCommands(IAppDbContext db) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ctr-info", "Affiche les informations sur la session en cours")]
    public async Task RosterInfoAsync()
    {
        await DeferAsync(ephemeral: true);

        var channelId = Context.Channel.Id;
        var guildId = Context.Guild?.Id ?? 0;

        if (guildId != 0)
        {
            var config = await db.GuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
            if (config?.AllowedChannelId.HasValue == true && config.AllowedChannelId.Value != 0 && config.AllowedChannelId.Value != channelId)
            {
                await FollowupAsync($"⛔ Cette commande ne peut être utilisée que dans le salon <#{config.AllowedChannelId.Value}>.", ephemeral: true);
                return;
            }
        }

        var session = await db.GameSessions
            .Include(s => s.Tables)
            .Include(s => s.Availabilities)
            .FirstOrDefaultAsync(s => (guildId == 0 || s.GuildId == guildId || s.GuildId == 0) && s.DiscordChannelId == channelId && s.Status == SessionStatus.Open);

        if (session == null)
        {
            await FollowupAsync("ℹ️ Aucune session active n'est ouverte sur ce salon.", ephemeral: true);
            return;
        }

        var tablesCountStr = session.MaxTables.HasValue
            ? $"{session.Tables.Count}/{session.MaxTables.Value} table(s)"
            : $"{session.Tables.Count} table(s)";

        var embed = new EmbedBuilder()
            .WithTitle("🎲 Information Session CTR-Roster")
            .WithColor(Color.Green)
            .WithDescription($"Session prévue pour le **{session.ScheduledDate:dddd dd MMMM yyyy à HH:mm}**.")
            .AddField("Statut", "🟢 Inscriptions ouvertes", inline: true)
            .AddField("Tables formées", tablesCountStr, inline: true)
            .AddField("Disponibilités", $"{session.Availabilities.Count(a => !a.IsAbsent)} joueur(s) en attente", inline: true)
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true);
    }
}
