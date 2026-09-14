# Guide de Développement & Débogage Local — Sans Raspberry Pi

Ce guide détaille comment développer, exécuter et déboguer le bot **CTR-Roster** directement sur votre poste de travail local sous Linux (ou Windows/macOS) avec **JetBrains Rider**, **VS Code** ou simplement le **CLI .NET 10**, sans jamais avoir besoin de déployer sur le Raspberry Pi pour tester.

---

## 1. Principes du Débogage Local vs Déploiement Raspberry Pi

| Aspect | Développement Local (Votre PC) | Production (Raspberry Pi) |
| :--- | :--- | :--- |
| **Exécution** | Directement via `dotnet run` ou bouton **Debug (F5)** dans Rider | Conteneur Docker optimisé Linux ARM64 |
| **Base de données** | Fichier SQLite local `./ctr_roster_dev.db` (auto-migré au lancement) | Volume Docker `/app/data/association_bot.db` |
| **Propagation Slash Commands** | **Instantanée** (enregistrées uniquement sur le serveur Discord de test) | Globale (enregistrées pour tous les serveurs du bot) |
| **Rechargement / Débogage** | Points d'arrêt (breakpoints), inspection pas-à-pas, Hot Reload | Logs de conteneur `docker compose logs -f` |

---

## 2. Configuration Discord pour le Débogage Local

### 2.1. Créer un serveur Discord de test (Sandbox)
1. Créez un serveur Discord privé (ex. "CTR Lab / Sandbox").
2. Activez le **Mode Développeur** sur Discord (Paramètres > Avancés > Mode Développeur).
3. Faites un clic droit sur le nom de votre serveur de test $\rightarrow$ **Copier l'identifiant du serveur** (ce sera votre `DevGuildId`).

### 2.2. Avoir un Bot de Test dédié (Recommandé)
> [!TIP] **Bonne pratique : Deux Bots Discord distincts**
> Plutôt que de partager le même token avec le bot de production sur le Raspberry Pi (ce qui provoquerait un conflit de sessions Gateway WebSocket), créez deux applications sur le [Portail Développeur Discord](https://discord.com/developers/applications) :
> - `CTR-Roster-Prod` (déployé sur le Raspberry Pi).
> - `CTR-Roster-Dev` (utilisé uniquement sur votre machine en local).
> 
> Donnez à votre bot de dev les Intents nécessaires : **Server Members Intent**, **Message Content Intent**. Invitez-le sur votre serveur de test avec les permissions Administrateur ou Bot.

---

## 3. Gestion Sécurisée des Secrets Locaux

Pour ne jamais risquer de commiter vos tokens Discord ou identifiants sur GitHub :

### Option A : `dotnet user-secrets` (Recommandé par .NET)
À la racine du projet `src/CtrRoster.Presentation` :
```bash
/home/anihithe/.dotnet/dotnet user-secrets init
/home/anihithe/.dotnet/dotnet user-secrets set "Discord:Token" "VOTRE_TOKEN_BOT_DEV"
/home/anihithe/.dotnet/dotnet user-secrets set "Discord:DevGuildId" "123456789012345678"
/home/anihithe/.dotnet/dotnet user-secrets set "Discord:AdminRoleId" "123456789012345678"
/home/anihithe/.dotnet/dotnet user-secrets set "ConnectionStrings:Default" "Data Source=ctr_roster_dev.db;Cache=Shared"
```

### Option B : Fichier `appsettings.Development.json` (ignoré par `.gitignore`)
Créez `src/CtrRoster.Presentation/appsettings.Development.json` :
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "Discord": "Debug"
    }
  },
  "ConnectionStrings": {
    "Default": "Data Source=ctr_roster_dev.db;Cache=Shared"
  },
  "Discord": {
    "Token": "VOTRE_TOKEN_BOT_DEV",
    "DevGuildId": 123456789012345678,
    "AdminRoleId": 123456789012345678
  }
}
```

---

## 4. Enregistrement Instantané des Slash Commands (0 Délai)

Par défaut, enregistrer une Slash Command globalement (`RegisterCommandsGloballyAsync`) met **jusqu'à 1 heure** à se propager sur Discord.

Pour avoir un cycle de débug immédiat, le bot applique cette règle au démarrage :
```csharp
if (builder.Environment.IsDevelopment() && devGuildId != 0)
{
    // Enregistrement instantané (0 seconde) sur le serveur de test !
    await interactionService.RegisterCommandsToGuildAsync(devGuildId);
    logger.LogInformation("Slash commands enregistrées instantanément sur le serveur Dev {GuildId}", devGuildId);
}
else
{
    // Enregistrement global en production
    await interactionService.RegisterCommandsGloballyAsync();
    logger.LogInformation("Slash commands enregistrées globalement.");
}
```

---

## 5. Lancer et Déboguer sous JetBrains Rider ou CLI

### Lancement via CLI
```bash
/home/anihithe/.dotnet/dotnet run --project src/CtrRoster.Presentation --environment Development
```

### Lancement via JetBrains Rider
1. Ouvrez `CtrRoster.sln` dans Rider.
2. Rider détectera automatiquement le profil d'exécution `CtrRoster.Presentation`.
3. Cliquez sur l'icône **Debug (Coccinelle)** ou faites `Shift + F9`.
4. Posez vos points d'arrêt dans les handlers (ex: `HandleTableInteractionAsync`, `LeaveTableHandler`).
5. Cliquez sur un bouton dans Discord : l'exécution se fige immédiatement sur votre point d'arrêt dans Rider pour inspecter l'état.

### Auto-migration SQLite au lancement local
En mode `Development`, le bot applique automatiquement les migrations au démarrage pour que vous n'ayez aucune commande manuelle à taper :
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsDevelopment())
    {
        await db.Database.MigrateAsync();
    }
}
```
