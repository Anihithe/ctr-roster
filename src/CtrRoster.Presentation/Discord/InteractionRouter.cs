using System.Text.Json;
using CtrRoster.Application.Availabilities.Commands;
using CtrRoster.Application.Common.Interfaces;
using CtrRoster.Application.Tables.Commands;
using CtrRoster.Domain.Enums;
using CtrRoster.Domain.Exceptions;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CtrRoster.Presentation.Discord;

/// <summary>
/// Routeur d'interactions Discord sans état (Stateless).
/// Exploite le catalogue de jeux pour les sélections multiples (SelectMenu) et offre
/// une option de saisie libre (Modale) pour les jeux hors-catalogue.
/// </summary>
public class InteractionRouter(
    DiscordSocketClient client,
    IServiceScopeFactory scopeFactory,
    ILogger<InteractionRouter> logger)
{
    public void Initialize()
    {
        client.ButtonExecuted += HandleButtonAsync;
        client.SelectMenuExecuted += HandleSelectMenuAsync;
        client.ModalSubmitted += HandleModalSubmittedAsync;
        logger.LogInformation("InteractionRouter initialisé et connecté aux événements Discord.");
    }

    private static Guid ExtractGuid(params string[] tokens)
    {
        for (int i = tokens.Length - 1; i >= 0; i--)
        {
            if (Guid.TryParse(tokens[i], out var id))
            {
                return id;
            }
        }
        return Guid.Empty;
    }

    private static List<string> ParseGames(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task HandleButtonAsync(SocketMessageComponent component)
    {
        var tokens = component.Data.CustomId.Split(':');
        if (tokens.Length < 2) return;

        var (module, action) = (tokens[0], tokens[1]);
        var args = tokens.Skip(2).ToArray();

        try
        {
            switch (module)
            {
                case "session":
                    await HandleSessionButtonAsync(component, action, args);
                    break;
                case "table":
                    await HandleTableButtonAsync(component, action, args);
                    break;
            }
        }
        catch (DomainException ex)
        {
            await component.RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur inattendue lors du traitement du bouton {CustomId}", component.Data.CustomId);
            await component.RespondAsync($"❌ Erreur inattendue : {ex.Message}", ephemeral: true);
        }
    }

    private async Task HandleSessionButtonAsync(SocketMessageComponent component, string action, string[] args)
    {
        var sessionId = ExtractGuid(args);
        if (sessionId == Guid.Empty)
        {
            sessionId = ExtractGuid(component.Data.CustomId.Split(':'));
        }

        switch (action)
        {
            case "avail":
                // Si l'utilisateur a cliqué sur le bouton "custom" de saisie libre
                if (args.Length > 0 && args[0] == "custom")
                {
                    var customModal = new ModalBuilder()
                        .WithTitle("Proposer un autre jeu")
                        .WithCustomId($"session:avail:submit:{sessionId}")
                        .AddTextInput(
                            "Nom du jeu (ou jeux séparés par virgules)",
                            "games",
                            TextInputStyle.Paragraph,
                            placeholder: "Ex: Nemesis, Blood Bowl, Twilight Imperium...",
                            required: true)
                        .Build();

                    await component.RespondWithModalAsync(customModal);
                    return;
                }

                // Si l'utilisateur a cliqué sur "clear" pour réinitialiser ses souhaits
                if (args.Length > 0 && args[0] == "clear")
                {
                    using var scopeClear = scopeFactory.CreateScope();
                    var availHandler = scopeClear.ServiceProvider.GetRequiredService<DeclareAvailabilityHandler>();
                    await availHandler.HandleAsync(sessionId, component.User.Id, component.User.Username, []);
                    await component.RespondAsync("🗑️ Tes préférences de jeux ont été réinitialisées.", ephemeral: true);
                    return;
                }

                // Clic standard sur "Déclarer mes souhaits" : affichage du catalogue en SelectMenu
                using (var scope = scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                    await SyncSessionCardMessageAsync(db, sessionId, component);

                    var session = await db.GameSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
                    var guildId = session?.GuildId ?? (component.Channel as SocketGuildChannel)?.Guild.Id ?? 0;
                    var activeGames = await db.Games
                        .Where(g => g.IsActive && (g.GuildId == guildId || g.GuildId == 0))
                        .OrderBy(g => g.Name)
                        .ToListAsync();

                    var existing = await db.PlayerAvailabilities
                        .FirstOrDefaultAsync(a => a.GameSessionId == sessionId && a.DiscordUserId == component.User.Id);

                    var currentGames = existing != null && !existing.IsAbsent
                        ? ParseGames(existing.PreferredGamesJson)
                        : [];

                    // Si aucun jeu n'est dans le catalogue, ouvrir directement la modale de saisie libre
                    if (activeGames.Count == 0)
                    {
                        var customModal = new ModalBuilder()
                            .WithTitle("Indiquer mes souhaits de jeux")
                            .WithCustomId($"session:avail:submit:{sessionId}")
                            .AddTextInput(
                                "Nom du jeu (ou jeux séparés par virgules)",
                                "games",
                                TextInputStyle.Paragraph,
                                placeholder: "Ex: Nemesis, Blood Bowl, Twilight Imperium...",
                                required: true,
                                value: currentGames.Count > 0 ? string.Join(", ", currentGames) : null)
                            .Build();

                        await component.RespondWithModalAsync(customModal);
                        return;
                    }

                    var builder = new ComponentBuilder();

                    if (activeGames.Count > 0)
                    {
                        var selectMenu = new SelectMenuBuilder()
                            .WithCustomId($"session:avail:select:{sessionId}")
                            .WithPlaceholder("Coche un ou plusieurs jeux du club...")
                            .WithMinValues(1)
                            .WithMaxValues(Math.Min(activeGames.Count, 25));

                        foreach (var g in activeGames.Take(25))
                        {
                            bool isDefault = currentGames.Any(cg => cg.Equals(g.Name, StringComparison.OrdinalIgnoreCase));
                            selectMenu.AddOption(
                                g.Name,
                                g.Name,
                                g.MinPlayers.HasValue ? $"{g.MinPlayers} à {g.MaxPlayers ?? 0} joueurs" : null,
                                new Emoji("🎲"),
                                isDefault: isDefault);
                        }

                        builder.WithSelectMenu(selectMenu, row: 0);
                    }

                    // Ligne 2 : Options complémentaires
                    builder.WithButton(
                        "➕ Autre jeu (saisie libre)",
                        $"session:avail:custom:{sessionId}",
                        ButtonStyle.Secondary,
                        row: 1);

                    if (currentGames.Count > 0)
                    {
                        builder.WithButton(
                            "🗑️ Effacer mes choix",
                            $"session:avail:clear:{sessionId}",
                            ButtonStyle.Danger,
                            row: 1);
                    }

                    string prompt = currentGames.Count > 0
                        ? $"📋 **Tes choix actuels :** *{string.Join(", ", currentGames)}*\nTu peux cocher ci-dessous les jeux du catalogue qui t'intéressent :"
                        : "📋 **Sélectionne tes préférences de jeu dans le catalogue :**\n*(Tu peux cocher plusieurs jeux d'un coup)*";

                    await component.RespondAsync(prompt, components: builder.Build(), ephemeral: true);
                }
                break;

            case "absent":
                using (var scope = scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                    await SyncSessionCardMessageAsync(db, sessionId, component);

                    var handler = scope.ServiceProvider.GetRequiredService<SetAbsentHandler>();
                    var result = await handler.HandleAsync(sessionId, component.User.Id, component.User.Username);
                    await component.RespondAsync(result, ephemeral: true);
                }
                break;
        }
    }

    private async Task HandleTableButtonAsync(SocketMessageComponent component, string action, string[] args)
    {
        var sessionId = ExtractGuid(args);
        if (sessionId == Guid.Empty)
        {
            sessionId = ExtractGuid(component.Data.CustomId.Split(':'));
        }

        switch (action)
        {
            case "create":
                // Vérifier d'abord la capacité maximale de la session
                using (var scopeCheck = scopeFactory.CreateScope())
                {
                    var dbCheck = scopeCheck.ServiceProvider.GetRequiredService<IAppDbContext>();
                    var sessCheck = await dbCheck.GameSessions.Include(s => s.Tables).FirstOrDefaultAsync(s => s.Id == sessionId);
                    if (sessCheck != null && sessCheck.MaxTables.HasValue && sessCheck.Tables.Count >= sessCheck.MaxTables.Value)
                    {
                        await component.RespondAsync($"⚠️ La capacité maximale de cette session est atteinte ({sessCheck.MaxTables.Value} table{(sessCheck.MaxTables.Value > 1 ? "s" : "")} max). Impossible de créer une nouvelle table.", ephemeral: true);
                        return;
                    }
                }

                // Si l'utilisateur clique sur le bouton "custom" de création libre
                if (args.Length > 0 && args[0] == "custom")
                {
                    var tableModal = new ModalBuilder()
                        .WithTitle("Créer une table (Saisie libre)")
                        .WithCustomId($"table:create:submit:{sessionId}")
                        .AddTextInput(
                            "Nom du jeu",
                            "game_name",
                            TextInputStyle.Short,
                            placeholder: "Ex: Warhammer 40k (1v1), Blood Bowl, etc.",
                            required: true,
                            maxLength: 100)
                        .AddTextInput(
                            "Ajout direct de joueurs (pseudos)",
                            "additional_players",
                            TextInputStyle.Short,
                            placeholder: "Optionnel : séparés par des virgules",
                            required: false)
                        .Build();

                    await component.RespondWithModalAsync(tableModal);
                    return;
                }

                // Clic sur "Créer une table" : proposer le catalogue OU saisie libre
                using (var scope = scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                    await SyncSessionCardMessageAsync(db, sessionId, component);

                    var session = await db.GameSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
                    var guildId = session?.GuildId ?? (component.Channel as SocketGuildChannel)?.Guild.Id ?? 0;
                    var activeGames = await db.Games
                        .Where(g => g.IsActive && (g.GuildId == guildId || g.GuildId == 0))
                        .OrderBy(g => g.Name)
                        .ToListAsync();

                    // Si aucun jeu n'est dans le catalogue, ouvrir directement la modale de saisie libre
                    if (activeGames.Count == 0)
                    {
                        var tableModal = new ModalBuilder()
                            .WithTitle("Créer une table (Saisie libre)")
                            .WithCustomId($"table:create:submit:{sessionId}")
                            .AddTextInput(
                                "Nom du jeu",
                                "game_name",
                                TextInputStyle.Short,
                                placeholder: "Ex: Warhammer 40k (1v1), Blood Bowl, etc.",
                                required: true,
                                maxLength: 100)
                            .AddTextInput(
                                "Ajout direct de joueurs (pseudos)",
                                "additional_players",
                                TextInputStyle.Short,
                                placeholder: "Optionnel : séparés par des virgules",
                                required: false)
                            .Build();

                        await component.RespondWithModalAsync(tableModal);
                        return;
                    }

                    var builder = new ComponentBuilder();

                    if (activeGames.Count > 0)
                    {
                        var selectMenu = new SelectMenuBuilder()
                            .WithCustomId($"table:create:select:{sessionId}")
                            .WithPlaceholder("Choisir un jeu du catalogue...")
                            .WithMinValues(1)
                            .WithMaxValues(1);

                        foreach (var g in activeGames.Take(25))
                        {
                            selectMenu.AddOption(
                                g.Name,
                                g.Name,
                                g.MinPlayers.HasValue ? $"{g.MinPlayers} à {g.MaxPlayers ?? 0} joueurs" : null,
                                new Emoji("⚔️"));
                        }

                        builder.WithSelectMenu(selectMenu, row: 0);
                    }

                    builder.WithButton(
                        "➕ Autre jeu (saisie libre)",
                        $"table:create:custom:{sessionId}",
                        ButtonStyle.Secondary,
                        row: 1);

                    await component.RespondAsync(
                        "⚔️ **Création de table** : choisis un jeu du catalogue ci-dessous ou clique sur *Autre jeu* :",
                        components: builder.Build(),
                        ephemeral: true);
                }
                break;

            case "leave":
                var target = args.Length > 0 ? args[0] : "current";
                using (var scope = scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                    if (target == "current")
                    {
                        await SyncSessionCardMessageAsync(db, sessionId, component);
                    }

                    var leaveHandler = scope.ServiceProvider.GetRequiredService<LeaveTableHandler>();

                    Guid tableToLeaveId;
                    if (target == "current")
                    {
                        var sId = sessionId;
                        var currentTable = await db.GameTables
                            .Include(t => t.Participants)
                            .FirstOrDefaultAsync(t => t.GameSessionId == sId && t.Participants.Any(p => p.DiscordUserId == component.User.Id));

                        if (currentTable == null)
                        {
                            await component.RespondAsync("⚠️ Tu n'es actuellement inscrit sur aucune table de cette session.", ephemeral: true);
                            return;
                        }

                        tableToLeaveId = currentTable.Id;
                    }
                    else
                    {
                        tableToLeaveId = Guid.TryParse(target, out var parsed) ? parsed : sessionId;
                    }

                    var result = await leaveHandler.HandleAsync(tableToLeaveId, component.User.Id);
                    await component.RespondAsync(result, ephemeral: true);
                }
                break;
        }
    }

    private static async Task SyncSessionCardMessageAsync(IAppDbContext db, Guid sessionId, SocketMessageComponent component)
    {
        if (sessionId == Guid.Empty || component.Message == null) return;
        var session = await db.GameSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null) return;

        bool changed = false;
        var guildId = (component.Channel as SocketGuildChannel)?.Guild.Id ?? 0;
        if (session.GuildId == 0 && guildId != 0)
        {
            session.GuildId = guildId;
            changed = true;
        }

        if (session.DiscordMessageId != component.Message.Id)
        {
            session.DiscordMessageId = component.Message.Id;
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    private async Task HandleSelectMenuAsync(SocketMessageComponent component)
    {
        var tokens = component.Data.CustomId.Split(':');
        if (tokens.Length < 2) return;

        var (module, action) = (tokens[0], tokens[1]);
        var sessionId = ExtractGuid(tokens);

        try
        {
            // 1. Déclaration de préférences via sélection multiple du catalogue
            if (module == "session" && action == "avail")
            {
                var selectedGames = component.Data.Values.ToList();

                using var scope = scopeFactory.CreateScope();
                var availHandler = scope.ServiceProvider.GetRequiredService<DeclareAvailabilityHandler>();
                await availHandler.HandleAsync(
                    sessionId,
                    component.User.Id,
                    component.User.Username,
                    selectedGames);

                await component.UpdateAsync(msg =>
                {
                    msg.Content = $"✅ **Disponibilités enregistrées !**\n🎮 Jeux sélectionnés : **{string.Join(", ", selectedGames)}**\n*(La Card de la session a été mise à jour !)*";
                    msg.Components = null;
                });
                return;
            }

            // 2. Création rapide d'une table à partir d'un jeu du catalogue
            if (module == "table" && action == "create")
            {
                var selectedGame = component.Data.Values.FirstOrDefault();
                if (string.IsNullOrEmpty(selectedGame)) return;

                using var scope = scopeFactory.CreateScope();
                var createHandler = scope.ServiceProvider.GetRequiredService<CreateTableHandler>();
                var result = await createHandler.HandleAsync(
                    sessionId,
                    component.User.Id,
                    component.User.Username,
                    selectedGame,
                    gameId: null,
                    creatorRole: ParticipantRole.Player,
                    additionalParticipants: []);

                await component.UpdateAsync(msg =>
                {
                    msg.Content = $"⚔️ Table pour **{result.Table.GameName}** créée avec succès ! Tu es inscrit comme joueur.";
                    msg.Components = null;
                });
                return;
            }

            // 3. Rejoindre une table existante (Menu déroulant sous la Card)
            if (module == "table" && action == "join")
            {
                var selectedValue = component.Data.Values.FirstOrDefault();
                if (string.IsNullOrEmpty(selectedValue)) return;

                var valueParts = selectedValue.Split(':');
                if (valueParts.Length < 2) return;

                var role = valueParts[0] == "spectator" ? ParticipantRole.Spectator : ParticipantRole.Player;
                var tableId = Guid.Parse(valueParts[1]);

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                await SyncSessionCardMessageAsync(db, sessionId, component);

                var joinHandler = scope.ServiceProvider.GetRequiredService<JoinTableHandler>();
                var message = await joinHandler.HandleAsync(
                    tableId,
                    component.User.Id,
                    component.User.Username,
                    role);

                await component.RespondAsync(message, ephemeral: true);
            }
        }
        catch (DomainException ex)
        {
            await component.RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du traitement de la sélection {CustomId}", component.Data.CustomId);
            await component.RespondAsync($"❌ Erreur inattendue : {ex.Message}", ephemeral: true);
        }
    }

    private async Task HandleModalSubmittedAsync(SocketModal modal)
    {
        var tokens = modal.Data.CustomId.Split(':');
        if (tokens.Length < 2) return;

        var (module, action) = (tokens[0], tokens[1]);
        var sessionId = ExtractGuid(tokens);

        try
        {
            using var scope = scopeFactory.CreateScope();

            if (module == "session" && action == "avail")
            {
                var gamesText = modal.Data.Components
                    .FirstOrDefault(c => c.CustomId == "games")?.Value ?? string.Empty;

                var newGames = gamesText
                    .Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var existing = await db.PlayerAvailabilities
                    .FirstOrDefaultAsync(a => a.GameSessionId == sessionId && a.DiscordUserId == modal.User.Id);

                var existingGames = existing != null && !existing.IsAbsent
                    ? ParseGames(existing.PreferredGamesJson)
                    : [];

                // Fusion des sélections existantes avec les nouveaux ajouts personnalisés
                var allGames = existingGames.Concat(newGames).Distinct().ToList();

                var availHandler = scope.ServiceProvider.GetRequiredService<DeclareAvailabilityHandler>();
                await availHandler.HandleAsync(
                    sessionId,
                    modal.User.Id,
                    modal.User.Username,
                    allGames);

                await modal.RespondAsync($"✅ **Disponibilités enregistrées !**\n🎮 Jeux : **{string.Join(", ", allGames)}**", ephemeral: true);
            }
            else if (module == "table" && action == "create")
            {
                var gameName = modal.Data.Components
                    .FirstOrDefault(c => c.CustomId == "game_name")?.Value ?? string.Empty;

                var createHandler = scope.ServiceProvider.GetRequiredService<CreateTableHandler>();
                var result = await createHandler.HandleAsync(
                    sessionId,
                    modal.User.Id,
                    modal.User.Username,
                    gameName,
                    gameId: null,
                    creatorRole: ParticipantRole.Player,
                    additionalParticipants: []);

                await modal.RespondAsync($"⚔️ Table pour **{result.Table.GameName}** créée avec succès !", ephemeral: true);
            }
        }
        catch (DomainException ex)
        {
            await modal.RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du traitement de la modale {CustomId}", modal.Data.CustomId);
            await modal.RespondAsync($"❌ Erreur inattendue : {ex.Message}", ephemeral: true);
        }
    }
}
