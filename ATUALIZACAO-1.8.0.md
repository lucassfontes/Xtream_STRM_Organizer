# Xtream STRM Organizer 1.8.0

Projeto completo baseado no commit `0a45ae107752edab617ae875f6fbf6f558ce1323` da branch main de lucassfontes/Xtream_STRM_Organizer.

## Atualizar o GitHub e ZimaOS

1. Extraia o ZIP e copie o conteúdo da pasta do projeto para a raiz do seu repositório. Inclua a pasta oculta `.github`, o Dockerfile, os testes e os arquivos novos.
2. Mantenha as pastas de dados da instalação atual: `config`, biblioteca, cache e chaves. O ZIP contém código, não substitui seus dados ou suas credenciais.
3. Faça commit e envie para main. O workflow existente agora compila e roda os testes antes de publicar a imagem no GHCR. Aguarde o resultado verde.
4. No ZimaOS, atualize a imagem do aplicativo mantendo os mesmos volumes, porta e variáveis de ambiente. Para uma instalação por Compose com o arquivo GHCR, os comandos são:
   ```sh
   docker compose -f docker-compose.github.yml pull
   docker compose -f docker-compose.github.yml up -d
   ```
   Use o arquivo/serviço efetivamente utilizado na sua instalação; não crie um segundo aplicativo sobre os mesmos dados.
5. Confira `/health` e a tela: ambos devem indicar **1.8.0**.
6. Selecione sua lista, marque as opções desejadas, clique em **Salvar conexão** e depois **Atualizar biblioteca**.

As seis opções começam desmarcadas em configurações antigas, para preservar o comportamento anterior. São persistidas por lista e também usadas nas execuções automáticas. O conteúdo correspondente (TV, filmes ou séries) precisa estar habilitado para ser processado.

Para compilar a imagem local a partir deste código:
```sh
docker compose build
docker compose up -d
```

## O que muda

- Seis checkboxes independentes: capas e NFOs de TV, filmes e séries.
- Filmes consultam `get_vod_info` quando faltam arquivos selecionados; TV usa os campos de `get_live_streams`; séries, temporadas e episódios usam `get_series_info`.
- Sinopse, título, data/ano, avaliação, duração, gêneros, elenco, diretor, país e identificadores TMDB/IMDb são gravados quando fornecidos e válidos.
- NFOs XML em UTF-8, com escape de caracteres. Nenhuma sinopse ou imagem é inventada quando o provedor não a fornece.
- Séries continuam nas mesmas pastas de categoria, série e `Temporada NN`. Não há renomeação, movimentação ou exclusão dos STRMs anteriores.
- `tvshow.nfo` e `poster.jpg/png` na pasta da série; `season.nfo` e `poster.jpg/png` na temporada; NFO com o mesmo nome do episódio e imagem `nome-do-episodio-thumb.jpg/png` ao lado do STRM.
- Filme/canal usa `nome-do-strm.nfo` e `nome-do-strm-poster.jpg/png`. Isso mantém os filmes/canais em suas pastas atuais, sem misturar capas de vários itens.
- Temporada sem capa própria usa a capa da série se disponível. Episódio sem miniatura permanece sem imagem.
- Cache antigo é aceito; se faltarem detalhes necessários, uma consulta o completa. Falha de atualização mantém o cache anterior.
- Capas e NFOs existentes são preservados, inclusive ao usar consulta completa. Apenas ausentes são criados, mesmo quando o STRM já existe. A proteção usa gravação temporária seguida de renomeação sem sobrescrita.
- Falhas de imagem/NFO são contabilizadas como avisos; não impedem a geração dos demais STRMs. Uma imagem interrompida não é publicada como arquivo final.
- JPEG e PNG são reconhecidos pelo conteúdo; limite de 20 MiB por imagem e timeout de leitura. WebP/GIF/HTML não são salvos com extensão JPEG falsa e geram aviso.
- Login, agendamento, perfis, ícone, layout, nomes de pastas e scripts existentes foram mantidos. O retorno da fila de STRMs identifica o caminho final para manter os arquivos auxiliares junto do item correto em colisões de nome.

A regra de não sobrescrever se aplica à biblioteca. Salvar preferências, atualizar cache e registrar atividade continua alterando os respectivos arquivos operacionais, como nas versões anteriores.

## Emby

Cadastre filmes como biblioteca de Filmes e séries como biblioteca de Séries/TV. Como a estrutura anterior tem categorias, prefira adicionar cada pasta de categoria de séries como uma pasta da biblioteca; assim os filhos imediatos são as séries. Ative a leitura de NFOs na biblioteca quando a sua versão expuser essa opção. Faça uma varredura/atualização de dados ausentes, sem pedir substituição das suas artes.

**TV ao vivo:** os NFOs são do tipo `movie` e descrevem o STRM como item de uma biblioteca de vídeos. Eles **não configuram canais, programação ou logos no módulo Live TV**. Para Live TV nativo, configure a fonte M3U do provedor e o guia XMLTV no Emby. Este projeto não cria nem atualiza uma playlist M3U ou guia XMLTV. A sinopse do programa no ar não é consultada nem usada como sinopse permanente do canal.

Documentação oficial consultada:
- https://emby.media/support/articles/TV-Naming.html
- https://emby.media/support/articles/M3U-Tuners.html
- https://emby.media/support/articles/Live-TV.html
- https://support.emby.media/support/articles/Xml-Tv.html

## Testar

Requer SDK .NET 8 para os testes C#. Não usa credenciais reais nem pacotes de teste NuGet externos.

```sh
dotnet build XtreamStrmOrganizer.csproj -c Release
dotnet run --project tests/MetadataTests -c Release
dotnet publish XtreamStrmOrganizer.csproj -c Release -o /tmp/xtream-publish -p:UseAppHost=false
python3 tests/test-http.py /tmp/xtream-publish
python3 tests/test-migracao.py
bash tests/test-instalador.sh
```

A suíte C# é um executável de testes, executado por `dotnet run`, e retorna código diferente de zero em falha. Não é uma suíte descoberta por `dotnet test`.

O workflow `testes.yml` também prepara uma imagem Docker e testa sua inicialização em pull requests ou execução manual. Esse workflow foi incluído, mas não foi executado no seu GitHub durante esta entrega. Consulte `VALIDACAO-1.8.0.md` para os resultados locais e testes ainda pendentes.
