# Xtream STRM Organizer 1.8.5 — progresso do catálogo

## Problema corrigido
A barra anterior usava a quantidade de séries e um percentual fixo de 10% durante
TV/filmes. Podia chegar ao fim antes de todas as etapas terminarem.

Agora o sistema lê primeiro os catálogos selecionados (sem baixar os vídeos), fixa
o total de canais + filmes + séries e calcula o progresso no servidor. Não consulta
esses catálogos novamente durante a mesma execução. A interface mostra:
- "Calculando" enquanto verifica pastas, conexão e total do catálogo;
- quantidade de itens processados/total, episódios processados, fila e avisos;
- percentual por itens, sem reiniciar a contagem ao mudar entre TV, filmes e séries;
- no máximo 99% até o processamento terminar e a fila STRM esvaziar;
- 100% apenas quando concluir, inclusive conclusão com avisos claramente indicada.

Capas e NFOs habilitados são tentados antes de contar o item como processado.
Arquivos existentes continuam sendo ignorados; isso também conta como trabalho
processado. Cancelamento ou erro não é apresentado como conclusão.

O percentual mede itens, NÃO tempo, bytes ou arquivos individuais. Cada série é
um item e conta após processar seus episódios e arquivos auxiliares. Uma série
longa pode manter o percentual parado enquanto o contador de episódios avança.
A estimativa de tempo existente foi rotulada explicitamente como estimativa para
séries; ela não prevê a duração de toda a execução.

A interface consulta o estado a cada segundo quando visível, impede requisições
sobrepostas e cancela uma consulta que demore mais de 10 segundos, permitindo
nova tentativa. Enquanto o total não é conhecido, a animação não apresenta uma
porcentagem inventada e respeita a preferência por movimento reduzido.

## Instalação
Atualize a imagem/projeto para 1.8.5 e recarregue o painel. Todos os ajustes das
versões anteriores, incluindo permissões e FILMES ADULTOS, continuam presentes.
O ZIP também padroniza os comandos multilinha dos Composes como um único argumento
para `sh -ec`, conforme o Compose ZimaOS avulso corrigido. Use o modelo que mantém
os caminhos existentes. Publicar o ZIP no GitHub e concluir o Actions é necessário
para atualizar a imagem `latest`; esta entrega não publica nem altera o servidor.

## Testes e limites
Compilação sem erros/avisos e publicação concluídas. Na execução local, 5.619 verificações C# aprovadas (a contagem varia com as amostras de progresso durante os testes).
Testes C# cobrem progresso monotônico, total fixo, fila pendente, download de capa
bloqueado, consulta de catálogo bloqueada, cancelamento e catálogo vazio, além das
matrizes existentes de opções manuais e agendadas. Testes JavaScript executam a
função real que desenha o progresso, com respostas controladas, incluindo ausência
de total, percentual, cancelamento e bloqueio de consultas simultâneas.

Compilação/publicação .NET, testes HTTP, sintaxe JavaScript, estrutura YAML e script
dos Composes também são verificados. A validação da interface é por testes de lógica;
não houve nova inspeção visual em navegador real nesta versão. Docker/ZimaOS,
provedor real e troca de UID/GID ainda precisam ser validados no ambiente real.
