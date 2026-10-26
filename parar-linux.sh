#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
# shellcheck disable=SC1091
source ./preparar-docker.sh
xtream_preparar_docker
"${XTREAM_COMPOSE[@]}" down
