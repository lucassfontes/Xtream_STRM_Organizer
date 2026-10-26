# Instalar via GitHub no ZimaOS

Documentação completa: [README-ZimaOS.md](README-ZimaOS.md).

**Fluxo:** C# no GitHub → GitHub Actions compila → GitHub Container Registry publica imagem → ZimaOS instala a imagem com `docker-compose.zimaos.yml`.

**Não funciona antes de publicar a imagem.** O repositório novo deve chamar-se `Xtream_STRM_Organizer` no perfil `lucassfontes` para que o Compose fornecido funcione sem alterações.

- Fonte: repositório `lucassfontes/Xtream_STRM_Organizer` a criar.
- Imagem após publicação: `ghcr.io/lucassfontes/xtream_strm_organizer:latest`.
- Biblioteca: `/DATA/AppData/XtreamStrmOrganizer/Biblioteca_IPTV/Nome da Conexao/`.
- Persistência: `/DATA/AppData/XtreamStrmOrganizer/config`.
