# PRD — ServiceDesk

**Status:** ativo · **Versão:** 1.0 · **Produto:** aplicação local Windows para um analista N1.

## Contexto e objetivo

O analista trabalha a fila atribuída a ele no Chatwoot e precisa priorizar conversas sem mensagem pública. A aplicação deve apresentar, sem executar operações no Chatwoot, o tempo de chat aberto, o tempo desde a última mensagem pública e se a conversa está adiada.

O sucesso é o analista conseguir, ao abrir o ServiceDesk, identificar a fila aberta e os adiados, pesquisar uma conversa e abrir a conversa correspondente no Chatwoot. A atualização é manual ou automática em intervalo configurável.

## Escopo confirmado

| ID | Requisito | Aceite |
|---|---|---|
| RF-01 | Consultar em modo de leitura as conversas abertas e adiadas atribuídas ao usuário da chave API. | A lista não altera conversa alguma. |
| RF-02 | Considerar somente mensagens públicas para o tempo sem interação. | Notas privadas e eventos do sistema não reiniciam o contador. |
| RF-03 | Mostrar se está adiada e separar Em aberto, Adiados e Todos. | Um adiado não aparece em Em aberto e é encontrado nas demais visões. |
| RF-04 | Permitir busca por telefone, empresa, cliente, ticket, assunto ou ID. | O termo restringe a lista sem gravação no Chatwoot. |
| RF-05 | Configurar atualização automática de 1 a 120 minutos e alertas locais por tempo sem interação no chat. | As escolhas persistem após reabrir e cada conversa alerta uma vez até receber nova interação. |

## Fora de escopo

- Enviar mensagens, criar notas, mudar status, adiar ou atribuir conversas pelo ServiceDesk.
- Login próprio, múltiplos analistas e automação de decisões do fluxo N1.

## Questões abertas

| Decisão pendente | Impacto |
|---|---|
| Critérios visuais de prioridade além do tempo sem interação. | Define cores, ordenação secundária e possíveis notificações futuras. |
| Forma de apresentação e edição das pendências locais. | Define a próxima tela e uso das tabelas existentes. |
