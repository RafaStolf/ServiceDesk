# ADR-001 — Integração Chatwoot de leitura e configuração local

**Status:** aceito · **Data:** 2026-11-06

## Contexto

O ServiceDesk monitora a fila individual do Chatwoot. O analista pediu a última interação e conversas adiadas, sem automação de mensagens, notas, estados ou adiamentos.

## Opções

1. Permitir comandos de escrita na API Chatwoot.
2. Consultar somente `GET` e manter decisões do fluxo N1 no Chatwoot pelo analista.
3. Armazenar segredos no código ou no pacote.
4. Ler segredos de arquivo local fora do controle de versão.

## Decisão

Adotamos as opções 2 e 4. `MonitoramentoService` orquestra consultas de leitura; a configuração vem de `application.properties` local.

## Consequências

- Reduz risco de mudanças acidentais nas conversas.
- A instalação precisa do arquivo de configuração local.
- Automação de escrita futura exige novo ADR, autorização explícita e auditoria.
