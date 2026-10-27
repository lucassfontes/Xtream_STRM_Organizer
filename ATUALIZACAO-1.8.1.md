# Xtream STRM Organizer 1.8.1

Inclui o projeto completo da 1.8.0, com as três melhorias abaixo.

## Agendamento com escolhas próprias

Na seção **Atualização automática**, selecione a lista, ative o agendamento e escolha:

- TV: baixar capas / gerar NFOs.
- Filmes: baixar capas / gerar NFOs.
- Séries, temporadas e episódios: baixar capas / gerar NFOs.

As seis opções são independentes das opções da execução manual. Clique em **Salvar agendamento**; a confirmação aparece na própria seção. O horário e as preferências são persistidos por lista, continuam após reiniciar o aplicativo e são usados pelo serviço automático. Alterar apenas as opções não muda o horário já programado.

O conteúdo também precisa estar habilitado em **Conteúdo que será organizado** e em **Conteúdo a atualizar**. Os arquivos existentes continuam preservados.

Configurações antigas sem campos específicos do agendamento herdam as escolhas anteriores até o primeiro salvamento destas opções. Isso mantém o comportamento das instalações antigas.

## Celular

- Menu com largura corrigida, navegação horizontal e acesso às seções durante a rolagem.
- Botões e seletores com áreas maiores para toque.
- Campos com fonte de 16px para evitar o zoom automático ao digitar no iPhone.
- Cartões, métricas, mensagens e opções de agendamento ajustados às telas estreitas.
- Atualizações periódicas da interface são suspensas quando a aba está oculta.
- Tema, cores, ícone e organização principal mantidos.

## Lembrar login

O último **usuário do painel** usado com sucesso aparece preenchido na próxima visita.

A opção **Manter conectado por 30 dias** vem marcada. A sessão é guardada em um cookie HttpOnly e continua após fechar/reabrir o navegador, enquanto válida. O prazo pode ser renovado com o uso. Desmarcar mantém o login limitado à sessão do navegador, com validade de 12 horas.

O botão **Sair da conta** encerra a sessão neste navegador, mas mantém o nome de usuário preenchido. A senha não é gravada pelo aplicativo em localStorage. Limpar os dados do navegador, usar outro dispositivo ou trocar o endereço/origem de acesso exige novo login. As sessões anteriores à atualização precisam de um novo login para ativar a persistência.

O volume de configuração e suas chaves devem ser mantidos nas atualizações, como já ocorria na versão anterior.

## Instalação

1. Extraia o ZIP. Copie o conteúdo da pasta Xtream_STRM_Organizer para a raiz do repositório, incluindo arquivos novos e .github.
2. Preserve os volumes existentes de configuração e biblioteca.
3. Envie para main e aguarde o workflow de testes/publicação no GHCR.
4. Atualize a imagem no ZimaOS mantendo os mesmos volumes e portas.
5. Confira **1.8.1** na interface e em /health.

Os comandos de instalação Docker, a geração de capas/NFOs e as limitações de TV ao vivo permanecem descritos em [ATUALIZACAO-1.8.0.md](ATUALIZACAO-1.8.0.md); use a versão 1.8.1 ao conferir a instalação.

## Testes

Requer SDK .NET 8:

```sh
dotnet build tests/MetadataTests/MetadataTests.csproj -c Release
dotnet run --project tests/MetadataTests -c Release
dotnet publish -c Release -o /tmp/xtream-publish -p:UseAppHost=false
python3 tests/test-http.py /tmp/xtream-publish
python3 tests/test-migracao.py
bash tests/test-instalador.sh
```

Teste opcional de navegador, com Node e Playwright instalados no ambiente de testes:

```sh
npm install --no-save --package-lock=false playwright
npx playwright install chromium
PUBLISH_DIR=/tmp/xtream-publish node tests/ui-smoke.cjs
```

O teste de navegador aceita DOTNET e CHROME_EXECUTABLE para caminhos específicos. UI_SCREENSHOTS é opcional e permite guardar capturas da interface com dados fictícios. O ZIP não inclui navegador, dependências npm, credenciais ou configurações pessoais.
