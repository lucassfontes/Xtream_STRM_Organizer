#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
# shellcheck disable=SC1091
source ./preparar-docker.sh
# shellcheck disable=SC1091
source ./permissoes-linux.sh
xtream_preparar_permissoes
xtream_preparar_docker
# sudo não preserva variáveis exportadas normalmente; passar UID/GID explicitamente.
if [ "${XTREAM_COMPOSE[0]}" = sudo ]; then
  XTREAM_COMPOSE=(sudo env "PUID=$PUID" "PGID=$PGID" "${XTREAM_COMPOSE[@]:1}")
fi
xtream_info 'Iniciando Xtream STRM Organizer...'
"${XTREAM_COMPOSE[@]}" up -d --build

# Mostra o endereço de acesso no terminal toda vez que o programa for iniciado.
# Não mostramos senhas, tokens ou credenciais Xtream.
printf '\n'
printf '=============================================================\n'
printf ' XTREAM STRM ORGANIZER  |  ACESSO WEB\n'
printf '=============================================================\n'
printf ' No próprio PC:       http://localhost:8686\n'
if command -v hostname >/dev/null 2>&1; then
  # Mostra apenas um possível IP do host (não presume conexão de rede).
  ip_local="$(hostname -I 2>/dev/null | awk '{print $1}' || true)"
  if [ -n "$ip_local" ]; then
    printf ' Na sua rede local:  http://%s:8686\n' "$ip_local"
  fi
fi
printf ' Primeiro acesso:     Cadastre usuário e senha (mínimo 6 caracteres).\n'
printf ' Arquivos preservados: config/ e Biblioteca_IPTV/.\n'
printf ' Para testar:         ./testar-linux.sh\n'
printf ' Para parar:          ./parar-linux.sh\n'
printf '=============================================================\n\n'
