using System.Text.Json;
using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CtrRoster.Infrastructure.Discord;

public class DiscordMessageRenderer(
    DiscordSocketClient discordClient,
    IAppDbContext db,
    ILogger<DiscordMessageRenderer> logger)
{
    public async Task RenderSessionMessageAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.GameSessions
            .Include(s => s.Tables)
                .ThenInclude(t => t.Participants)
            .Include(s => s.Availabilities)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null)
        {
            logger.LogWarning("Session {SessionId} introuvable pour le rendu.", sessionId);
            return;
        }

        if (session.DiscordChannelId == 0 || session.DiscordMessageId == 0)
        {
            logger.LogDebug("Session {SessionId} n'a pas encore de message Discord associé.", sessionId);
            return;
        }

        IMessageChannel? channel = discordClient.GetChannel(session.DiscordChannelId) as IMessageChannel;
        if (channel == null)
        {
            channel = await discordClient.Rest.GetChannelAsync(session.DiscordChannelId) as IMessageChannel;
        }

        if (channel == null)
        {
            logger.LogWarning("Salon Discord {ChannelId} introuvable.", session.DiscordChannelId);
            return;
        }

        var message = await channel.GetMessageAsync(session.DiscordMessageId) as IUserMessage;
        if (message == null)
        {
            logger.LogWarning("Message Discord {MessageId} introuvable sur le salon {ChannelId}.", session.DiscordMessageId, session.DiscordChannelId);
            return;
        }

        var (embed, components) = BuildSessionCard(session);

        await message.ModifyAsync(msg =>
        {
            msg.Embed = embed;
            msg.Embeds = new[] { embed };
            msg.Components = components;
        });

        logger.LogInformation("Card Discord de la session {SessionId} actualisée avec succès.", sessionId);
    }

    public (Embed Embed, MessageComponent Components) BuildSessionCard(GameSession session)
    {
        var dateFormatted = session.ScheduledDate.ToString("dddd dd MMMM yyyy à HH'h'mm", new System.Globalization.CultureInfo("fr-FR"));
        var titleDate = char.ToUpper(dateFormatted[0]) + dateFormatted[1..];

        var embedBuilder = new EmbedBuilder()
            .WithTitle($"🎲 SESSION DE JEU DU {titleDate.ToUpper()}")
            .WithColor(session.Status switch
            {
                SessionStatus.Open => Color.Green,
                SessionStatus.Locked => Color.Orange,
                _ => Color.DarkGrey
            })
            .WithFooter($"Session ID: {session.Id} • Mis à jour à {DateTime.UtcNow:HH:mm:ss} UTC");

        string statusText = session.Status switch
        {
            SessionStatus.Open => "🟢 **Inscriptions ouvertes**",
            SessionStatus.Locked => "🔒 **Inscriptions verrouillées**",
            _ => "🔴 **Session clôturée**"
        };
        embedBuilder.WithDescription(statusText);

        // 1. DISPONIBILITÉS (Non assignés)
        var assignedUserIds = session.Tables
            .SelectMany(t => t.Participants)
            .Select(p => p.DiscordUserId)
            .ToHashSet();

        var nonAssignedAvailable = session.Availabilities
            .Where(a => !a.IsAbsent && !assignedUserIds.Contains(a.DiscordUserId))
            .OrderBy(a => a.DiscordUsername)
            .ToList();

        if (nonAssignedAvailable.Count > 0)
        {
            var lines = nonAssignedAvailable.Select(a =>
            {
                var games = ParseGames(a.PreferredGamesJson);
                var gamesStr = games.Count > 0 ? string.Join(", ", games) : "Tous jeux";
                return $"• <@{a.DiscordUserId}> : *{gamesStr}*";
            });

            embedBuilder.AddField("📋 DISPONIBILITÉS (Non assignés)", string.Join("\n", lines), inline: false);
        }
        else
        {
            embedBuilder.AddField("📋 DISPONIBILITÉS (Non assignés)", "*Aucun joueur en attente pour le moment.*", inline: false);
        }

        // 2. TABLES FORMÉES (Tri stable et identique entre l'Embed et le Menu déroulant)
        var sortedTables = session.Tables
            .OrderBy(t => t.CreatedAtUtc)
            .ThenBy(t => t.Id)
            .ToList();

        if (sortedTables.Count > 0)
        {
            var tableDetails = new List<string>();
            int index = 1;

            foreach (var table in sortedTables)
            {
                var players = table.Participants.Where(p => p.Role == ParticipantRole.Player).ToList();
                var spectators = table.Participants.Where(p => p.Role == ParticipantRole.Spectator).ToList();

                var playersStr = players.Count > 0
                    ? string.Join(", ", players.Select(p => $"<@{p.DiscordUserId}>"))
                    : "Aucun";

                var block = $"**Table {index} : {table.GameName}**\n" +
                            $"└ **Joueurs ({players.Count})** : {playersStr}";

                if (spectators.Count > 0)
                {
                    var specsStr = string.Join(", ", spectators.Select(p => $"<@{p.DiscordUserId}>"));
                    block += $"\n└ **Observateurs** : {specsStr}";
                }

                tableDetails.Add(block);
                index++;
            }

            // Découpage automatique pour respecter la limite stricte de 1024 caractères par champ Discord
            var currentFieldContent = new List<string>();
            int currentLength = 0;
            int partIndex = 1;

            for (int i = 0; i < tableDetails.Count; i++)
            {
                var item = tableDetails[i];
                if (currentLength + item.Length + 2 > 1000 && currentFieldContent.Count > 0)
                {
                    string fieldName = partIndex == 1 ? "⚔️ TABLES FORMÉES" : $"⚔️ TABLES FORMÉES (suite {partIndex})";
                    embedBuilder.AddField(fieldName, string.Join("\n\n", currentFieldContent), inline: false);
                    currentFieldContent.Clear();
                    currentLength = 0;
                    partIndex++;
                }

                currentFieldContent.Add(item);
                currentLength += item.Length + 2;
            }

            if (currentFieldContent.Count > 0)
            {
                string fieldName = partIndex == 1 ? "⚔️ TABLES FORMÉES" : $"⚔️ TABLES FORMÉES (suite {partIndex})";
                embedBuilder.AddField(fieldName, string.Join("\n\n", currentFieldContent), inline: false);
            }
        }
        else
        {
            embedBuilder.AddField("⚔️ TABLES FORMÉES", "*Aucune table constituée pour le moment.*", inline: false);
        }

        // 3. ABSENTS
        var absents = session.Availabilities
            .Where(a => a.IsAbsent)
            .OrderBy(a => a.DiscordUsername)
            .ToList();

        if (absents.Count > 0)
        {
            var absentMentions = string.Join(", ", absents.Select(a => $"<@{a.DiscordUserId}>"));
            embedBuilder.AddField("❌ ABSENTS", absentMentions, inline: false);
        }

        // 4. COMPOSANTS INTERACTIFS (Boutons et Menus)
        var componentBuilder = new ComponentBuilder();

        if (session.Status == SessionStatus.Open)
        {
            // Ligne 1 : Boutons d'action principaux
            componentBuilder.WithButton("Déclarer mes souhaits", $"session:avail:{session.Id}", ButtonStyle.Primary, new Emoji("📋"), row: 0);
            componentBuilder.WithButton("Créer une table", $"table:create:{session.Id}", ButtonStyle.Success, new Emoji("⚔️"), row: 0);
            componentBuilder.WithButton("Quitter ma table", $"table:leave:current:{session.Id}", ButtonStyle.Secondary, new Emoji("🚪"), row: 0);
            componentBuilder.WithButton("Absent", $"session:absent:{session.Id}", ButtonStyle.Danger, new Emoji("❌"), row: 0);

            // Ligne 2 : Menu déroulant pour rejoindre une table existante (si au moins 1 table existe)
            if (sortedTables.Count > 0)
            {
                var selectMenu = new SelectMenuBuilder()
                    .WithCustomId($"table:join:select:{session.Id}")
                    .WithPlaceholder("Rejoindre une table existante...")
                    .WithMinValues(1)
                    .WithMaxValues(1);

                int tableNum = 1;
                foreach (var table in sortedTables.Take(12)) // Max 25 options autorisées par Discord (12*2 = 24 options)
                {
                    var playerNames = table.Participants
                        .Where(p => p.Role == ParticipantRole.Player)
                        .Select(p => p.DiscordUsername)
                        .ToList();

                    var playersSummary = playerNames.Count > 0
                        ? $"Joueur(s) : {string.Join(", ", playerNames)}"
                        : "Aucun joueur pour le moment";

                    if (playersSummary.Length > 95)
                    {
                        playersSummary = playersSummary[..92] + "...";
                    }

                    selectMenu.AddOption(
                        $"Rejoindre T{tableNum} : {table.GameName} (Joueur)",
                        $"player:{table.Id}",
                        playersSummary,
                        new Emoji("🎮"));

                    selectMenu.AddOption(
                        $"Observer T{tableNum} : {table.GameName} (Observateur)",
                        $"spectator:{table.Id}",
                        $"Observer la table T{tableNum}",
                        new Emoji("👁️"));

                    tableNum++;
                }

                componentBuilder.WithSelectMenu(selectMenu, row: 1);
            }
        }

        return (embedBuilder.Build(), componentBuilder.Build());
    }

    private static List<string> ParseGames(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
