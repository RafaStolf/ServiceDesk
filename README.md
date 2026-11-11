# ServiceDesk

Aplicativo Windows para acompanhar conversas atribuídas ao analista no Chatwoot. Os cards priorizam identificação, empresa e ID do ticket e mostram o tempo desde a última interação identificada do agente atribuído.

## O que o aplicativo faz

- Consulta conversas abertas e adiadas atribuídas ao analista no Chatwoot.
- Permite filtrar a fila por Em aberto, Adiados e Todos.
- Mostra contato, empresa, ID, situação e o tempo da última interação do agente atribuído, sem prévia de mensagem ou nome do agente.
- Ignora notas privadas e eventos do sistema para o cálculo de interação.
- Exibe alertas locais configuráveis por tempo sem interação.
- Abre a conversa selecionada diretamente no Chatwoot.

O ServiceDesk somente lê dados do Chatwoot. Ele não envia mensagens, não altera status, não adiciona notas e não adia conversas.

## Configuração inicial

Ao abrir o aplicativo, abra **Configurações → Chatwoot** e informe o endereço, a chave API pessoal, o ID da conta e o ID da caixa de entrada. A chave fica oculta por padrão; use **Mostrar chave da API** apenas quando necessário.

A configuração é salva somente no perfil Windows do usuário, em `%USERPROFILE%\.servicedesk\application.properties`, e não deve ser enviada ao GitHub. Em **Configurações → Fila**, escolha o intervalo de sincronização. Em **Alertas**, ajuste o limite de minutos e as notificações.

## Requisitos para desenvolvimento

- Windows 10/11.
- .NET SDK 10.
- Windows App SDK, instalado como dependência do projeto.
- Acesso ao Chatwoot e chave API válida.

Não há dependência de SQL Server ou outro banco de dados.

## Estrutura e arquitetura

Consulte [docs/ESTRUTURA.md](docs/ESTRUTURA.md) para o mapa de arquivos e [docs/ARQUITETURA.md](docs/ARQUITETURA.md) para decisões técnicas.

## Desenvolvimento no Antigravity IDE

Abra a pasta que contém `ServiceDesk.csproj`. O arquivo principal da tela é `MainPage.xaml`; a lógica da tela está em `MainPage.xaml.cs`. Os diretórios `bin` e `obj` são resultados de compilação e já estão ignorados pelo Git.

## Distribuição

Baixe `ServiceDesk-Setup-win-x64.exe` em [Releases](https://github.com/RafaStolf/ServiceDesk/releases/latest). O instalador inclui as dependências e não exige Git. O ZIP portátil também está disponível. A versão instalada verifica atualizações em **Configurações → Versão**.

Cada pessoa deverá configurar sua própria chave API, conta e caixa de entrada após instalar. Não inclua credenciais em commits, releases ou instaladores.

## Documentos de processo

`Diretriz Oficial do Suporte - N1 3.docx` e `FluxoN1.png` são materiais de referência do processo N1; não são usados nem alterados pelo aplicativo.

## Usar um atendimento no ChatGPT

No menu **…** do card, escolha **Copiar texto**, **Exportar chat** ou **Abrir no Chatwoot**. Em **Exportar chat**, Revise o histórico completo na prévia editável. **Copiar texto** copia o conteúdo; **Salvar arquivo** exporta um arquivo **.md (Markdown)**, pronto para anexar em um chat do projeto Atendimentos.

O Markdown contém a identificação, situação, datas e mensagens públicas em ordem cronológica, com referências aos anexos. Ele não incorpora os arquivos de imagem, áudio ou vídeo. Para levá-los junto, mantenha marcada a opção de mídias e escolha uma pasta. O aplicativo cria uma subpasta do atendimento com nome como `Atendimento-19656`, contendo `Atendimento-19656.md` e os anexos originais juntos, sem ZIP. No horário de cada mensagem, o histórico informa que foi enviada uma imagem, vídeo ou áudio e inclui o nome/link do arquivo correspondente. Anexe o histórico e as mídias no mesmo chat. Notas privadas e eventos são excluídos.

Não há envio automático ao ChatGPT. A análise depende dos formatos e limites disponíveis; áudio e vídeo podem exigir transcrição ou conversão, que o ServiceDesk ainda não realiza. Se uma mídia estiver indisponível, o pacote não é copiado ao destino como se estivesse completo.

## Download e atualização (Windows x64)

As versões ficam em https://github.com/RafaStolf/ServiceDesk/releases/latest. Para instalar, baixe **ServiceDesk-Setup-win-x64.exe** nos anexos (**Assets**) da release e execute. Não é necessário clonar o repositório, instalar Git, Visual Studio ou .NET.

O instalador usa `%LOCALAPPDATA%\Programs\ServiceDesk`, sem exigir administrador, e cria um atalho no menu Iniciar. A desinstalação aparece nas configurações de aplicativos do Windows e preserva `%USERPROFILE%\.servicedesk`. A inicialização com Windows e a execução em segundo plano podem ser ajustadas em **Configurações → Geral**. O instalador ainda não possui assinatura digital; o Windows pode exibir um aviso de publicador desconhecido.

Como alternativa portátil, baixe **ServiceDesk-win-x64.zip**, extraia todo o conteúdo em uma pasta e abra **ServiceDesk.exe**. A pasta `Assets` do código-fonte contém imagens; os arquivos para download ficam nos anexos da release no GitHub. Não é necessário Visual Studio nem .NET instalado. Feche a versão antiga e atualize seu atalho para o novo executável. O pacote portátil não substitui automaticamente uma instalação MSIX.

Em **Configurações**, a aba **Chatwoot** reúne endereço HTTPS, conta, chave da API, agente e caixa de entrada. A aba **Fila** controla a atualização automática e o intervalo. **Aparência** permite escolher tema e cor; **Alertas** controla o limite, as notificações do Windows e o som. A aba **Versão** mostra a versão instalada, **Verificar atualizações** e **Atualizar**.

Ao verificar, o aplicativo consulta as versões públicas no GitHub. Havendo versão nova, **Atualizar** baixa o pacote, verifica SHA-256, fecha o aplicativo, substitui os arquivos e reinicia no mesmo caminho, preservando os atalhos e configurações. Um backup permite restaurar os arquivos anteriores se a substituição ou inicialização falhar. A atualização exige Windows x64 e permissão de escrita na pasta da versão portátil. Uma instalação MSIX precisa migrar para a versão portátil; não é substituída por esse mecanismo. As versões antigas que apenas abrem a página de download precisam atualizar manualmente uma última vez.

As configurações em `%USERPROFILE%\.servicedesk` são preservadas. Na versão portátil, notificações do Windows podem não estar disponíveis; os alertas dentro do aplicativo continuam funcionando.

O workflow `.github/workflows/windows-release.yml` compila em Windows, gera o instalador com Inno Setup 6 e testa instalação, abertura, reinstalação e desinstalação. Após um push na `main`, publica o instalador, o ZIP portátil e os respectivos SHA-256. O ZIP permanece disponível para o atualizador interno. Nenhum arquivo de configuração local é incluído no pacote.

O nome da exportação usa o ID da conversa da URL do Chatwoot. Ao repetir uma exportação com mídias no mesmo destino, o Windows cria uma pasta com sufixo numérico para preservar os arquivos anteriores.

## Segundo plano e início com Windows

Ao abrir esta versão pela primeira vez, a inicialização com Windows e a permanência em segundo plano ficam habilitadas para o usuário atual. O início com Windows ocorre no login, sem abrir o painel. O aplicativo mantém a sincronização e os alertas enquanto estiver em execução, mesmo com a janela oculta.

Fechar a janela envia para o ícone junto ao relógio. Clique no ícone para abrir, ou use o botão direito → **Abrir ServiceDesk** / **Sair**. Abrir novamente pelo atalho mostra a instância existente. Em **Configurações → Geral** é possível desligar o início com Windows ou a permanência em segundo plano. A atualização encerra completamente o processo antes de substituir os arquivos. Se o Windows não disponibilizar o ícone, a janela permanece acessível.

A inicialização automática registra somente o executável no perfil do usuário (HKCU Run), sem privilégios de administrador nem credenciais do Chatwoot. Mantenha o executável em uma pasta permanente. O Windows também permite desabilitar aplicativos de inicialização pelo Gerenciador de Tarefas; o ServiceDesk não reabilita uma preferência desligada dentro do aplicativo.

## Contrato e nomes do Chatwoot

Consulte [o glossário e os critérios de validação](docs/GLOSSARIO-CHATWOOT.md). O card mostra a última interação identificada do agente atribuído; esse tempo não é uma meta de SLA. Quando a data é desconhecida, não é substituída pela criação da conversa. O campo de remetente do endpoint é `sender.type`/`sender.id`.

O ícone da Centrium é preservado. As preferências visuais ficam em `%USERPROFILE%\.servicedesk\interface.json`. As notificações não incluem botão ou ação para abrir o Chatwoot. Na versão instalada, o atalho do menu Iniciar registra a identidade das notificações; versões portáteis sem esse registro usam o alerta dentro do aplicativo como alternativa.

## Métricas e SLA

Abra **Métricas** no topo para consultar a fila atual ou o histórico. No histórico, escolha datas inclusivas no fuso de Brasília e clique em Atualizar, ou use Hoje, 7 dias e 30 dias. As médias de primeira resposta, respostas seguintes e resolução usam eventos oficiais do usuário autenticado; cada indicador informa seu número de registros. Reaberturas podem produzir vários eventos de resolução para o mesmo ticket. A base de tempo alterna entre corrido e útil sem alterar a política do Chatwoot.

**Ver detalhes** mostra metas, vencimentos e ocorrências retornadas pela API. O contador de última interação da fila é independente desses indicadores. Dados indisponíveis não viram zero nem SLA cumprido; a edição, as permissões e as políticas da instalação determinam a disponibilidade.

O histórico consulta tickets **atualmente atribuídos** ao usuário; transferidos podem não aparecer. O período filtra a criação dos eventos de relatório, ocorrências ou atualização do SLA; o estado do SLA mostrado é o atual. Tickets sem consulta suficiente para confirmar o período são identificados e ficam fora do resumo de SLA. A leitura é sob demanda, cancelável, limitada a 10 páginas por situação e 250 tickets, com avisos de resultados parciais. Após três recusas/ausências consecutivas dos eventos, a consulta desses eventos é interrompida e informada; uma falha isolada não impede os tickets seguintes. Um limite HTTP 429 interrompe novas consultas.

A integração continua somente GET. Nenhuma meta contratual, pausa ou calendário é presumido localmente. Horas úteis e prazos nativos seguem o cadastro do Chatwoot.
