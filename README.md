## Versão 1.8.4 — FILMES ADULTOS fora de Filmes

A categoria `FILMES ADULTOS` agora fica ao lado de `Filmes`, `Series` e `TV ao vivo`. Veja [ATUALIZACAO-1.8.4.md](ATUALIZACAO-1.8.4.md) para a migração automática e cuidados com conflitos.

## Versão 1.8.3 — permissões das subpastas

Veja [ATUALIZACAO-1.8.3.md](ATUALIZACAO-1.8.3.md). Para a biblioteca em `/media/ZimaOS-HD/AppData/XtreamStrmOrganizer/Biblioteca_IPTV`, use `docker-compose.zimaos-hd.yml`. Preserve os caminhos da instalação existente.

# Versão 1.8.2

Avisos claros por filme, canal, série, temporada e episódio. Histórico persistente em config/log_avisos.txt e botão para baixar pelo painel. Veja [alterações e validação](ATUALIZACAO-1.8.2.md).

# Pacote 1.8.1 corrigido

Inclui o .env.example exigido na primeira instalação. iniciar-linux.sh mantém a janela interativa aberta até pressionar Enter e grava a saída completa do Docker em logs/. Veja [a correção do instalador](CORRECAO-INSTALADOR.md).

# Atualização 1.8.1

Agendamento com seis opções próprias de capas/NFOs, interface otimizada para celular e login lembrado. Veja [instruções da versão atual](ATUALIZACAO-1.8.1.md) e [validação atual](VALIDACAO-1.8.1.md).

# Atualização 1.8.0

Capas e NFOs opcionais para Emby. Veja [instruções de atualização](ATUALIZACAO-1.8.0.md) e [validação](VALIDACAO-1.8.0.md). As instruções abaixo foram preservadas como referência das funcionalidades anteriores.

## Documentação das funcionalidades e testes locais

# Xtream STRM Organizer v1.7.3 — Instalação via GitHub e ZimaOS

**Instrução atual de instalação (recomendada):** veja [README-ZimaOS.md](README-ZimaOS.md). Essa modalidade não precisa copiar código C# para o ZimaOS; compila no GitHub e instala por imagem GHCR.

**IMPORTANTE:** publicar o ZIP aqui não publica a imagem automaticamente. É preciso criar o repositório no GitHub, enviar os arquivos e esperar o Actions publicar a imagem.

---

## Documentação das funcionalidades e testes locais

**ZimaOS:** abra `README-ZimaOS.md` e use `docker-compose.zimaos.yml` para instalar pelo painel sem `.env` nem `apt`.

# Xtream STRM Organizer v1.7 — C# + Docker (Linux / Zorin / ZimaOS)

Organiza catálogos Xtream Codes em arquivos `.strm` por TV, filmes, séries, categorias e temporadas. **Não baixa vídeos**, apenas gera os atalhos do stream. Use somente fontes a que tenha direito de acesso.

## Novidades v1.7 — escrita acelerada e segura

- A criação de `.strm` agora usa **uma fila de gravação assíncrona e limitada (512 itens)**, com **4 gravadores em paralelo** por padrão. A API pode continuar entregando filmes, canais e episódios enquanto os gravadores trabalham. A fila limitada evita consumo excessivo de memória com bibliotecas grandes.
- Pastas já preparadas são reutilizadas durante a execução, sem executar `Directory.CreateDirectory` para cada vídeo.
- A existência de cada arquivo é verificada **uma vez** pelo trabalhador (e confirmada de forma atômica ao renomear), evitando a verificação duplicada de arquivos novos.
- Cada `.strm` novo passa por arquivo temporário no mesmo diretório antes de ser renomeado sem sobrescrita, para evitar deixar arquivos incompletos; arquivos antigos não são alterados, mesmo quando os links mudam na API.
- O painel mostra **arquivos aguardando gravação** e **gravadores simultâneos**. A Atividade recente registra totais e marcos de 2.000 arquivos existentes, para não sobrecarregar o disco com milhares de linhas de log.
- Configure `STRM_WRITE_WORKERS=2`, `4` ou `8` no `.env` para ajustar ao seu disco (1 a 16 suportados). **HDD: comece com 2 ou 4; SSD: 4 ou 8.** Mais gravadores não garantem maior velocidade.
- Não apaga, limpa nem move bibliotecas existentes. Mantém login, configurações, permissões do usuário Linux e agendamento de 12h/24h.

**Importante:** o ganho varia conforme HDD/SSD, número de arquivos e latência do sistema de arquivos. A análise Xtream e a velocidade da própria API continuam sendo fatores separados.

## Novidades v1.6

- **Cadastro inicial na interface web (senha a partir de 6 caracteres)**: escolha seu próprio usuário e senha no primeiro acesso. Sem senha aleatória e sem popup de autenticação do navegador. A conta é salva com hash **PBKDF2-SHA256 + salt** em `./config/account.json`, não com a senha em texto claro.
- **Várias listas Xtream**: cadastre e escolha pelo seletor, salve URLs, usuários, senhas e opções. Cada lista tem sua própria pasta em `./Biblioteca_IPTV/Nome da Conexão/`. Se a conexão for renomeada, novas gravações usam a nova pasta; a antiga é preservada. Não existe pasta intermediária Conexoes.
- **Horário da atualização e próxima execução**: para cada lista, escolha no relógio a hora local do servidor (por exemplo, 03:00). O painel mostra a próxima execução com **dia, mês, ano, hora, segundos, fuso do servidor e contagem regressiva**, além das próximas execuções das demais listas. A programação fica em `config/connections.json`. Intervalo 12h executa no horário escolhido e 12h depois; 24h executa uma vez por dia no horário escolhido. O contêiner usa `TZ: Europe/Brussels` por padrão (configurável). A verificação acontece a cada ~30 segundos, e uma sincronização em andamento pode atrasar outra.
- **Atualização automática por lista**: escolha quais listas terão atualização; dentro de cada lista escolha TV ao vivo, Filmes e/ou Séries; selecione **a cada 12 horas** ou **a cada 1 dia (24 horas)**. Desative individualmente quando quiser.
- A agenda é persistida em `./config/connections.json` e retomada após reiniciar o contêiner. O serviço consulta a agenda a cada ~30 segundos e executa **uma lista de cada vez**; agendamentos vencidos esperam a sincronização atual terminar.
- A atualização agendada consulta o catálogo e os detalhes das séries novamente. **Não apaga ou sobrescreve .strm existentes**. Se um link mudar, o arquivo antigo permanece como você solicitou; para recriar links antigos é necessário tratar isso manualmente.
- Sem filtros de conteúdo, mantendo as categorias originais da API.

## Instalar no Zorin OS / Ubuntu

1. Extraia o ZIP e abra o terminal na pasta `XtreamStrmOrganizer`.
2. Execute:

```bash
./iniciar-linux.sh
```

O script verifica se existe Docker Engine e Compose. Se Compose estiver ausente, usa o instalador automático disponibilizado no projeto. Os dados não são apagados entre instalações.

3. Ao iniciar, o terminal mostra **http://localhost:8686** e, quando disponível, o IP para acesso pela rede local. A página de cadastro solicita senha com **no mínimo 6 caracteres**.
4. Na primeira abertura, acesse a página **Criar conta** e defina seu usuário e sua senha. Depois faça login.
5. Em **Conexão Xtream**, digite nome da lista, URL, usuário e senha da API. Marque TV/Filmes/Séries e clique **Salvar conexão**.
6. Em **Atualização automática**, ative o agendamento da lista atual, selecione conteúdos, o intervalo `12 horas` ou `24 horas`, escolha o horário (formato HH:mm) e salve. Confira a caixa **Próxima execução**.
7. Para adicionar outras listas, use **Nova lista**; cada uma pode ter sua própria programação.

## Testar no Linux

```bash
./testar-linux.sh
```

Executa teste isolado com API simulada, sem precisar usar sua conta real. Valida o cadastro inicial, login, duas conexões independentes, programação em 12 e 24 horas, criação dos `.strm`, cache e preservação de arquivos existentes. Testes dependem do Docker funcionando no seu Linux.

Para parar:

```bash
./parar-linux.sh
```

## Docker Compose / ZimaOS

`docker-compose.yml` contém:

```yaml
services:
  xtream-strm:
    build: .
    container_name: xtream-strm
    restart: unless-stopped
    user: "${PUID:-1000}:${PGID:-1000}"
    ports:
      - "8686:8080"
    environment:
      TZ: Europe/Brussels
      OUTPUT_ROOT: /media
      CONFIG_FILE: /app/data/config.json
    volumes:
      - ./config:/app/data
      - ${MEDIA_PATH:-./Biblioteca_IPTV}:/media
```

- Com **`user: "${PUID:-1000}:${PGID:-1000}"`**, o contêiner grava arquivos sob o UID/GID do host, sem usar root. O script detecta os IDs automaticamente no Linux; no ZimaOS/Compose manual defina `PUID` e `PGID` no `.env` conforme `id -u` e `id -g`. Na primeira execução, arquivos antigos com UID/GID diferente dentro de `config/` e `Biblioteca_IPTV/` são ajustados para seu usuário (sem apagar nada). Se `MEDIA_PATH` apontar para uma pasta externa, ela deve estar gravável pelo UID/GID escolhido; o instalador **não muda recursivamente a propriedade de um disco externo**.
- O volume **`./config:/app/data`** guarda conta, conexões, horários, chaves de sessão e cache. **Nunca use volume anônimo se quiser preservar os dados**.
- O volume **`${MEDIA_PATH:-./Biblioteca_IPTV}:/media`** salva a biblioteca `.strm` no host (padrão `./Biblioteca_IPTV`). Configure `MEDIA_PATH=/seu/disco` no `.env` se desejar outro local.
- Use `docker compose up -d --build` na pasta. Para verificar: `docker compose logs -f xtream-strm`.
- Para instalar via GitHub, a instância no ZimaOS deve ter acesso ao código e à pasta host que receberá os volumes; não basta publicar só o arquivo Compose com `build: .` sem o projeto.

## Segurança e publicação no GitHub

- `.gitignore` exclui `.env`, `config/`, `saida/`, `Biblioteca_IPTV/`, `test-config/` e `test-saida/` de novos commits. **Não publique esses arquivos nem suas credenciais Xtream**. Atenção: `.gitignore` não remove arquivos que já foram commitados em repositórios anteriores.
- Senha do **painel** é um hash com salt; a senha da **API Xtream** é guardada no `connections.json` porque o processo precisa dela para gerar links de stream. Os `.strm` contêm links com usuário e senha Xtream. Restrinja acesso a essas pastas.
- `account.json` e `connections.json` são gravados com permissões Unix para o usuário do contêiner quando suportadas pelo volume; administre permissões da pasta no host.
- Exponha a página somente em rede local ou por HTTPS/VPN. Evite abrir a porta 8686 diretamente na Internet. **A primeira configuração deve ser feita em uma rede confiável**, pois a primeira pessoa que acessar uma instalação nova poderá cadastrar o administrador.
- Caso esqueça a senha, recupere seus dados de um backup do `account.json`; não há recuperação por e-mail. Para iniciar um novo cadastro, é necessário tomar cuidado para não apagar outras configurações.

## Atualizar da v1.3/v1.4/v1.5 (sem perder a biblioteca)

1. **Faça backup** do `.env`, `config/` e `saida/`, ou do destino `MEDIA_PATH` se for um disco diferente.
2. Extraia v1.7.2 em uma pasta nova e copie a pasta `config/` da instalação anterior para a nova pasta (ou mantenha o mesmo volume mapeado no Compose).
3. Copie a pasta `saida/` **sem substituir nem remover arquivos antigos** ou aponte `MEDIA_PATH` para a mesma pasta anterior.
4. Execute `./iniciar-linux.sh`. A versão antiga `config/config.json` será importada para `config/connections.json` automaticamente. As novas gravações usam `Biblioteca_IPTV/Nome da Conexão`; arquivos antigos permanecem intocados e precisam de migração opcional.
5. Se ainda não existir a conta do painel, crie-a na primeira abertura na tela de cadastro. O antigo `WEB_PASSWORD` do `.env` não é usado nesta versão.

### Como funciona a atualização

O agendamento usa a hora local escolhida na tela. A versão 1.6 preserva a próxima execução de listas previamente programadas quando o intervalo e horário continuam iguais; se o usuário alterar o horário ou intervalo, calcula o próximo horário futuro. Atualizações reprogramam a próxima execução ao começar, inclusive se o processo precisar de repetição; no caso de vários agendamentos vencidos, elas são serializadas. Se a instância ficar desligada, o agendamento vencido roda após a próxima inicialização. O PC/servidor precisa estar ligado e o Docker executando. O progresso aparece no painel em Atividade recente.

*Esta versão contém testes de regressão para criação e preservação de arquivos. Para validação real, execute `./testar-linux.sh` em Linux com Docker. A compilação .NET não está disponível neste ambiente.*

### Permissões e início pelo terminal (v1.6+)

- Inicie como seu usuário normal: `./iniciar-linux.sh` (não use `sudo ./iniciar-linux.sh`). O script pedirá sudo apenas se necessário para ajustar arquivos antigos sem a propriedade correta ou instalar dependências.
- O endereço de acesso é impresso ao final no próprio terminal: `http://localhost:8686`; na primeira abertura, crie usuário e senha a partir de 6 caracteres.
- Para ver a propriedade no host: `ls -ld config saida && ls -l config`.
- Se a biblioteca fica em outro disco (`MEDIA_PATH`), verifique que o UID/GID tem acesso de leitura/gravação. Não se faz `chmod 777` nem `chown` em toda a unidade automaticamente.
- O contêiner roda como usuário não root também no teste Docker; o teste valida as permissões do arquivo de conta, da lista de conexões e de um `.strm`.


## Horários e fuso (v1.6)

- O horário mostrado é o do **servidor Docker**, por padrão `Europe/Brussels`, não o fuso do navegador. Configure `TZ` em `docker-compose.yml` caso queira usar `America/Sao_Paulo`, por exemplo. Ao mudar `TZ`, revise e salve os agendamentos.
- O horário pode sofrer uma pequena diferença devido à verificação a cada 30 segundos e ao fato de que somente uma sincronização roda por vez.
- Em transições de horário de verão, horários locais inexistentes são deslocados até o primeiro horário válido; horários repetidos usam a primeira ocorrência.
- Em instalações v1.5, o painel infere o horário de referência da próxima execução salva, evitando trocar um agendamento existente silenciosamente.
- Para ver as execuções também nos logs, use `docker compose logs -f xtream-strm`.


## Teste da fila acelerada (v1.7)

`./testar-linux.sh` inclui um catálogo sintético extra com 750 filmes para exercitar o limite da fila (512), confirmar a criação correta de novos `.strm` e uma segunda execução que ignora todos os arquivos existentes. Os testes usam a API simulada e não exigem suas credenciais reais. O programa não promete um ganho percentual fixo; monitore a fila de arquivos no painel e ajuste `STRM_WRITE_WORKERS` conforme seu disco.

## ZimaOS — estrutura de pastas na versão 1.7.2

A instalação do ZimaOS utiliza `/DATA/AppData/XtreamStrmOrganizer/Biblioteca_IPTV/Nome da Conexão/TV ao vivo`, `Filmes` e `Series`, sem `saida`, `Biblioteca IPTV` ou `Conexoes` como intermediárias. Consulte `README-ZimaOS.md`.

**Migração:** pastas antigas não são apagadas nem automaticamente movidas. Faça backup e use opcionalmente `migrar-biblioteca-zimaos.py` para copiar apenas arquivos ainda não existentes.
