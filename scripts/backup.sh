#!/bin/bash
# Script de sauvegarde à chaud SQLite pour Raspberry Pi
# Compatible mode WAL (Write-Ahead Logging) sans verrouiller les écritures

BACKUP_DIR="${BACKUP_DIR:-/opt/ctr-roster/backups}"
DATA_DIR="${DATA_DIR:-/opt/ctr-roster/data}"
DB_FILE="$DATA_DIR/ctr_roster.db"
DATE=$(date +"%Y%m%d_%H%M%S")

mkdir -p "$BACKUP_DIR"

if [ -f "$DB_FILE" ]; then
    echo "[$(date)] Début du backup de $DB_FILE vers $BACKUP_DIR/ctr_roster_$DATE.db..."
    sqlite3 "$DB_FILE" ".backup '$BACKUP_DIR/ctr_roster_$DATE.db'"
    echo "[$(date)] Sauvegarde terminée avec succès."
    
    # Rétention : suppression des sauvegardes de plus de 14 jours
    find "$BACKUP_DIR" -name "ctr_roster_*.db" -mtime +14 -delete
else
    echo "[$(date)] ATTENTION : Fichier de base de données $DB_FILE introuvable."
fi
