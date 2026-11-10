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

Selecione um atendimento e clique em **Usar no ChatGPT**. Revise a prévia e escolha **Copiar texto** ou **Salvar pacote com mídias**. A cópia contém somente texto. O pacote ZIP contém `historico.txt`, instruções e os anexos originais das mensagens públicas (imagens, vídeos, áudios e outros arquivos), relacionados à mensagem pelo nome. A exportação percorre todo o histórico público; notas privadas e eventos ficam de fora.

Extraia o ZIP e anexe o histórico e as mídias em um chat do projeto **Atendimentos**. O envio é manual. A aceitação e análise de formatos e tamanhos depende dos recursos disponíveis no ChatGPT; preservar o original não garante que ele seja interpretado. Áudio/vídeo não aceitos precisam de transcrição e/ou extração de imagens em uma etapa adicional; o ServiceDesk ainda não faz essa conversão.

Downloads utilizam os endereços HTTPS fornecidos pelo Chatwoot, sem enviar a chave da API ao servidor de mídia. Se algum anexo estiver indisponível, a exportação informa erro e não copia um pacote incompleto ao destino. Não há alteração da conversa no Chatwoot.
