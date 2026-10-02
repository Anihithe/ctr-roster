# Journal des Modifications (Changelog) — CTR-Roster

Toutes les modifications notables apportées à ce projet sont documentées dans ce fichier.
Le format est basé sur [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/) et ce projet adhère à [Semantic Versioning](https://semver.org/lang/fr/).

## [0.2.0-beta] - 2026-10-02

### ✨ Nouvelles Fonctionnalités (Features)
- **Gestion des Limites de Capacité de Tables (Point 3)** :
  - **Capacité par défaut du serveur** : Configuration d'un nombre maximum de tables par défaut pour les sessions via `/ctr-config max_tables_defaut:<nombre>` (supprimable avec `reset_max_tables:true`).
  - **Capacité unitaire par session** : 
    - Définition à la création de la session via `/ctr-session-create ... max_tables:<nombre>`.
    - Modification unitaire sur une session existante via la nouvelle commande `/ctr-session-capacity max_tables:<nombre> [session_id:<id>]`.
  - **Affichage dynamique sur la Card Discord** :
    - En-tête des tables mis à jour automatiquement (ex: `⚔️ TABLES FORMÉES (3/5)`).
    - Jauge d'état dans la description : `🪑 Capacité : 3/5 tables` ou `🔴 Capacité atteinte (5/5 tables)`.
  - **Sécurité et ergonomie anti-surréservation** :
    - Dès que le quota de tables est atteint, le bouton *"Créer une table"* passe automatiquement en état grisé désactivé (`🛑 Créer une table (Complet)`).
    - Protection stricte dans la couche métier (`CreateTableHandler`) et le routeur d'interactions contre toute ouverture de table au-delà du quota.
  - **Migration de schéma SQLite sans perte** :
    - Vérification et ajout automatique des colonnes `DefaultMaxTables` (`GuildConfigs`) et `MaxTables` (`GameSessions`).

---

## [0.1.0-beta] - 2026-10-02

### 🐛 Corrections (Fixes)
- **Actualisation de la Card Discord** : Correction de l'envoi de `msg.Embeds = new[] { embed };` requis par l'API Discord v10 et Discord.Net 3.20+ pour forcer l'actualisation visuelle de l'embed.
- **Auto-alignement `DiscordMessageId`** : Correction automatique en base de l'ID du message lors de toute interaction sur la Card.
- **Résilience salon Socket/REST** : Prise en charge automatique via `Rest.GetChannelAsync()` lorsque le salon Discord n'est pas encore présent dans le cache Socket local au démarrage.
- **Sécurisation des embeds volumineux** : Découpage automatique des champs de tables dépassant la limite stricte de 1024 caractères imposée par Discord.

### ✨ Nouvelles Fonctionnalités (Features)
- **Isolation Multi-Serveurs (Multi-Guild)** :
  - Cloisonnement strict du catalogue des jeux (`Games`) par `GuildId`. Possibilité pour chaque serveur Discord d'avoir ses propres jeux sans collision de nom.
  - Cloisonnement des sessions (`GameSessions`) par `GuildId`.
  - Maintien de la rétrocompatibilité pour les jeux existants (`GuildId = 0`).
- **Gestion Dynamique des Droits en Base de Données (`GuildConfigs`)** :
  - Le Propriétaire du serveur Discord (*Server Owner*) dispose désormais d'office et inconditionnellement des droits de gestion du bot.
  - Les Administrateurs Discord (`Administrator`, `ManageGuild`) disposent d'office des droits d'administration.
  - Configuration dynamique d'un rôle gestionnaire par serveur : `/ctr-config role_admin:@NomRole`.
  - Configuration dynamique d'une restriction de salon par serveur : `/ctr-config salon_restreint:#nom-du-salon`.
- **Migration Automatique SQLite Non-Destructive** :
  - Vérification des `PRAGMA table_info` au démarrage du bot avec injection dynamique des colonnes manquantes sans perte de données.
- **Fiabilisation Raspberry Pi / ARM** :
  - Utilisation systématique de `DeferAsync` pour éviter les timeouts d'interaction Discord (3s) sur les machines à ressources modérées.

---
