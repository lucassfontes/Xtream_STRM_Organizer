# Xtream STRM Organizer 1.8.3

## O problema corrigido
A raiz da biblioteca permitia escrita ao UID 1000, mas subpastas antigas,
como `AMERICA_PLAY/TV ao vivo/CANAIS ESPN`, pertenciam ao root com modo 755.
O aplicativo conseguia ler STRMs, mas não criar NFOs, capas ou novos STRMs.

## Instalação e atualização
O serviço `preparar-pastas` agora percorre os diretórios da biblioteca:
- transfere apenas diretórios pertencentes ao root para o usuário da aplicação;
- concede leitura, escrita e acesso ao proprietário desses diretórios;
- preserva o conteúdo e a propriedade dos arquivos de mídia;
- não segue links simbólicos e não atravessa outros sistemas de arquivos;
- deve concluir com sucesso antes de iniciar o aplicativo.

A preparação roda ao aplicar o Compose; não é necessário executar o aplicativo
como root. O serviço web continua com UID/GID 1000:1000 nos modelos ZimaOS.
No Compose local, acompanha PUID/PGID. Pastas de outras contas não são tomadas.
A preparação pode levar algum tempo em bibliotecas com muitas pastas.

**Atualizar somente a imagem não atualiza a configuração do Compose existente.**
Atualize também o Compose no painel do ZimaOS, mantendo os volumes atuais:
- `docker-compose.zimaos-hd.yml`: configuração em `/DATA/AppData/XtreamStrmOrganizer/config`
  e biblioteca em `/media/ZimaOS-HD/AppData/XtreamStrmOrganizer/Biblioteca_IPTV`.
- `docker-compose.zimaos.yml` e `docker-compose.github.yml`: ambos os volumes em `/DATA/AppData/XtreamStrmOrganizer`.
- `docker-compose.yml`: compilação local com o instalador Linux.

Os caminhos da biblioteca devem ser iguais nos dois serviços do Compose.
Não instale uma segunda instância sobre os mesmos dados. A imagem GHCR precisa
ser compilada/publicada pelo seu GitHub Actions após atualizar o repositório.
Este ZIP não publica nem altera seu servidor automaticamente.

## Verificação dentro do sistema
Toda organização manual ou agendada verifica a pasta da lista e as subpastas
existentes dos tipos selecionados antes de consultar a API Xtream. Cria um
arquivo temporário exclusivo em cada pasta e o remove ao fechar. Não modifica
STRMs, capas ou NFOs existentes. O processo pode ser cancelado.

Se encontrar bloqueio, interrompe essa execução, mostra a pasta no painel e
registra a explicação em `config/log_avisos.txt`. O painel permanece disponível.
Links de diretório nas seções selecionadas são recusados para evitar escrever
fora da biblioteca. Uma pasta de outro usuário, volume somente leitura, ACL,
NTFS/NFS ou disco indisponível pode exigir ajuste no servidor: o aplicativo sem
root não consegue ultrapassar essas restrições. Falhas posteriores à verificação
continuam sendo registradas por arquivo.

## Validação
- Compilação e publicação .NET 8 concluídas, zero erros e zero avisos. Suíte C#: 3.670 verificações aprovadas, incluindo preservação, cancelamento e falha na verificação.
- Testes HTTP autenticados, configuração, agendamento e log de avisos.
- Testes do instalador com Docker simulado, migração e estrutura dos Composes.
- Novo `tests/test-permissoes.py`: no CI com root, reproduz diretórios root/755,
  verifica a falha como UID 1000, executa a preparação duas vezes e confirma
  escrita, preservação do STRM e ausência de alteração em destino de link externo.
- Neste ambiente, Docker não está disponível e chown para UID 1000 é recusado:
  o teste real de troca de proprietário é explicitamente pulado aqui e obrigatório
  no GitHub Actions. Não afirmamos execução desse teste no CI antes de publicar.
- Ainda falta validar a nova instalação no Docker/ZimaOS real e com seu provedor.
