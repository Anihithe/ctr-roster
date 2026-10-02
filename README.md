# 🎲 CTR-Roster

> Bot Discord d'organisation événementielle & gestion de sessions de jeux en temps réel pour clubs, associations et boutiques.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512bd4.svg)](https://dotnet.microsoft.com/)
[![Version](https://img.shields.io/badge/version-0.3.0--beta-blue.svg)](CHANGELOG.md)
[![Tests](https://img.shields.io/badge/tests-32%20passed%20(100%25)-brightgreen.svg)]()
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

---

## 🌟 Points Forts

- **Card Discord Interactive en Temps Réel** : Fiche récapitulative unique mise à jour dynamiquement à chaque interaction (disponibilités, tables formées, jauges de capacité).
- **Multi-Sessions en Parallèle** : Gestion simultanée de plusieurs sessions ouvertes sur un même serveur ou salon (ex: réservations ouvertes sur les 7 jours à venir).
- **Jours d'Ouverture Configurables** : Définition des jours d'ouverture de l'association ou du magasin avec saut automatique des jours fermés.
- **Gestion des Capacités** : Quotas de tables par défaut ou par session avec bouton de création auto-bloquant (`🛑 Complet`) anti-surréservation.
- **Architecture 100% Sans État (*Stateless*)** : Toutes les interactions encodent leur identifiant de session (`sessionId`) sans mémoire vive éphémère.
- **Multi-Serveurs & Isolation** : Chaque serveur Discord dispose de sa propre configuration et de son catalogue de jeux dédié en base de données.
- **Optimisé pour Raspberry Pi** : Persistance SQLite en mode WAL, consommation mémoire minimale, protection contre les timeouts Discord (3s).

---

## 📚 Documentation

| Document | Description |
| :--- | :--- |
| [**📖 Guide d'Utilisation**](docs/GUIDE_UTILISATEUR.md) | **Guide complet pour les Joueurs et Administrateurs (Commandes, FAQ, Inscriptions)** |
| [**🎯 Spécifications Métier**](docs/01-specification-metier.md) | Invariants de gestion, personas, règles de dissolution et d'exclusivité |
| [**🏛️ Architecture Technique**](docs/02-architecture-technique.md) | Clean Architecture, N-Tiers, SQLite WAL, Throttler Discord anti-429 |
| [**🍓 Déploiement Raspberry Pi**](docs/04-deploiement-raspberry-pi.md) | Guide d'installation en service systemd et compilation ARM |
| [**📜 Journal des Modifications**](CHANGELOG.md) | Historique complet des versions et releases |

---

## 🚀 Démarrage Rapide

### Prérequis
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Un bot Discord avec son Token et les Intents *Server Members* activés sur le [Discord Developer Portal](https://discord.com/developers/applications).

### Lancement Local
```bash
# 1. Cloner le dépôt
git clone https://github.com/Anihithe/ctr-roster.git
cd ctr-roster

# 2. Configurer les variables d'environnement (.env ou appsettings.json)
cp .env.example .env # Renseigner DISCORD_BOT_TOKEN

# 3. Lancer les tests unitaires
dotnet test

# 4. Démarrer le bot
dotnet run --project src/CtrRoster.Presentation
```
