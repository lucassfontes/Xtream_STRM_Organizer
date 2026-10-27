# Validação 1.8.1 — 27/10/2026

## Aprovado

- Build Release em .NET 8: zero erros e zero avisos.
- Publish Release do aplicativo.
- **3.640 verificações C#**, preservando os testes da 1.8.0 e acrescentando:
  - 64 combinações próprias do agendamento, com opções manuais opostas.
  - Persistência por lista, conservação da próxima execução e compatibilidade com clientes sem os novos campos.
  - Execução efetiva pelo AutoUpdateService com agenda vencida, geração de arquivos e persistência da próxima data.
  - Não alteração das opções manuais durante execução automática.
- Testes HTTP: cadastro, login/logout, autorização, campos manuais/agendados independentes, senha da conexão preservada.
- Cookie HttpOnly com duração de 30 dias; reabertura simulada do navegador carregando cookies persistentes; reinício real do aplicativo usando as mesmas chaves; logout; login sem persistência quando desmarcado.
- **Chromium 131 real, automatizado:** interface em 320, 390, 768 e 1440px, sem transbordamento horizontal da página. Cadastro e login pelas telas, salvamento e recarregamento das escolhas próprias do agendamento, nome de usuário lembrado, sessão restaurada em novo contexto de navegador, saída e desativação da persistência. Sem erros JavaScript.
- Capturas da interface móvel/desktop e login inspecionadas.
- Migração e instalador: testes antigos aprovados. O teste de instalador utiliza Docker simulado.
- Conferência do ZIP, preservação dos arquivos originais e ausência de dados pessoais no pacote.

## Ainda depende do ambiente real

- Safari/iPhone físico e outros navegadores não foram executados; o teste responsivo usou Chromium.
- Docker/ZimaOS real e publicação GHCR não foram executados aqui. O workflow do projeto inclui build/testes e há workflow de inicialização Docker.
- API Xtream e Emby reais continuam sem validação por falta de acesso. O teste de geração usa dados fictícios controlados.
- Não houve espera de 30 dias; a expiração do cookie foi verificada no momento da emissão. Não houve espera de 12/24 horas; o serviço automático foi testado com horário já vencido.
- Nenhum push no GitHub nem alteração no servidor do usuário foi realizado.

Para conferir após atualizar: habilite opções diferentes no modo manual e automático, salve, recarregue, verifique a próxima execução e faça login com “Manter conectado” marcado. Feche/reabra o navegador no mesmo endereço.
