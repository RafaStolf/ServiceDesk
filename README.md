# ServiceDesk

Aplicativo Windows para acompanhar conversas atribuídas ao analista no Chatwoot. Ele prioriza a fila pela última **mensagem pública** da conversa e sinaliza atendimentos sem interação.

## O que o aplicativo faz

- Consulta conversas abertas e adiadas atribuídas ao analista no Chatwoot.
- Permite filtrar por situação e pesquisar cliente, telefone, empresa, ticket, assunto ou ID.
- Mostra em cada card o tempo de chat aberto e o tempo desde a última mensagem pública.
- Ignora notas privadas e eventos do sistema para o cálculo de interação.
- Exibe alertas locais configuráveis por tempo sem interação.
- Abre a conversa selecionada diretamente no Chatwoot.

O ServiceDesk somente lê dados do Chatwoot. Ele não envia mensagens, não altera status, não adiciona notas e não adia conversas.

## Configuração inicial

Ao abrir o aplicativo, selecione **Chatwoot** no canto superior direito e informe a chave API pessoal, o ID do usuário e o ID do Inbox. A chave fica oculta por padrão; use **Mostrar chave da API** apenas quando necessário.

A configuração é salva somente no perfil Windows do usuário, em `%USERPROFILE%\.servicedesk\application.properties`, e não deve ser enviada ao GitHub. Em **Atualização automática**, escolha o intervalo de sincronização e o limite de minutos que gera uma notificação.

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

Cada pessoa deverá configurar sua própria chave API, ID de usuário e Inbox após instalar. Não inclua credenciais em commits, releases ou instaladores.

## Documentos de processo

`Diretriz Oficial do Suporte - N1 3.docx` e `FluxoN1.png` são materiais de referência do processo N1; não são usados nem alterados pelo aplicativo.

## Usar um atendimento no ChatGPT

Selecione um atendimento e clique em **Usar no ChatGPT**. Revise o histórico completo na prévia editável. **Copiar texto** copia o conteúdo; **Salvar arquivo** exporta um arquivo **.md (Markdown)**, pronto para anexar em um chat do projeto Atendimentos.

O Markdown contém a identificação, situação, datas e mensagens públicas em ordem cronológica, com referências aos anexos. Ele não incorpora os arquivos de imagem, áudio ou vídeo. Para levá-los junto, mantenha marcada a opção de mídias e escolha uma pasta. O aplicativo cria uma subpasta do atendimento contendo `historico.md` e os anexos originais juntos, sem ZIP. No horário de cada mensagem, o histórico informa que foi enviada uma imagem, vídeo ou áudio e inclui o nome/link do arquivo correspondente. Anexe o histórico e as mídias no mesmo chat. Notas privadas e eventos são excluídos.

Não há envio automático ao ChatGPT. A análise depende dos formatos e limites disponíveis; áudio e vídeo podem exigir transcrição ou conversão, que o ServiceDesk ainda não realiza. Se uma mídia estiver indisponível, o pacote não é copiado ao destino como se estivesse completo.

## Download e atualização (Windows x64)

As versões ficam em https://github.com/RafaStolf/ServiceDesk/releases/latest. Baixe **ServiceDesk-win-x64.zip**, extraia todo o conteúdo em uma pasta e abra **ServiceDesk.exe**. Não é necessário Visual Studio nem .NET instalado. Feche a versão antiga e atualize seu atalho para o novo executável. O pacote portátil não substitui automaticamente uma instalação MSIX.

O botão **Verificar atualização** consulta a versão pública mais recente no GitHub, compara com a versão em execução e oferece abrir a página de download. A consulta é manual; o aplicativo não baixa nem executa atualizações automaticamente. As configurações em `%USERPROFILE%\.servicedesk` são preservadas. Na versão portátil, notificações do Windows podem não estar disponíveis; os alertas dentro do aplicativo continuam funcionando.

O workflow `.github/workflows/windows-release.yml` compila em Windows e publica o ZIP e seu SHA-256 após um push na `main`. Nenhum arquivo de configuração local é incluído no pacote.
