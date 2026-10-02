# Journal des Modifications (Changelog) — CTR-Roster

Toutes les modifications notables apportées à ce projet sont documentées dans ce fichier.
Le format est basé sur [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/) et ce projet adhère à [Semantic Versioning](https://semver.org/lang/fr/).

## [0.3.1-beta] - 2026-10-03

### 🐛 Corrections (Fixes)
- **Suppression du dédoublement de la Card Discord** :
  - Correction dans `DiscordMessageRenderer` lors de la mise à jour (`message.ModifyAsync`).
  - Suppression de l'assignation redondante de `msg.Embed = embed;` pour ne conserver que le tableau unitaire `msg.Embeds = new[] { embed };`.
  - Résout le comportement de l'API Discord v10 qui interprétait les deux champs simultanément et affichait deux cartes identiques empilées sur le message.

### ⚙️ Déploiement & Environnements (DevOps)
- **Modèles de configuration multi-environnements** :
  - Ajout des templates `.env.dev.example` et `.env.prod.example` permettant de faire tourner deux instances du bot (DEV et PRD) en parallèle sur un même hôte (ex: Raspberry Pi) avec isolation stricte des bases SQLite et des tokens Discord.
  - Paramétrage dynamique du nom de conteneur, du volume de données et de l'environnement d'exécution via variables d'environnement dans `docker-compose.yml`.

---

## [0.3.0-beta] - 2026-10-03

### ✨ Nouvelles Fonctionnalités (Features)
- **Multi-Sessions Actives en Parallèle (Point 2)** :
  - Support de plusieurs sessions de jeu simultanément ouvertes sur un même serveur / salon (ex: réservations ouvertes pour les 7 prochains jours).
  - Suppression de la fermeture automatique agressive lors de la création d'une nouvelle session.
  - Détection et prévention des doublons sur un même créneau horaire (`ScheduledDate`).
  - Architecture 100% *stateless* préservée : chaque Card Discord fonctionne en totale autonomie avec son `sessionId` embarqué dans les interactions.
  - Commande `/ctr-info` enrichie : vue détaillée si 1 session active, ou liste synthétique avec liens directs (*"Aller à la Card"*) et indicateurs de complétude si plusieurs sessions sont actives.
- **Jours d'Ouverture du Lieu / Boutique (Point 2.1)** :
  - Configuration des jours d'ouverture autorisés via `/ctr-config jours_ouverture:<liste>` (support du français et de l'anglais, ex: `mardi,mercredi,jeudi,vendredi,samedi`), réinitialisable via `reset_jours_ouverture:true`.
  - Rejet immédiat avec message informatif lors d'une tentative de création sur un jour fermé.
  - Option de contournement administratif `forcer:true` dans `/ctr-session-create` pour les ouvertures exceptionnelles.
  - Le worker de renouvellement automatique saute automatiquement les jours de fermeture pour planifier directement sur le prochain jour d'ouverture configuré.
- **Intervalle de Renouvellement Configurable (Point 1)** :
  - Définition de la fréquence de renouvellement automatique soit en jours via `/ctr-config intervalle_jours:<nombre>` (ex: 7 pour hebdo, 1 pour quotidien), soit en heures via `/ctr-config intervalle_heures:<nombre>` (ex: 24 pour journalier).
  - Intégration dans le moteur d'arrière-plan `SessionLifecycleWorker`.
- **Clôture Manuelle de Session** :
  - Nouvelle commande `/ctr-session-close [date] [heure] [session_id]` permettant aux administrateurs de clôturer manuellement une session à tout moment.
  - Actualisation instantanée de la Card Discord sur le salon avec désactivation des boutons et affichage du badge `🔴 Session clôturée`.
- **Migration Automatique SQLite Non-Destructive** :
  - Ajout transparent et rétrocompatible des colonnes `RenewIntervalHours` (INTEGER NULL) et `OpenDaysJson` (TEXT NULL) sur la table `GuildConfigs`.

---

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
