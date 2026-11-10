using System.Net.Http.Headers;
using System.Text.Json;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

public sealed class ChatwootSyncService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ConfiguracaoChatwoot _configuracao;

    public ChatwootSyncService(ConfiguracaoChatwoot configuracao)
    {
        _configuracao = configuracao;
        _httpClient = new HttpClient { BaseAddress = new Uri(configuracao.BaseUrl + "/"), Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuracao.Token);
        _httpClient.DefaultRequestHeaders.Add("api_access_token", configuracao.Token);
    }

    public async Task<IReadOnlyList<AtendimentoMonitorado>> SincronizarAsync(CancellationToken cancellationToken)
    {
        var usuarioId = _configuracao.UsuarioId ?? await BuscarIdUsuarioAsync(cancellationToken);
        var conversas = (await BuscarConversasAsync("open", cancellationToken))
            .Concat(await BuscarConversasAsync("snoozed", cancellationToken))
            .GroupBy(conversa => conversa.GetProperty("id").GetInt64())
            .Select(grupo => grupo.First())
            .ToList();

        using var limite = new SemaphoreSlim(5);
        var tarefas = conversas.Select(async conversa =>
        {
            await limite.WaitAsync(cancellationToken);
            try { return await ConverterConversaAsync(conversa, usuarioId, cancellationToken); }
            finally { limite.Release(); }
        });

        var resultado = await Task.WhenAll(tarefas);
        return resultado.OrderBy(item => item.ReferenciaInteracao).ToList();
    }

    public async Task<ExportacaoAtendimento> ExportarAtendimentoAsync(AtendimentoMonitorado atendimento, CancellationToken cancellationToken)
    {
        var mensagens = new Dictionary<long, JsonElement>();
        long? before = null;
        while (true)
        {
            var sufixo = before is null ? "" : $"?before={before}";
            using var documento = await ObterJsonAsync($"api/v1/accounts/{_configuracao.ContaId}/conversations/{atendimento.Id}/messages{sufixo}", cancellationToken);
            var pagina = documento.RootElement.GetProperty("payload").EnumerateArray().ToList();
            if (pagina.Count == 0) break;
            var menorId = pagina.Min(item => item.GetProperty("id").GetInt64());
            if (before is not null && menorId >= before.Value)
                throw new InvalidOperationException("Não foi possível obter o histórico completo: a paginação não avançou.");
            foreach (var mensagem in pagina)
                mensagens.TryAdd(mensagem.GetProperty("id").GetInt64(), mensagem.Clone());
            before = menorId;
        }
        var arquivos = new List<AnexoAtendimento>();
        var texto = new System.Text.StringBuilder();
        texto.AppendLine($"ATENDIMENTO #{atendimento.Id}");
        texto.AppendLine($"Cliente: {atendimento.Cliente}");
        texto.AppendLine($"Empresa: {atendimento.Empresa}");
        texto.AppendLine($"Ticket: {atendimento.Ticket}");
        texto.AppendLine($"Situação: {atendimento.SituacaoText}");
        texto.AppendLine($"Abertura: {atendimento.CriadoEm:O}");
        texto.AppendLine($"Exportado em: {DateTimeOffset.Now:O}");
        texto.AppendLine("Histórico público em ordem cronológica. Notas privadas e eventos excluídos. Anexos identificados pelo nome do arquivo no pacote de exportação.");
        texto.AppendLine();
        var publicas = mensagens.Values.Where(EMensagemPublica)
            .OrderBy(item => LerData(item, "created_at") ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.GetProperty("id").GetInt64()).ToList();
        if (publicas.Count == 0) texto.AppendLine("Nenhuma mensagem pública disponível.");
        foreach (var mensagem in publicas)
        {
            var nome = mensagem.TryGetProperty("sender", out var remetente) && remetente.ValueKind == JsonValueKind.Object
                ? LerTexto(remetente, "name") : null;
            var autor = nome ?? (EMensagemDoCliente(mensagem) ? "Cliente" : "Atendente / bot");
            var data = LerData(mensagem, "created_at")?.ToString("O") ?? "Data indisponível";
            texto.AppendLine($"[{data}] {autor}:");
            texto.AppendLine(LerTexto(mensagem, "content") ?? "[Mensagem sem texto]");
            if (mensagem.TryGetProperty("attachments", out var anexos) && anexos.ValueKind == JsonValueKind.Array && anexos.GetArrayLength() > 0)
            {
                foreach (var anexo in anexos.EnumerateArray())
                {
                    var url = LerTexto(anexo, "data_url");
                    var tipo = LerTexto(anexo, "file_type") ?? "arquivo";
                    var indice = arquivos.Count + 1;
                    var extensao = Uri.TryCreate(url, UriKind.Absolute, out var uri)
                        ? Path.GetExtension(uri.AbsolutePath) : "";
                    if (extensao.Length > 10 || extensao.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '.')) extensao = "";
                    var nomeArquivo = $"anexos/{indice:D3}-mensagem-{mensagem.GetProperty("id").GetInt64()}{extensao}";
                    arquivos.Add(new AnexoAtendimento(nomeArquivo, url, tipo));
                    texto.AppendLine($"[Anexo: {nomeArquivo} | tipo: {tipo}]");
                }
            }
            texto.AppendLine();
        }
        return new ExportacaoAtendimento(texto.ToString(), arquivos);
    }

    private async Task<long> BuscarIdUsuarioAsync(CancellationToken cancellationToken)
    {
        using var documento = await ObterJsonAsync("api/v1/profile", cancellationToken);
        return documento.RootElement.GetProperty("id").GetInt64();
    }

    private async Task<List<JsonElement>> BuscarConversasAsync(string status, CancellationToken cancellationToken)
    {
        var conversas = new List<JsonElement>();
        for (var pagina = 1; pagina <= 100; pagina++)
        {
            var inbox = _configuracao.InboxId is { } inboxId ? $"&inbox_id={inboxId}" : string.Empty;
            var caminho = $"api/v1/accounts/{_configuracao.ContaId}/conversations?assignee_type=me&status={status}&page={pagina}{inbox}";
            using var documento = await ObterJsonAsync(caminho, cancellationToken);
            var itens = documento.RootElement.GetProperty("data").GetProperty("payload").EnumerateArray().Select(item => item.Clone()).ToList();
            conversas.AddRange(itens);
            if (itens.Count == 0) break;
        }
        return conversas;
    }

    private async Task<AtendimentoMonitorado> ConverterConversaAsync(JsonElement conversa, long usuarioId, CancellationToken cancellationToken)
    {
        var id = conversa.GetProperty("id").GetInt64();
        var interacoes = await BuscarInteracoesAsync(id, usuarioId, cancellationToken);

        var remetente = conversa.TryGetProperty("meta", out var meta) && meta.TryGetProperty("sender", out var sender)
            ? LerTexto(sender, "name") : null;
        var telefone = conversa.TryGetProperty("meta", out var metadados) && metadados.TryGetProperty("sender", out var contato)
            ? LerTexto(contato, "phone_number") : null;
        var empresa = LerAtributo(conversa, "empresa", "company");
        var ticket = LerAtributo(conversa, "ticket", "ticket_id");
        var assunto = conversa.TryGetProperty("last_non_activity_message", out var ultimaMensagem)
            ? LerTexto(ultimaMensagem, "content") : null;
        var status = LerTexto(conversa, "status") ?? "open";

        return new AtendimentoMonitorado
        {
            Id = id,
            Cliente = string.IsNullOrWhiteSpace(remetente) ? $"Atendimento #{id}" : remetente,
            Telefone = telefone ?? string.Empty,
            Empresa = empresa ?? string.Empty,
            Ticket = ticket ?? string.Empty,
            Assunto = string.IsNullOrWhiteSpace(assunto) ? "Sem assunto disponível" : Resumir(assunto),
            Status = status,
            CriadoEm = LerData(conversa, "created_at") ?? DateTimeOffset.UtcNow,
            UltimaInteracaoMinhaEm = interacoes.UltimaMinha,
            UltimaMensagemClienteEm = interacoes.UltimaCliente,
            UltimaInteracaoChatEm = interacoes.UltimaNoChat ?? LerData(conversa, "last_activity_at") ?? LerData(ultimaMensagem, "created_at"),
            AdiadoAte = LerData(conversa, "snoozed_until")
        };
    }

    private static bool EMensagemPublica(JsonElement mensagem)
    {
        if (mensagem.TryGetProperty("private", out var privada) && privada.ValueKind == JsonValueKind.True) return false;
        if (mensagem.TryGetProperty("message_type", out var tipo) && tipo.ValueKind == JsonValueKind.Number && tipo.GetInt32() == 2) return false;
        return true;
    }

    private static bool EAcaoDoUsuario(JsonElement mensagem, long usuarioId)
    {
        if (!EMensagemPublica(mensagem)) return false;
        if (!string.Equals(LerTexto(mensagem, "sender_type"), "User", StringComparison.OrdinalIgnoreCase)) return false;
        if (mensagem.TryGetProperty("sender_id", out var senderId) && senderId.ValueKind == JsonValueKind.Number && senderId.GetInt64() == usuarioId) return true;
        return mensagem.TryGetProperty("sender", out var sender)
            && sender.TryGetProperty("id", out var id)
            && id.ValueKind == JsonValueKind.Number
            && id.GetInt64() == usuarioId;
    }

    private static bool EMensagemDoCliente(JsonElement mensagem) => EMensagemPublica(mensagem)
        && string.Equals(LerTexto(mensagem, "sender_type"), "Contact", StringComparison.OrdinalIgnoreCase);

    private sealed record InteracoesDaConversa(DateTimeOffset? UltimaMinha, DateTimeOffset? UltimaCliente, DateTimeOffset? UltimaAtividade)
    {
        public DateTimeOffset? UltimaNoChat => UltimaAtividade ?? (UltimaMinha is null && UltimaCliente is null
            ? null
            : new[] { UltimaMinha, UltimaCliente }.Where(data => data is not null).Select(data => data!.Value).Max());
    }

    private async Task<InteracoesDaConversa> BuscarInteracoesAsync(long conversaId, long usuarioId, CancellationToken cancellationToken)
    {
        long? before = null;
        DateTimeOffset? ultimaMinha = null;
        DateTimeOffset? ultimaCliente = null;
        DateTimeOffset? ultimaAtividade = null;
        for (var pagina = 0; pagina < 100; pagina++)
        {
            var sufixo = before is null ? string.Empty : $"?before={before}";
            using var documento = await ObterJsonAsync($"api/v1/accounts/{_configuracao.ContaId}/conversations/{conversaId}/messages{sufixo}", cancellationToken);
            var mensagens = documento.RootElement.GetProperty("payload").EnumerateArray().Select(mensagem => mensagem.Clone()).ToList();
            if (mensagens.Count == 0) break;

            // Inclui mensagens e eventos de sistema (flag, status e outras mudanças do Chatwoot).
            ultimaAtividade ??= mensagens.Where(EMensagemPublica).Select(mensagem => LerData(mensagem, "created_at"))
                .Where(data => data is not null).Select(data => data!.Value).DefaultIfEmpty().Max();
            if (ultimaAtividade == default) ultimaAtividade = null;

            ultimaMinha ??= mensagens.Where(mensagem => EAcaoDoUsuario(mensagem, usuarioId))
                .Select(mensagem => LerData(mensagem, "created_at")).Where(data => data is not null)
                .Select(data => data!.Value).DefaultIfEmpty().Max();
            if (ultimaMinha == default) ultimaMinha = null;

            ultimaCliente ??= mensagens.Where(EMensagemDoCliente)
                .Select(mensagem => LerData(mensagem, "created_at")).Where(data => data is not null)
                .Select(data => data!.Value).DefaultIfEmpty().Max();
            if (ultimaCliente == default) ultimaCliente = null;

            if (ultimaMinha is not null && ultimaCliente is not null) break;

            before = mensagens.Min(mensagem => mensagem.GetProperty("id").GetInt64());
        }
        return new InteracoesDaConversa(ultimaMinha, ultimaCliente, ultimaAtividade);
    }

    private async Task<JsonDocument> ObterJsonAsync(string caminho, CancellationToken cancellationToken)
    {
        using var resposta = await _httpClient.GetAsync(caminho, cancellationToken);
        if (!resposta.IsSuccessStatusCode)
            throw new HttpRequestException($"O Chatwoot retornou {(int)resposta.StatusCode} ({resposta.ReasonPhrase}).");
        await using var stream = await resposta.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static DateTimeOffset? LerData(JsonElement elemento, string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out var valor) || valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (valor.ValueKind == JsonValueKind.Number && valor.TryGetInt64(out var epoch)) return ConverterEpoch(epoch);
        if (valor.ValueKind != JsonValueKind.String) return null;
        if (long.TryParse(valor.GetString(), out var epochTexto)) return ConverterEpoch(epochTexto);
        return DateTimeOffset.TryParse(valor.GetString(), out var data) ? data : null;
    }

    private static DateTimeOffset? ConverterEpoch(long epoch)
    {
        if (epoch <= 0) return null;
        try { return epoch > 99_999_999_999 ? DateTimeOffset.FromUnixTimeMilliseconds(epoch) : DateTimeOffset.FromUnixTimeSeconds(epoch); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static string? LerTexto(JsonElement elemento, string propriedade) =>
        elemento.TryGetProperty(propriedade, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    private static string? LerAtributo(JsonElement conversa, params string[] nomes)
    {
        if (!conversa.TryGetProperty("custom_attributes", out var atributos) || atributos.ValueKind != JsonValueKind.Object) return null;
        foreach (var nome in nomes)
        {
            if (atributos.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.String) return valor.GetString();
        }
        return null;
    }

    private static string Resumir(string texto) => texto.Length <= 120 ? texto : texto[..117] + "...";

    public void Dispose() => _httpClient.Dispose();
}
