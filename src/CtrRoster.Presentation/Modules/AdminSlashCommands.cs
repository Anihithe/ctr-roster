using System.Globalization;
using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Application.Games.Commands;
using CtrRoster.Application.Games.Queries;
using CtrRoster.Application.Sessions.Commands;
using CtrRoster.Infrastructure.Discord;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CtrRoster.Presentation.Modules;

public class AdminSlashCommands(
    CreateSessionHandler createSessionHandler,
    AddGameHandler addGameHandler,
    ToggleGameActiveHandler toggleGameActiveHandler,
    GetActiveGamesHandler getActiveGamesHandler,
    DiscordMessageRenderer renderer,
    IAppDbContext db,
    IConfiguration config,
    ILogger<AdminSlashCommands> logger) : InteractionModuleBase<SocketInteractionContext>
{
    private bool IsAdmin()
    {
        if (Context.User is not SocketGuildUser guildUser) return false;
        if (guildUser.GuildPermissions.Administrator) return true;

        var adminRoleId = config.GetValue<ulong>("Discord:AdminRoleId");
        return adminRoleId != 0 && guildUser.Roles.Any(r => r.Id == adminRoleId);
    }

    [SlashCommand("admin-session-create", "Crée et publie une nouvelle session de jeu")]
    public async Task CreateSessionAsync(
        [Summary("date_heure", "Format: yyyy-MM-dd HH:mm (ex: 2026-09-18 20:00)")] string dateTimeInput,
        [Summary("salon", "Salon où poster la Card (défaut: salon actuel)")] ITextChannel? targetChannel = null)
    {
        if (!IsAdmin())
        {
            await RespondAsync("⛔ Seuls les administrateurs peuvent exécuter cette commande.", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        if (!DateTime.TryParse(dateTimeInput, new CultureInfo("fr-FR"), DateTimeStyles.None, out var scheduledDate) &&
            !DateTime.TryParse(dateTimeInput, CultureInfo.InvariantCulture, DateTimeStyles.None, out scheduledDate))
        {
            await FollowupAsync("⚠️ Format de date invalide. Utilisez par exemple `2026-09-18 20:00`.", ephemeral: true);
            return;
        }

        ITextChannel? channel = targetChannel;
        if (channel == null)
        {
            var defaultChannelId = config.GetValue<ulong>("Discord:DefaultChannelId");
            if (defaultChannelId != 0)
            {
                channel = Context.Guild.GetTextChannel(defaultChannelId);
            }
        }
        channel ??= (ITextChannel)Context.Channel;

        try
        {
            // 1. Créer l'entité en base (qui clôture automatiquement les anciennes sessions)
            var session = await createSessionHandler.HandleAsync(scheduledDate, channel.Id);

            // 2. Générer et poster le message initial sur le salon
            var (embed, components) = renderer.BuildSessionCard(session);
            var postedMessage = await channel.SendMessageAsync(embed: embed, components: components);

            // 3. Mémoriser le DiscordMessageId
            session.DiscordMessageId = postedMessage.Id;
            await db.SaveChangesAsync();

            await FollowupAsync($"✅ Session du **{scheduledDate:dddd dd MMMM yyyy à HH:mm}** créée avec succès sur <#{channel.Id}> !", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la création de session.");
            await FollowupAsync("❌ Erreur lors de la création de la session.", ephemeral: true);
        }
    }

    [SlashCommand("admin-game-add", "Ajoute un jeu au catalogue de l'association")]
    public async Task AddGameAsync(
        [Summary("nom", "Nom du jeu")] string name,
        [Summary("min_joueurs", "Nombre minimum de joueurs")] int? minPlayers = null,
        [Summary("max_joueurs", "Nombre maximum de joueurs")] int? maxPlayers = null)
    {
        if (!IsAdmin())
        {
            await RespondAsync("⛔ Seuls les administrateurs peuvent exécuter cette commande.", ephemeral: true);
            return;
        }

        try
        {
            var game = await addGameHandler.HandleAsync(name, minPlayers, maxPlayers);
            await RespondAsync($"✅ Le jeu **{game.Name}** a été ajouté au catalogue !", ephemeral: true);
        }
        catch (Exception ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("admin-game-list", "Liste les jeux du catalogue")]
    public async Task ListGamesAsync()
    {
        var games = await getActiveGamesHandler.HandleAsync();

        if (games.Count == 0)
        {
            await RespondAsync("ℹ️ Aucun jeu n'est actuellement configuré dans le catalogue.", ephemeral: true);
            return;
        }

        var list = string.Join("\n", games.Select(g =>
            $"• **{g.Name}** ({(g.MinPlayers.HasValue ? $"{g.MinPlayers} à {g.MaxPlayers ?? 0} joueurs" : "Sans limite")})"));

        var embed = new EmbedBuilder()
            .WithTitle("🎲 Catalogue des Jeux de l'Association")
            .WithColor(Color.Blue)
            .WithDescription(list)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("admin-game-toggle", "Active ou désactive un jeu du catalogue")]
    public async Task ToggleGameAsync([Summary("nom_jeu", "Nom exact du jeu à activer/désactiver")] string gameName)
    {
        if (!IsAdmin())
        {
            await RespondAsync("⛔ Seuls les administrateurs peuvent exécuter cette commande.", ephemeral: true);
            return;
        }

        var game = await db.Games.FirstOrDefaultAsync(g => g.Name.ToLower() == gameName.ToLower());
        if (game == null)
        {
            await RespondAsync($"⚠️ Le jeu '{gameName}' est introuvable dans le catalogue.", ephemeral: true);
            return;
        }

        var newState = await toggleGameActiveHandler.HandleAsync(game.Id);
        string stateStr = newState ? "activé" : "désactivé";
        await RespondAsync($"✅ Le jeu **{game.Name}** a été {stateStr} du catalogue.", ephemeral: true);
    }
}
