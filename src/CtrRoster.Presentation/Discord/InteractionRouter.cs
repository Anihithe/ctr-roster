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
/// Intercepte et aiguille les clics sur les boutons, les sélections de menus et les soumissions de modales
/// à partir des tokens encodés dans les CustomId (format: module:action:param1:param2).
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
            await component.RespondAsync("❌ Une erreur inattendue est survenue.", ephemeral: true);
        }
    }

    private async Task HandleSessionButtonAsync(SocketMessageComponent component, string action, string[] args)
    {
        var sessionId = args.Length > 0 ? Guid.Parse(args[0]) : Guid.Empty;

        switch (action)
        {
            case "avail":
                // Ouverture de la modale de déclaration de disponibilités
                var availModal = new ModalBuilder()
                    .WithTitle("Déclarer mes disponibilités")
                    .WithCustomId($"session:avail:submit:{sessionId}")
                    .AddTextInput(
                        "Jeux souhaités (séparés par des virgules)",
                        "games",
                        TextInputStyle.Paragraph,
                        placeholder: "Ex: Warhammer 40k, Catan, Autre (Dune)",
                        required: false)
                    .Build();

                await component.RespondWithModalAsync(availModal);
                break;

            case "absent":
                using (var scope = scopeFactory.CreateScope())
                {
                    var handler = scope.ServiceProvider.GetRequiredService<SetAbsentHandler>();
                    var result = await handler.HandleAsync(sessionId, component.User.Id, component.User.Username);
                    await component.RespondAsync(result, ephemeral: true);
                }
                break;
        }
    }

    private async Task HandleTableButtonAsync(SocketMessageComponent component, string action, string[] args)
    {
        switch (action)
        {
            case "create":
                var sessionId = args.Length > 0 ? Guid.Parse(args[0]) : Guid.Empty;
                var tableModal = new ModalBuilder()
                    .WithTitle("Créer une nouvelle table")
                    .WithCustomId($"table:create:submit:{sessionId}")
                    .AddTextInput(
                        "Nom du jeu",
                        "game_name",
                        TextInputStyle.Short,
                        placeholder: "Ex: Warhammer 40k (1v1), 7 Wonders, etc.",
                        required: true,
                        maxLength: 100)
                    .AddTextInput(
                        "Ajout direct de joueurs (pseudos ou @mentions)",
                        "additional_players",
                        TextInputStyle.Short,
                        placeholder: "Optionnel : séparés par des virgules",
                        required: false)
                    .Build();

                await component.RespondWithModalAsync(tableModal);
                break;

            case "leave":
                var target = args.Length > 0 ? args[0] : "current";
                using (var scope = scopeFactory.CreateScope())
                {
                    var leaveHandler = scope.ServiceProvider.GetRequiredService<LeaveTableHandler>();
                    var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                    Guid tableToLeaveId;
                    if (target == "current")
                    {
                        var sId = args.Length > 1 ? Guid.Parse(args[1]) : Guid.Empty;
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
                        tableToLeaveId = Guid.Parse(target);
                    }

                    var result = await leaveHandler.HandleAsync(tableToLeaveId, component.User.Id);
                    await component.RespondAsync(result, ephemeral: true);
                }
                break;
        }
    }

    private async Task HandleSelectMenuAsync(SocketMessageComponent component)
    {
        var tokens = component.Data.CustomId.Split(':');
        if (tokens.Length < 2) return;

        var (module, action) = (tokens[0], tokens[1]);

        try
        {
            if (module == "table" && action == "join")
            {
                var selectedValue = component.Data.Values.FirstOrDefault();
                if (string.IsNullOrEmpty(selectedValue)) return;

                // Format de la valeur : role:tableId (ex: player:Guid ou spectator:Guid)
                var valueParts = selectedValue.Split(':');
                if (valueParts.Length < 2) return;

                var role = valueParts[0] == "spectator" ? ParticipantRole.Spectator : ParticipantRole.Player;
                var tableId = Guid.Parse(valueParts[1]);

                using var scope = scopeFactory.CreateScope();
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
            await component.RespondAsync("❌ Une erreur inattendue est survenue.", ephemeral: true);
        }
    }

    private async Task HandleModalSubmittedAsync(SocketModal modal)
    {
        var tokens = modal.Data.CustomId.Split(':');
        if (tokens.Length < 3) return;

        var (module, action) = (tokens[0], tokens[1]);
        var sessionId = Guid.Parse(tokens[2]);

        try
        {
            using var scope = scopeFactory.CreateScope();

            if (module == "session" && action == "avail")
            {
                var gamesText = modal.Data.Components
                    .FirstOrDefault(c => c.CustomId == "games")?.Value ?? string.Empty;

                var gamesList = gamesText
                    .Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                var availHandler = scope.ServiceProvider.GetRequiredService<DeclareAvailabilityHandler>();
                var result = await availHandler.HandleAsync(
                    sessionId,
                    modal.User.Id,
                    modal.User.Username,
                    gamesList);

                await modal.RespondAsync(result, ephemeral: true);
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
            await modal.RespondAsync("❌ Une erreur inattendue est survenue.", ephemeral: true);
        }
    }
}
