# PRD — ServiceDesk

**Status:** ativo · **Versão:** 1.0 · **Produto:** aplicação local Windows para um analista N1.

## Contexto e objetivo

O analista trabalha a fila atribuída a ele no Chatwoot e precisa identificar clientes que aguardam resposta, inclusive enquanto usa outros aplicativos. O ServiceDesk mostra o contato, a empresa, o ID e a situação do ticket, com o tempo de espera quando aplicável. A integração é somente leitura.

O analista deve conseguir identificar esperas acima do limite nas filas que pode consultar e abrir a conversa correspondente no Chatwoot. Os alertas externos acompanham apenas **Meus tickets** enquanto a fila está atualizada. A sincronização pode ser manual, em tempo real ou em intervalo fixo configurável.

## Escopo confirmado

| ID | Requisito | Aceite |
|---|---|---|
| RF-01 | Consultar em modo de leitura as conversas abertas e adiadas atribuídas ao usuário da chave API. | A lista não altera conversa alguma. |
| RF-02 | Usar a interação pública identificada entre o cliente e o agente atribuído. | O card identifica quem enviou a última mensagem pública pertinente. Notas, eventos, bots e mensagens de outro agente ficam fora desse cálculo. |
| RF-03 | Mostrar se está adiada e separar Em aberto, Adiados e Todos. | Um adiado não aparece em Em aberto e é encontrado nas demais visões. |
| RF-04 | Organizar a fila pelos filtros Em aberto, Adiados e Todos, sem campo de busca. | A seleção mantém a cor de destaque escolhida e não altera dados no Chatwoot. |
| RF-05 | Configurar a sincronização manual, em tempo real ou em intervalo fixo de 1 a 120 minutos. | O modo e o intervalo persistem após reabrir o aplicativo. |

## Fora de escopo

- Enviar mensagens, criar notas, mudar status, adiar ou atribuir conversas pelo ServiceDesk.
- Login próprio, múltiplos analistas e automação de decisões do fluxo N1.
- Alertas externos para tickets de outras filas e cálculo local das metas de SLA do Chatwoot.

## Questões abertas

| Decisão pendente | Impacto |
|---|---|
| Critérios visuais de prioridade além do tempo de espera do cliente. | Define cores, ordenação secundária e possíveis notificações futuras. |
| Forma de apresentação e edição das pendências locais. | Define a próxima tela e uso das tabelas existentes. |

## Contrato da integração e validação

O [glossário do Chatwoot](GLOSSARIO-CHATWOOT.md) registra os campos oficiais, os requisitos RF-06 a RF-08 e RNF-01 a RNF-03 e os critérios de teste. A diretriz e o fluxo N1 orientarão as regras futuras de resposta, horário e adiamento.

## Ajustes de interface confirmados

| ID | Requisito | Aceite |
|---|---|---|
| RF-09 | Menu do card reúne Copiar texto, Exportar chat e Abrir no Chatwoot. | Nenhum botão de abrir no Chatwoot aparece na notificação. |
| RF-10 | Mostrar tema, cor e controles de notificações apenas nas configurações. | As preferências persistem e o ícone Centrium não muda. |
| RF-11 | Não exibir prévia de mensagem, nome de agente ou ticket externo. | O ID é o da conversa e a duração não é substituída pela criação. |

## Métricas e SLA

- RF-12: Botão Métricas abre uma tela separada, com retorno à fila preservando seu filtro; a sincronização periódica da fila fica suspensa enquanto a tela estiver aberta.
- RF-13: Fila atual e histórico por período inclusivo em Brasília; atalhos Hoje/7/30 dias, progresso e cancelamento.
- RF-14: Médias pessoais oficiais de primeira resposta, respostas seguintes e resolução, em tempo corrido/útil, com denominadores de eventos. Eventos de outros usuários não entram; reaberturas não são deduplicadas como se fossem um único evento.
- RF-15: Política, metas, vencimentos e ocorrências oficiais por ticket. O estado atual não representa uma fotografia histórica. Registros ausentes não equivalem a zero ou meta cumprida.
- RNF-04: Somente GET, mesma chave e permissões, nenhuma consulta de conteúdo de mensagens para calcular métricas.
- RNF-05: Recorte de tickets atualmente atribuídos, máximo de 250 tickets/10 páginas por situação e avisos de limitações/período não confirmado. Consultas interrompidas não apagam dados anteriores nem os apresentam como uma nova consulta bem-sucedida.
- RNF-06: Tratar autenticação, permissão, recurso ausente, limite de chamadas e timeout. Cancelar ao sair; não carregar histórico no temporizador da fila.

Especificação: [Métricas e SLA](superpowers/specs/2026-11-11-metricas-sla-design.md). O workflow Windows testa navegação, histórico, médias, detalhes e cancelamento contra uma API local com dados sintéticos.

## Alertas de espera do cliente

| ID | Requisito | Aceite |
|---|---|---|
| RF-16 | Usar um único início de espera para card, painel e notificações. Para ticket aberto, usar `waiting_since` quando válido. Somente quando a propriedade estiver ausente, usar a última mensagem pública do cliente se ela indicar que o cliente ainda aguarda resposta. | Valor explícito `null` ou `0` impede a inferência por mensagem. Um ticket aberto sem resposta anterior do agente também pode entrar por essa regra. Notas e eventos ficam fora da regra. |
| RF-17 | Destacar todo o card de espera em vermelho ao atingir o limite, com texto que indique o estado e a duração. O padrão e o mínimo são 10 minutos. O limite pode ser ajustado de 10 a 1.440 minutos. | Aos 10 minutos, com o limite padrão, o card muda de aparência nas filas acessíveis que exibem o ticket. O destaque continua ativo com os alertas desligados. Um valor salvo abaixo de 10 minutos é ajustado ao mínimo. |
| RF-18 | Exibir um único painel persistente acima das outras janelas, com canto e monitor configuráveis em **Alertas**. O padrão usa o canto inferior direito do monitor em uso ao abrir o painel. | Com a fila pessoal atualizada e ao menos um ticket no limite, o painel mostra nome e tempo em uma linha para cada um dos três tickets mais antigos e a contagem dos demais. O aplicativo em primeiro plano mantém o foco e recebe os cliques. O painel funciona com a janela principal na bandeja e some ao encerrar a última espera elegível. |
| RF-19 | Emitir um aviso inicial do Windows uma vez por episódio de espera elegível, com espaçamento global entre envios. Enquanto houver esperas acima do limite, emitir um lembrete conjunto a cada 5 minutos por padrão. O intervalo aceita de 1 a 60 minutos ou **Nunca**. | Dois tickets que atingem o limite juntos recebem avisos iniciais espaçados. Com **Nunca**, o lembrete fica desativado e os avisos iniciais seguem a regra do episódio. O Windows usa o som selecionado ou fica silencioso conforme a preferência. Desligar os alertas oculta o painel e suspende as notificações de espera. |
| RNF-07 | Suspender os alertas externos quando a sincronização falhar ou o último sucesso ultrapassar o prazo de validade da fila. Na atualização automática, usar o intervalo de sincronização mais um minuto. Na manual, usar cinco minutos. | Uma falha ou expiração oculta o painel e pausa o envio de avisos. Uma sincronização bem-sucedida volta a refletir os tickets elegíveis, sem reproduzir avisos acumulados nem repetir o aviso inicial já emitido. |
| RNF-08 | Reusar a sincronização de leitura e manter nomes de contatos fora dos logs. | O fluxo de alertas usa os dados da fila, as chamadas ao Chatwoot continuam somente GET e os logs omitem os nomes dos contatos. |
