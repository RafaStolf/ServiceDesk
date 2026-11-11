# Métricas e SLA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Disponibilizar fila atual e histórico por período com métricas oficiais, sem modificar o Chatwoot.

**Architecture:** Serviço GET independente da sincronização da fila, com cancelamento e limites de paginação explícitos. Modelos puros calculam agregações dos eventos oficiais; tela WinUI dedicada apresenta resultados e detalhes sem conteúdo de mensagens.

**Tech Stack:** C#/.NET 10, WinUI, System.Text.Json; sem novas dependências.

**Spec:** ../specs/2026-11-11-metricas-sla-design.md

## Global Constraints
- Somente GET; mesma chave, sem elevar permissões.
- Indicadores ausentes nunca equivalem a zero; metas não são inventadas.
- Fuso de Brasília; datas inclusivas na interface e limite final exclusivo na consulta local de eventos.
- Tema e ícone Centrium preservados; nova tela sob demanda, fora do sincronizador.
- Histórico de tickets atualmente atribuídos ao usuário; informar que transferidos podem não aparecer.

## Review Focus
- Falta de permissão/edição: aviso explícito, sem médias fictícias.
- Eventos de outros agentes, bots, reaberturas: filtrar pelo usuário autenticado e distinguir eventos de tickets.
- Paginação repetida ou limitada: interromper e declarar resultado parcial.
- Cancelamento e falhas parciais: preservar fila e impedir atualização tardia após sair da tela.
- Fuso, fronteiras de datas e zero útil válido: testes de agregação e renderização.

### Task 1: Leitura e agregação
**Files:** Create Models/MetricasTicket.cs; Services/MetricasChatwootService.cs; Tests/ChatwootContract/MetricasTests.cs. Modify Tests/ChatwootContract/Program.cs and csproj.
**Interfaces:** CarregarAsync(bool historico, DateOnly inicio, DateOnly fim, IProgress<string> progresso, CancellationToken token) returns ResultadoMetricas; ResumoMetricas.Calcular returns métricas com contagens.
- [ ] Adicionar testes sintéticos de janela de Brasília, duração útil zero, eventos de outro usuário, reabertura, política ausente, 403/404/429, paginação repetida e cancelamento.
- [ ] Implementar leitura de perfil, lista assignee_type=me e reporting_events por ticket, com no máximo 10 páginas por status e 250 tickets por consulta. Interpretar applied_sla/sla_events, durações em segundos e eventos sem texto de mensagens.
- [ ] Executar dotnet run --project Tests/ChatwootContract/ChatwootContract.csproj; todos os cenários passam.

### Task 2: Tela e navegação
**Files:** Create MetricasPage.cs. Modify MainPage.xaml/MainPage.xaml.cs, README.md, docs/PRD.md.
**Interfaces:** MetricasPage(Action voltar, ElementTheme tema, Brush destaque) oferece fila, histórico, datas/atalhos, base de tempo, resumo e detalhes.
- [ ] Adicionar botão Métricas e navegar preservando a instância da fila; cancelar consultas ao sair e parar o sincronizador da fila enquanto a tela estiver aberta.
- [ ] Renderizar os três indicadores com denominadores, SLA separado, avisos de permissões/limites, lista e diálogo de detalhes com metas/vencimentos/eventos. Histórico filtra eventos por created_at do relatório oficial; fila mostra histórico disponível dos tickets atuais.
- [ ] Diagnóstico de inicialização renderiza um resultado sintético na tela de métricas para verificar controles WinUI sem configuração real.
- [ ] Atualizar documentação, validar diff e compilar/testar no workflow Windows antes da integração e publicação.
