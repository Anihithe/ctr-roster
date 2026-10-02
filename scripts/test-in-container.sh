#!/usr/bin/env bash
# ==============================================================================
# Script d'exécution des tests en conteneur éphémère (Agnostique Git / CI-CD)
# Fonctionne indifféremment avec Docker ou Podman sur Linux, macOS ou Windows.
# ==============================================================================
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Détection du moteur de conteneurisation disponible (docker ou podman)
CONTAINER_ENGINE=""
if command -v docker >/dev/null 2>&1; then
    CONTAINER_ENGINE="docker"
elif command -v podman >/dev/null 2>&1; then
    CONTAINER_ENGINE="podman"
else
    echo "❌ Erreur : Ni docker ni podman n'a été trouvé sur ce système."
    exit 1
fi

echo "🚀 Lancement des tests CTR-Roster dans un conteneur éphémère ($CONTAINER_ENGINE)..."
echo "📦 Image : mcr.microsoft.com/dotnet/sdk:10.0"

$CONTAINER_ENGINE run --rm \
    -v "$ROOT_DIR:/workspace:rw" \
    -w /workspace \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    bash -c "dotnet test -c Release --logger 'console;verbosity=normal'"

echo "✅ Tous les tests ont été exécutés avec succès dans le conteneur !"
