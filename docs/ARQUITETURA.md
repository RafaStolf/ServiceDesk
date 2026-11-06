# Arquitetura — ServiceDesk

## Contextos e fronteiras

```text
Janela WinUI (apresentação)
        │
        └── MonitoramentoService ── ChatwootSyncService ── API Chatwoot (GET)
        │
        └── ConfiguracaoAtualizacaoService ── LocalSettings do Windows
        └── ServicoNotificacoes ── notificação local do Windows

```

### Responsabilidades

- **Apresentação WinUI:** comandos, filtros, feedback e abertura da conversa no Chatwoot.
- **MonitoramentoService:** coordena a sincronização remota de leitura.
- **ChatwootSyncService:** única fronteira HTTP; não pode realizar `POST`, `PUT`, `PATCH` ou `DELETE`.
- **ConfiguracaoLocal:** única leitora de `application.properties`; segredos não são exibidos, versionados nem empacotados.
- **ConfiguracaoAtualizacaoService:** preferências não sensíveis por usuário no pacote Windows.
- **ServicoNotificacoes:** alerta local uma única vez por atendimento e referência de interação; adiados não alertam.

## Invariantes

1. A integração Chatwoot é somente leitura.
2. A última interação usada no contador é uma mensagem pública; notas privadas e eventos do sistema são ignorados.
3. `snoozed` ou `snoozed_until` futuro representa atendimento adiado.

## Entrega incremental

1. **Atual:** monitoramento remoto, filtros, atalho e atualização automática configurável.
2. **Próxima:** testes de regras e filtros da integração.

## Operação e segurança

- A configuração preferencial está em `%USERPROFILE%\.servicedesk\application.properties`; ela inclui chave API, ID do usuário e ID do Inbox.
- Erros devem ser úteis sem conter token, senha ou string de conexão.
- O pacote é iniciado pelo atalho instalado/Start Menu.
