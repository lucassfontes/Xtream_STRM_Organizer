# ZimaOS — instalação direto pelo GitHub

**Versão: 1.7.3 / C# .NET 8.** Não é preciso copiar o código-fonte C# para o servidor. O GitHub Actions compila e envia uma imagem pronta para o GitHub Container Registry (`ghcr.io`).

> **Status importante:** este ZIP prepara a publicação. A imagem `ghcr.io/lucassfontes/xtream_strm_organizer:latest` **ainda não existe até você criar o repositório e deixar o GitHub Actions publicar com sucesso**. Não tente importar o Compose antes disso.

## 1) Criar o repositório

1. Acesse https://github.com/new, logado no perfil `lucassfontes`.
2. Nome: `Xtream_STRM_Organizer`; visibilidade **Public**, sem README nem `.gitignore` iniciais.
3. Extraia o ZIP. Use **a pasta interna** `XtreamStrmOrganizer/` como raiz do repositório (o Dockerfile deve ficar na raiz).
4. Abra o terminal na pasta interna e execute:

```bash
./enviar-github-linux.sh
```

O script usa `git push` e exige que o seu computador já consiga autenticar no GitHub (SSH ou HTTPS, conforme configuração local). Alternativamente, envie todos os arquivos, inclusive `.github/workflows/publicar-ghcr.yml`, por Git ou pela interface do GitHub.

**Nunca envie as pastas `config/`, `Biblioteca_IPTV/`, nem o `.env` para o GitHub.** Elas ficam ignoradas pelo `.gitignore` e `.dockerignore`.

## 2) Publicar a imagem

1. Abra seu repositório no GitHub e acesse **Actions**.
2. Aguarde o fluxo **Publicar imagem Docker no GitHub (GHCR)** ficar **verde** (ou use **Run workflow**).
3. A imagem será publicada como `ghcr.io/lucassfontes/xtream_strm_organizer:latest`.
4. O GitHub publica novos pacotes GHCR como privados por padrão. Abra o pacote **xtream_strm_organizer** no seu perfil > **Package settings** > **Change visibility** > **Public** para o ZimaOS poder baixar sem login.

> Tornar a imagem pública expõe o software, mas **não** expõe suas configurações; mantenha dados Xtream exclusivamente em `config` e na biblioteca, fora do GitHub.

## 3) Instalar pelo painel do ZimaOS

1. No ZimaOS, abra **App Store / Instalar aplicativo personalizado** e escolha **Importar Docker Compose**.
2. Copie o conteúdo de `docker-compose.zimaos.yml` (também fornecido separadamente como `docker-compose-xtream-github-zimaos.yml`).
3. Confira os caminhos em `/DATA/AppData/XtreamStrmOrganizer/` e a porta 8686.
4. Clique em **Instalar**. O ZimaOS baixa a imagem do GitHub, sem compilar C# localmente.
5. Abra `http://IP-DO-ZIMAOS:8686` e crie o usuário e senha de no mínimo seis caracteres.

O contêiner de inicialização `preparar-pastas` ajusta a pasta `config` e a raiz da biblioteca para UID/GID `1000:1000`; o serviço principal não roda como root. Se o seu usuário/armazenamento usar outro UID/GID, ajuste-os no Compose. Bibliotecas antigas com subpastas pertencentes ao root talvez exijam correção pontual de permissões.

## 4) Pastas persistentes

```text
/DATA/AppData/XtreamStrmOrganizer/
├── config/
│   ├── account.json
│   ├── connections.json
│   ├── keys/
│   └── cache/
└── Biblioteca_IPTV/
    ├── Nome da Conexao 1/
    │   ├── TV ao vivo/...
    │   ├── Filmes/...
    │   └── Series/...
    └── Nome da Conexao 2/...
```

Sem pastas `saida`, `Conexoes` ou uma segunda `Biblioteca_IPTV` no caminho. A estrutura do gerador permanece não destrutiva: arquivos `.strm` já existentes são ignorados, não apagados.

## 5) Atualizar o aplicativo

Atualize os arquivos do repositório e faça `git push` para a branch `main`. O GitHub Actions publica a nova imagem `:latest`. No ZimaOS, use atualizar/recriar aplicativo para baixar a nova imagem, **sem apagar** as pastas persistentes. O `pull_policy: always` ajuda a baixar a imagem ao recriar, mas **não faz atualização automática de um contêiner já em execução**.

## 6) Problemas comuns

- `manifest unknown` / `pull access denied`: o fluxo Actions ainda não terminou, a imagem está privada, ou o nome do repositório/imagem é diferente do indicado.
- `permission denied`: confira UID/GID no Compose e a propriedade das pastas do servidor.
- `address already in use` na porta `8686`: outro aplicativo usa a mesma porta. Altere `8686:8080` e `port_map`.
- `docker: unknown command: docker compose`: o ZimaOS instala pelo painel sem você precisar executar o script do Zorin.

**A validação local deste pacote é estrutural (arquivos, sintaxe YAML e scripts). O build GitHub Actions e a execução em Docker/ZimaOS exigem teste após publicação.**
