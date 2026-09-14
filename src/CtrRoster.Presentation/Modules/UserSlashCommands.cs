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
        var channelId = Context.Channel.Id;
        var session = await db.GameSessions
            .Include(s => s.Tables)
            .Include(s => s.Availabilities)
            .FirstOrDefaultAsync(s => s.DiscordChannelId == channelId && s.Status == SessionStatus.Open);

        if (session == null)
        {
            await RespondAsync("ℹ️ Aucune session active n'est ouverte sur ce salon.", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🎲 Information Session CTR-Roster")
            .WithColor(Color.Green)
            .WithDescription($"Session prévue pour le **{session.ScheduledDate:dddd dd MMMM yyyy à HH:mm}**.")
            .AddField("Statut", "🟢 Inscriptions ouvertes", inline: true)
            .AddField("Tables formées", $"{session.Tables.Count} table(s)", inline: true)
            .AddField("Disponibilités", $"{session.Availabilities.Count(a => !a.IsAbsent)} joueur(s) en attente", inline: true)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }
}
