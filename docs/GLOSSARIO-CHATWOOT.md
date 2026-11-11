# Nomenclaturas e contrato do Chatwoot

Referência: Chatwoot oficial v4.19.1. A versão usada pela equipe ainda será confirmada. O ServiceDesk acompanha a fila individual, em modo de leitura. "Atendimento" continua sendo o termo do processo N1; a entidade correspondente na API é uma **conversa**.

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
| `private=true` | Nota/mensagem privada; excluída dos contadores públicos e da exportação |
| `status=open` | Aberta |
| `status=snoozed` | Adiada; sem `snoozed_until`, até a próxima resposta |
| `status=pending` | Pendente; não é sinônimo de adiada |
| `status=resolved` | Resolvida |
| `created_at` da conversa | Criação; não mede quanto tempo a conversa permaneceu aberta após reaberturas |
| `last_activity_at` | Última atividade; pode incluir evento e não substitui a data da última mensagem pública |
| ID do atendimento | O `display_id` da conversa, retornado como `id` pelo endpoint; não existe um campo de ticket externo no produto |

Os campos `sender_type`/`sender_id` no nível raiz pertencem a outros formatos, como atributos de eventos/modelos. O endpoint GET de mensagens usa `sender.type`/`sender.id`. A implementação prioriza o formato do endpoint e mantém compatibilidade com os campos antigos quando o remetente aninhado não está disponível.

O card exibe apenas o tempo da última interação identificada do agente atribuído, junto à identificação, empresa e situação. Não exibe texto das mensagens ou nome do agente. A integração lê `meta.assignee.id`; o `chatwoot.user-id` legado não substitui uma atribuição ausente. Mensagens privadas e eventos continuam excluídos. A criação da conversa e `last_activity_at` não são usados como duração de uma interação desconhecida.

Ações do card ficam no menu **…**: Copiar texto, Exportar chat e Abrir no Chatwoot. Não há campo de ticket externo; o ID identifica o próprio atendimento. Tema, cor e preferências de notificações são ajustados apenas nas configurações. O ícone original da Centrium permanece. As notificações não contêm ação para abrir o Chatwoot.

## Indicadores e regras ainda a confirmar

- **Tempo de primeira resposta:** intervalo até a primeira resposta elegível do atendimento.
- **Tempo de espera do cliente:** intervalo de espera até uma resposta subsequente elegível. Esse é o rótulo usado pelo relatório oficial `reply_time`.
- **Tempo de resolução:** intervalo até a resolução, conforme a regra adotada para reaberturas.
- **SLA (Service Level Agreement):** acordo de nível de serviço, com metas e condições, por exemplo prazo de primeira resposta em minutos úteis. Medir um tempo não cria automaticamente um SLA.

Antes de implementar esses indicadores, a diretriz N1 deve determinar o início e a pausa de cada relógio, as mensagens que contam como resposta, o efeito das macros de adiamento e se o cálculo usa tempo corrido ou apenas períodos atendidos. Horário comercial e plantão só afetam os indicadores se a regra do suporte assim definir. O tempo da última interação exibido continua sendo corrido enquanto as metas e regras de SLA não forem confirmadas.

Contexto informado: dois N1, até cinco chats simultâneos por N1; em dias de pico, fila adicional de cerca de dez atendimentos, chegando a vinte ou mais conversas no conjunto. Existem doze agentes cadastrados. Resposta automática conhecida: mensagem de fora do expediente. A diretriz e o fluxo N1 serão fornecidos pelo usuário. Não foram presumidas regras de plantão ou pausas.

## Requisitos verificáveis desta revisão

| ID | Requisito | Aceite |
|---|---|---|
| RF-06 | Ler remetente conforme o endpoint oficial. | Contato, próprio agente, outro agente e bot com IDs coincidentes são distinguidos. |
| RF-07 | Usar o status retornado como referência da situação. | Data residual de adiamento não muda uma conversa `open` para `snoozed`. |
| RF-08 | Identificar corretamente o tempo exibido. | A duração exibida usa a última interação do agente atribuído e não é apresentada como SLA. |
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

- [Serialização de mensagens](https://github.com/chatwoot/chatwoot/blob/v4.19.1/app/views/api/v1/models/_message.json.jbuilder)
- [Tipos de mensagem](https://github.com/chatwoot/chatwoot/blob/v4.19.1/app/models/message.rb)
- [Remetente usuário](https://github.com/chatwoot/chatwoot/blob/v4.19.1/app/models/user.rb) e [contato](https://github.com/chatwoot/chatwoot/blob/v4.19.1/app/models/contact.rb)
- [Nomes dos indicadores em português](https://github.com/chatwoot/chatwoot/blob/v4.19.1/app/javascript/dashboard/i18n/locale/pt_BR/report.json)

## Situação atual da interface

Os cards ocupam a largura disponível em uma a quatro colunas e a busca ocupa a faixa inteira. A sincronização bem-sucedida aparece em uma linha discreta; erros indicam que a fila anterior pode estar desatualizada. O relógio atualiza sem reconstruir os cards a cada trinta segundos. Os alertas usam apenas uma última interação conhecida do agente e são emitidos uma vez por referência; não são emitidos a partir de uma data de criação presumida.
