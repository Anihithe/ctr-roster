# Guide de Déploiement & Exploitation — Raspberry Pi

Ce guide résume la procédure complète de déploiement et de fiabilisation de **CTR-Roster** sur un Raspberry Pi 3B+, 4 ou 5 (sous Raspberry Pi OS 64-bit ou Ubuntu Server).

---

## 1. Préparation du Raspberry Pi

Installer Docker et Docker Compose :
```bash
sudo apt update && sudo apt upgrade -y
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER
newgrp docker
```

---

## 2. Dockerfile Multi-Stage (.NET 10 Multi-Architecture ARM32/ARM64)

Ce Dockerfile est placé à la racine du dépôt (`./Dockerfile`). Il compile l'application pour l'architecture native du Raspberry Pi (`armv7l` ou `arm64`) et produit une image d'exécution minimale.

```dockerfile
# Étape 1 : Compilation
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/CtrRoster.Domain/*.csproj src/CtrRoster.Domain/
COPY src/CtrRoster.Application/*.csproj src/CtrRoster.Application/
COPY src/CtrRoster.Infrastructure/*.csproj src/CtrRoster.Infrastructure/
COPY src/CtrRoster.Presentation/*.csproj src/CtrRoster.Presentation/

RUN dotnet restore src/CtrRoster.Presentation/CtrRoster.Presentation.csproj

COPY . .
WORKDIR /source/src/CtrRoster.Presentation
RUN dotnet publish -c Release -o /app --no-restore

# Étape 2 : Image d'exécution finale
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Installation de libsqlite3 et fuseau horaire
RUN apt-get update && apt-get install -y libsqlite3-0 tzdata && rm -rf /var/lib/apt/lists/*
ENV TZ=Europe/Paris

COPY --from=build /app .
VOLUME /app/data

ENTRYPOINT ["dotnet", "CtrRoster.Presentation.dll"]
```

---

## 3. Architecture Multi-Instances : Production (PRD) & Développement (DEV)

Pour isoler hermétiquement la production réelle et permettre des tests occasionnels sans impacter les joueurs, le Raspberry Pi héberge **deux répertoires et deux conteneurs Docker indépendants** :

```text
/opt/
├── ctr-roster/          <-- Instance de PRODUCTION (active 24/7, branchée sur 'main' ou tag stable)
│   ├── docker-compose.yml
│   ├── .env             (Token du bot de PROD)
│   └── data/
│       └── ctr_roster.db (Vraie base SQLite de production)
│
└── ctr-roster-dev/      <-- Instance de DÉVELOPPEMENT (à la demande, branchée sur branche de test)
    ├── docker-compose.yml
    ├── .env             (Token du bot de DEV)
    └── data/
        └── ctr_roster_dev.db (Base SQLite de test isolée)
```

### 3.1. Avantages de cette architecture
1. **Zéro conflit réseau** : Les bots Discord fonctionnent en WebSocket sortant (aucun port d'écoute TCP exposé sur la machine). Les deux bots peuvent donc tourner en simultané sans aucun conflit.
2. **Indépendance des branches Git** : Vous pouvez faire `git checkout feature/nom-de-branche` dans `/opt/ctr-roster-dev/` sans risquer de perturber le code source de la production.
3. **Consommation mémoire minimale** : Le conteneur de DEV est configuré avec `RESTART_POLICY=no` (ne démarre pas au reboot). Il ne consomme de la mémoire que lorsqu'il est allumé pour vos tests.

---

### 3.2. Procédure de Mise en Place Initiale sur le Raspberry Pi

#### Étape 1 : Préparer l'instance de PRODUCTION (`/opt/ctr-roster/`)
```bash
cd /opt/ctr-roster

# Récupérer les nouveautés du dépôt
git fetch --tags
git checkout v0.3.0-beta

# Créer le fichier .env de Production à partir du modèle
cp .env.prod.example .env

# Éditer .env et renseigner votre token Discord de PROD
nano .env # (DISCORD_BOT_TOKEN=...)

# Démarrer le conteneur de production
docker compose up -d --build

# Vérifier les logs
docker compose logs -f --tail=50
```

#### Étape 2 : Préparer l'instance de DÉVELOPPEMENT (`/opt/ctr-roster-dev/`)
```bash
# Cloner le dépôt dans le dossier de DEV
cd /opt
sudo git clone https://github.com/Anihithe/ctr-roster.git ctr-roster-dev
sudo chown -R $USER:$USER /opt/ctr-roster-dev

cd /opt/ctr-roster-dev

# Créer le fichier .env de Dev à partir du modèle
cp .env.dev.example .env

# Éditer .env et renseigner votre token Discord de DEV
nano .env # (DISCORD_BOT_TOKEN=...)

# Optionnel : démarrer la dev pour tester
docker compose up -d --build
```

---

## 4. Exploitation Quotidienne : Démarrer et Arrêter la DEV

```bash
cd /opt/ctr-roster-dev

# Allumer le bot de DEV pour faire vos tests :
docker compose up -d

# Suivre les logs en direct :
docker compose logs -f --tail=50

# Éteindre le bot de DEV dès la fin des tests (libère 100% de la RAM) :
docker compose stop
```

---

## 5. Sauvegardes Quotidiennes SQLite (Production)

Pour éviter toute corruption SQLite en cas de coupure de courant ou d'usure de la carte SD, un backup à chaud non bloquant compatible WAL est exécuté chaque nuit sur la base de production.

Fichier `backup.sh` :
```bash
#!/bin/bash
BACKUP_DIR="/opt/ctr-roster/backups"
DB_FILE="/opt/ctr-roster/data/ctr_roster.db"
DATE=$(date +"%Y%m%d_%H%M%S")

mkdir -p "$BACKUP_DIR"

# Sauvegarde à chaud transactionnelle via sqlite3 CLI
if [ -f "$DB_FILE" ]; then
    sqlite3 "$DB_FILE" ".backup '$BACKUP_DIR/ctr_roster_$DATE.db'"
    find "$BACKUP_DIR" -name "ctr_roster_*.db" -mtime +14 -delete
fi
```

Automatisation dans crontab de l'hôte :
```bash
chmod +x /opt/ctr-roster/scripts/backup.sh
(crontab -l 2>/dev/null; echo "0 3 * * * /opt/ctr-roster/scripts/backup.sh") | crontab -
```
