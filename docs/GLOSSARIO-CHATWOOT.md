# Nomenclaturas e contrato do Chatwoot

Instalação informada: **CCChatwoot v4.5.0, build `75d3569`**. Contrato público conferido no código oficial da tag v4.5.0; as particularidades da build personalizada ainda não foram conferidas em payload real. O ServiceDesk acompanha a fila individual, em modo de leitura. "Atendimento" continua sendo o termo do processo N1; a entidade correspondente na API é uma **conversa**.

| Campo / termo | Significado no ServiceDesk |
|---|---|
| `account_id` | ID da conta |
| `inbox_id` / caixa de entrada | Caixa à qual a conversa pertence. "Canal" é o tipo de integração da caixa, não outro nome para inbox. |
| `sender.type=contact` | Remetente do tipo contato |
| `sender.type=user` | Remetente do tipo usuário/agente; comparar `sender.id` para identificar a própria mensagem |
| `message_type=0` / incoming | Mensagem recebida |
| `message_type=1` / outgoing | Mensagem enviada |
| `message_type=2` / activity | Evento de atividade, excluído das mensagens públicas |
| `message_type=3` / template | Mensagem de modelo; pode ser pública, mas não se presume resposta humana |
| `private=true` / `is.private=true` | Mensagem privada (nota interna); excluída dos contadores públicos e da exportação. A API oficial usa `private`; `is.private` é aceito como propriedade literal ou objeto `is` com `private`. Qualquer marcação verdadeira prevalece. |
| `status=open` | Aberta |
| `status=snoozed` | Adiada; sem `snoozed_until`, até a próxima resposta |
| `status=pending` | Pendente; não é sinônimo de adiada |
| `status=resolved` | Resolvida |
| `created_at` da conversa | Criação; não mede quanto tempo a conversa permaneceu aberta após reaberturas |
| `last_activity_at` | Última atividade; pode incluir evento e não substitui a data da última mensagem pública |
| ID do ticket | O `display_id` da conversa, retornado como `id` pelo endpoint; não existe um campo de ticket externo no produto |

Os campos `sender_type`/`sender_id` no nível raiz pertencem a outros formatos, como atributos de eventos/modelos. O endpoint GET de mensagens usa `sender.type`/`sender.id`. A implementação prioriza o formato do endpoint e mantém compatibilidade com os campos antigos quando o remetente aninhado não está disponível.

O card exibe a interação mais recente identificada entre cliente e agente atribuído, indicando o remetente. Os alertas continuam usando apenas a última resposta pública do agente atribuído. A integração lê `meta.assignee.id`; o `chatwoot.user-id` legado não substitui uma atribuição ausente. Mensagens privadas e eventos são excluídos. Criação e `last_activity_at` não substituem uma data de interação desconhecida.

Ações do card ficam no menu **…**: Copiar texto, Exportar chat e Abrir no Chatwoot. Não há campo de ticket externo; o ID identifica o próprio ticket. Tema, cor e preferências de notificações são ajustados apenas nas configurações. O ícone original da Centrium permanece. As notificações não contêm ação para abrir o Chatwoot.

## Métricas, SLA e fluxo N1

- **Primeira resposta (FRT):** média dos registros oficiais `first_response` do usuário autenticado.
- **Espera do cliente:** média dos registros oficiais `reply_time`; não confundir com a meta de próxima resposta (NRT).
- **Resolução:** média dos registros `conversation_resolved`. Reaberturas podem gerar vários registros por ticket.
- **SLA:** metas, situação e ocorrências retornadas pelo Chatwoot. Ausência de dados não significa cumprimento. FRT corresponde à primeira resposta; NRT, à próxima resposta; RT, à resolução.
- **Última interação:** relógio corrido das mensagens identificadas no card. Não é SLA nem duração de espera calculada pelo relatório.
- **Mensagem privada:** comunicação interna, também chamada nota interna no material N1. **Mensagem para o cliente:** mensagem pública da conversa; não implica envio automático pelo ServiceDesk.

As médias usam somente eventos disponíveis e durações oficiais; cada indicador informa o número de registros. Tempo útil segue o cadastro do Chatwoot. A seleção de base de tempo não muda a política. O histórico inclui tickets atualmente atribuídos ao usuário e filtra os eventos por data em Brasília; o estado de SLA é o atual. Não é uma auditoria completa de todos os atendimentos do período.

O painel expansível em Métricas contém as cinco situações do fluxograma, os prazos e o encaminhamento. A etapa N1 não é inferida por texto, etiqueta ou status. Os prazos operacionais não são usados para inventar metas ou vencimentos de SLA. Um prazo de 30 min só é apresentado como útil onde o fluxograma o declara assim.

**Precedência decidida pelo usuário:** o fluxograma prevalece para esperas e encerramento por ausência de retorno, em vez da exigência de três tentativas e ligação na terceira da diretriz. O restante da diretriz orienta entendimento, investigação, documentação e qualidade. O ServiceDesk continua somente leitura: não executa macros nem encerra atendimentos.

A versão oficial 4.5.0 retorna nome, estado e metas em `applied_sla`, mas não inclui ali os campos `sla_frt_due_at`, `sla_nrt_due_at`, `sla_rt_due_at` e `sla_completed_at`. Esses campos continuam opcionais para instalações personalizadas; quando ausentes, os detalhes mostram “Não informado”. Não se calcula um vencimento local sem contrato de pausas e calendário.

## Requisitos verificáveis desta revisão

| ID | Requisito | Aceite |
|---|---|---|
| RF-06 | Ler remetente conforme o endpoint oficial. | Contato, próprio agente, outro agente e bot com IDs coincidentes são distinguidos. |
| RF-07 | Usar o status retornado como referência da situação. | Data residual de adiamento não muda uma conversa `open` para `snoozed`. |
| RF-08 | Identificar corretamente o tempo exibido. | O card distingue cliente e agente; os alertas usam a resposta do agente e não são apresentados como SLA. |
| RNF-01 | Tolerar mensagem nula ou conversa vazia. | A sincronização não lança exceção por `last_non_activity_message=null`. |
| RNF-02 | Preservar privacidade de notas no fluxo público. | Notas e atividades não reiniciam o contador, não viram prévia pública e não entram na exportação. |
| RNF-03 | Validar o contrato sem dados reais. | Testes reproduzíveis usam HttpMessageHandler simulado e verificam que todas as chamadas são GET. |

Executar: `dotnet run --project Tests/ChatwootContract/ChatwootContract.csproj`.

## Conferência dos campos de uma conversa real

A tentativa de leitura da conversa 19622 a partir do ambiente de desenvolvimento foi bloqueada pelo proxy. Isso não comprova proteção ou falha do servidor Chatwoot. Não houve leitura das mensagens reais nem alteração da conversa.

No computador que já tem o ServiceDesk configurado, `Scripts/Exportar-CamposConversa.ps1` consulta somente GET e lê a chave do arquivo local, sem colocá-la na linha de comando. Por padrão consulta a conta 1, conversa 19622 e salva `chatwoot-campos-19622.json` na área de trabalho. O arquivo contém apenas IDs, tipos, data, flag de privacidade e indicadores de automação; não inclui texto, nomes, telefone, URLs de anexos nem a chave. Esses metadados ainda devem ser tratados como informação interna.

```powershell
powershell -File .\Scripts\Exportar-CamposConversa.ps1
```

A leitura das propriedades usa o primeiro sinal de igual e é testada por execução no Windows PowerShell 5.1, inclusive com token fictício contendo `=`.

O script não altera estado de leitura, status, atribuição ou mensagens. Redirecionamentos HTTP são rejeitados para evitar encaminhar a chave. O JSON permite validar a classificação real; a interpretação de macros e conteúdo depende da diretriz.

"Anonimizar uma resposta" significa retirar conteúdo/identificadores desnecessários do JSON recebido da API, mantendo a estrutura dos campos que se pretende testar. Não significa responder ao cliente. Um teste de ponta a ponta exercita o aplicativo contra o servidor real, com autenticação, resposta e resultado exibido. Os testes sintéticos desta revisão validam o código, mas não a instalação real da empresa.

CVSS (Common Vulnerability Scoring System) é uma escala técnica de severidade de vulnerabilidades de segurança, de 0 a 10. Não mede tempo de atendimento nem substitui a prioridade de correção do produto.

## Fontes

- [Serialização de mensagens](https://github.com/chatwoot/chatwoot/blob/v4.5.0/app/views/api/v1/models/_message.json.jbuilder)
- [Tipos de mensagem](https://github.com/chatwoot/chatwoot/blob/v4.5.0/app/models/message.rb)
- [Remetente usuário](https://github.com/chatwoot/chatwoot/blob/v4.5.0/app/models/user.rb) e [contato](https://github.com/chatwoot/chatwoot/blob/v4.5.0/app/models/contact.rb)
- [Nomes dos indicadores em português](https://github.com/chatwoot/chatwoot/blob/v4.5.0/app/javascript/dashboard/i18n/locale/pt_BR/report.json)

## Situação atual da interface

Os cards ocupam a largura disponível em uma a quatro colunas e não há campo de busca. A sincronização bem-sucedida aparece em uma linha discreta; erros indicam que a fila anterior pode estar desatualizada. O relógio atualiza sem reconstruir os cards a cada trinta segundos. Os alertas usam apenas uma última interação conhecida do agente e são emitidos uma vez por referência; não são emitidos a partir de uma data de criação presumida.

O campo Empresa prioriza `meta.sender.additional_attributes.company_name`, usado pelo contato padrão do Chatwoot. Campos personalizados `empresa` e `company` permanecem como alternativas. A interface utiliza Ticket para identificar a conversa. O filtro Em aberto dispensa a situação repetida no card; Todos mantém a distinção e adiados mantêm a informação do adiamento. As seções das configurações ficam em posições fixas, sem Pivot.

- [Campos de SLA na versão 4.5.0](https://github.com/chatwoot/chatwoot/blob/v4.5.0/enterprise/app/models/applied_sla.rb)
