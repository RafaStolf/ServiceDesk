using System.Net;
using System.Text;
using System.Text.Json;
using ServiceDesk.Models;
using ServiceDesk.Services;

internal static class MetricasTests
{
    private static void Check(bool valor, string nome)
    {
        if (!valor) throw new InvalidOperationException(nome);
        Console.WriteLine("OK: métricas — " + nome);
    }
    public static async Task ExecutarAsync()
    {
        var (inicio, fim) = PeriodoMetricas.Limites(new(2026, 11, 9), new(2026, 11, 9));
        Check(inicio == DateTimeOffset.Parse("2026-11-09T03:00:00Z") && fim == DateTimeOffset.Parse("2026-11-10T03:00:00Z"), "dia de Brasília usa limites UTC corretos");
        using (var api = new MetricasChatwootService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, 999, 1), new HttpClient(new ApiMetricas())))
        {
            var resultado = await api.CarregarAsync(true, new(2026, 11, 9), new(2026, 11, 9), null, CancellationToken.None);
            Check(resultado.Tickets.Count == 1 && resultado.Tickets[0].Empresa == "Empresa oficial", "histórico inclui resolvido, empresa padrão e filtra eventos por período");
            var ticket = resultado.Tickets.Single();
            Check(ticket.Eventos.Count == 4, "eventos de outro usuário e fora do dia não entram no indicador pessoal");
            var resumo = ResumoMetricas.Calcular(resultado.Tickets, false);
            Check(resumo.PrimeiraResposta.MediaSegundos == 60 && resumo.RespostasSeguintes.MediaSegundos == 120, "usa durações oficiais em segundos");
            Check(resumo.Resolucao.Quantidade == 2 && resumo.Resolucao.MediaSegundos == 900, "reabertura conserva duas resoluções em um ticket");
            Check(ResumoMetricas.Calcular(resultado.Tickets, true).PrimeiraResposta.MediaSegundos == 0, "zero de tempo útil é válido");
            Check(ticket.Sla?.PrimeiraRespostaMeta == 300 && ticket.Sla.Estado == "active_with_misses" && ticket.Ocorrencias.Count == 1, "metas e violações oficiais são preservadas");
            Check(!resultado.Parcial, "consulta completa não é marcada parcial");
        }
        foreach (var status in new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests })
        {
            using var api = new MetricasChatwootService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, null, 1), new HttpClient(new ApiMetricas(status)));
            var resultado = await api.CarregarAsync(false, new(2026, 11, 9), new(2026, 11, 9), null, CancellationToken.None);
            Check(resultado.Parcial && resultado.Avisos.Count > 0 && ResumoMetricas.Calcular(resultado.Tickets, false).PrimeiraResposta.MediaSegundos is null, status + " não vira duração zero");
        }
        using (var api = new MetricasChatwootService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, null, 1), new HttpClient(new ApiMetricas(repetir: true))))
        {
            var resultado = await api.CarregarAsync(true, new(2026,11,9), new(2026,11,9), null, CancellationToken.None);
            Check(resultado.Parcial && resultado.Tickets.Count == 1, "paginação repetida é interrompida sem duplicar tickets");
        }
        using (var api = new MetricasChatwootService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, null, 1), new HttpClient(new ApiMetricas(semSla: true))))
        {
            var resultado = await api.CarregarAsync(false, new(2026,11,9), new(2026,11,9), null, CancellationToken.None);
            Check(resultado.Tickets.Single().Sla is null, "ausência de política não é tratada como SLA cumprido");
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            try { await api.CarregarAsync(false, new(2026,11,9), new(2026,11,9), null, cts.Token); throw new Exception("Cancelamento ignorado"); }
            catch (OperationCanceledException) { Check(true, "cancelamento interrompe a consulta"); }
        }
        using (var api = new MetricasChatwootService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, null, 1), new HttpClient(new ApiMetricas(HttpStatusCode.NotFound, tickets: 2, somentePrimeiro: true))))
        {
            var resultado = await api.CarregarAsync(false, new(2026,11,9), new(2026,11,9), null, CancellationToken.None);
            Check(resultado.Tickets.Single(x => x.Id == 124).Eventos.Count > 0, "404 em um ticket não esconde eventos de outro ticket");
        }
        using (var api = new MetricasChatwootService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, null, 1), new HttpClient(new ApiMetricas(interceptar: path => path.Contains("page=2&") ? new(HttpStatusCode.Unauthorized) : null))))
        {
            try { await api.CarregarAsync(false, new(2026,11,9), new(2026,11,9), null, CancellationToken.None); throw new Exception("401 da paginação foi ignorado"); }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) { Check(true, "401 durante paginação interrompe a consulta"); }
        }
        using (var api = new MetricasChatwootService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, null, 1), new HttpClient(new ApiMetricas(interceptar: path => path.Contains("page=2&") ? throw new TaskCanceledException("Timeout simulado") : null))))
        {
            var resultado = await api.CarregarAsync(false, new(2026,11,9), new(2026,11,9), null, CancellationToken.None);
            Check(resultado.Parcial && resultado.Tickets.Count == 1, "timeout depois de obter dados preserva resultado parcial");
        }
        using (var api = new MetricasChatwootService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, null, 1), new HttpClient(new ApiMetricas(interceptar: path => path.Contains("page=2&") ? new(HttpStatusCode.TooManyRequests) : null))))
        {
            var resultado = await api.CarregarAsync(false, new(2026,11,9), new(2026,11,9), null, CancellationToken.None);
            Check(resultado.Parcial && resultado.Tickets.All(x => x.Eventos.Count == 0), "429 na listagem impede novas consultas de eventos");
        }
        using (var api = new MetricasChatwootService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, null, 1), new HttpClient(new ApiMetricas(tickets: 251))))
        {
            var resultado = await api.CarregarAsync(false, new(2026,11,9), new(2026,11,9), null, CancellationToken.None);
            Check(resultado.Parcial && resultado.Tickets.Count == 250, "limite de 250 tickets é explícito e respeitado");
        }
        using (var api = new MetricasChatwootService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, null, 1), new HttpClient(new ApiMetricas(tickets: 5, interceptar: path =>
            path.Contains("/123/reporting_events") || path.Contains("/126/reporting_events") ? new(HttpStatusCode.Forbidden)
                : path.Contains("/124/reporting_events") ? new(HttpStatusCode.InternalServerError)
                : path.Contains("/125/reporting_events") ? new(HttpStatusCode.NotFound) : null))))
        {
            var resultado = await api.CarregarAsync(false, new(2026,11,9), new(2026,11,9), null, CancellationToken.None);
            Check(resultado.Tickets.Single(x => x.Id == 127).Eventos.Count > 0, "falha genérica interrompe sequência de recusas e permite consultar ticket seguinte");
        }
    }
}

internal sealed class ApiMetricas(HttpStatusCode? falhaEventos = null, bool repetir = false, bool semSla = false, int tickets = 1, bool somentePrimeiro = false, Func<string, HttpResponseMessage?>? interceptar = null) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Method != HttpMethod.Get) throw new Exception("Métricas devem usar somente GET");
        var path = request.RequestUri!.PathAndQuery;
        if (interceptar?.Invoke(path) is { } resposta) return Task.FromResult(resposta);
        if (path.EndsWith("/profile")) return Resposta(new { id = 42 });
        if (path.Contains("/reporting_events"))
        {
            if (falhaEventos is { } falha && (!somentePrimeiro || path.Contains("/123/"))) return Task.FromResult(new HttpResponseMessage(falha));
            object Evento(string nome, double valor, double? util, string data = "2026-11-09T12:00:00Z", int usuario = 42) => new { name = nome, value = valor, value_in_business_hours = util, created_at = data, user_id = usuario };
            return Resposta(new[] { Evento("first_response",60,0), Evento("reply_time",120,null), Evento("conversation_resolved",600,300), Evento("conversation_resolved",1200,600), Evento("reply_time",900,500,usuario:99), Evento("first_response",1000,1000,"2026-11-09T02:59:59Z"), Evento("reply_time",1000,1000,"2026-11-10T03:00:00Z") });
        }
        if (!path.Contains("assignee_type=me") || !path.Contains("inbox_id=1")) throw new Exception("Escopo da consulta não preservado");
        if ((!repetir && !path.Contains("page=1&")) || path.Contains("status=snoozed"))
            return Resposta(new { data = new { payload = Array.Empty<object>() } });
        object sla = new { sla_name = "Política de teste", sla_status = "active_with_misses", sla_first_response_time_threshold = 300, sla_next_response_time_threshold = 600, sla_resolution_time_threshold = 3600, sla_only_during_business_hours = true, sla_frt_due_at = 1794222000 };
        return Resposta(new { data = new { payload = Enumerable.Range(0, tickets).Select(indice => new { id = 123 + indice, status = path.Contains("status=all") ? "resolved" : "open", created_at = 1794221900,
            meta = new { sender = new { name = "Contato de teste", additional_attributes = new { company_name = "Empresa oficial" } }, assignee = new { id = 42 } },
            applied_sla = semSla ? null : sla, sla_events = new[] { new { event_type = "frt", created_at = 1794222500 } } }).ToArray() } });
    }
    private static Task<HttpResponseMessage> Resposta(object valor) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(valor), Encoding.UTF8, "application/json") });
}
