# Xtream STRM Organizer 1.8.2 — avisos fáceis de entender

Todos os recursos e correções da 1.8.1 foram mantidos. Esta versão melhora a identificação das falhas.

## O que aparece nos avisos

Exemplos ilustrativos:

- Aviso: a capa do filme "Meu Filme" não foi baixada. Motivo: o servidor bloqueou o acesso ao conteúdo (HTTP 403).
- Aviso: a capa da série "Minha Série" não foi baixada. Motivo: o servidor não encontrou o arquivo solicitado (HTTP 404).
- Aviso: a miniatura do episódio S02E03 "O início" da série "Minha Série" não foi baixada. Motivo: o servidor demorou demais para responder.
- Aviso: o NFO da temporada 02 da série "Minha Série" não foi criado. Motivo: o programa não tem permissão para gravar na pasta de destino.
- Aviso: o STRM do canal "Meu Canal" não foi criado. Motivo: não foi possível acessar ou gravar o arquivo.

As mensagens também distinguem imagem não fornecida pelo provedor, endereço inválido, imagem acima do limite, formato não aceito, excesso de solicitações (429) e falhas 5xx. Quando a causa exata não está disponível, o texto indica o que verificar sem afirmar uma causa não confirmada.

Se os detalhes do filme não puderem ser consultados, o aviso informa que o NFO poderá conter apenas os dados básicos disponíveis. Isso é diferente de falha ao gravar o NFO.

## Onde fica log_avisos.txt

O arquivo é criado quando ocorre o primeiro aviso, ao lado das configurações:

- Compose local padrão: **config/log_avisos.txt**, dentro da pasta do projeto.
- Dentro do contêiner: **/app/data/log_avisos.txt**.
- Com CONFIG_FILE personalizado: na mesma pasta desse arquivo.
- ZimaOS: na pasta do host montada como /app/data; mantenha esse volume ao atualizar.

Cada linha tem data, hora, tipo de arquivo, item e motivo. O conteúdo anterior é mantido: o histórico não é apagado ao iniciar outra sincronização. A lista de atividade recente na tela continua limitada às mensagens mais recentes, mas o arquivo guarda os avisos antigos.

Na seção **Gerador de biblioteca**, clique em **Baixar log de avisos**. O download exige login. Antes do primeiro aviso, o botão entrega um TXT indicando que nenhum aviso foi registrado.

Se não for possível gravar o próprio log, o painel informa o problema sem interromper a sincronização. Mensagens brutas de exceção e URLs de acesso não são incluídas no histórico de avisos. Cancelamentos solicitados pelo usuário e arquivos existentes preservados não são classificados como falhas.

Avisos anteriores à instalação desta versão não são convertidos: a identificação dos itens não existia nos registros antigos.

## Atualizar

Extraia o projeto completo sobre os arquivos de código atuais, incluindo arquivos ocultos como .env.example. Preserve .env, config, biblioteca e volumes Docker. Execute:

~~~bash
bash iniciar-linux.sh
~~~

No fluxo GitHub/GHCR, envie os arquivos para main, aguarde a publicação e atualize a imagem. Confirme **1.8.2** em /health e no rodapé do painel. O instalador continua mantendo o terminal aberto e registrando falhas do Docker na pasta logs; esse log do instalador é separado de log_avisos.txt.

## Validação

- Build Release e publish do aplicativo aprovados; build sem erros nem avisos.
- Suíte C# com 3.666 verificações aprovadas: mensagens de HTTP 401/403/404/429/500/503, contexto de filme/série/temporada/episódio, timeout, permissão, imagem ausente, falha real de gravação STRM e NFO em diretórios de teste.
- Histórico persistente e gravações concorrentes do log testados; preservação dos registros anteriores e tratamento da impossibilidade de gravar o log.
- Identificador inválido para STRM registrado com contexto.
- Testes HTTP aprovados: download autenticado do log, recusa sem login, arquivo completo e comportamento quando ainda não existem avisos, além dos testes de sessão e configurações.
- Teste do instalador aprovado, com Docker/sudo/chown simulados.
- Não houve teste em Docker/ZimaOS, API Xtream ou Emby reais nesta entrega.
- O navegador não foi reexecutado nesta versão; o novo botão aponta para o endpoint HTTP validado. Os testes visuais da 1.8.1 permanecem documentados separadamente.

Comandos de testes:

~~~bash
dotnet build tests/MetadataTests/MetadataTests.csproj -c Release
dotnet run --project tests/MetadataTests -c Release
dotnet publish -c Release -o /tmp/xtream-publish -p:UseAppHost=false
python3 tests/test-http.py /tmp/xtream-publish
python3 tests/test-primeira-inicializacao.py
~~~
