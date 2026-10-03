#!/usr/bin/env bash
set -Eeuo pipefail
umask 077
revision=${1:?Commit requis}
registry_user=${2:?Utilisateur GitHub requis}
[[ $revision =~ ^[a-f0-9]{40}$ ]] || exit 2
base=/opt/worlds-tcg
release="$base/releases/$revision"
cd "$release"
chmod 600 runtime.env registry-token
export DOCKER_CONFIG
DOCKER_CONFIG=$(mktemp -d)
cleanup() { rm -f -- "$release/registry-token"; rm -rf -- "$DOCKER_CONFIG"; }
trap cleanup EXIT
docker login ghcr.io -u "$registry_user" --password-stdin < registry-token >/dev/null
compose=(docker compose --project-name worlds-tcg --env-file "$release/runtime.env" -f "$release/docker-compose.prod.yml")
"${compose[@]}" config --quiet
"${compose[@]}" pull
previous=$(readlink -f "$base/current" || true)
if [[ -n "$previous" && "$previous" != "$release" && -f "$previous/runtime.env" ]]; then
  [[ "$previous" == "$base/releases/"* ]] || exit 2
  mkdir -p "$base/backups"
  docker compose --project-name worlds-tcg --env-file "$previous/runtime.env" -f "$previous/docker-compose.prod.yml" exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB"' > "$base/backups/$(date -u +%Y%m%dT%H%M%SZ)-$revision.sql"
fi
if "${compose[@]}" up -d --no-build --wait --wait-timeout 240; then
  domain=$(sed -n "s/^APP_DOMAIN='\([^']*\)'$/\1/p" runtime.env)
  if curl --fail --silent --retry 12 --retry-all-errors --retry-delay 5 "https://$domain/health" >/dev/null; then
    ln -sfn "$release" "$base/current"
    echo "Déploiement validé : $revision"
    exit 0
  fi
fi
echo 'Échec du démarrage ou du contrôle HTTPS.' >&2
if [[ -n "$previous" && "$previous" != "$release" && -f "$previous/runtime.env" ]]; then
  docker compose --project-name worlds-tcg --env-file "$previous/runtime.env" -f "$previous/docker-compose.prod.yml" up -d --no-build --wait --wait-timeout 180
  echo "Image précédente rétablie. La base n'a pas été restaurée automatiquement." >&2
fi
exit 1
