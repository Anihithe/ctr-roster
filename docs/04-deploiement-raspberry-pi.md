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

## 2. Dockerfile Multi-Stage (.NET 10 pour ARM64)

Ce Dockerfile est placé à la racine du dépôt (`./Dockerfile`). Il compile l'application pour `linux-arm64` et produit une image finale minimale.

```dockerfile
# Étape 1 : Compilation
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY *.sln .
COPY src/CtrRoster.Domain/*.csproj src/CtrRoster.Domain/
COPY src/CtrRoster.Application/*.csproj src/CtrRoster.Application/
COPY src/CtrRoster.Infrastructure/*.csproj src/CtrRoster.Infrastructure/
COPY src/CtrRoster.Presentation/*.csproj src/CtrRoster.Presentation/

RUN dotnet restore -r linux-arm64

COPY . .
WORKDIR /source/src/CtrRoster.Presentation
RUN dotnet publish -c Release -o /app --no-restore -r linux-arm64 --self-contained false

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

## 3. Déploiement via Docker Compose

Arborescence sur le Raspberry Pi (`/opt/ctr-roster/`) :
```text
/opt/ctr-roster/
├── docker-compose.yml
├── backup.sh
└── data/
    └── (ctr_roster.db généré ici)
```

Fichier `docker-compose.yml` :
```yaml
version: '3.8'

services:
  ctr-roster:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: ctr_roster_bot
    restart: unless-stopped
    environment:
      - DOTNET_ENVIRONMENT=Production
      - Discord__Token=VOTRE_TOKEN_BOT_PROD
      - Discord__AdminRoleId=VOTRE_ROLE_ID_ADMIN
      - ConnectionStrings__Default=Data Source=/app/data/ctr_roster.db;Cache=Shared
    volumes:
      - ./data:/app/data
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
```

---

## 4. Sauvegardes Quotidiennes SQLite (Protection Carte SD)

Pour éviter toute corruption SQLite en cas de coupure de courant ou d'usure de la carte SD, un backup à chaud non bloquant compatible WAL est exécuté chaque nuit.

Fichier `backup.sh` :
```bash
#!/bin/bash
BACKUP_DIR="/opt/ctr-roster/backups"
DB_FILE="/opt/ctr-roster/data/ctr_roster.db"
DATE=$(date +"%Y%m%d_%H%M%S")

mkdir -p "$BACKUP_DIR"

# Sauvegarde à chaud transactionnelle via sqlite3 CLI
sqlite3 "$DB_FILE" ".backup '$BACKUP_DIR/ctr_roster_$DATE.db'"

# Rétention : suppression des sauvegardes de plus de 14 jours
find "$BACKUP_DIR" -name "ctr_roster_*.db" -mtime +14 -delete
```

Automatisation dans crontab de l'hôte :
```bash
chmod +x /opt/ctr-roster/backup.sh
(crontab -l 2>/dev/null; echo "0 3 * * * /opt/ctr-roster/backup.sh") | crontab -
```

---

## 5. Commandes d'Exploitation

- Démarrer / Mettre à jour : `docker compose up -d --build`
- Voir les logs en direct : `docker compose logs -f --tail=100`
- Arrêter le conteneur : `docker compose down`
