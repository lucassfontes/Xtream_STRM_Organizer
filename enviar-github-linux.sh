#!/usr/bin/env bash
set -Eeuo pipefail
cd "$(dirname "$0")"
url='https://github.com/lucassfontes/Xtream_STRM_Organizer.git'
command -v git >/dev/null || { echo '[Xtream] Instale git antes de prosseguir.' >&2; exit 1; }
if [ ! -f Dockerfile ] || [ ! -f .github/workflows/publicar-ghcr.yml ]; then
  echo '[Xtream] Execute o script na pasta interna XtreamStrmOrganizer.' >&2; exit 1
fi
if [ ! -d .git ]; then git init -b main; fi
# Prevenir publicação acidental de dados pessoais mesmo se o .gitignore for modificado.
if git check-ignore .env config/account.json config/connections.json Biblioteca_IPTV/exemplo.strm 2>/dev/null | grep -qv '^$'; then
  :
else
  echo '[Xtream] ERRO: confira o .gitignore antes do envio.' >&2; exit 2
fi
if git remote get-url origin >/dev/null 2>&1; then
  actual="$(git remote get-url origin)"
  if [[ "$actual" != "$url" && "$actual" != 'git@github.com:lucassfontes/Xtream_STRM_Organizer.git' ]]; then
    echo "[Xtream] Outro remoto já está configurado: $actual" >&2; exit 3
  fi
else
  git remote add origin "$url"
fi
git add -A
# Bloqueia arquivos confidenciais se alguém os incluiu via git -f antes.
if git diff --cached --name-only | grep -E '(^|/)(config|Biblioteca_IPTV|saida)/|(^|/)\.env$' >/dev/null; then
  echo '[Xtream] ERRO: arquivos privados foram adicionados ao commit. Remova-os antes de publicar.' >&2; exit 4
fi
if ! git diff --cached --quiet; then
  git -c user.name="$(git config user.name || echo Xtream)" -c user.email="$(git config user.email || echo user@localhost)" commit -m 'Publicar Xtream STRM Organizer com imagem Docker GitHub'
fi
git branch -M main
echo '[Xtream] Enviando para o GitHub...'
git push -u origin main
echo '[Xtream] Acompanhe o GitHub Actions. Depois torne o pacote GHCR público.'
