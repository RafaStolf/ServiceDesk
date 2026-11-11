# Progresso — Métricas e SLA

Base: a1a536dde70d8ebb2514583685af1b091967a223 (PR #13, release v1.1.43.0).
Branch: feat/metricas-sla. A proposta de métricas foi aprovada pelo usuário nesta conversa.

## Concluído neste checkpoint
- Models/MetricasTicket.cs: eventos, política/ocorrências, agregação de médias com denominadores, datas de Brasília.
- Services/MetricasChatwootService.cs: GET de perfil/lista/eventos oficiais, escopo assignee_type=me, limites e cancelamento. Sem acesso a mensagens de clientes.
- Testes sintéticos em Tests/ChatwootContract/MetricasTests.cs; suite completa passou em Linux/.NET 10, inclusive fronteiras UTC de Brasília, zero útil, reaberturas, 403/404/429, paginação repetida, política ausente e cancelamento.
- Especificação e plano registrados. Task 1 implementada, mas ainda sujeita à revisão final e novos testes de cenários descobertos.

## Ainda não implementado (Task 2)
- MetricasPage.cs com fila, histórico por período, atalhos Hoje/7/30 dias, tempo corrido/útil, resumo, avisos, lista e detalhes.
- Botão Métricas em MainPage.xaml; navegação de ida/volta preservando a fila, suspendendo temporizadores no Unloaded e retomando-os no Loaded. MainWindow usa RootFrame; MainPage pode usar NavigationCacheMode.Required para preservar a instância.
- Teste Windows de navegação/renderização, documentação de produto e README (a busca já foi removida no PR #13, mas a linha do README ainda precisa ser corrigida).
- Revisão final independente, validação Windows, integração e release só após concluir a tela e todas as verificações.

## Pontos para revisar antes de integrar
- Histórico encontra tickets atualmente atribuídos, não necessariamente todos os atendidos no passado; manter esse recorte explícito na tela.
- Estado do SLA é atual, não fotografia do passado; eventos são filtrados pela data created_at em Brasília. Sem evento/permissão, não afirmar pertencimento ao período nem resultado zero.
- first_response/reply_time/conversation_resolved são durações oficiais em segundos; value_in_business_hours zero é válido, ausente não vira zero. Eventos de outro user_id são excluídos.
- Endpoint reporting_events pertence à edição enterprise no código consultado. A instalação real é desconhecida. Mostrar ausência claramente, sem pedir chave de administrador.
- Revisar falhas 401 na paginação, tratamento de timeout parcial e 429. Confirmar que ausência global do endpoint não encobre falha apenas de um ticket.
- Não usar inatividade do card como resposta, resolução ou SLA. Não inventar metas ou motor de SLA sem diretriz.

O PR em rascunho é um checkpoint técnico, não uma entrega da tela completa.
