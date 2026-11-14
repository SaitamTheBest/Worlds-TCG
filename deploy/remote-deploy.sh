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
if grep -qx "MONITORING_ENABLED='true'" runtime.env; then
  compose+=(-f "$release/docker-compose.monitoring.yml")
fi
"${compose[@]}" config --quiet
"${compose[@]}" pull
previous=$(readlink -f "$base/current" || true)
previous_compose=()
if [[ -n "$previous" && "$previous" != "$release" && -f "$previous/runtime.env" ]]; then
  [[ "$previous" == "$base/releases/"* ]] || exit 2
  previous_compose=(docker compose --project-name worlds-tcg --env-file "$previous/runtime.env" -f "$previous/docker-compose.prod.yml")
  if grep -qx "MONITORING_ENABLED='true'" "$previous/runtime.env" && [[ -f "$previous/docker-compose.monitoring.yml" ]]; then
    previous_compose+=(-f "$previous/docker-compose.monitoring.yml")
  fi
  mkdir -p "$base/backups"
  "${previous_compose[@]}" exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB"' > "$base/backups/$(date -u +%Y%m%dT%H%M%SZ)-$revision.sql"
fi
# Laisser terminer l'initialisation de Grafana sur les petits VPS.
if "${compose[@]}" up -d --no-build --remove-orphans --wait --wait-timeout 420; then
  domain=$(sed -n "s/^APP_DOMAIN='\([^']*\)'$/\1/p" runtime.env)
  if curl --fail --silent --retry 12 --retry-all-errors --retry-delay 5 "https://$domain/health" >/dev/null; then
    monitoring_healthy=true
    if grep -qx "MONITORING_ENABLED='true'" runtime.env; then
      grafana_domain=$(sed -n "s/^GRAFANA_DOMAIN='\([^']*\)'$/\1/p" runtime.env)
      curl --fail --silent --retry 12 --retry-all-errors --retry-delay 5 "https://$grafana_domain/api/health" >/dev/null || monitoring_healthy=false
    fi
    if [[ "$monitoring_healthy" == true ]]; then
      ln -sfn "$release" "$base/current"
      echo "Déploiement validé : $revision"
      exit 0
    fi
  fi
fi
echo 'Échec du démarrage ou du contrôle HTTPS.' >&2
# Donner le diagnostic du conteneur dans Actions avant un éventuel rollback.
# Ne jamais afficher runtime.env, les variables des conteneurs ou le Compose résolu.
"${compose[@]}" ps -a >&2 || true
if grep -qx "MONITORING_ENABLED='true'" runtime.env; then
  grafana_id=$("${compose[@]}" ps -a -q grafana 2>/dev/null || true)
  if [[ -n "$grafana_id" ]]; then
    docker inspect --format 'Grafana: status={{.State.Status}} OOMKilled={{.State.OOMKilled}} restarts={{.RestartCount}} memory={{.HostConfig.Memory}} health={{json .State.Health}}' "$grafana_id" >&2 || true
    docker stats --no-stream --format 'Grafana: memory={{.MemUsage}} CPU={{.CPUPerc}}' "$grafana_id" >&2 || true
  fi
fi
if [[ -n "$previous" && "$previous" != "$release" && -f "$previous/runtime.env" ]]; then
  "${previous_compose[@]}" up -d --no-build --remove-orphans --wait --wait-timeout 420
  echo "Image précédente rétablie. La base n'a pas été restaurée automatiquement." >&2
fi
exit 1
