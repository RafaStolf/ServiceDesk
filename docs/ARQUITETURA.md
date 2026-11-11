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
- **ServicoNotificacoes:** alerta local uma única vez por última interação conhecida do agente atribuído; adiados não alertam.

## Invariantes

1. A integração Chatwoot é somente leitura.
2. A última interação exibida é uma mensagem pública enviada pelo agente de `meta.assignee.id`; notas privadas, eventos e outros remetentes são ignorados.
3. `status=snoozed` representa conversa adiada; `snoozed_until` informa a data, quando definida.

## Entrega incremental

1. **Atual:** monitoramento remoto, filtros, atalho e atualização automática configurável.
2. **Próxima:** testes de regras e filtros da integração.

## Operação e segurança

- A configuração preferencial está em `%USERPROFILE%\.servicedesk\application.properties`; ela inclui chave API, conta e caixa de entrada. O ID de usuário legado é preservado no arquivo, mas não substitui o agente atribuído.
- Erros devem ser úteis sem conter token, senha ou string de conexão.
- O pacote é iniciado pelo atalho instalado/Start Menu.
