#!/usr/bin/env bash
# Funções compartilhadas. Não usar sudo para iniciar o projeto: o sudo será
# requisitado somente se arquivos antigos do contêiner pertencerem ao root.
xtream_uid_gid() {
  if [ "$(id -u)" -eq 0 ] && [ -n "${SUDO_UID:-}" ] && [ -n "${SUDO_GID:-}" ]; then
    PUID="$SUDO_UID"; PGID="$SUDO_GID"
  elif [ "$(id -u)" -eq 0 ]; then
    # Execução como root sem sudo (ex.: ZimaOS): usar UID/GID do .env, ou 1000.
    PUID="${PUID:-1000}"; PGID="${PGID:-1000}"
  else
    PUID="$(id -u)"; PGID="$(id -g)"
  fi
  export PUID PGID
  case "$PUID:$PGID" in
    *[!0-9:]*|:*|*:) xtream_erro 'PUID/PGID precisam ser números válidos.'; return 1 ;;
  esac
  if [ "$PUID" -eq 0 ]; then
    xtream_erro 'Recusando executar a aplicação como root: configure PUID diferente de 0.'
    return 1
  fi
}

xtream_preparar_permissoes() {
  local dir media_dir marcador
  # No Linux comum, o script identifica o usuário real, mesmo quando usa
  # sudo internamente para ter acesso ao daemon Docker.
  xtream_uid_gid || return 1
  xtream_info "Arquivos Docker serão gravados como UID=$PUID GID=$PGID (sem root)."
  umask 077
  if [ ! -f .env ]; then
    if [ ! -f .env.example ]; then
      xtream_erro 'O arquivo .env.example está ausente. Extraia o ZIP completo e execute dentro da pasta XtreamStrmOrganizer.'
      return 1
    fi
    cp .env.example .env || return 1
    chmod 600 .env || return 1
    xtream_info 'Arquivo .env criado automaticamente.'
  fi
  if [ ! -w .env ]; then
    # Instalações anteriores feitas com sudo podiam deixar o .env pertencendo ao root.
    if [ "$(stat -c %u .env)" -eq 0 ]; then
      xtream_info 'Corrigindo propriedade antiga de .env...'
      xtream_root chown "$PUID:$PGID" .env || return 1
    fi
    if [ ! -w .env ]; then
      xtream_erro 'O arquivo .env não é editável pelo usuário atual. Verifique sua propriedade.'
      return 1
    fi
  fi
  chmod 600 .env || return 1
  # Mesmo se for executado por sudo, .env fica acessível ao usuário real.
  if [ "$(id -u)" -eq 0 ]; then
    chown "$PUID:$PGID" .env || return 1
  fi
  mkdir -p config Biblioteca_IPTV || {
    xtream_erro 'Não consegui criar config/ e Biblioteca_IPTV/. Verifique propriedade da pasta do projeto.'
    return 1
  }

  # Corrige arquivos históricos dos volumes do projeto cuja propriedade é
  # diferente da conta atual (root, UID antigo etc.); não remove arquivos.
  marcador="config/.ownership-v15-${PUID}-${PGID}"
  if [ ! -f "$marcador" ]; then
    xtream_info 'Conferindo a propriedade de config/ e Biblioteca_IPTV/ (sem excluir dados)...'
    for dir in config Biblioteca_IPTV; do
      if ! xtream_root find "$dir" -xdev \( ! -uid "$PUID" -o ! -gid "$PGID" \) -exec chown -h "$PUID:$PGID" {} +; then
        xtream_erro "Não consegui ajustar a propriedade dos arquivos em $dir."
        return 1
      fi
    done
    # Donos não root de outras contas não são modificados.
    if [ ! -w config ] || [ ! -w Biblioteca_IPTV ]; then
      xtream_erro 'config/ ou Biblioteca_IPTV/ não é editável por esse usuário. Confira ls -ld config Biblioteca_IPTV.'
      return 1
    fi
    touch "$marcador" || return 1
    chmod 600 "$marcador" || return 1
    if [ "$(id -u)" -eq 0 ]; then
      chown "$PUID:$PGID" "$marcador" || return 1
    fi
  fi

  # Se MEDIA_PATH for um caminho personalizado, criá-lo como o usuário atual.
  # Nunca dar chown recursivo automaticamente em um disco compartilhado.
  media_dir="$(sed -n 's/^[[:space:]]*MEDIA_PATH=//p' .env | tail -n 1)"
  media_dir="${MEDIA_PATH:-${media_dir:-./Biblioteca_IPTV}}"
  # Valores da forma MEDIA_PATH="/disco/meus filmes" são aceitos.
  case "$media_dir" in
    \"*\") media_dir="${media_dir:1:${#media_dir}-2}" ;;
    \'*\') media_dir="${media_dir:1:${#media_dir}-2}" ;;
  esac
  if [ "$media_dir" != './Biblioteca_IPTV' ] && [ "$media_dir" != 'Biblioteca_IPTV' ]; then
    local created_media=0
    if [ ! -d "$media_dir" ]; then created_media=1; fi
    if ! mkdir -p "$media_dir"; then
      xtream_erro "MEDIA_PATH '$media_dir' não é editável. Corrija propriedade deste destino no host."
      return 1
    fi
    if [ "$created_media" -eq 1 ] && [ "$(id -u)" -eq 0 ]; then
      chown "$PUID:$PGID" "$media_dir" || return 1
    fi
    # Se o script for iniciado como root, -w seria sempre verdadeiro.
    if [ ! -w "$media_dir" ] || [ "$(stat -c %u "$media_dir")" != "$PUID" ]; then
      xtream_erro "MEDIA_PATH '$media_dir' precisa pertencer e permitir gravação ao UID=$PUID."
      return 1
    fi
    xtream_info "Pasta externa detectada: $media_dir. Arquivos antigos não terão propriedade alterada automaticamente."
  fi
  # Evita bloquear acesso a pastas previamente criadas com permissão 700
  # quando a aplicação é executada pelo usuário correto.
  if [ ! -w config ] || [ ! -w Biblioteca_IPTV ]; then
    xtream_erro 'Pasta de trabalho não permite gravação. Verifique permissões.'
    return 1
  fi
}
