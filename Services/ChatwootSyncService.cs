using System.Net.Http.Headers;
using System.Text.Json;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

public sealed class ChatwootSyncService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ConfiguracaoChatwoot _configuracao;

    public ChatwootSyncService(ConfiguracaoChatwoot configuracao) : this(configuracao, new HttpClient()) { }

    internal ChatwootSyncService(ConfiguracaoChatwoot configuracao, HttpClient cliente)
    {
        _configuracao = configuracao;
        _httpClient = cliente;
        _httpClient.BaseAddress = new Uri(configuracao.BaseUrl + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuracao.Token);
        _httpClient.DefaultRequestHeaders.Add("api_access_token", configuracao.Token);
    }

    public async Task<IReadOnlyList<AtendimentoMonitorado>> SincronizarAsync(CancellationToken cancellationToken)
    {
        var conversas = (await BuscarConversasAsync("open", cancellationToken))
            .Concat(await BuscarConversasAsync("snoozed", cancellationToken))
            .GroupBy(conversa => conversa.GetProperty("id").GetInt64())
            .Select(grupo => grupo.First())
            .ToList();

        using var limite = new SemaphoreSlim(5);
        var tarefas = conversas.Select(async conversa =>
        {
            await limite.WaitAsync(cancellationToken);
            try { return await ConverterConversaAsync(conversa, cancellationToken); }
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
        texto.AppendLine($"# Ticket #{atendimento.Id}");
        texto.AppendLine();
        texto.AppendLine($"Contato: {atendimento.Cliente}");
        texto.AppendLine($"Empresa: {atendimento.Empresa}");
        texto.AppendLine($"Situação: {atendimento.SituacaoText}");
        texto.AppendLine($"Criação: {atendimento.CriadoEm:O}");
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
            var autor = nome ?? (EMensagemDoContato(mensagem) ? "Contato" : "Agente / bot");
            var data = LerData(mensagem, "created_at")?.ToString("O") ?? "Data indisponível";
            texto.AppendLine($"## {data} — {autor}");
            texto.AppendLine();
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
                    var nomeArquivo = $"{indice:D3}-mensagem-{mensagem.GetProperty("id").GetInt64()}{extensao}";
                    arquivos.Add(new AnexoAtendimento(nomeArquivo, url, tipo));
                    var descricao = tipo switch { "image" => "uma imagem", "video" => "um vídeo", "audio" => "um áudio", _ => "um arquivo" };
                    texto.AppendLine($"Foi enviado {descricao}. Arquivo: [{nomeArquivo}]({nomeArquivo})");
                }
            }
            texto.AppendLine();
        }
        return new ExportacaoAtendimento(texto.ToString(), arquivos);
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

    private async Task<AtendimentoMonitorado> ConverterConversaAsync(JsonElement conversa, CancellationToken cancellationToken)
    {
        var id = conversa.GetProperty("id").GetInt64();
        var agenteId = LerAgenteAtribuido(conversa);
        var interacoes = await BuscarInteracoesAsync(id, agenteId ?? long.MinValue, cancellationToken);

        var remetente = conversa.TryGetProperty("meta", out var meta) && meta.TryGetProperty("sender", out var sender)
            ? LerTexto(sender, "name") : null;
        var telefone = conversa.TryGetProperty("meta", out var metadados) && metadados.TryGetProperty("sender", out var contato)
            ? LerTexto(contato, "phone_number") : null;
        string? empresa = null;
        string? email = null;
        if (conversa.TryGetProperty("meta", out var contexto) && contexto.ValueKind == JsonValueKind.Object
            && contexto.TryGetProperty("sender", out var contatoDados) && contatoDados.ValueKind == JsonValueKind.Object)
        {
            email = LerTexto(contatoDados, "email");
            if (contatoDados.TryGetProperty("additional_attributes", out var adicionais) && adicionais.ValueKind == JsonValueKind.Object)
                empresa = LerTexto(adicionais, "company_name");
            if (string.IsNullOrWhiteSpace(empresa)) empresa = LerAtributo(contatoDados, "empresa", "company");
        }
        if (string.IsNullOrWhiteSpace(empresa)) empresa = LerAtributo(conversa, "empresa", "company");
        var status = LerTexto(conversa, "status") ?? "open";

        return new AtendimentoMonitorado
        {
            Id = id,
            Cliente = string.IsNullOrWhiteSpace(remetente) ? $"Ticket #{id}" : remetente,
            Telefone = telefone ?? string.Empty,
            Email = email?.Trim() ?? string.Empty,
            Empresa = empresa ?? string.Empty,
            Status = status,
            CriadoEm = LerData(conversa, "created_at") ?? DateTimeOffset.UtcNow,
            AgenteAtribuidoId = agenteId,
            UltimaInteracaoAgenteEm = interacoes.UltimaMinha,
            UltimaMensagemClienteEm = interacoes.UltimaCliente,
            UltimaMensagemPublicaEm = interacoes.UltimaNoChat,
            AdiadoAte = LerData(conversa, "snoozed_until")
        };
    }

    private static long? LerAgenteAtribuido(JsonElement conversa)
    {
        if (!conversa.TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object
            || !meta.TryGetProperty("assignee", out var agente) || agente.ValueKind != JsonValueKind.Object) return null;
        var tipo = LerTexto(meta, "assignee_type") ?? LerTexto(agente, "type");
        if (tipo is not null && !string.Equals(tipo, "user", StringComparison.OrdinalIgnoreCase)) return null;
        return agente.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var valor) && valor > 0 ? valor : null;
    }

    private static bool EMensagemPublica(JsonElement mensagem) => MensagemChatwoot.Publica(mensagem);

    private static bool EMensagemDoAgente(JsonElement mensagem, long usuarioId) => MensagemChatwoot.DoAgente(mensagem, usuarioId);

    private static bool EMensagemDoContato(JsonElement mensagem) => MensagemChatwoot.DoContato(mensagem);

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

            // Atividade pública inclui mensagens de contatos, agentes e bots; exclui notas privadas e eventos.
            ultimaAtividade ??= mensagens.Where(EMensagemPublica).Select(mensagem => LerData(mensagem, "created_at"))
                .Max();

            ultimaMinha ??= mensagens.Where(mensagem => EMensagemDoAgente(mensagem, usuarioId))
                .Select(mensagem => LerData(mensagem, "created_at")).Max();

            ultimaCliente ??= mensagens.Where(EMensagemDoContato)
                .Select(mensagem => LerData(mensagem, "created_at")).Max();

            if (ultimaMinha is not null && ultimaCliente is not null) break;

            var menorId = mensagens.Min(mensagem => mensagem.GetProperty("id").GetInt64());
            if (before is not null && menorId >= before.Value)
                throw new InvalidOperationException("Não foi possível obter as mensagens: a paginação não avançou.");
            before = menorId;
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
        if (elemento.ValueKind != JsonValueKind.Object || !elemento.TryGetProperty(propriedade, out var valor) || valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
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
        MensagemChatwoot.Texto(elemento, propriedade);

    private static string? LerAtributo(JsonElement conversa, params string[] nomes)
    {
        if (conversa.ValueKind != JsonValueKind.Object || !conversa.TryGetProperty("custom_attributes", out var atributos) || atributos.ValueKind != JsonValueKind.Object) return null;
        foreach (var nome in nomes)
        {
            if (atributos.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(valor.GetString())) return valor.GetString()!.Trim();
        }
        return null;
    }

    public void Dispose() => _httpClient.Dispose();
}
