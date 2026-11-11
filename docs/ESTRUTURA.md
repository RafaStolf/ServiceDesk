# Estrutura do projeto

```text
ServiceDesk/
├── Assets/                 Ícones e imagens do pacote Windows
├── Models/                 Modelo de uma conversa monitorada
├── Services/               Integração Chatwoot, configuração e alertas
├── Properties/             Perfis de execução e publicação
├── docs/                   Produto, arquitetura, decisões e este mapa
├── App.xaml(.cs)           Inicialização da aplicação WinUI
├── MainWindow.xaml(.cs)    Janela e barra de título
├── MainPage.xaml(.cs)      Tela principal e interação do usuário
├── Package.appxmanifest    Identidade e metadados do pacote MSIX
└── ServiceDesk.csproj      Projeto .NET e dependências
```

## Arquivos da raiz

| Arquivo ou pasta | Responsabilidade |
|---|---|
| `ServiceDesk.csproj` | Define o aplicativo .NET 10, WinUI, empacotamento MSIX e dependências. |
| `App.xaml` / `App.xaml.cs` | Recursos globais e criação da janela principal. |
| `MainWindow.xaml` / `MainWindow.xaml.cs` | Estrutura, título, ícone e navegação inicial da janela. |
| `MainPage.xaml` / `MainPage.xaml.cs` | Controles visuais, filtros, cards, atualizações, alertas e diálogos de configuração. |
| `Package.appxmanifest` | Nome exibido, identidade e recursos do pacote Windows. |
| `app.manifest` | Metadados de execução do aplicativo Windows. |
| `.gitignore` | Impede que arquivos gerados e pacotes de saída sejam commitados. |

## Código de domínio e serviços

| Arquivo | Responsabilidade |
|---|---|
| `Models/AtendimentoMonitorado.cs` | Representa uma conversa e calcula o tempo desde a criação da conversa e última interação. |
| `Services/ChatwootSyncService.cs` | Faz requisições de leitura ao Chatwoot, filtra caixa de entrada e ignora notas privadas/eventos do sistema. |
| `Services/ConfiguracaoChatwoot.cs` | Lê e valida URL, chave API, conta, agente e caixa de entrada do Chatwoot. |
| `Services/ConfiguracaoLocal.cs` | Localiza e grava `application.properties` no perfil Windows do usuário. |
| `Services/ConfiguracaoAtualizacaoService.cs` | Salva as preferências de atualização e notificação no Windows. |
| `Services/MonitoramentoService.cs` | Coordena a sincronização e entrega as conversas para a tela. |
| `Services/ServicoNotificacoes.cs` | Emite alertas locais e evita repetição até que haja nova mensagem. |

## Recursos, publicação e documentos

| Caminho | Responsabilidade |
|---|---|
| `Assets/` | Ícone do ServiceDesk e imagens exigidas pelo MSIX. |
| `Properties/launchSettings.json` | Perfil local usado durante o desenvolvimento. |
| `Properties/PublishProfiles/` | Perfis de publicação para `x64`, `x86` e `ARM64`. |
| `docs/PRD.md` | Escopo e requisitos do produto. |
| `docs/ARQUITETURA.md` | Fronteiras, responsabilidades e invariantes. |
| `docs/ADR-001-INTEGRACAO-LEITURA-E-CONFIGURACAO-LOCAL.md` | Decisão de manter a API somente em leitura e configuração local. |

## Arquivos que não entram no Git

- `bin/` e `obj/`: produtos temporários de compilação.
- `AppPackages/` e `BundleArtifacts/`: pacotes gerados para distribuição.
- `%USERPROFILE%\.servicedesk\application.properties`: chave API e dados individuais do usuário.

## Fluxo de dados

1. `MainPage` solicita atualização ao `MonitoramentoService`.
2. `MonitoramentoService` cria `ChatwootSyncService` com a configuração local.
3. `ChatwootSyncService` consulta conversas da caixa de entrada e as mensagens públicas.
4. Os dados são convertidos em `AtendimentoMonitorado` e exibidos nos cards.
5. `ServicoNotificacoes` alerta quando uma conversa não recebe mensagem pública dentro do limite configurado.
