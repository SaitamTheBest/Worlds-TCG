#!/usr/bin/env bash
set -euo pipefail
# Exécuter en root sur le VPS Debian 12 OVH (Docker préinstallé).
[[ $(id -u) == 0 ]] || { echo "Exécutez avec sudo." >&2; exit 1; }
. /etc/os-release
[[ "$ID" == debian && "$VERSION_ID" == 12 ]] || { echo "Ce script cible Debian 12." >&2; exit 1; }
command -v docker >/dev/null || { echo "Docker absent : https://docs.docker.com/engine/install/debian/" >&2; exit 1; }
docker compose version
apt-get update
apt-get install -y ca-certificates curl openssh-server ufw
id deploy >/dev/null 2>&1 || useradd --create-home --shell /bin/bash deploy
usermod -aG docker deploy
install -d -m 700 -o deploy -g deploy /home/deploy/.ssh /opt/worlds-tcg /opt/worlds-tcg/releases /opt/worlds-tcg/backups
echo "Ajoutez la clé publique GitHub à /home/deploy/.ssh/authorized_keys (propriétaire deploy, mode 600)."
echo "Le pare-feu reste à configurer selon votre port SSH : voir deploy/README.md."
