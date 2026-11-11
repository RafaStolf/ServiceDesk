# Progresso — Métricas e SLA

Base: a1a536dde70d8ebb2514583685af1b091967a223 (PR #13).
Branch: feat/metricas-sla; PR #14.

## Implementado
- Leitura GET de perfil, lista e eventos oficiais; metas e ocorrências de SLA; agregações e período de Brasília.
- Tela Métricas e SLA, fila/histórico, datas/atalhos, base de tempo, resumo com denominadores e detalhes por ticket.
- Navegação com cache da fila e suspensão/retomada dos timers; cancelamento de consultas ao sair e proteção contra progresso atrasado.
- Testes sintéticos de período, duração útil zero, reaberturas, falhas/permissões, limites e cancelamento. Script UIAutomation testa o aplicativo publicado contra API sintética local no runner Windows.

## Decisões
- Ruling: histórico consulta tickets atualmente atribuídos ao usuário, pois a API de lista não oferece um histórico de atribuição pessoal completo; transferidos podem ficar de fora. Esse recorte está explícito na tela e documentação.
- Ruling: o estado do SLA é atual, pois o payload não constitui fotografia histórica; o período filtra eventos/criação de registros de relatório, ocorrências e atualização de SLA. Tickets com período não confirmado não entram no resumo de SLA.
- Ruling: sem eventos oficiais, a duração é indisponível. Não foi criado motor local de SLA nem presumidas metas, pausas ou calendários.
- Ruling: após três recusas/ausências consecutivas dos eventos, suspender a consulta desses eventos e informar os não consultados. Uma falha genérica reinicia a sequência. HTTP 429 impede novas consultas.

## Evidências e revisão
A suite offline passou em .NET 10 no Linux. A revisão independente conferiu as falhas, os cálculos e o ciclo da tela. Encontrou uma sequência de erros que podia ocultar eventos de tickets posteriores: o caso foi reproduzido com teste que falhou e foi corrigido; a suite passou novamente.

A validação Windows, incluindo teste real de navegação/renderização e instalação, deve estar aprovada no commit final antes da integração. Consultar os checks do PR e a release correspondente; um workflow em andamento não comprova aprovação. A instalação Chatwoot real da empresa não foi consultada nesta validação.
