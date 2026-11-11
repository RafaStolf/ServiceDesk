# Proposta — Métricas e SLA do ServiceDesk

## Objetivo
Abrir pelo botão Métricas, ao lado de Configurações e Atualizar, uma tela com a fila atual e o histórico de tickets por período. Preservar a identidade Centrium, o tema e a cor escolhidos. A tela principal fica sem busca.

## Fonte e alternativas
A proposta usa os dados oficiais por ticket, com a mesma chave já configurada, sem escrever no Chatwoot. Os campos applied_sla e sla_events trazem política, metas, vencimentos e ocorrências quando disponíveis; reporting_events pode trazer durações de primeira resposta, respostas seguintes e resolução. Esta é a opção recomendada para respeitar os números do Chatwoot.
Os relatórios gerais do Chatwoot exigem permissão de administrador no código oficial consultado. Por isso, o painel do ServiceDesk deve funcionar com os dados dos tickets que a sua chave já pode consultar, sem depender desses relatórios gerais. Se algum tempo não vier calculado pelo Chatwoot, ele será identificado como indisponível nesta primeira entrega. Para calcular esse tempo no ServiceDesk, primeiro precisamos da regra do suporte: por exemplo, se um adiamento pausa o prazo, se uma resposta automática conta como resposta e como tratar um ticket reaberto. Ainda falta confirmar quais dados a instalação de vocês fornece.

## Tela
- Cabeçalho Métricas e SLA, botões Voltar e Atualizar, informação da última consulta.
- Visões Fila atual e Histórico. Histórico com datas de início/fim, atalhos Hoje, 7 dias e 30 dias, fuso de Brasília.
- Resumo de primeira resposta, respostas seguintes e resolução, com quantidade de registros usados nas médias; dados indisponíveis não entram como zero.
- Situação de SLA separada das durações: no prazo, violado, concluído e sem política/dados, conforme evidências retornadas.
- Lista por ticket: número, contato, empresa, situação, primeira resposta, resposta seguinte, resolução e política/meta aplicada quando disponível. Os valores ausentes mostram um motivo, sem inventar duração.
- Detalhe do ticket permite consultar metas, vencimentos e ocorrências oficiais. Não mostra texto das mensagens nem nomes de agentes desnecessários.

## Recorte e definição dos indicadores
A fila usa os tickets abertos e adiados já consultados. O histórico inclui tickets resolvidos acessíveis ao usuário, obtidos por paginação, e registros de eventos no período selecionado. Reaberturas e múltiplas resoluções devem ser apresentadas como eventos, sem confundir quantidade de resoluções com quantidade de tickets. A interface informa o critério do período e eventuais limites/consultas incompletas.
Primeira resposta, respostas seguintes e resolução usam durações oficiais dos eventos quando disponíveis, identificando a unidade e se são corridas ou úteis. O tempo desde a última interação do card continua sendo outro indicador.

## Ausência de dados e permissões
A versão/edição da instalação ainda é desconhecida. Cada capacidade é detectada pela resposta da API; negar acesso ou não oferecer SLA/eventos não significa valor zero nem meta cumprida. Não solicitar chave de administrador como solução padrão. Quando os dados não existirem, mostrar explicitamente que a métrica não está disponível nessa instalação/permissão. Um motor local de SLA só será considerado depois da diretriz e das metas do suporte.

## Operação e validação
Carregar histórico sob demanda, com progresso, cancelamento, paginação com limites explícitos e baixa concorrência. Não baixar todo o histórico durante a sincronização de 30 segundos nem persistir conteúdo de clientes. Aplicar tratamento de 401/403/404/429 e falhas parciais sem perder a fila atual. Testar contratos sintéticos de políticas presentes/ausentes, durações/unidades, período de Brasília, reaberturas e respostas sem permissão; compilar e testar navegação no Windows.

## Fora desta entrega
Editar políticas/metas no Chatwoot, enviar mensagens, recalcular metas contratuais sem diretriz, elevar permissões e criar painel de outros agentes. A tela permite acompanhar/analisar métricas; configurar regras oficiais requer escopo específico posterior.

## Referências técnicas consultadas (Chatwoot v4.19.1)
- [Campos da conversa](https://github.com/chatwoot/chatwoot/blob/v4.19.1/app/views/api/v1/conversations/partials/_conversation.json.jbuilder): first_reply_created_at e waiting_since não são durações consolidadas de todos os indicadores.
- [SLA aplicado](https://github.com/chatwoot/chatwoot/blob/v4.19.1/enterprise/app/views/api/v1/models/_applied_sla.json.jbuilder): metas em segundos e prazos frt/nrt/rt; disponibilidade vinculada à funcionalidade da instalação.
- [Eventos de relatório](https://github.com/chatwoot/chatwoot/blob/v4.19.1/enterprise/app/views/api/v1/models/_reporting_event.json.jbuilder): value e value_in_business_hours, início/fim e nome. Os nomes first_response, reply_time e conversation_resolved são usados pelo [listener oficial](https://github.com/chatwoot/chatwoot/blob/v4.19.1/app/listeners/reporting_event_listener.rb); seus valores são segundos.
- [Política de relatórios](https://github.com/chatwoot/chatwoot/blob/v4.19.1/app/policies/report_policy.rb): administração no comportamento padrão; extensões da edição podem mudar a autorização.
