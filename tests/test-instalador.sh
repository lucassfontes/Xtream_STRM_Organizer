#!/usr/bin/env bash
# Teste de logica (nao instala nem baixa nada e nao exige Docker).
set -euo pipefail
cd "$(dirname "$0")/.."
# shellcheck disable=SC1091
source ./preparar-docker.sh

# Simula Docker existente e daemon acessivel.
docker() {
  case "$*" in
    '--version') printf '%s\n' 'Docker version 29.1.3 (simulado)' ;;
    'compose version')
      if [ "${TESTE_COMPOSE_INSTALADO:-0}" = 1 ]; then
        printf '%s\n' 'Docker Compose version v5.6.1 (simulado)'
      else
        return 1
      fi ;;
    'info') return 0 ;;
    *) echo "docker simulado: comando inesperado $*" >&2; return 1 ;;
  esac
}
xtream_instalar_compose_oficial() {
  TESTE_HOUVE_INSTALACAO=1
  TESTE_COMPOSE_INSTALADO=1
  return 0
}
xtream_preparar_apt() {
  echo 'ERRO DE TESTE: apt update nao deveria ter sido chamado.' >&2
  return 1
}
xtream_tentar_instalar_compose_cache() {
  echo 'ERRO DE TESTE: APT nao deveria ter sido usado.' >&2
  return 1
}

TESTE_COMPOSE_INSTALADO=1 TESTE_HOUVE_INSTALACAO=0
xtream_preparar_docker
[ "$TESTE_HOUVE_INSTALACAO" = 0 ] || { echo 'Falha: tentou instalar Compose presente' >&2; exit 1; }
[ "${XTREAM_COMPOSE[*]}" = 'docker compose' ] || exit 1

TESTE_COMPOSE_INSTALADO=0 TESTE_HOUVE_INSTALACAO=0
xtream_preparar_docker
[ "$TESTE_HOUVE_INSTALACAO" = 1 ] || { echo 'Falha: nao tentou instalar Compose ausente' >&2; exit 1; }
[ "${XTREAM_COMPOSE[*]}" = 'docker compose' ] || exit 1
printf '%s\n' 'OK: Docker ja instalado / Compose presente ou ausente; sem APT.'
