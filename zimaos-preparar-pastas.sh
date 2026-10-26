#!/usr/bin/env sh
set -eu
BASE="${XTREAM_ZIMAOS_DIR:-/DATA/AppData/XtreamStrmOrganizer}"
PUID="${PUID:-1000}"; PGID="${PGID:-1000}"
case "$PUID:$PGID" in
  *[!0-9:]*|:*|*:) echo '[Xtream] ERRO: IDs de usuário inválidos.' >&2; exit 1 ;;
esac
if [ "$PUID" -eq 0 ]; then echo '[Xtream] ERRO: não use UID 0 para a aplicação.' >&2; exit 1; fi
if [ ! -f "$BASE/app/XtreamStrmOrganizer.csproj" ]; then
  echo "[Xtream] ERRO: primeiro copie o projeto para $BASE/app (faltou .csproj)." >&2
  exit 1
fi
for pasta in config Biblioteca_IPTV build; do
  dest="$BASE/$pasta"
  if [ ! -d "$dest" ]; then
    mkdir -p "$dest"
    if [ "$(id -u)" -eq 0 ]; then chown "$PUID:$PGID" "$dest"; fi
    echo "[Xtream] Criada $dest com UID/GID $PUID:$PGID."
  else
    dono="$(stat -c %u "$dest")"
    grupo="$(stat -c %g "$dest")"
    if [ "$dono:$grupo" != "$PUID:$PGID" ]; then
      echo "[Xtream] ATENÇÃO: $dest já existia com dono $dono:$grupo; não vou alterar seus arquivos automaticamente." >&2
      echo "[Xtream] Confirme no ZimaOS as permissões do caminho. O contêiner precisa gravar como $PUID:$PGID." >&2
    else
      echo "[Xtream] Pasta existente preservada: $dest"
    fi
  fi
done
echo '[Xtream] Preparação concluída. Importe docker-compose.zimaos.yml no painel ZimaOS.'
