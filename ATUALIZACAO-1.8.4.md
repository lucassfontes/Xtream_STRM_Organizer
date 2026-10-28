# Xtream STRM Organizer 1.8.4

## Nova localização
Dentro da pasta da lista, a categoria de filmes `FILMES ADULTOS` fica diretamente
em `Nome da lista/FILMES ADULTOS`, ao lado de `Filmes`, `Series` e `TV ao vivo`.
Somente essa categoria de filmes é promovida, ignorando diferenças de maiúsculas
ou espaços repetidos. Categorias como `XXX`, `18+` ou `FILMES ADULTOS OUTROS`
continuam com a organização anterior. Nenhum conteúdo é filtrado ou bloqueado.

A seleção Filmes e as opções de capas/NFOs de filmes controlam essa pasta.
O comportamento vale para execução manual e agendamento. STRMs, capas e NFOs
novos são criados juntos na nova localização; arquivos existentes são ignorados.

## Pasta antiga
Na próxima organização com Filmes selecionado, após validar permissões e a
conexão Xtream, o sistema move `Filmes/FILMES ADULTOS` para `FILMES ADULTOS`.
Move também os arquivos auxiliares e eventuais subpastas, preservando seu conteúdo.
A pasta antiga é removida apenas quando fica vazia. Não há cópia de conteúdo
nem substituição de arquivos existentes. A migração pode ser executada novamente.

Se o destino já existir, arquivos sem conflito são reunidos nele. Antes da mudança,
o sistema verifica colisões: se houver algum mesmo caminho ocupado no destino,
interrompe a execução e explica o conflito no painel e no `log_avisos.txt`, mantendo
os arquivos nas duas pastas. Não apaga nem substitui sequer arquivos iguais.
Resolva o conflito antes de tentar novamente. Links simbólicos na pasta migrada
são recusados. Se houver interrupção durante a reunião de duas pastas, os arquivos
já movidos permanecem no destino e os demais ficam na origem para a próxima execução.

## Atualização
Atualize o projeto/imagem para 1.8.4. As correções de permissões da 1.8.3 continuam
incluídas. Caso ainda não tenha aplicado o Compose atualizado, use o modelo adequado
sem mudar os volumes existentes. Para a biblioteca em ZimaOS-HD, use
`docker-compose.zimaos-hd.yml`. Este ZIP não publica o GitHub nem atualiza o servidor.

No Emby, se a biblioteca de filmes aponta somente para `Nome da lista/Filmes`,
a pasta promovida fica fora desse caminho. Para exibir seu conteúdo, adicione o
novo caminho à biblioteca desejada e faça uma nova varredura. O projeto não altera
as configurações do Emby automaticamente.

## Validação
Compilação e publicação .NET 8 concluídas, zero erros e zero avisos; 3.700 verificações C# aprovadas.
Testes cobrem roteamento da categoria, transferência de STRM/NFO/capa sem mudança
de bytes, repetição, conflito sem sobrescrita, reunião de pastas e geração manual
e agendada com capas e NFOs. Também verificam que arquivos existentes não são
sobrescritos e capas não são baixadas novamente.

A execução real com Docker/ZimaOS, o provedor e a varredura do Emby não foram
validadas neste ambiente. A limitação de teste de UID/GID descrita na versão 1.8.3
permanece. Não há promessa de migração através de sistemas de arquivos diferentes.
