# Validação da versão 1.8.0 — 27/10/2026

## Executado com sucesso

- SDK .NET 8.0.408, Linux x64: compilação Release do aplicativo e dos testes, **zero erros e zero avisos**.
- Publicação Release com UseAppHost=false, correspondente ao comando usado pelo Dockerfile.
- Suíte C# com **1.848 verificações**: 64 combinações de opções, configurações persistidas, senha preservada, geração integrada de STRMs e metadados, XML válido, caracteres especiais, duração, temporadas 0/1 e episódio 100, colisões de nomes, preenchimento de arquivos ausentes, cache completo e antigo, execução automática e consulta completa.
- Comparação de hashes dos arquivos da biblioteca de teste antes/depois de uma segunda execução, inclusive STRM personalizado: sem alterações.
- Contagem de requisições de imagens: zero novos downloads quando os arquivos já existem. Ao remover uma miniatura na biblioteca descartável de teste, somente a miniatura ausente foi buscada.
- Escritas concorrentes sobre uma mesma imagem: um download; sem arquivos temporários restantes.
- HTML em lugar de imagem rejeitado; NFO continua sendo criado; próxima tentativa com imagem válida funciona.
- Cancelamento antes da gravação, imagem ausente e extensão alternativa existente.
- Testes antigos de migração e instalador: aprovados. O teste de instalador usa Docker simulado; não instala Docker.
- Aplicativo publicado iniciado: /health, cadastro inicial, login/logout, controle de acesso, seis controles no HTML, persistência independente via API e conservação da senha.
- Sintaxe JavaScript das páginas index/login/setup verificada com Node.
- Verificação de espaços/patch por git diff --check.

A API Xtream e as imagens dos testes C# são fornecidas por um HttpMessageHandler de teste, sem rede externa. Os testes passam pelo Organizer e gravam arquivos reais em pastas temporárias. Os testes HTTP executam o aplicativo publicado e acessam os endpoints locais de verdade.

## Falta validar no ambiente real

- **Imagem Docker em execução e instalação/atualização no ZimaOS:** não havia Docker disponível aqui. O publish do aplicativo foi validado, mas isso não equivale a executar o contêiner. Foi incluído um workflow de build e inicialização Docker para execução no GitHub.
- **Sua API Xtream real:** sem credenciais do provedor, não foi possível confirmar seus campos específicos, disponibilidade das artes, limites de requisições e comportamento de um catálogo grande.
- **Importação e reprodução no Emby real:** não houve acesso ao seu Emby. Os nomes e NFOs seguem as convenções previstas, mas a leitura dos metadados, associação das imagens e reprodução devem ser verificadas na sua instalação.
- **Inspeção visual no navegador desktop/celular:** não realizada; o navegador automatizado não estava disponível e o download falhou. O HTML foi mantido, as seis opções foram verificadas via HTTP e a sintaxe JavaScript foi validada.
- Não houve teste de interrupção de energia, disco cheio nem cancelamento durante um download real.
- Nenhum commit, push, publicação GHCR ou atualização no seu servidor foi feito nesta entrega.

## Checklist de aceitação

1. Instale/atualize a imagem mantendo os volumes atuais e confirme /health = 1.8.0.
2. Comece com uma lista pequena: um canal, um filme e uma série com duas temporadas.
3. Salve e reabra a página; confira as seis opções.
4. Gere capas e NFOs e confira a associação de cada episódio dentro da respectiva temporada.
5. Faça varredura no Emby e confira capa, sinopse, números de episódio/temporada e reprodução.
6. Execute novamente e confira que os arquivos existentes permanecem iguais.
7. Teste o agendamento mantendo as mesmas opções.
8. Para Live TV nativo, use M3U/XMLTV; NFOs de STRM não alimentam canais nem EPG.
