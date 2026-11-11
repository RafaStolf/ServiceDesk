# PRD — ServiceDesk

**Status:** ativo · **Versão:** 1.0 · **Produto:** aplicação local Windows para um analista N1.

## Contexto e objetivo

O analista trabalha a fila atribuída a ele no Chatwoot e precisa priorizar conversas sem mensagem pública. A aplicação deve apresentar, sem executar operações no Chatwoot, o identificação, empresa, ID e o tempo desde a última interação identificada do agente atribuído e se a conversa está adiada.

O sucesso é o analista conseguir, ao abrir o ServiceDesk, identificar a fila aberta e os adiados, pesquisar uma conversa e abrir a conversa correspondente no Chatwoot. A atualização é manual ou automática em intervalo configurável.

## Escopo confirmado

| ID | Requisito | Aceite |
|---|---|---|
| RF-01 | Consultar em modo de leitura as conversas abertas e adiadas atribuídas ao usuário da chave API. | A lista não altera conversa alguma. |
| RF-02 | Usar mensagens públicas enviadas pelo agente atribuído para a última interação exibida. | Notas, eventos, bots e mensagens de outro agente não substituem a interação do agente atribuído. |
| RF-03 | Mostrar se está adiada e separar Em aberto, Adiados e Todos. | Um adiado não aparece em Em aberto e é encontrado nas demais visões. |
| RF-04 | Permitir busca por telefone, empresa, contato ou ID. | O termo restringe a lista sem gravação no Chatwoot. |
| RF-05 | Configurar atualização automática de 1 a 120 minutos e alertas locais por tempo desde a última interação identificada do agente. | As escolhas persistem após reabrir e cada conversa alerta uma vez por referência de interação conhecida. |

## Fora de escopo

- Enviar mensagens, criar notas, mudar status, adiar ou atribuir conversas pelo ServiceDesk.
- Login próprio, múltiplos analistas e automação de decisões do fluxo N1.

## Questões abertas

| Decisão pendente | Impacto |
|---|---|
| Critérios visuais de prioridade além do tempo sem interação. | Define cores, ordenação secundária e possíveis notificações futuras. |
| Forma de apresentação e edição das pendências locais. | Define a próxima tela e uso das tabelas existentes. |

## Contrato da integração e validação

O [glossário do Chatwoot](GLOSSARIO-CHATWOOT.md) registra os campos oficiais, os requisitos RF-06 a RF-08 e RNF-01 a RNF-03 e os critérios de teste. A diretriz e o fluxo N1 orientarão as regras futuras de resposta, horário e adiamento.

## Ajustes de interface confirmados

| ID | Requisito | Aceite |
|---|---|---|
| RF-09 | Menu do card reúne Copiar texto, Exportar chat e Abrir no Chatwoot. | Nenhum botão de abrir no Chatwoot aparece na notificação. |
| RF-10 | Mostrar tema, cor e controles de notificações apenas nas configurações. | As preferências persistem e o ícone Centrium não muda. |
| RF-11 | Não exibir prévia de mensagem, nome de agente ou ticket externo. | O ID é o da conversa e a duração não é substituída pela criação. |
