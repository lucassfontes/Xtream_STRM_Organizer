# Correção do pacote 1.8.1

O ZIP anterior estava sem .env.example, necessário quando não existe .env. Este pacote inclui o arquivo, com fuso Europe/Brussels, pasta ./Biblioteca_IPTV e quatro gravadores STRM. Um .env existente continua preservado.

O iniciar-linux.sh agora aguarda Enter no fim, tanto no sucesso quanto no erro, quando executado em um terminal interativo. Fechar o terminal manualmente continua possível. Em automações/sem terminal não há espera; XTREAM_NO_PAUSE=1 também desativa a pausa.

A saída do Docker é exibida sem interface dinâmica e salva em logs/iniciar-DATA-HORA-PID.log. O código de erro original é mantido. Não há remoção automática de contêineres.

## Usar

Extraia o ZIP completo e copie os arquivos de código para a pasta atual do projeto, incluindo .env.example. No gerenciador de arquivos Linux, Ctrl+H mostra arquivos iniciados por ponto. Mantenha .env, config e sua biblioteca existentes.

Na pasta em que está iniciar-linux.sh, execute:

~~~bash
bash iniciar-linux.sh
~~~

Se aparecer erro do Docker, a janela ficará aberta. Copie a mensagem completa; o log também estará na pasta logs.

A versão do aplicativo continua 1.8.1: esta é uma correção do instalador e do empacotamento. As funcionalidades de agendamento, capas/NFOs, celular e login permanecem.

## Validação

- Sintaxe Bash verificada.
- Primeira inicialização testada em pasta temporária sem .env: criação e permissão 600 verificadas.
- Segunda inicialização: conteúdo personalizado do .env preservado.
- Falha simulada do Docker: mensagem completa preservada no log e código de saída mantido.
- Terminal PTY real: janela aguardando Enter no sucesso e no erro.
- Teste adicionado ao CI e executado também a partir do ZIP extraído.

Docker, sudo e chown são simulados nesse teste; criação/leitura dos arquivos e o terminal são reais. Não houve execução de contêiner Docker neste ambiente. O erro do daemon mostrado pelo usuário estava truncado; sua causa ainda depende da mensagem completa.
