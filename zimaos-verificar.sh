#!/usr/bin/env sh
set -eu
BASE="${XTREAM_ZIMAOS_DIR:-/DATA/AppData/XtreamStrmOrganizer}"
if ! command -v docker >/dev/null 2>&1; then
  echo '[Xtream] ERRO: Docker não encontrado. No ZimaOS, use o Docker integrado.' >&2
  exit 1
fi
printf '[Xtream] Docker: '; docker --version
if ! docker info >/dev/null 2>&1; then
  echo '[Xtream] ERRO: sem acesso ao serviço Docker. Abra o terminal autorizado do ZimaOS.' >&2
  exit 1
fi
if [ ! -f "$BASE/app/XtreamStrmOrganizer.csproj" ]; then
  echo "[Xtream] ERRO: coloque os arquivos do projeto em $BASE/app" >&2
  exit 1
fi
for d in config Biblioteca_IPTV build; do
  if [ ! -d "$BASE/$d" ]; then
    echo "[Xtream] ATENÇÃO: a pasta $BASE/$d ainda não existe. Crie pelo gerenciador de arquivos." >&2
    exit 1
  fi
  if command -v stat >/dev/null 2>&1; then
    printf '[Xtream] Pasta %s: ' "$d"
    stat -c 'dono UID=%u GID=%g permissão=%a' "$BASE/$d" || true
  fi
done
printf '[Xtream] Compose: '
if docker compose version >/dev/null 2>&1; then docker compose version
else echo 'indisponível (pode usar a instalação personalizada na interface web).'; fi
echo '[Xtream] Estrutura verificada. Abra a interface ZimaOS e importe docker-compose.zimaos.yml.'
