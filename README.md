# ServiceDesk

Aplicativo Windows para acompanhar conversas atribuídas ao analista no Chatwoot. Os cards priorizam identificação, empresa e ID do ticket e mostram a interação mais recente identificada entre cliente e agente atribuído, indicando quem enviou. O destaque de espera e os alertas usam o início da espera do cliente.

## O que o aplicativo faz

- Em **Meus tickets**, as conversas atribuídas aparecem em blocos **Em aberto** e **Adiados**, com contagens em **Situação de atendimentos**. Use **Status** para filtrar os blocos ou escolha **Adiados** nas abas superiores.
- A aba **Não atribuídos** aparece quando as permissões do agente permitem ver essa fila no Chatwoot e mostra os tickets acessíveis na caixa configurada.
- Em **Resolvidos**, **Meus tickets** usa o responsável atual, como **Minhas** no Chatwoot. **Caixa de entrada** reúne os resolvidos acessíveis nas caixas configuradas. Role até o fim para carregar outra página. A busca filtra os tickets carregados por número, contato ou empresa.
- Mostra contato, empresa, ID, situação e o tempo da última interação, indicando quem enviou a mensagem.
- Os cards acompanham a espera do cliente, a revisão após a resposta pública do agente e o horário dos adiamentos do Chatwoot. O card inteiro fica avermelhado quando a espera do cliente atinge o limite configurado em **Alertas**. A antecedência dos outros destaques fica em **Configurações → Alertas**.
- Ignora notas privadas e eventos do sistema para o cálculo de interação.
- Exibe um painel de espera e notificações do Windows para os tickets de **Meus tickets** que atingem o limite de espera.
- Abre a conversa selecionada diretamente no Chatwoot.

O ServiceDesk somente lê dados do Chatwoot. Ele não envia mensagens, não altera status, não adiciona notas e não adia conversas.

## Configuração inicial

Ao abrir o aplicativo, abra **Configurações → Conexão** e informe o endereço, a chave API pessoal, o ID da conta e o ID da caixa de entrada. A chave fica oculta por padrão; use **Mostrar chave da API** apenas quando necessário.

A configuração é salva somente no perfil Windows do usuário, em `%USERPROFILE%\.servicedesk\application.properties`, e não deve ser enviada ao GitHub. Em **Configurações → Atendimento**, o modo padrão atualiza a fila ao receber eventos do Chatwoot. Você pode desativar o tempo real e escolher um intervalo fixo de 1 a 120 minutos. Em **Alertas**, ajuste o limite de espera, de 10 a 1.440 minutos, o painel, as notificações do Windows, o som e o intervalo dos lembretes.

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

Baixe `ServiceDesk-Setup-win-x64.exe` em [Releases](https://github.com/RafaStolf/ServiceDesk/releases/latest). O instalador inclui as dependências e não exige Git. O ZIP portátil também está disponível. A versão instalada verifica atualizações em **Configurações → Aplicativo**.

Cada pessoa deverá configurar sua própria chave API, conta e caixa de entrada após instalar. Não inclua credenciais em commits, releases ou instaladores.

## Documentos de processo

`Diretriz Oficial do Suporte - N1 3.docx` e `FluxoN1.png` são materiais de referência do processo N1; não são usados nem alterados pelo aplicativo.

## Usar um atendimento no ChatGPT

No menu **…** do card, escolha **Copiar texto**, **Exportar chat** ou **Abrir no Chatwoot**. **Copiar texto** copia o histórico público para a área de transferência. **Exportar chat** salva diretamente na pasta Downloads configurada no Windows, sem janela de prévia ou seleção de destino.

Cada exportação cria uma subpasta `Ticket-19680`, com o histórico Markdown e os anexos originais. Exportações repetidas ganham sufixos para preservar as anteriores. Notas privadas e eventos são excluídos. Se o download de uma mídia falhar, a pasta incompleta é removida e o aplicativo informa o erro. Não há envio automático ao ChatGPT, transcrição de áudio ou conversão de vídeo.

## Download e atualização (Windows x64)

As versões ficam em https://github.com/RafaStolf/ServiceDesk/releases/latest. Para instalar, baixe **ServiceDesk-Setup-win-x64.exe** nos anexos (**Assets**) da release e execute. Não é necessário clonar o repositório, instalar Git, Visual Studio ou .NET.

O instalador usa `%LOCALAPPDATA%\Programs\ServiceDesk`, sem exigir administrador, e cria um atalho no menu Iniciar. A desinstalação aparece nas configurações de aplicativos do Windows e preserva `%USERPROFILE%\.servicedesk`. A inicialização com Windows e a execução em segundo plano podem ser ajustadas em **Configurações → Geral**. O instalador ainda não possui assinatura digital; o Windows pode exibir um aviso de publicador desconhecido.

Como alternativa portátil, baixe **ServiceDesk-win-x64.zip**, extraia todo o conteúdo em uma pasta e abra **ServiceDesk.exe**. A pasta `Assets` do código-fonte contém imagens; os arquivos para download ficam nos anexos da release no GitHub. Não é necessário Visual Studio nem .NET instalado. Feche a versão antiga e atualize seu atalho para o novo executável. O pacote portátil não substitui automaticamente uma instalação MSIX.

Em **Configurações**, a categoria **Conexão** reúne endereço HTTPS, conta, chave da API e caixa de entrada. A categoria **Atendimento** permite usar tempo real, intervalo fixo ou atualização manual. Em tempo real, uma consulta de contingência roda a cada minuto enquanto o WebSocket estiver indisponível. **Personalização** permite escolher tema, cor, campos, ordem e densidade dos cards e do painel flutuante. **Alertas** controla o limite de espera, o painel, as notificações do Windows, o som e os lembretes. A categoria **Aplicativo** mostra a versão instalada, **Verificar atualizações** e **Atualizar**. O aplicativo consulta novas versões ao iniciar e a cada quatro horas. A engrenagem mantém um ponto vermelho enquanto houver atualização disponível. Clicar nela abre **Aplicativo**. O Windows recebe um aviso por versão durante a execução, se as notificações estiverem habilitadas.

Ao verificar, o aplicativo consulta as versões públicas no GitHub. Havendo versão nova, **Atualizar** baixa o pacote, verifica SHA-256, fecha o aplicativo, substitui os arquivos e reinicia no mesmo caminho, preservando os atalhos e configurações. Um backup permite restaurar os arquivos anteriores se a substituição ou inicialização falhar. A atualização exige Windows x64 e permissão de escrita na pasta da versão portátil. Uma instalação MSIX precisa migrar para a versão portátil; não é substituída por esse mecanismo. As versões antigas que apenas abrem a página de download precisam atualizar manualmente uma última vez.

As configurações em `%USERPROFILE%\.servicedesk` são preservadas. Em **Configurações → Alertas → Testar notificação do Windows**, o aplicativo verifica se o Windows registrou o aviso na Central de Notificações.

O workflow `.github/workflows/windows-release.yml` compila em Windows, gera o instalador com Inno Setup 6 e testa instalação, abertura, reinstalação e desinstalação. Após um push na `main`, publica o instalador, o ZIP portátil e os respectivos SHA-256. O ZIP permanece disponível para o atualizador interno. Nenhum arquivo de configuração local é incluído no pacote.

O nome da exportação usa o ID da conversa da URL do Chatwoot. Ao repetir uma exportação com mídias no mesmo destino, o Windows cria uma pasta com sufixo numérico para preservar os arquivos anteriores.

## Segundo plano e início com Windows

Ao abrir esta versão pela primeira vez, a inicialização com Windows e a permanência em segundo plano ficam habilitadas para o usuário atual. O início com Windows ocorre no login, sem abrir o painel. O aplicativo mantém a sincronização e os alertas enquanto estiver em execução, mesmo com a janela oculta.

Fechar a janela envia para o ícone junto ao relógio. Clique no ícone para abrir, ou use o botão direito → **Abrir ServiceDesk** / **Sair**. Abrir novamente pelo atalho mostra a instância existente. Em **Configurações → Geral** é possível desligar o início com Windows ou a permanência em segundo plano. A atualização encerra completamente o processo antes de substituir os arquivos. Se o Windows não disponibilizar o ícone, a janela permanece acessível.

A inicialização automática registra somente o executável no perfil do usuário (HKCU Run), sem privilégios de administrador nem credenciais do Chatwoot. Mantenha o executável em uma pasta permanente. O Windows também permite desabilitar aplicativos de inicialização pelo Gerenciador de Tarefas; o ServiceDesk não reabilita uma preferência desligada dentro do aplicativo.

## Contrato e nomes do Chatwoot

Consulte [o glossário e os critérios de validação](docs/GLOSSARIO-CHATWOOT.md). O card identifica o remetente da interação mais recente entre cliente e agente atribuído. A espera é medida a partir de `waiting_since` do Chatwoot. Quando o campo está ausente e o cliente ainda aguarda resposta, vale a última mensagem pública do cliente. Valores explícitos `null` e `0` indicam ausência de espera. O campo de remetente do endpoint é `sender.type`/`sender.id`.

As preferências visuais ficam em `%USERPROFILE%\.servicedesk\interface.json`. O limite padrão é 10 minutos. O painel de espera mostra nome e duração em uma linha para cada uma das três esperas mais longas de **Meus tickets** que chegaram ao limite, com a contagem dos demais tickets elegíveis. Em **Alertas**, escolha um dos quatro cantos e o monitor ou use a seleção automática. Por padrão, o painel fica no canto inferior direito do monitor da janela em uso quando aparece. Se o monitor escolhido estiver desconectado, o painel usa o principal até ele voltar. O painel funciona com o ServiceDesk na bandeja enquanto houver espera elegível e a fila estiver atualizada. O aplicativo em uso mantém o foco e recebe os cliques.

Quando a data de resolução está ausente, o aplicativo consulta eventos do histórico para completar a data exibida.

## Métricas e SLA

Referência informada: **CCChatwoot v4.5.0, build `75d3569`**. O guia em Métricas descreve as cinco situações do fluxo N1. Os cards priorizam clientes aguardando resposta e avisam sobre a revisão 10 minutos após a última resposta pública do agente. Para adiamentos, usam a data de retomada do Chatwoot, que já representa o horário selecionado para o atendimento. A revisão sugere conferir o ticket quando o processo depende do motivo da espera. O SLA usa a política do Chatwoot.

O indicador `reply_time` é apresentado como **Espera do cliente**; **Próxima resposta (NRT)** identifica a meta do SLA. Mensagens privadas são distinguidas de mensagens para o cliente: a leitura aceita `private` da API oficial e `is.private` literal ou aninhado para compatibilidade. Uma marcação privada verdadeira prevalece e exclui a mensagem da exportação e dos contadores públicos. O contrato específico da build personalizada ainda precisa ser confirmado com dados reais.

Abra **Métricas** na lateral, escolha datas inclusivas no fuso de Brasília e clique em **Atualizar**. A consulta usa a conta configurada e o acesso concedido pelo Chatwoot. Administradores e agentes com `report_manage` consultam as conversas acessíveis pela API. O recorte pessoal usa os tickets atribuídos ao agente e seus próprios eventos. As médias de primeira resposta, espera do cliente e resolução usam os eventos oficiais do período e informam o número de registros. A base de tempo das médias alterna entre tempo corrido e horário comercial. O gráfico mostra o estado atual das políticas aplicadas no período, com os quatro estados do Chatwoot.

**Ver detalhes** mostra metas, vencimentos e ocorrências retornadas pela API. O contador de última interação da fila é independente desses indicadores. Dados indisponíveis não viram zero nem SLA cumprido; a edição, as permissões e as políticas da instalação determinam a disponibilidade.

O histórico pessoal consulta tickets **atualmente atribuídos** ao usuário. Tickets transferidos podem ficar de fora desse recorte. O período filtra a criação dos eventos de relatório, ocorrências ou atualização do SLA; o estado do SLA mostrado é o atual. Tickets sem consulta suficiente para confirmar o período são identificados e ficam fora do resumo de SLA. A leitura é sob demanda, cancelável, limitada a 10 páginas por situação e 250 tickets, com avisos de resultados parciais. Após três recusas/ausências consecutivas dos eventos, a consulta desses eventos é interrompida e informada; uma falha isolada não impede os tickets seguintes. Um limite HTTP 429 interrompe novas consultas.

A integração continua somente GET. Nenhuma meta contratual, pausa ou calendário é presumido localmente. Horas úteis e prazos nativos seguem o cadastro do Chatwoot.

## Assinatura de código

Consulte [docs/ASSINATURA.md](docs/ASSINATURA.md) para o certificado próprio, sua guarda e a ordem de assinatura no Windows. O workflow está preparado para assinatura automática; a publicação exige o cadastro dos secrets descritos nessa documentação. PRs validam o processo com certificado descartável de teste.

## E-mail nos cards

O card exibe o e-mail cadastrado no contato (`meta.sender.email`), abaixo da empresa, sem seleção para cópia ou abertura de cliente de e-mail. Quando estiver ausente, vazio ou composto apenas por espaços, mostra **E-mail não informado** em vermelho, com cor adaptada ao tema claro ou escuro. O endereço não é inferido das mensagens. Nomes, empresas e e-mails quebram linha conforme a largura disponível.

## Diagnóstico de notificações do Windows

Em **Configurações → Alertas**, ative **Notificações do Windows** e use **Testar notificação do Windows**. O teste independe de tickets e do limite de minutos. O teste verifica o registro do aviso na Central de Notificações. O modo Não incomodar pode ocultar o banner. O aplicativo informa permissões desabilitadas e registra falhas detectadas no envio em `%LOCALAPPDATA%\ServiceDesk\notificacoes.log`, sem nomes de contatos ou conteúdo das conversas.

Com as notificações do Windows ativadas, cada episódio de espera pode gerar um aviso inicial ao atingir o limite. Se vários tickets atingirem o limite juntos, os envios são espaçados. Por padrão, um lembrete conjunto é enviado a cada cinco minutos enquanto houver tickets elegíveis. Em **Alertas**, escolha de 1 a 60 minutos ou **Nunca** e selecione o som do Windows ou o modo silencioso.

Uma falha de sincronização oculta o painel e pausa os envios. O mesmo ocorre quando a última leitura bem-sucedida passa do intervalo automático mais um minuto, ou de cinco minutos no modo manual. Após uma leitura bem-sucedida, os alertas voltam a seguir a fila atual, sem reproduzir avisos acumulados.
