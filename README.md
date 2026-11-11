# ServiceDesk

Aplicativo Windows para acompanhar conversas atribuídas ao analista no Chatwoot. Ele prioriza a fila pela última **mensagem pública** da conversa e sinaliza atendimentos sem interação.

## O que o aplicativo faz

- Consulta conversas abertas e adiadas atribuídas ao analista no Chatwoot.
- Permite filtrar por situação e pesquisar cliente, telefone, empresa, ticket externo, mensagem ou ID.
- Mostra em cada card o tempo desde a criação da conversa e o tempo desde a última mensagem pública.
- Ignora notas privadas e eventos do sistema para o cálculo de interação.
- Exibe alertas locais configuráveis por tempo sem interação.
- Abre a conversa selecionada diretamente no Chatwoot.

O ServiceDesk somente lê dados do Chatwoot. Ele não envia mensagens, não altera status, não adiciona notas e não adia conversas.

## Configuração inicial

Ao abrir o aplicativo, selecione **Chatwoot** no canto superior direito e informe a chave API pessoal, o ID do usuário e o ID da caixa de entrada. A chave fica oculta por padrão; use **Mostrar chave da API** apenas quando necessário.

A configuração é salva somente no perfil Windows do usuário, em `%USERPROFILE%\.servicedesk\application.properties`, e não deve ser enviada ao GitHub. Em **Configurações → Atendimentos**, escolha o intervalo de sincronização e o limite de minutos que gera uma notificação.

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

O projeto é um aplicativo WinUI empacotado (MSIX). Quando estiver aprovado, gere o pacote pelo perfil `Properties/PublishProfiles/win-x64.pubxml` e entregue o instalador aos colegas.

Cada pessoa deverá configurar sua própria chave API, ID de agente e caixa de entrada após instalar. Não inclua credenciais em commits, releases ou instaladores.

## Documentos de processo

`Diretriz Oficial do Suporte - N1 3.docx` e `FluxoN1.png` são materiais de referência do processo N1; não são usados nem alterados pelo aplicativo.

## Usar um atendimento no ChatGPT

Selecione um atendimento e clique em **Usar no ChatGPT**. Revise o histórico completo na prévia editável. **Copiar texto** copia o conteúdo; **Salvar arquivo** exporta um arquivo **.md (Markdown)**, pronto para anexar em um chat do projeto Atendimentos.

O Markdown contém a identificação, situação, datas e mensagens públicas em ordem cronológica, com referências aos anexos. Ele não incorpora os arquivos de imagem, áudio ou vídeo. Para levá-los junto, mantenha marcada a opção de mídias e escolha uma pasta. O aplicativo cria uma subpasta do atendimento com nome como `Atendimento-19656`, contendo `Atendimento-19656.md` e os anexos originais juntos, sem ZIP. No horário de cada mensagem, o histórico informa que foi enviada uma imagem, vídeo ou áudio e inclui o nome/link do arquivo correspondente. Anexe o histórico e as mídias no mesmo chat. Notas privadas e eventos são excluídos.

Não há envio automático ao ChatGPT. A análise depende dos formatos e limites disponíveis; áudio e vídeo podem exigir transcrição ou conversão, que o ServiceDesk ainda não realiza. Se uma mídia estiver indisponível, o pacote não é copiado ao destino como se estivesse completo.

## Download e atualização (Windows x64)

As versões ficam em https://github.com/RafaStolf/ServiceDesk/releases/latest. Para instalar, baixe **ServiceDesk-Setup-win-x64.exe** nos anexos (**Assets**) da release e execute. Não é necessário clonar o repositório, instalar Git, Visual Studio ou .NET.

O instalador usa `%LOCALAPPDATA%\Programs\ServiceDesk`, sem exigir administrador, e cria um atalho no menu Iniciar. A desinstalação aparece nas configurações de aplicativos do Windows e preserva `%USERPROFILE%\.servicedesk`. A inicialização com Windows e a execução em segundo plano podem ser ajustadas em **Configurações → Geral**. O instalador ainda não possui assinatura digital; o Windows pode exibir um aviso de publicador desconhecido.

Como alternativa portátil, baixe **ServiceDesk-win-x64.zip**, extraia todo o conteúdo em uma pasta e abra **ServiceDesk.exe**. A pasta `Assets` do código-fonte contém imagens; os arquivos para download ficam nos anexos da release no GitHub. Não é necessário Visual Studio nem .NET instalado. Feche a versão antiga e atualize seu atalho para o novo executável. O pacote portátil não substitui automaticamente uma instalação MSIX.

Em **Configurações**, a aba **Chatwoot** reúne endereço HTTPS, conta, chave da API, agente e caixa de entrada. A aba **Atendimentos** controla a atualização automática da fila, intervalo e alertas. A aba **Versão** mostra a versão instalada, **Verificar atualizações** e **Atualizar**.

Ao verificar, o aplicativo consulta as versões públicas no GitHub. Havendo versão nova, **Atualizar** baixa o pacote, verifica SHA-256, fecha o aplicativo, substitui os arquivos e reinicia no mesmo caminho, preservando os atalhos e configurações. Um backup permite restaurar os arquivos anteriores se a substituição ou inicialização falhar. A atualização exige Windows x64 e permissão de escrita na pasta da versão portátil. Uma instalação MSIX precisa migrar para a versão portátil; não é substituída por esse mecanismo. As versões antigas que apenas abrem a página de download precisam atualizar manualmente uma última vez.

As configurações em `%USERPROFILE%\.servicedesk` são preservadas. Na versão portátil, notificações do Windows podem não estar disponíveis; os alertas dentro do aplicativo continuam funcionando.

O workflow `.github/workflows/windows-release.yml` compila em Windows, gera o instalador com Inno Setup 6 e testa instalação, abertura, reinstalação e desinstalação. Após um push na `main`, publica o instalador, o ZIP portátil e os respectivos SHA-256. O ZIP permanece disponível para o atualizador interno. Nenhum arquivo de configuração local é incluído no pacote.

O nome da exportação usa o ID da conversa da URL do Chatwoot. Ao repetir uma exportação com mídias no mesmo destino, o Windows cria uma pasta com sufixo numérico para preservar os arquivos anteriores.

## Segundo plano e início com Windows

Ao abrir esta versão pela primeira vez, a inicialização com Windows e a permanência em segundo plano ficam habilitadas para o usuário atual. O início com Windows ocorre no login, sem abrir o painel. O aplicativo mantém a sincronização e os alertas enquanto estiver em execução, mesmo com a janela oculta.

Fechar a janela envia para o ícone junto ao relógio. Clique no ícone para abrir, ou use o botão direito → **Abrir ServiceDesk** / **Sair**. Abrir novamente pelo atalho mostra a instância existente. Em **Configurações → Geral** é possível desligar o início com Windows ou a permanência em segundo plano. A atualização encerra completamente o processo antes de substituir os arquivos. Se o Windows não disponibilizar o ícone, a janela permanece acessível.

A inicialização automática registra somente o executável no perfil do usuário (HKCU Run), sem privilégios de administrador nem credenciais do Chatwoot. Mantenha o executável em uma pasta permanente. O Windows também permite desabilitar aplicativos de inicialização pelo Gerenciador de Tarefas; o ServiceDesk não reabilita uma preferência desligada dentro do aplicativo.

## Contrato e nomes do Chatwoot

Consulte [o glossário e os critérios de validação](docs/GLOSSARIO-CHATWOOT.md). O contador mede tempo sem mensagem pública, não tempo de resposta ou SLA. O campo de remetente do endpoint é `sender.type`/`sender.id`.
