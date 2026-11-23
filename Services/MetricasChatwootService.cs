using System.Net;
using System.Text.Json;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

/// <summary>Consulta sob demanda, apenas GET; nunca infere SLA a partir de inatividade.</summary>
public sealed class MetricasChatwootService : IDisposable
{
    private const int MaximoPaginas = 10;
    private const int MaximoTickets = 250;
    private readonly HttpClient _http;
    private readonly ConfiguracaoChatwoot _configuracao;
    public MetricasChatwootService(ConfiguracaoChatwoot configuracao)
        : this(configuracao, new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })) { }
    internal MetricasChatwootService(ConfiguracaoChatwoot configuracao, HttpClient cliente)
    {
        _configuracao = configuracao;
        _http = cliente;
        _http.BaseAddress = new Uri(configuracao.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Add("api_access_token", configuracao.Token);
    }

    public Task<ResultadoMetricas> CarregarAsync(bool historico, DateOnly inicio, DateOnly fim,
        IProgress<string>? progresso, CancellationToken token) =>
        CarregarAsync(historico, inicio, fim, progresso, token, usarCaixaConfigurada: true);

    public Task<ResultadoMetricas> CarregarPeriodoAsync(DateOnly inicio, DateOnly fim,
        IProgress<string>? progresso, CancellationToken token) =>
        CarregarAsync(true, inicio, fim, progresso, token, usarCaixaConfigurada: false);

    private async Task<ResultadoMetricas> CarregarAsync(bool historico, DateOnly inicio, DateOnly fim,
        IProgress<string>? progresso, CancellationToken token, bool usarCaixaConfigurada)
    {
        var periodo = PeriodoMetricas.Limites(inicio, fim);
        token.ThrowIfCancellationRequested();
        progresso?.Report("Identificando o usuário da API…");
        using var perfil = await ObterAsync("api/v1/profile", token);
        var usuarioId = Numero(perfil.RootElement, "id") ?? throw new InvalidOperationException("A API não informou o usuário autenticado; nenhum indicador pessoal foi calculado.");
        var podeConsultarRelatorios = PermissoesChatwoot.Ler(perfil.RootElement, _configuracao.ContaId).PodeConsultarRelatorios;
        var escopo = podeConsultarRelatorios ? "all" : "me";
        var avisos = new HashSet<string>();
        var conversas = new Dictionary<long, JsonElement>();
        var parcial = false;
        var interromperLista = false;
        var limiteDaApi = false;
        var prefixo = $"api/v1/accounts/{_configuracao.ContaId}/conversations";
        var filtrosCaixa = usarCaixaConfigurada && _configuracao.InboxIds is { Count: > 0 } caixas
            ? caixas.Select(id => $"&inbox_id={id}").ToArray()
            : new[] { "" };
        foreach (var estado in historico ? new[] { "all" } : new[] { "open", "snoozed" })
        {
            foreach (var inbox in filtrosCaixa)
            {
                var idsLidos = new HashSet<long>();
                for (var pagina = 1; pagina <= MaximoPaginas; pagina++)
                {
                    progresso?.Report($"Lendo tickets · {conversas.Count} encontrados…");
                    JsonDocument documento;
                    try { documento = await ObterAsync($"{prefixo}?assignee_type={escopo}&status={estado}&page={pagina}{inbox}", token); }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) { throw; }
                    catch (Exception ex) when (conversas.Count > 0 && (ex is HttpRequestException || ex is OperationCanceledException && !token.IsCancellationRequested))
                    {
                        parcial = interromperLista = true;
                        limiteDaApi = ex is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests };
                        avisos.Add(ex is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests }
                            ? "O Chatwoot limitou as consultas (429). Lista parcial; aguarde antes de atualizar."
                            : "A consulta dos tickets foi interrompida. Os resultados são parciais; tente atualizar mais tarde.");
                        break;
                    }
                    using (documento)
                    {
                        var itens = documento.RootElement.GetProperty("data").GetProperty("payload");
                        if (itens.GetArrayLength() == 0) break;
                        var novos = 0;
                        foreach (var item in itens.EnumerateArray())
                        {
                            var id = Numero(item, "id");
                            if (id is not null && idsLidos.Add(id.Value)) novos++;
                            // Confere atribuição também no payload: o endpoint pode mudar durante a paginação.
                            if (id is null || conversas.ContainsKey(id.Value)) continue;
                            if (!podeConsultarRelatorios && Numero(Objeto(Objeto(item, "meta"), "assignee"), "id") != usuarioId) continue;
                            conversas.Add(id.Value, item.Clone());
                            if (interromperLista || conversas.Count >= MaximoTickets) break;
                        }
                        if (novos == 0 || pagina == MaximoPaginas || conversas.Count >= MaximoTickets)
                        {
                            parcial = true;
                            avisos.Add($"Consulta limitada a {MaximoTickets} tickets e {MaximoPaginas} páginas por situação. Paginação repetida ou limite atingido; o histórico pode estar incompleto.");
                            break;
                        }
                    }
                }
                if (interromperLista || conversas.Count >= MaximoTickets) break;
            }
            if (interromperLista || conversas.Count >= MaximoTickets) break;
        }
        var resultado = new List<TicketMetricas>();
        string? indisponibilidadeEventos = limiteDaApi ? "Eventos não consultados: o Chatwoot limitou a consulta de tickets (429). Aguarde antes de atualizar." : null;
        var falhasEventosConsecutivas = 0;
        foreach (var (id, conversa) in conversas)
        {
            token.ThrowIfCancellationRequested();
            progresso?.Report($"Lendo métricas · {resultado.Count + 1} de {conversas.Count} tickets…");
            var eventos = new List<EventoMetrica>();
            var aviso = indisponibilidadeEventos;
            if (indisponibilidadeEventos is null)
            {
                try
                {
                    using var documento = await ObterAsync($"{prefixo}/{id}/reporting_events", token);
                    var raiz = documento.RootElement;
                    if (raiz.ValueKind != JsonValueKind.Array) throw new JsonException("Formato de eventos desconhecido.");
                    falhasEventosConsecutivas = 0;
                    foreach (var item in raiz.EnumerateArray())
                    {
                        var data = Data(item, "created_at");
                        if ((!podeConsultarRelatorios && Numero(item, "user_id") != usuarioId) || (historico && (data is null || data < periodo.Inicio || data >= periodo.FimExclusivo))) continue;
                        var nome = Texto(item, "name");
                        if (nome is not ("first_response" or "reply_time" or "conversation_resolved")) continue;
                        eventos.Add(new(nome, Duracao(item,"value"), Duracao(item,"value_in_business_hours"), data));
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.TooManyRequests)
                {
                    aviso = ex.StatusCode switch
                    {
                        HttpStatusCode.Forbidden => "A API não permitiu consultar eventos de métricas com esta chave (403).",
                        HttpStatusCode.NotFound => "Não foi possível obter eventos deste ticket (404); o recurso ou o ticket pode não estar disponível.",
                        _ => "O Chatwoot limitou as consultas (429). Aguarde antes de atualizar."
                    };
                    avisos.Add(aviso); parcial = true;
                    falhasEventosConsecutivas++;
                    if (ex.StatusCode == HttpStatusCode.TooManyRequests || falhasEventosConsecutivas >= 3)
                    {
                        indisponibilidadeEventos = "Consultas de eventos interrompidas após limite da API ou falhas consecutivas. Os tickets restantes não tiveram os eventos consultados.";
                        avisos.Add(indisponibilidadeEventos);
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) { throw; }
                catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !token.IsCancellationRequested)
                { falhasEventosConsecutivas = 0; aviso = "Não foi possível obter os eventos deste ticket."; avisos.Add("Alguns tickets estão sem eventos; as médias incluem somente registros disponíveis."); parcial = true; }
            }
            var contato = Objeto(Objeto(conversa, "meta"), "sender");
            var empresa = Texto(Objeto(contato, "additional_attributes"), "company_name")
                ?? Texto(Objeto(contato, "custom_attributes"), "empresa") ?? Texto(Objeto(contato, "custom_attributes"), "company")
                ?? Texto(Objeto(conversa, "custom_attributes"), "empresa") ?? Texto(Objeto(conversa, "custom_attributes"), "company") ?? "Empresa não informada";
            var sla = Objeto(conversa, "applied_sla");
            var politica = LerPolitica(sla);
            var ocorrencias = new List<OcorrenciaSla>();
            if (conversa.TryGetProperty("sla_events", out var slaEventos) && slaEventos.ValueKind == JsonValueKind.Array)
                foreach (var ocorrencia in slaEventos.EnumerateArray())
                    ocorrencias.Add(new(Texto(ocorrencia, "event_type") ?? "desconhecido", Data(ocorrencia, "created_at")));
            // Na visão histórica, um ticket entra quando há evento no período ou alteração do SLA no período.
            bool DentroDoPeriodo(DateTimeOffset? data) => data >= periodo.Inicio && data < periodo.FimExclusivo;
            var slaAplicadoNoPeriodo = DentroDoPeriodo(Data(sla, "created_at"));
            var slaNoPeriodo = usarCaixaConfigurada || politica is null || slaAplicadoNoPeriodo;
            var periodoConfirmado = !historico || eventos.Count > 0 || slaAplicadoNoPeriodo
                || ocorrencias.Any(x => DentroDoPeriodo(x.RegistradaEm)) || DentroDoPeriodo(Data(sla, "updated_at"));
            if (periodoConfirmado || aviso is not null)
                resultado.Add(new(id, Texto(contato, "name") ?? $"Ticket #{id}", empresa, Texto(conversa,"status") ?? "", eventos, politica, ocorrencias, aviso, periodoConfirmado, slaNoPeriodo));
        }
        if (resultado.Any(x => x.Sla is null)) avisos.Add("Tickets sem política de SLA retornada não são classificados como SLA cumprido. A disponibilidade depende da instalação e da política aplicada.");
        if (resultado.Any(x => !x.PeriodoConfirmado)) avisos.Add("Alguns tickets estão listados apenas para indicar falha de consulta: sua presença no período não pôde ser confirmada e eles não entram no resumo de SLA.");
        if (historico)
        {
            if (!podeConsultarRelatorios) avisos.Add("O Chatwoot limita este acesso aos eventos pessoais. Tickets transferidos podem ficar de fora.");
            if (!usarCaixaConfigurada && resultado.Any(x => x.PeriodoConfirmado && x.Sla is not null && !x.SlaNoPeriodo))
                avisos.Add("O gráfico inclui políticas aplicadas no período. Políticas aplicadas em outra data, ou sem data informada, continuam nos detalhes.");
        }
        return new(resultado, avisos.ToArray(), parcial, DateTimeOffset.UtcNow, podeConsultarRelatorios);
    }

    private async Task<JsonDocument> ObterAsync(string caminho, CancellationToken token)
    {
        using var resposta = await _http.GetAsync(caminho, token);
        if (!resposta.IsSuccessStatusCode)
            throw new HttpRequestException($"Não foi possível ler métricas do Chatwoot (HTTP {(int)resposta.StatusCode}).", null, resposta.StatusCode);
        await using var stream = await resposta.Content.ReadAsStreamAsync(token);
        return await JsonDocument.ParseAsync(stream, cancellationToken: token);
    }
    private static PoliticaSla? LerPolitica(JsonElement sla) => sla.ValueKind != JsonValueKind.Object ? null : new(
        Texto(sla,"sla_name") ?? "Política sem nome", Texto(sla,"sla_status") ?? "", Duracao(sla,"sla_first_response_time_threshold"),
        Duracao(sla,"sla_next_response_time_threshold"), Duracao(sla,"sla_resolution_time_threshold"),
        sla.TryGetProperty("sla_only_during_business_hours",out var util) && util.ValueKind is JsonValueKind.True or JsonValueKind.False ? util.GetBoolean() : null,
        Data(sla,"sla_frt_due_at"), Data(sla,"sla_nrt_due_at"), Data(sla,"sla_rt_due_at"), Data(sla,"sla_completed_at"));
    private static JsonElement Objeto(JsonElement objeto,string nome) => objeto.ValueKind == JsonValueKind.Object && objeto.TryGetProperty(nome,out var valor) ? valor : default;
    private static string? Texto(JsonElement objeto,string nome) => Objeto(objeto,nome) is { ValueKind: JsonValueKind.String } valor && !string.IsNullOrWhiteSpace(valor.GetString()) ? valor.GetString()!.Trim() : null;
    private static long? Numero(JsonElement objeto,string nome) => Objeto(objeto,nome) is { ValueKind: JsonValueKind.Number } valor && valor.TryGetInt64(out var numero) ? numero : null;
    private static double? Duracao(JsonElement objeto,string nome) => Objeto(objeto,nome) is { ValueKind: JsonValueKind.Number } valor && valor.TryGetDouble(out var numero) && double.IsFinite(numero) && numero >= 0 ? numero : null;
    private static DateTimeOffset? Data(JsonElement objeto,string nome)
    {
        var campo = Objeto(objeto,nome);
        if (campo.ValueKind == JsonValueKind.Number && campo.TryGetInt64(out var epoch) && epoch > 0)
        { try { return DateTimeOffset.FromUnixTimeSeconds(epoch); } catch (ArgumentOutOfRangeException) { return null; } }
        return campo.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(campo.GetString(), out var data) ? data : null;
    }
    public void Dispose() => _http.Dispose();
}
