# 📖 Guide d'Utilisation — CTR-Roster

Bienvenue sur le guide officiel d'utilisation de **CTR-Roster** !  
Ce document a pour vocation d'expliquer simplement et concrètement comment utiliser le bot pour organiser ou rejoindre des sessions de jeux de société, wargames, jeux de rôle et jeux de cartes au sein de votre association ou boutique.

---

## 🧭 Sommaire rapide

1. [Le concept : La Card Discord interactive](#-le-concept--la-card-discord-interactive)
2. [Espace Joueurs (Comment participer ?)](#-espace-joueurs-comment-participer-)
   - [Consulter les sessions ouvertes (`/ctr-info`)](#1-consulter-les-sessions-ouvertes-ctr-info)
   - [Déclarer ses disponibilités ou son absence](#2-déclarer-ses-disponibilités-ou-son-absence)
   - [Créer une table de jeu](#3-créer-une-table-de-jeu)
   - [Rejoindre ou quitter une table](#4-rejoindre-ou-quitter-une-table)
3. [Espace Organisateurs & Gérants (Administration)](#-espace-organisateurs--gérants-administration)
   - [Les droits d'accès](#1-les-droits-daccès)
   - [Configurer le bot (`/ctr-config`)](#2-configurer-le-bot-ctr-config)
   - [Gérer les sessions (`/ctr-session-create`, `/ctr-session-capacity`, `/ctr-session-close`)](#3-gérer-les-sessions)
   - [Gérer le catalogue des jeux (`/ctr-game-*`)](#4-gérer-le-catalogue-de-jeux)
4. [Règles importantes & FAQ](#-règles-importantes--faq)

---

## 🪟 Le concept : La Card Discord interactive

Fini les messages éparpillés sur plusieurs salons !  
Pour chaque soirée ou journée de jeu, **CTR-Roster** publie une **fiche récapitulative unique** (appelée *Card*).
- **Temps réel :** Dès qu'un joueur clique sur un bouton ou choisit un jeu, la Card se met à jour immédiatement sur le salon.
- **Zéro message superflu :** Vous n'avez pas besoin d'écrire dans le chat, tout se pilote via les boutons et menus interactifs situés sous la Card.
- **Multi-sessions :** Plusieurs sessions peuvent être ouvertes en parallèle pour les jours à venir (ex: mardi, vendredi et samedi).

---

## 🎲 Espace Joueurs (Comment participer ?)

### 1. Consulter les sessions ouvertes (`/ctr-info`)
Tapez la commande `/ctr-info` dans n'importe quel salon autorisé :
- Si **une seule session** est ouverte, le bot vous affiche son résumé complet (date, nombre de tables formées, joueurs en attente).
- Si **plusieurs sessions** sont ouvertes en même temps, le bot affiche la liste avec un indicateur de complétude (`🟢 Places dispo` ou `🔴 Complet`) ainsi qu'un lien cliquable direct **"Aller à la Card"** pour vous téléporter directement au message d'inscription !

### 2. Déclarer ses disponibilités ou son absence
Sous la Card de la session :
- **🟢 Se déclarer disponible :** Cliquez sur le bouton `Disponible`. Un menu s'affiche pour vous permettre de sélectionner un ou plusieurs jeux du catalogue auxquels vous souhaitez jouer, ou d'indiquer un jeu libre non répertorié. Vous apparaissez alors dans la section *Disponibilités*.
- **🔴 Se déclarer absent :** Cliquez sur le bouton `Absent` si vous changez d'avis ou ne pouvez plus venir. Le bot libère automatiquement votre place.

### 3. Créer une table de jeu
Vous avez un jeu précis en tête et cherchez des partenaires ?
- Cliquez sur le bouton **`➕ Créer une table`**.
- Choisissez le jeu dans le catalogue (ou saisissez un nom libre), le nombre de places et votre rôle (Joueur ou Organisateur/MJ).
- Vous pouvez inviter directement des amis à votre table.
- *Note de capacité :* Si la salle a atteint son quota maximum de tables, le bouton devient temporairement grisé `🛑 Complet`.

### 4. Rejoindre ou quitter une table
- **Rejoindre :** Cliquez sur le bouton `🎮 Rejoindre` sous la Card, puis sélectionnez la table de votre choix. Vous pouvez participer en tant que **Joueur** actif ou simple **Observateur/Spectateur**.
- **Quitter :** Cliquez sur `🚪 Quitter la table`. Vous êtes retiré de la table et repassé en joueur disponible.
  - *Astuce :* Si vous étiez le créateur ou l'un des joueurs, vos camarades restent sur la table en attendant qu'un nouveau joueur vous remplace. Une table n'est automatiquement supprimée que si elle devient totalement vide (0 joueur).

---

## 🛠️ Espace Organisateurs & Gérants (Administration)

### 1. Les droits d'accès
Qui peut administrer le bot ?
1. Le **propriétaire du serveur** Discord (*Server Owner*), automatiquement et partout.
2. Les membres ayant la permission Discord **Administrateur** ou **Gérer le serveur**.
3. Tout membre possédant le **Rôle Admin** spécifiquement désigné via `/ctr-config role_admin:@Role`.

---

### 2. Configurer le bot (`/ctr-config`)

La commande `/ctr-config` permet de personnaliser le fonctionnement du bot pour votre serveur :

| Paramètre | Description | Exemple |
| :--- | :--- | :--- |
| `salon_sessions` | Salon par défaut où les cartes de sessions sont publiées | `#inscriptions-jeux` |
| `salon_restreint` | Limite l'usage des commandes du bot à un unique salon | `#commandes-bot` |
| `reset_restriction_salon` | Supprime la restriction de salon (autorise tous les salons) | `true` |
| `role_admin` | Rôle Discord habilité à gérer le bot | `@Responsable Jeux` |
| `auto_renouvellement` | Active ou désactive la reconduction automatique des sessions | `true` ou `false` |
| `intervalle_jours` | Fréquence de renouvellement en jours | `7` (hebdomadaire), `1` (quotidien) |
| `intervalle_heures` | Fréquence de renouvellement en heures | `24` (toutes les 24h) |
| `jours_ouverture` | Jours d'ouverture du local / magasin (séparés par virgules) | `mardi,mercredi,vendredi,samedi` |
| `reset_jours_ouverture` | Réinitialise les jours d'ouverture (tous les jours ouverts) | `true` |
| `max_tables_defaut` | Nombre maximum de tables par défaut dans la salle | `5` tables |
| `reset_max_tables` | Supprime la limite de tables par défaut (illimité) | `true` |

---

### 3. Gérer les sessions

#### Créer une session manuelle : `/ctr-session-create`
- **Exemple simple :**  
  `/ctr-session-create date:2026-10-16 heure:20:00`
- **Options avancées :**
  - `salon:#mon-salon` : poster sur un salon spécifique.
  - `max_tables:4` : définir une capacité spécifique pour cette soirée (écrase la valeur par défaut du serveur).
  - `forcer:true` : forcer la création même si la date tombe sur un jour configuré comme fermé.

#### Modifier la capacité d'une session : `/ctr-session-capacity`
Besoin d'ajouter ou de réduire le nombre de tables pour une soirée ?  
`/ctr-session-capacity max_tables:6`  
*(Si plusieurs sessions sont ouvertes sur le salon, le bot vous demandera de préciser le `session_id` parmi la liste affichée).*

#### Clôturer une session : `/ctr-session-close`
Vous souhaitez verrouiller les inscriptions avant l'heure ?  
`/ctr-session-close [date] [heure] [session_id]`  
La Card passe instantanément en mode lecture seule (`🔴 Session clôturée`) et tous les boutons sont désactivés.

---

### 4. Gérer le catalogue de jeux

Le catalogue permet de proposer aux joueurs une liste déroulante claire lors de leurs inscriptions :

- **`/ctr-game-list`** : Affiche la liste des jeux répertoriés pour votre serveur avec leur statut (`🟢 Actif` ou `⚪ Désactivé`) et le nombre de joueurs recommandés.
- **`/ctr-game-add nom:<Nom du jeu> [min_joueurs] [max_joueurs]`** : Ajoute un nouveau jeu (ex: `/ctr-game-add nom:Catan min_joueurs:3 max_joueurs:4`).
- **`/ctr-game-toggle nom_jeu:<Nom>`** : Active ou désactive temporairement un jeu sans le supprimer de la base.
- **`/ctr-game-remove nom:<Nom>`** : Supprime définitivement un jeu du catalogue.
- **`/ctr-game-claim-legacy`** : Rattache l'ensemble des jeux historiques créés avant le multi-serveurs (`GuildId = 0`) au serveur Discord actuel.

---

## 📌 Règles importantes & FAQ

> **Puis-je être inscrit sur plusieurs tables en même temps ?**  
> ❌ Non. Pour éviter les confusions d'horaires et les surréservations, un joueur ne peut appartenir qu'à **une seule table active** par session. Si vous souhaitez changer de table, quittez simplement votre table actuelle.

> **Que se passe-t-il si la capacité maximale de tables est atteinte ?**  
> 🛑 Le bouton `➕ Créer une table` passe automatiquement en état grisé `🛑 Complet`. Dès qu'une table est dissoute ou que la capacité est augmentée par un admin, le bouton redevient disponible instantanément.

> **Pouvons-nous jouer à un jeu qui n'est pas dans le catalogue ?**  
> ✅ Oui ! Lors de la création d'une table ou du choix de disponibilité, vous pouvez saisir un nom de jeu libre.

> **Quand les sessions se clôturent-elles automatiquement ?**  
> ⏰ Une session se clôture automatiquement dès que sa date et son heure de début sont atteintes. Si le renouvellement automatique est activé, la session suivante est générée automatiquement en respectant vos jours d'ouverture.
