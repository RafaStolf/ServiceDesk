# Arquitetura — ServiceDesk

## Contextos e fronteiras

```text
Janela WinUI (apresentação)
        │
        ├── MonitoramentoService ── ChatwootSyncService ── API Chatwoot (GET)
        ├── ConfiguracaoAtualizacaoService ── preferências locais de sincronização e alertas
        ├── Painel de espera ── janela auxiliar exibida sobre a área de trabalho
        └── ServicoNotificacoes ── notificações locais do Windows

```

### Responsabilidades

- A apresentação WinUI reúne comandos, filtros, feedback e abertura da conversa no Chatwoot.
- `MonitoramentoService` coordena a sincronização remota de leitura. `ChatwootSyncService` lê os dados de fila via GET e distingue `waiting_since` ausente de um valor explícito.
- `ConfiguracaoLocal` lê `application.properties` e mantém as credenciais no perfil do usuário, fora dos logs, do repositório e do pacote.
- `ConfiguracaoAtualizacaoService` preserva as preferências locais de sincronização e alertas. A leitura de configurações antigas ajusta limites de espera abaixo de 10 minutos para o mínimo.
- O painel de espera é uma janela auxiliar única e reaproveitada pelo aplicativo, inclusive quando a janela principal está oculta na bandeja. A apresentação ordena a fila pessoal pelo início da espera e atualiza nomes, durações e a contagem a partir da fila sincronizada.
- `ServicoNotificacoes` realiza os envios para o Windows com espaçamento global. Os avisos usam o som nativo selecionado ou o modo silencioso.

## Invariantes

1. A integração Chatwoot é somente leitura.
2. A última interação exibida considera as mensagens públicas do cliente e do agente de `meta.assignee.id`. Notas privadas, eventos e outros remetentes ficam fora desse cálculo.
3. `status=snoozed` representa conversa adiada. `snoozed_until` informa a data, quando definida.
4. Cards, painel e avisos usam a mesma referência de espera para tickets com `status=open`. Um `waiting_since` válido tem precedência. Se o campo estiver ausente, a data da última mensagem pública do cliente indica o início quando ela for a última interação pertinente e o agente ainda não tiver respondido a ela. `null` e `0` explícitos indicam ausência de espera e impedem a inferência.
5. O card usa a referência de espera em qualquer fila acessível e o limite de 10 a 1.440 minutos. Painel e notificações exigem ticket elegível de **Meus tickets**, sincronização recente e alertas habilitados.

## Validade da fila e ciclo de alertas

A sincronização bem-sucedida registra o horário da leitura e fornece os tickets para os alertas. Uma falha marca a fila como desatualizada imediatamente. A validade da última leitura bem-sucedida também expira após o intervalo de sincronização automática mais um minuto, ou após cinco minutos no modo manual.

O episódio de espera é identificado pelo ticket e pelo início da espera. Quando o ticket atinge o limite, pode emitir um aviso inicial. A aplicação espaça os avisos mesmo quando vários tickets atingem o prazo juntos e calcula um único lembrete com os tickets ainda elegíveis. O lembrete é configurável de 1 a 60 minutos, com padrão de 5 minutos e opção **Nunca**.

Quando a fila fica desatualizada, o aplicativo oculta o painel e pausa os envios. Uma nova leitura bem-sucedida, inclusive após retomar a execução, restabelece a exibição dos tickets elegíveis e a cadência do lembrete a partir do estado atual, sem reproduzir avisos acumulados. O episódio cujo aviso inicial já saiu mantém esse registro.

Os critérios de aceite dos alertas estão no [PRD](PRD.md#alertas-de-espera-do-cliente).

## Operação e segurança

- A configuração preferencial está em `%USERPROFILE%\.servicedesk\application.properties`; ela inclui chave API, conta e caixa de entrada. O ID de usuário legado é preservado no arquivo, mas não substitui o agente atribuído.
- Os logs de sincronização e notificações omitem nomes de contatos, conteúdo de conversas e credenciais.
- O pacote é iniciado pelo atalho instalado/Start Menu.
