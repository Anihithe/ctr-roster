using System.Globalization;
using System.Text.Json;
using CtrRoster.Application.Common;
using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Application.Games.Commands;
using CtrRoster.Application.Games.Queries;
using CtrRoster.Application.Sessions.Commands;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using CtrRoster.Domain.Exceptions;
using CtrRoster.Infrastructure.Discord;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CtrRoster.Presentation.Modules;

public class CtrAdminSlashCommands(
    CreateSessionHandler createSessionHandler,
    CloseSessionHandler closeSessionHandler,
    SetSessionCapacityHandler setSessionCapacityHandler,
    AddGameHandler addGameHandler,
    RemoveGameHandler removeGameHandler,
    ToggleGameActiveHandler toggleGameActiveHandler,
    DiscordMessageRenderer renderer,
    IAppDbContext db,
    IConfiguration config,
    ILogger<CtrAdminSlashCommands> logger) : InteractionModuleBase<SocketInteractionContext>
{
    private async Task<bool> IsAdminAsync()
    {
        if (Context.User is not SocketGuildUser guildUser) return false;

        // 1. Propriétaire du serveur Discord (Server Owner) : toujours tous les droits
        if (Context.Guild != null && Context.Guild.OwnerId == guildUser.Id) return true;

        // 2. Administrateurs Discord ou gestionnaires d'événements du serveur
        if (guildUser.GuildPermissions.Administrator || guildUser.GuildPermissions.ManageGuild || guildUser.GuildPermissions.ManageEvents)
        {
            return true;
        }

        // 3. Super-Admins / Développeurs autorisés directement par leur User ID Discord (partout)
        var adminUserIds = GetConfiguredIds("Discord:AdminUserIds", "Discord:AdminUserId");
        if (adminUserIds.Contains(guildUser.Id)) return true;

        // 4. Rôle configuré dynamiquement en base de données pour ce serveur
        var guildId = Context.Guild?.Id ?? 0;
        if (guildId != 0)
        {
            var guildConfig = await db.GuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
            if (guildConfig?.AdminRoleId.HasValue == true && guildConfig.AdminRoleId.Value != 0 &&
                guildUser.Roles.Any(r => r.Id == guildConfig.AdminRoleId.Value))
            {
                return true;
            }
        }

        // 5. Rôles globaux configurés via appsettings ou .env (ex: rôles de test / staff)
        var globalAdminRoleIds = GetConfiguredIds("Discord:AdminRoleIds", "Discord:AdminRoleId");
        if (guildUser.Roles.Any(r => globalAdminRoleIds.Contains(r.Id)))
        {
            return true;
        }

        return false;
    }

    private List<ulong> GetConfiguredIds(string arrayKey, string singleKey)
    {
        var result = new List<ulong>();

        var section = config.GetSection(arrayKey);
        var children = section.GetChildren().Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        foreach (var val in children)
        {
            if (ulong.TryParse(val, out var id) && id != 0) result.Add(id);
        }

        if (!string.IsNullOrWhiteSpace(section.Value))
        {
            foreach (var part in section.Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (ulong.TryParse(part, out var id) && id != 0 && !result.Contains(id)) result.Add(id);
            }
        }

        var single = config.GetValue<ulong>(singleKey);
        if (single != 0 && !result.Contains(single)) result.Add(single);

        return result;
    }

    [SlashCommand("ctr-config", "Affiche ou configure les paramètres du bot pour ce serveur")]
    public async Task ConfigAsync(
        [Summary("salon_sessions", "Salon par défaut où publier les sessions de jeu")] ITextChannel? channel = null,
        [Summary("salon_restreint", "Restreindre l'utilisation du bot à ce salon uniquement")] ITextChannel? allowedChannel = null,
        [Summary("role_admin", "Rôle administrateur pour gérer le bot")] IRole? adminRole = null,
        [Summary("auto_renouvellement", "Activer ou désactiver l'ouverture auto de la session suivante")] bool? autoRenew = null,
        [Summary("reset_restriction_salon", "Supprimer la restriction de salon")] bool resetAllowedChannel = false,
        [Summary("max_tables_defaut", "Nombre maximum de tables par défaut pour les sessions (vide = illimité)")] int? defaultMaxTables = null,
        [Summary("reset_max_tables", "Supprimer la limite de tables par défaut")] bool resetDefaultMaxTables = false,
        [Summary("jours_ouverture", "Jours d'ouverture séparés par virgules (ex: mardi,mercredi,jeudi,vendredi,samedi)")] string? openDays = null,
        [Summary("reset_jours_ouverture", "Réinitialiser les jours d'ouverture (tous les jours ouverts)")] bool resetOpenDays = false,
        [Summary("intervalle_jours", "Intervalle de renouvellement en jours (ex: 7 pour hebdo, 1 pour quotidien)")] int? renewDays = null,
        [Summary("intervalle_heures", "Intervalle de renouvellement en heures (ex: 24 pour journalier)")] int? renewHours = null)
    {
        await DeferAsync(ephemeral: true);

        if (!await IsAdminAsync())
        {
            await FollowupAsync("⛔ Seuls les administrateurs peuvent exécuter cette commande.", ephemeral: true);
            return;
        }

        var guildId = Context.Guild.Id;
        var guildConfig = await db.GuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);

        if (guildConfig == null)
        {
            guildConfig = new GuildConfig { GuildId = guildId };
            db.GuildConfigs.Add(guildConfig);
        }

        bool hasChanges = false;
        if (channel != null)
        {
            guildConfig.DefaultChannelId = channel.Id;
            hasChanges = true;
        }

        if (allowedChannel != null)
        {
            guildConfig.AllowedChannelId = allowedChannel.Id;
            hasChanges = true;
        }
        else if (resetAllowedChannel)
        {
            guildConfig.AllowedChannelId = null;
            hasChanges = true;
        }

        if (adminRole != null)
        {
            guildConfig.AdminRoleId = adminRole.Id;
            hasChanges = true;
        }

        if (autoRenew.HasValue)
        {
            guildConfig.AutoRenewSessions = autoRenew.Value;
            hasChanges = true;
        }

        if (defaultMaxTables.HasValue)
        {
            if (defaultMaxTables.Value < 1)
            {
                await FollowupAsync("⚠️ La limite par défaut de tables doit être au minimum de 1.", ephemeral: true);
                return;
            }
            guildConfig.DefaultMaxTables = defaultMaxTables.Value;
            hasChanges = true;
        }
        else if (resetDefaultMaxTables)
        {
            guildConfig.DefaultMaxTables = null;
            hasChanges = true;
        }

        if (!string.IsNullOrWhiteSpace(openDays))
        {
            var parsedDays = DayParser.ParseDays(openDays);
            if (parsedDays.Count == 0)
            {
                await FollowupAsync("⚠️ Aucun jour valide reconnu dans la liste fournie. Ex: `mardi,mercredi,jeudi,vendredi,samedi`", ephemeral: true);
                return;
            }
            guildConfig.OpenDaysJson = JsonSerializer.Serialize(parsedDays.Select(d => d.ToString()).ToList());
            hasChanges = true;
        }
        else if (resetOpenDays)
        {
            guildConfig.OpenDaysJson = null;
            hasChanges = true;
        }

        if (renewHours.HasValue)
        {
            if (renewHours.Value < 1)
            {
                await FollowupAsync("⚠️ L'intervalle en heures doit être au minimum de 1 heure.", ephemeral: true);
                return;
            }
            guildConfig.RenewIntervalHours = renewHours.Value;
            hasChanges = true;
        }

        if (renewDays.HasValue)
        {
            if (renewDays.Value < 1)
            {
                await FollowupAsync("⚠️ L'intervalle en jours doit être au minimum de 1 jour.", ephemeral: true);
                return;
            }
            guildConfig.RenewIntervalDays = renewDays.Value;
            if (!renewHours.HasValue)
            {
                guildConfig.RenewIntervalHours = null;
            }
            hasChanges = true;
        }

        if (hasChanges)
        {
            await db.SaveChangesAsync();
        }

        var currentChannelStr = guildConfig.DefaultChannelId.HasValue && guildConfig.DefaultChannelId.Value != 0
            ? $"<#{guildConfig.DefaultChannelId.Value}>"
            : (config.GetValue<ulong>("Discord:DefaultChannelId") != 0 ? $"<#{config.GetValue<ulong>("Discord:DefaultChannelId")}> *(config globale)*" : "Non configuré");

        var allowedChannelStr = guildConfig.AllowedChannelId.HasValue && guildConfig.AllowedChannelId.Value != 0
            ? $"<#{guildConfig.AllowedChannelId.Value}>"
            : "Aucun (tous les salons)";

        var currentRoleStr = guildConfig.AdminRoleId.HasValue && guildConfig.AdminRoleId.Value != 0
            ? $"<@&{guildConfig.AdminRoleId.Value}>"
            : (config.GetValue<ulong>("Discord:AdminRoleId") != 0 ? $"<@&{config.GetValue<ulong>("Discord:AdminRoleId")}> *(config globale)*" : "Non configuré (Admins Discord)");

        string renewIntervalDesc = guildConfig.RenewIntervalHours.HasValue
            ? $"{guildConfig.RenewIntervalHours.Value} heure(s)"
            : $"{guildConfig.RenewIntervalDays} jour(s)";

        var renewStr = guildConfig.AutoRenewSessions
            ? $"🟢 Activé (tous les {renewIntervalDesc} à échéance)"
            : "⚪ Désactivé";

        var openDaysStr = string.IsNullOrWhiteSpace(guildConfig.OpenDaysJson)
            ? "Tous les jours"
            : DayParser.FormatDaysFrench(guildConfig.GetOpenDays());

        var maxTablesStr = guildConfig.DefaultMaxTables.HasValue
            ? $"{guildConfig.DefaultMaxTables.Value} table(s) max"
            : "Illimité";

        var embed = new EmbedBuilder()
            .WithTitle("⚙️ Configuration CTR-Roster")
            .WithColor(hasChanges ? Color.Green : Color.Blue)
            .WithDescription(hasChanges ? "✅ **Paramètres mis à jour avec succès !**" : "ℹ️ **Configuration actuelle du serveur :**")
            .AddField("Salon des sessions", currentChannelStr, inline: true)
            .AddField("Salon restreint", allowedChannelStr, inline: true)
            .AddField("Rôle Admin", currentRoleStr, inline: true)
            .AddField("Renouvellement auto", renewStr, inline: true)
            .AddField("Jours d'ouverture", openDaysStr, inline: true)
            .AddField("Tables max par défaut", maxTablesStr, inline: true)
            .WithFooter("Pour modifier : /ctr-config [options]")
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("ctr-session-create", "Crée et publie une nouvelle session de jeu")]
    public async Task CreateSessionAsync(
        [Summary("date", "Date de la session (ex: 2026-09-18 ou 18/09/2026)")] string dateInput,
        [Summary("heure", "Heure de début (ex: 20:00 ou 20h00)")] string timeInput,
        [Summary("salon", "Salon où poster la Card (défaut: salon configuré ou actuel)")] ITextChannel? targetChannel = null,
        [Summary("max_tables", "Nombre maximum de tables pour cette session (défaut: valeur serveur ou illimité)")] int? maxTables = null,
        [Summary("forcer", "Forcer la création même si le jour est configuré comme fermé")] bool force = false)
    {
        if (!await IsAdminAsync())
        {
            await RespondAsync("⛔ Seuls les administrateurs peuvent exécuter cette commande.", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        if (!TryParseDateTime(dateInput, timeInput, out var scheduledDate))
        {
            await FollowupAsync("⚠️ Format de date ou d'heure invalide.\n• Exemples de date : `2026-09-18` ou `18/09/2026`\n• Exemples d'heure : `20:00` ou `20h00`", ephemeral: true);
            return;
        }

        ITextChannel? channel = targetChannel;
        if (channel == null)
        {
            var guildConfig = await db.GuildConfigs.FirstOrDefaultAsync(c => c.GuildId == Context.Guild.Id);
            var defaultChannelId = guildConfig?.DefaultChannelId ?? config.GetValue<ulong>("Discord:DefaultChannelId");
            if (defaultChannelId != 0)
            {
                channel = Context.Guild.GetTextChannel(defaultChannelId);
            }
        }
        channel ??= (ITextChannel)Context.Channel;

        try
        {
            // 1. Créer l'entité en base (avec vérification des doublons de créneau et des jours d'ouverture)
            var session = await createSessionHandler.HandleAsync(scheduledDate, channel.Id, Context.Guild.Id, maxTables, force);

            // 2. Générer et poster le message initial sur le salon
            var (embed, components) = renderer.BuildSessionCard(session);
            var postedMessage = await channel.SendMessageAsync(embed: embed, components: components);

            // 3. Mémoriser le DiscordMessageId
            session.DiscordMessageId = postedMessage.Id;
            await db.SaveChangesAsync();

            string capStr = session.MaxTables.HasValue ? $" • Capacité : **{session.MaxTables.Value} tables max**" : "";
            await FollowupAsync($"✅ Session du **{scheduledDate:dddd dd MMMM yyyy à HH:mm}** créée avec succès sur <#{channel.Id}>{capStr} !", ephemeral: true);
        }
        catch (DomainException ex)
        {
            await FollowupAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la création de session.");
            await FollowupAsync("❌ Erreur lors de la création de la session.", ephemeral: true);
        }
    }

    [SlashCommand("ctr-session-capacity", "Définit ou modifie le nombre maximum de tables pour une session")]
    public async Task SetSessionCapacityAsync(
        [Summary("max_tables", "Nombre maximum de tables (0 ou vide pour illimité)")] int? maxTables = null,
        [Summary("session_id", "ID de la session (optionnel : prend la session active du salon par défaut)")] string? sessionIdStr = null)
    {
        await DeferAsync(ephemeral: true);

        if (!await IsAdminAsync())
        {
            await FollowupAsync("⛔ Seuls les administrateurs peuvent exécuter cette commande.", ephemeral: true);
            return;
        }

        Guid targetSessionId = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(sessionIdStr) && Guid.TryParse(sessionIdStr, out var parsedGuid))
        {
            targetSessionId = parsedGuid;
        }
        else
        {
            var channelId = Context.Channel.Id;
            var guildId = Context.Guild.Id;
            var activeSessions = await db.GameSessions
                .Where(s => s.DiscordChannelId == channelId && (s.GuildId == guildId || s.GuildId == 0) && s.Status == SessionStatus.Open)
                .OrderBy(s => s.ScheduledDate)
                .ToListAsync();

            if (activeSessions.Count == 0)
            {
                await FollowupAsync("⚠️ Aucune session active n'a été trouvée sur ce salon. Spécifie l'ID de la session via le paramètre `session_id`.", ephemeral: true);
                return;
            }

            if (activeSessions.Count > 1)
            {
                var list = string.Join("\n", activeSessions.Select(s => $"• {s.ScheduledDate:dddd dd/MM à HH:mm} (`{s.Id}`)"));
                await FollowupAsync($"⚠️ Plusieurs sessions sont actives sur ce salon. Spécifie le paramètre `session_id` parmi :\n{list}", ephemeral: true);
                return;
            }

            targetSessionId = activeSessions[0].Id;
        }

        try
        {
            int? effectiveCap = (maxTables.HasValue && maxTables.Value > 0) ? maxTables.Value : null;
            var updatedSession = await setSessionCapacityHandler.HandleAsync(targetSessionId, effectiveCap);

            string capStr = updatedSession.MaxTables.HasValue ? $"{updatedSession.MaxTables.Value} table(s) maximum" : "Illimitée";
            await FollowupAsync($"✅ Capacité de la session du **{updatedSession.ScheduledDate:dddd dd MMMM yyyy à HH:mm}** mise à jour : **{capStr}** !", ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("ctr-session-close", "Clôture manuellement une session de jeu active")]
    public async Task CloseSessionAsync(
        [Summary("date", "Date de la session à clôturer (ex: 2026-10-09 ou 09/10/2026)")] string? dateInput = null,
        [Summary("heure", "Heure de la session si plusieurs le même jour (ex: 20:00 ou 20h00)")] string? timeInput = null,
        [Summary("session_id", "ID spécifique de la session à clôturer")] string? sessionIdStr = null)
    {
        await DeferAsync(ephemeral: true);

        if (!await IsAdminAsync())
        {
            await FollowupAsync("⛔ Seuls les administrateurs peuvent exécuter cette commande.", ephemeral: true);
            return;
        }

        Guid targetSessionId = Guid.Empty;

        if (!string.IsNullOrWhiteSpace(sessionIdStr))
        {
            if (!Guid.TryParse(sessionIdStr.Trim(), out var parsedGuid))
            {
                await FollowupAsync("⚠️ Format de `session_id` invalide (attendu: format GUID/UUID).", ephemeral: true);
                return;
            }
            targetSessionId = parsedGuid;
        }
        else if (!string.IsNullOrWhiteSpace(dateInput))
        {
            DateTime targetDate;
            bool hasTime = !string.IsNullOrWhiteSpace(timeInput);
            if (hasTime)
            {
                if (!TryParseDateTime(dateInput, timeInput!, out targetDate))
                {
                    await FollowupAsync("⚠️ Format de date ou d'heure invalide.\n• Exemples de date : `2026-10-09` ou `09/10/2026`\n• Exemples d'heure : `20:00` ou `20h00`", ephemeral: true);
                    return;
                }
            }
            else
            {
                if (!TryParseDate(dateInput, out targetDate))
                {
                    await FollowupAsync("⚠️ Format de date invalide.\n• Exemples : `2026-10-09` ou `09/10/2026`", ephemeral: true);
                    return;
                }
            }

            var channelId = Context.Channel.Id;
            var guildId = Context.Guild.Id;

            var query = db.GameSessions
                .Where(s => s.DiscordChannelId == channelId && (s.GuildId == guildId || s.GuildId == 0) && s.Status == SessionStatus.Open);

            List<GameSession> candidates;
            if (hasTime)
            {
                candidates = await query.Where(s => s.ScheduledDate == targetDate).ToListAsync();
            }
            else
            {
                candidates = await query.Where(s => s.ScheduledDate.Date == targetDate.Date).ToListAsync();
            }

            if (candidates.Count == 0)
            {
                await FollowupAsync($"⚠️ Aucune session active trouvée sur ce salon pour le {targetDate:dddd dd MMMM yyyy}.", ephemeral: true);
                return;
            }

            if (candidates.Count > 1)
            {
                var list = string.Join("\n", candidates.Select(s => $"• {s.ScheduledDate:dddd dd/MM/yyyy à HH:mm} (ID: `{s.Id}`)"));
                await FollowupAsync($"⚠️ Plusieurs sessions actives correspondent à cette date :\n{list}\nPrécise l'heure ou le paramètre `session_id`.", ephemeral: true);
                return;
            }

            targetSessionId = candidates[0].Id;
        }
        else
        {
            var channelId = Context.Channel.Id;
            var guildId = Context.Guild.Id;
            var activeSessions = await db.GameSessions
                .Where(s => s.DiscordChannelId == channelId && (s.GuildId == guildId || s.GuildId == 0) && s.Status == SessionStatus.Open)
                .OrderBy(s => s.ScheduledDate)
                .ToListAsync();

            if (activeSessions.Count == 0)
            {
                await FollowupAsync("⚠️ Aucune session active n'a été trouvée sur ce salon. Spécifie la date ou l'ID via `session_id`.", ephemeral: true);
                return;
            }

            if (activeSessions.Count > 1)
            {
                var list = string.Join("\n", activeSessions.Select(s => $"• {s.ScheduledDate:dddd dd/MM/yyyy à HH:mm} (ID: `{s.Id}`)"));
                await FollowupAsync($"⚠️ Plusieurs sessions sont actives sur ce salon :\n{list}\nPrécise la date ou le paramètre `session_id` pour indiquer laquelle clôturer.", ephemeral: true);
                return;
            }

            targetSessionId = activeSessions[0].Id;
        }

        try
        {
            var closedSession = await closeSessionHandler.HandleAsync(targetSessionId);
            await FollowupAsync($"✅ La session du **{closedSession.ScheduledDate:dddd dd MMMM yyyy à HH:mm}** a été clôturée avec succès !", ephemeral: true);
        }
        catch (DomainException ex)
        {
            await FollowupAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la clôture de session.");
            await FollowupAsync("❌ Erreur lors de la clôture de la session.", ephemeral: true);
        }
    }

    private static bool TryParseDate(string dateStr, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(dateStr)) return false;

        string[] formats =
        [
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd-MM-yyyy",
            "d-M-yyyy"
        ];

        if (DateTime.TryParseExact(dateStr.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        if (DateTime.TryParse(dateStr.Trim(), new CultureInfo("fr-FR"), DateTimeStyles.None, out result))
            return true;

        if (DateTime.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        return false;
    }

    private static bool TryParseDateTime(string dateStr, string timeStr, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(dateStr) || string.IsNullOrWhiteSpace(timeStr))
            return false;

        var cleanTime = timeStr.Trim().ToLowerInvariant().Replace('h', ':').Replace('H', ':');
        if (cleanTime.EndsWith(':')) cleanTime += "00";
        if (!cleanTime.Contains(':')) cleanTime += ":00";

        var cleanDate = dateStr.Trim();
        var combined = $"{cleanDate} {cleanTime}";

        string[] formats =
        [
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd H:mm",
            "yyyy/MM/dd HH:mm",
            "yyyy/MM/dd H:mm",
            "dd/MM/yyyy HH:mm",
            "dd/MM/yyyy H:mm",
            "d/M/yyyy HH:mm",
            "d/M/yyyy H:mm",
            "dd-MM-yyyy HH:mm",
            "dd-MM-yyyy H:mm",
            "d-M-yyyy HH:mm",
            "d-M-yyyy H:mm"
        ];

        if (DateTime.TryParseExact(combined, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        if (DateTime.TryParse(combined, new CultureInfo("fr-FR"), DateTimeStyles.None, out result))
            return true;

        if (DateTime.TryParse(combined, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        return false;
    }

    [SlashCommand("ctr-game-add", "Ajoute un jeu au catalogue de l'association")]
    public async Task AddGameAsync(
        [Summary("nom", "Nom du jeu")] string name,
        [Summary("min_joueurs", "Nombre minimum de joueurs")] int? minPlayers = null,
        [Summary("max_joueurs", "Nombre maximum de joueurs")] int? maxPlayers = null)
    {
        await DeferAsync(ephemeral: true);

        if (!await IsAdminAsync())
        {
            await FollowupAsync("⛔ Seuls les administrateurs peuvent exécuter cette commande.", ephemeral: true);
            return;
        }

        try
        {
            var game = await addGameHandler.HandleAsync(Context.Guild.Id, name, minPlayers, maxPlayers);
            await FollowupAsync($"✅ Le jeu **{game.Name}** a été ajouté au catalogue !", ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("ctr-game-remove", "Supprime un jeu du catalogue de l'association")]
    public async Task RemoveGameAsync([Summary("nom", "Nom exact du jeu à supprimer")] string name)
    {
        await DeferAsync(ephemeral: true);

        if (!await IsAdminAsync())
        {
            await FollowupAsync("⛔ Seuls les administrateurs peuvent exécuter cette commande.", ephemeral: true);
            return;
        }

        try
        {
            var game = await removeGameHandler.HandleAsync(Context.Guild.Id, name);
            await FollowupAsync($"🗑️ Le jeu **{game.Name}** a été supprimé du catalogue !", ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("ctr-game-list", "Liste les jeux du catalogue")]
    public async Task ListGamesAsync()
    {
        await DeferAsync(ephemeral: true);

        var guildId = Context.Guild.Id;
        var games = await db.Games
            .Where(g => g.GuildId == guildId)
            .OrderBy(g => g.Name)
            .ToListAsync();

        if (games.Count == 0)
        {
            await FollowupAsync("ℹ️ Aucun jeu n'est actuellement configuré dans le catalogue.", ephemeral: true);
            return;
        }

        var list = string.Join("\n", games.Select(g =>
        {
            var status = g.IsActive ? "🟢" : "⚪ *[Désactivé]*";
            var players = g.MinPlayers.HasValue ? $"{g.MinPlayers} à {g.MaxPlayers ?? 0} joueurs" : "Sans limite";
            return $"{status} **{g.Name}** ({players})";
        }));

        var embed = new EmbedBuilder()
            .WithTitle("🎲 Catalogue des Jeux de l'Association")
            .WithColor(Color.Blue)
            .WithDescription(list)
            .WithFooter("🟢 Actif (disponible dans les sélecteurs) | ⚪ Désactivé")
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("ctr-game-toggle", "Active ou désactive un jeu du catalogue")]
    public async Task ToggleGameAsync([Summary("nom_jeu", "Nom exact du jeu à activer/désactiver")] string gameName)
    {
        await DeferAsync(ephemeral: true);

        if (!await IsAdminAsync())
        {
            await FollowupAsync("⛔ Seuls les administrateurs peuvent exécuter cette commande.", ephemeral: true);
            return;
        }

        var guildId = Context.Guild.Id;
        var game = await db.Games.FirstOrDefaultAsync(g => g.GuildId == guildId && g.Name.ToLower() == gameName.ToLower());
        if (game == null)
        {
            await FollowupAsync($"⚠️ Le jeu '{gameName}' est introuvable dans le catalogue.", ephemeral: true);
            return;
        }

        var newState = await toggleGameActiveHandler.HandleAsync(guildId, game.Id);
        string stateStr = newState ? "activé" : "désactivé";
        await FollowupAsync($"✅ Le jeu **{game.Name}** a été {stateStr} du catalogue.", ephemeral: true);
    }

    [SlashCommand("ctr-game-claim-legacy", "Rattache tous les jeux du catalogue sans serveur (historiques) à ce serveur")]
    public async Task ClaimLegacyGamesAsync()
    {
        await DeferAsync(ephemeral: true);

        if (!await IsAdminAsync())
        {
            await FollowupAsync("⛔ Seuls les administrateurs peuvent exécuter cette commande.", ephemeral: true);
            return;
        }

        var guildId = Context.Guild.Id;
        var legacyGames = await db.Games.Where(g => g.GuildId == 0).ToListAsync();
        if (legacyGames.Count == 0)
        {
            await FollowupAsync("ℹ️ Aucun jeu historique non rattaché (GuildId = 0) n'a été trouvé en base.", ephemeral: true);
            return;
        }

        foreach (var g in legacyGames)
        {
            g.GuildId = guildId;
        }
        await db.SaveChangesAsync();

        await FollowupAsync($"✅ **{legacyGames.Count} jeu(x) historique(s)** ont été rattachés avec succès à ce serveur (**{Context.Guild.Name}**) !", ephemeral: true);
    }
}
