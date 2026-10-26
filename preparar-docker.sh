#!/usr/bin/env bash
# Biblioteca compartilhada pelos scripts de inicio e de teste.
# Verifica a cada execucao Docker, Compose, daemon e permissoes.
# Instala somente dependencias ausentes em Ubuntu/Zorin/Debian com APT.

xtream_info() { printf '[Xtream] %s\n' "$*"; }
xtream_erro() { printf '[Xtream] ERRO: %s\n' "$*" >&2; }

xtream_root() {
  if [ "$(id -u)" -eq 0 ]; then
    "$@"
  elif command -v sudo >/dev/null 2>&1; then
    sudo "$@"
  else
    xtream_erro 'A instalacao precisa de permissao administrativa (sudo).'
    return 1
  fi
}

xtream_apt_disponivel() {
  if ! command -v apt-get >/dev/null 2>&1; then return 1; fi
  if [ -r /etc/os-release ]; then
    # shellcheck disable=SC1091
    . /etc/os-release
    case " ${ID:-} ${ID_LIKE:-} " in
      *ubuntu*|*debian*|*zorin*) return 0 ;;
    esac
  fi
  return 1
}

# Atualiza os indices somente se necessario. Repositorios PPA indisponiveis nao
# podem prender a instalacao indefinidamente.
xtream_preparar_apt() {
  if [ "${XTREAM_AUTO_INSTALL:-1}" = 0 ]; then
    xtream_erro 'Instalacao automatica desabilitada por XTREAM_AUTO_INSTALL=0.'
    return 1
  fi
  if ! xtream_apt_disponivel; then
    xtream_erro 'Instalacao automatica suportada apenas em Ubuntu, Zorin OS e Debian com APT.'
    return 1
  fi
  if [ "${XTREAM_APT_ATUALIZADO:-0}" != 1 ]; then
    xtream_info 'Atualizando indices APT com tempo limite (PPAs que nao respondem serao ignorados).'
    local -a cmd=(apt-get -o Acquire::Retries=0 -o Acquire::http::Timeout=12 -o Acquire::https::Timeout=12 update)
    if command -v timeout >/dev/null 2>&1; then
      if ! xtream_root timeout --signal=TERM --kill-after=5s 75s "${cmd[@]}"; then
        xtream_info 'ATENCAO: a atualizacao falhou ou excedeu o prazo. Tentando pacotes que ja constam no cache APT.'
      fi
    elif ! xtream_root "${cmd[@]}"; then
      xtream_info 'ATENCAO: a atualizacao falhou. Tentando pacotes que ja constam no cache APT.'
    fi
    XTREAM_APT_ATUALIZADO=1
  fi
}

xtream_instalar_pacote_apt() {
  local pkg="$1"
  xtream_info "Instalando pacote: ${pkg}"
  xtream_root apt-get -o Acquire::Retries=0 -o Acquire::http::Timeout=20 -o Acquire::https::Timeout=20 install -y --no-remove "$pkg"
}

xtream_pacote_apt_existe() {
  command -v apt-cache >/dev/null 2>&1 || return 1
  local candidate
  candidate="$(apt-cache policy "$1" 2>/dev/null | awk '/Candidate:/ {print $2; exit}')"
  [ -n "$candidate" ] && [ "$candidate" != '(none)' ]
}

xtream_instalar_docker() {
  xtream_info 'Docker nao encontrado: iniciando instalacao automatica.'
  if [ "${XTREAM_AUTO_INSTALL:-1}" = 0 ] || ! xtream_apt_disponivel; then
    xtream_erro 'Instalacao automatica requer APT (Ubuntu/Zorin/Debian) e XTREAM_AUTO_INSTALL diferente de 0.'
    return 1
  fi
  # Primeiro usa indices existentes, sem consultar repositorios externos.
  if xtream_pacote_apt_existe docker.io && xtream_instalar_pacote_apt docker.io; then return 0; fi
  xtream_preparar_apt || return 1
  if xtream_pacote_apt_existe docker.io && xtream_instalar_pacote_apt docker.io; then return 0; fi
  xtream_erro 'Docker nao pode ser instalado pelos repositorios disponiveis.'
  xtream_erro 'Consulte: https://docs.docker.com/engine/install/ubuntu/'
  return 1
}

xtream_detectar_compose() {
  if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
    XTREAM_COMPOSE=(docker compose)
    return 0
  fi
  # Compatibilidade com instalacoes antigas, sem sobrescrever o Compose existente.
  if command -v docker-compose >/dev/null 2>&1 && docker-compose version >/dev/null 2>&1; then
    XTREAM_COMPOSE=(docker-compose)
    xtream_info 'Compose legado detectado (docker-compose). Recomenda-se atualizar para Compose V2.'
    return 0
  fi
  return 1
}

xtream_tentar_instalar_compose_cache() {
  # Ubuntu 24.04/Zorin 18: pacote docker-compose-v2 (componente universe).
  # Instalacoes do repositorio da Docker podem usar docker-compose-plugin.
  # Em maquinas com Docker CE, prefere o plugin oficial para evitar conflitos.
  if command -v dpkg-query >/dev/null 2>&1 &&
     [ "$(dpkg-query -W -f='${db:Status-Status}' docker-ce 2>/dev/null || true)" = installed ]; then
    if xtream_pacote_apt_existe docker-compose-plugin && xtream_instalar_pacote_apt docker-compose-plugin; then return 0; fi
    if xtream_pacote_apt_existe docker-compose-v2 && xtream_instalar_pacote_apt docker-compose-v2; then return 0; fi
  else
    if xtream_pacote_apt_existe docker-compose-v2 && xtream_instalar_pacote_apt docker-compose-v2; then return 0; fi
    if xtream_pacote_apt_existe docker-compose-plugin && xtream_instalar_pacote_apt docker-compose-plugin; then return 0; fi
  fi
  return 1
}

# Plugin Compose oficial, independente dos indices APT e dos PPAs do Zorin.
# Versao fixa com hash do arquivo checksums.txt publicado pelo projeto Docker
# em sua release GitHub. O binario e validado antes de ser instalado.
xtream_baixar_https() {
  local origem="$1" destino="$2"
  case "$origem" in https://github.com/docker/compose/releases/download/*) ;; 
    *) xtream_erro 'Origem de download inesperada.'; return 1 ;;
  esac
  if command -v curl >/dev/null 2>&1; then
    curl --fail --location --silent --show-error --retry 2 \
      --connect-timeout 15 --max-time 240 --proto '=https' --proto-redir '=https' --tlsv1.2 \
      --output "$destino" "$origem"
  elif command -v wget >/dev/null 2>&1; then
    wget --quiet --timeout=20 --tries=3 --https-only -O "$destino" "$origem"
  else
    xtream_erro 'Nao encontrei curl nem wget para baixar o Compose.'
    return 1
  fi
}

xtream_instalar_compose_oficial() {
  local versao='v5.6.1'
  # SHA-256 do checksums.txt, publicado na release oficial v5.6.1.
  local checksum_lista='71a925494d98575a801f4332f372d41a83badaffa7755ccf42d841c1d8b38aa8'
  local arquitetura artefato base temporario esperado destino
  if ! command -v sha256sum >/dev/null 2>&1; then
    xtream_erro 'sha256sum nao encontrado; nao e seguro instalar o binario sem verificacao.'
    return 1
  fi
  arquitetura="$(uname -m)"
  case "$arquitetura" in
    x86_64|aarch64|armv7l|armv6l|ppc64le|riscv64|s390x)
      case "$arquitetura" in
        armv7l) arquitetura=armv7 ;;
        armv6l) arquitetura=armv6 ;;
      esac
      ;;
    *) xtream_erro "Arquitetura ainda nao suportada pelo instalador: $arquitetura"; return 1 ;;
  esac
  artefato="docker-compose-linux-$arquitetura"
  base="https://github.com/docker/compose/releases/download/$versao"
  temporario="$(mktemp -d "${TMPDIR:-/tmp}/xtream-compose.XXXXXXXX")" || return 1

  xtream_info "Baixando verificacao oficial do Compose $versao ($arquitetura), sem apt update..."
  if ! xtream_baixar_https "$base/checksums.txt" "$temporario/checksums.txt"; then
    xtream_erro 'Nao foi possivel acessar o GitHub para baixar a lista de hashes.'
    rm -rf -- "$temporario"
    return 1
  fi
  if ! printf '%s  %s\n' "$checksum_lista" "$temporario/checksums.txt" | sha256sum --check --status; then
    xtream_erro 'O checksum da lista oficial nao confere; instalacao cancelada.'
    rm -rf -- "$temporario"
    return 1
  fi
  esperado="$(awk -v nome="$artefato" '$2==nome || $2=="*"nome {print $1; exit}' "$temporario/checksums.txt")"
  if ! [[ "$esperado" =~ ^[0-9a-fA-F]{64}$ ]]; then
    xtream_erro "Nao encontrei um checksum valido para $artefato."
    rm -rf -- "$temporario"
    return 1
  fi
  xtream_info "Baixando o plugin oficial do Docker Compose ($versao)..."
  if ! xtream_baixar_https "$base/$artefato" "$temporario/$artefato"; then
    xtream_erro 'Download do plugin falhou. Verifique a conexao com o GitHub.'
    rm -rf -- "$temporario"
    return 1
  fi
  if ! printf '%s  %s\n' "$esperado" "$temporario/$artefato" | sha256sum --check --status; then
    xtream_erro 'O arquivo baixado falhou na verificacao SHA-256. Nao sera instalado.'
    rm -rf -- "$temporario"
    return 1
  fi

  destino='/usr/local/lib/docker/cli-plugins/docker-compose'
  xtream_info "Instalando plugin verificado em $destino ..."
  if ! xtream_root install -d -m 0755 /usr/local/lib/docker/cli-plugins ||
     ! xtream_root install -m 0755 "$temporario/$artefato" "$destino.xtream-novo" ||
     ! xtream_root mv -f -- "$destino.xtream-novo" "$destino"; then
    xtream_erro 'Nao foi possivel instalar o plugin. Verifique permissao sudo.'
    rm -rf -- "$temporario"
    return 1
  fi
  rm -rf -- "$temporario"
  if docker compose version >/dev/null 2>&1; then
    xtream_info "Docker Compose instalado com sucesso: $(docker compose version 2>/dev/null | head -n 1)"
    return 0
  fi
  xtream_erro 'O plugin foi instalado, mas a CLI do Docker nao o reconheceu.'
  return 1
}

xtream_instalar_compose() {
  xtream_info 'Docker Compose nao encontrado: instalando automaticamente.'
  if [ "${XTREAM_AUTO_INSTALL:-1}" = 0 ]; then
    xtream_erro 'Instalacao automatica desabilitada por XTREAM_AUTO_INSTALL=0.'
    return 1
  fi
  # Prioridade ao plugin oficial: evita PPAs quebrados ou fontes Debian
  # misturadas ao Ubuntu. Nao executa apt update neste caminho.
  if xtream_instalar_compose_oficial; then return 0; fi

  # Apenas se o GitHub falhar: pacotes que ja estejam disponiveis no APT,
  # sem apt-get update para nao repetir os erros de mirrors do Zorin.
  if xtream_apt_disponivel; then
    xtream_info 'Tentando pacotes Compose ja listados no APT, sem atualizar os repositorios...'
    if xtream_tentar_instalar_compose_cache; then return 0; fi
  fi
  xtream_erro 'Nao foi possivel instalar o Compose pelo GitHub nem pelos pacotes APT existentes.'
  xtream_erro 'Consulte: https://docs.docker.com/compose/install/linux/'
  return 1
}

xtream_iniciar_daemon() {
  if command -v systemctl >/dev/null 2>&1 && [ -d /run/systemd/system ]; then
    xtream_info 'Tentando iniciar o servico Docker...'
    xtream_root systemctl start docker || true
  fi
}

xtream_preparar_docker() {
  xtream_info 'Verificando Docker Engine...'
  if ! command -v docker >/dev/null 2>&1; then
    xtream_instalar_docker || return 1
  else
    xtream_info "Docker encontrado: $(docker --version 2>/dev/null || echo 'versao indisponivel')"
  fi
  command -v docker >/dev/null 2>&1 || { xtream_erro 'Docker nao ficou disponivel apos a instalacao.'; return 1; }

  xtream_info 'Verificando Docker Compose...'
  if ! xtream_detectar_compose; then
    xtream_instalar_compose || return 1
    if ! xtream_detectar_compose; then
      xtream_erro 'O Compose nao foi reconhecido depois da instalacao.'
      return 1
    fi
  fi
  xtream_info "Compose encontrado: $("${XTREAM_COMPOSE[@]}" version 2>/dev/null | head -n 1)"

  # Compose pode funcionar mesmo sem permissao de acesso ao daemon.
  # Nunca muda silenciosamente as permissoes do grupo docker (equivale a root).
  if docker info >/dev/null 2>&1; then
    : # Usuario atual ja tem permissao.
  else
    xtream_iniciar_daemon
    if docker info >/dev/null 2>&1; then
      :
    elif [ "$(id -u)" -ne 0 ] && command -v sudo >/dev/null 2>&1 && sudo docker info >/dev/null 2>&1; then
      XTREAM_COMPOSE=(sudo "${XTREAM_COMPOSE[@]}")
      xtream_info 'Sem acesso direto ao Docker: comandos executados com sudo.'
    else
      xtream_erro 'Docker instalado, mas daemon parado ou acesso negado.'
      xtream_erro 'Tente: sudo systemctl start docker && sudo docker info'
      return 1
    fi
  fi
  xtream_info 'Docker pronto. Continuando para o sistema.'
}
