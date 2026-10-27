#!/usr/bin/env bash
set -euo pipefail
# Mantém a janela interativa aberta tanto no sucesso como na falha.
xtream_finalizar_janela() {
  local resultado=$?
  trap - EXIT
  if [ "$resultado" -ne 0 ]; then
    printf '\n[Xtream] A inicialização falhou (código %s). A mensagem completa está acima.\n' "$resultado" >&2
    if [ -n "${xtream_log_file:-}" ]; then
      printf '[Xtream] Log desta tentativa: %s\n' "$xtream_log_file" >&2
    fi
  fi
  if [ -t 0 ] && [ "${XTREAM_NO_PAUSE:-0}" != 1 ]; then
    printf '\nA janela ficará aberta. Pressione Enter quando quiser sair... '
    read -r _xtream_tecla || true
  fi
  exit "$resultado"
}
trap xtream_finalizar_janela EXIT
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
mkdir -p logs
xtream_log_file="$(pwd)/logs/iniciar-$(date +%Y%m%d-%H%M%S)-$$.log"
# Saída sem interface dinâmica: não corta a mensagem de erro no terminal.
COMPOSE_PROGRESS=plain "${XTREAM_COMPOSE[@]}" --ansi never up -d --build 2>&1 | tee "$xtream_log_file"

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
