using System.Net;
using System.Text;
using System.Text.Json;
using ServiceDesk.Models;
using ServiceDesk.Services;

static void Verificar(bool condicao, string descricao)
{
    if (!condicao) throw new InvalidOperationException(descricao);
    Console.WriteLine("OK: " + descricao);
}
static JsonElement Mensagem(string json) => JsonDocument.Parse(json).RootElement.Clone();
var agente = Mensagem("""{"id":1,"created_at":1794340000,"message_type":1,"private":false,"sender":{"id":42,"type":"user"},"content":"Resposta pública"}""");
var contato = Mensagem("""{"id":2,"created_at":1794340010,"message_type":0,"private":false,"sender":{"id":20,"type":"contact"},"content":"Mensagem do contato"}""");
var bot = Mensagem("""{"id":3,"created_at":1794340020,"message_type":1,"private":false,"sender":{"id":42,"type":"agent_bot"},"content":"Resposta do bot"}""");
var nota = Mensagem("""{"id":4,"created_at":1794340030,"message_type":1,"private":true,"sender":{"id":42,"type":"user"},"content":"NOTA-PRIVADA-EXCLUIR"}""");
var atividade = Mensagem("""{"id":5,"created_at":1794340040,"message_type":2,"private":false,"content":"EVENTO-EXCLUIR"}""");
var template = Mensagem("""{"id":6,"created_at":1794340050,"message_type":3,"private":false,"content":"Modelo público"}""");
Verificar(MensagemChatwoot.DoAgente(agente, 42), "sender.type=user e sender.id identificam o agente");
Verificar(!MensagemChatwoot.DoAgente(agente, 99), "mensagem de outro agente não é resposta própria");
Verificar(MensagemChatwoot.DoContato(contato), "incoming e sender.type=contact identificam mensagem recebida");
Verificar(!MensagemChatwoot.DoAgente(bot, 42), "bot com mesmo ID numérico não é confundido com agente");
Verificar(!MensagemChatwoot.Publica(nota) && !MensagemChatwoot.Publica(atividade), "notas privadas e atividades não são mensagens públicas");
Verificar(MensagemChatwoot.Publica(template) && !MensagemChatwoot.DoAgente(template, 42), "template público sem remetente não é atribuído ao agente");
Verificar(!MensagemChatwoot.Publica(default) && !MensagemChatwoot.Publica(Mensagem("null")), "mensagens ausentes e nulas são aceitas sem exceção");
Verificar(!MensagemChatwoot.Publica(Mensagem("""{"message_type":99}""")), "tipo desconhecido não é presumido público");
Verificar(MensagemChatwoot.DoAgente(Mensagem("""{"message_type":1,"sender_type":"User","sender_id":42}"""),42), "campos de eventos legados continuam compatíveis");

var notasCC = new[]
{
    Mensagem("""{"id":7,"created_at":1794340060,"message_type":1,"is.private":true,"sender":{"id":42,"type":"user"},"content":"PRIVADA-LITERAL-EXCLUIR"}"""),
    Mensagem("""{"id":8,"created_at":1794340070,"message_type":1,"private":false,"is":{"private":true},"sender":{"id":42,"type":"user"},"content":"PRIVADA-ANINHADA-EXCLUIR"}""")
};
foreach (var privadaCC in notasCC)
    Verificar(MensagemChatwoot.Privada(privadaCC) && !MensagemChatwoot.Publica(privadaCC)
        && !MensagemChatwoot.DoAgente(privadaCC,42), "is.private exclui mensagem privada mesmo com private=false");
Verificar(MensagemChatwoot.Publica(Mensagem("""{"message_type":1,"is":{"private":false}}""")), "is.private=false permite mensagem para o cliente");

var mensagens = new[] { agente, contato, bot, nota, atividade, template }.Concat(notasCC).ToArray();
using (var sync = new ChatwootSyncService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, 42, 1), new HttpClient(new ApiSimulada(mensagens, false))))
{
    var conversas = await sync.SincronizarAsync(CancellationToken.None);
    var conversa = conversas.Single();
    Verificar(conversa.UltimaInteracaoAgenteEm == DateTimeOffset.FromUnixTimeSeconds(1794340000), "sincronização usa a última mensagem pública do agente");
    Verificar(conversa.UltimaMensagemClienteEm == DateTimeOffset.FromUnixTimeSeconds(1794340010), "sincronização usa a última mensagem do contato");
    Verificar(conversa.UltimaMensagemPublicaEm == DateTimeOffset.FromUnixTimeSeconds(1794340050), "relógio de mensagem pública não é confundido com resposta humana");
    var exportacao = await sync.ExportarAtendimentoAsync(conversa, CancellationToken.None);
    Verificar(exportacao.Texto.Contains("Resposta pública") && exportacao.Texto.Contains("Mensagem do contato")
        && !exportacao.Texto.Contains("NOTA-PRIVADA-EXCLUIR") && !exportacao.Texto.Contains("EVENTO-EXCLUIR")
        && !exportacao.Texto.Contains("PRIVADA-LITERAL-EXCLUIR") && !exportacao.Texto.Contains("PRIVADA-ANINHADA-EXCLUIR"), "exportação mantém mensagens públicas e exclui notas/eventos");
}
using (var sync = new ChatwootSyncService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, 42, 1), new HttpClient(new ApiSimulada([], true))))
{
    var conversa = (await sync.SincronizarAsync(CancellationToken.None)).Single();
    Verificar(conversa.UltimaMensagemPublicaEm is null && conversa.UltimaInteracaoAgenteEm is null, "conversa vazia com last_non_activity_message=null sincroniza");
    Verificar(conversa.ReferenciaInteracao == DateTimeOffset.MaxValue, "criação e last_activity_at não substituem última interação desconhecida");
    conversa.AtualizarTempo(DateTimeOffset.UtcNow);
    Verificar(conversa.TempoUltimaInteracaoAgente == "Última interação não identificada", "card não inventa duração para interação desconhecida");
}
using (var sync = new ChatwootSyncService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, 42, 1), new HttpClient(new ApiSimulada(mensagens, false, true))))
{
    var conversa = (await sync.SincronizarAsync(CancellationToken.None)).Single();
    Verificar(conversa.UltimaInteracaoAgenteEm == DateTimeOffset.FromUnixTimeSeconds(1794340000)
        && conversa.UltimaMensagemClienteEm == DateTimeOffset.FromUnixTimeSeconds(1794340010), "página recente só com notas/eventos não impede buscar respostas na página anterior");
}
using (var sync = new ChatwootSyncService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, 999, 1), new HttpClient(new ApiSimulada(mensagens, false))))
{
    var conversa = (await sync.SincronizarAsync(CancellationToken.None)).Single();
    Verificar(conversa.AgenteAtribuidoId == 42 && conversa.UltimaInteracaoAgenteEm == DateTimeOffset.FromUnixTimeSeconds(1794340000), "agente atribuído no Chatwoot prevalece sobre ID legado configurado");
    Verificar(conversa.Empresa == "Empresa de teste", "empresa do contato é usada quando ausente na conversa");
    conversa.AtualizarTempo(DateTimeOffset.FromUnixTimeSeconds(1794340300));
    Verificar(conversa.TempoUltimaInteracaoAgente == "há 5min", "card calcula o tempo da interação do agente atribuído");
}
using (var sync = new ChatwootSyncService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, 42, 1), new HttpClient(new ApiSimulada(mensagens, false, agenteId: null))))
{
    var conversa = (await sync.SincronizarAsync(CancellationToken.None)).Single();
    Verificar(conversa.UltimaInteracaoAgenteEm is null, "ausência do agente atribuído não é substituída pelo usuário da configuração");
}
// Regressão: conversa recebida ainda sem resposta do agente (ticket 19680).
{
    var agora = DateTimeOffset.UtcNow;
    var recebida = new AtendimentoMonitorado { UltimaMensagemClienteEm = agora.AddMinutes(-8) };
    recebida.AtualizarTempo(agora);
    Verificar(recebida.TempoUltimaInteracao == "Cliente: há 8min", "card identifica mensagem recebida sem resposta");
    Verificar(recebida.UltimaInteracaoAgenteEm is null, "mensagem do cliente não vira resposta do agente");
    recebida.UltimaInteracaoAgenteEm = agora.AddMinutes(-2);
    recebida.AtualizarTempo(agora);
    Verificar(recebida.TempoUltimaInteracao == "Agente: há 2min", "resposta mais recente do agente prevalece");
}
foreach (var email in new string?[] { "  contato@example.com  ", null, "", "   " })
{
    using var sync = new ChatwootSyncService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, 42, 1), new HttpClient(new ApiSimulada(mensagens, false, emailContato: email)));
    var conversa = (await sync.SincronizarAsync(CancellationToken.None)).Single();
    var informado = !string.IsNullOrWhiteSpace(email);
    Verificar(conversa.Email == (email?.Trim() ?? "") && conversa.TemEmail == informado,
        "e-mail vem do contato e normaliza vazio, nulo e espaços");
    Verificar(conversa.EmailText == (informado ? "contato@example.com" : "E-mail não informado"),
        "campo exibe endereço cadastrado ou ausência explícita");
}
foreach (var (padrao, esperado) in new[] { ("Empresa oficial", "Empresa oficial"), ("", "Empresa de teste"), ("   ", "Empresa de teste") })
{
    using var sync = new ChatwootSyncService(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, 42, 1), new HttpClient(new ApiSimulada(mensagens, false, empresaPadrao: padrao)));
    var conversa = (await sync.SincronizarAsync(CancellationToken.None)).Single();
    Verificar(conversa.Empresa == esperado, "campo padrão additional_attributes.company_name prevalece; vazio mantém alternativa personalizada");
}
var preferenciasDir = Path.Combine(Path.GetTempPath(), "ServiceDesk-Interface-Test-" + Guid.NewGuid().ToString("N"));
try
{
    var servico = new PreferenciasInterfaceService(preferenciasDir);
    Verificar(servico.Carregar() == new PreferenciasInterface(), "instalação sem preferências usa padrão");
    var escolhidas = new PreferenciasInterface { Tema = "Escuro", Cor = "Verde", NotificacoesSistema = false, SomNotificacoes = false };
    servico.Salvar(escolhidas);
    Verificar(new PreferenciasInterfaceService(preferenciasDir).Carregar() == escolhidas, "tema, cor e notificações persistem ao reabrir");
}
finally { if (Directory.Exists(preferenciasDir)) Directory.Delete(preferenciasDir, true); }
var aberta = new AtendimentoMonitorado { Status = "open", AdiadoAte = DateTimeOffset.UtcNow.AddDays(1) };
var adiada = new AtendimentoMonitorado { Status = "snoozed", AdiadoAte = null };
Verificar(!aberta.EstaAdiado && aberta.SituacaoText == "Aberta", "status oficial prevalece sobre data residual de adiamento");
Verificar(adiada.EstaAdiado && adiada.SituacaoText == "Adiada até a próxima resposta", "snoozed sem data representa adiamento até resposta");
await MetricasTests.ExecutarAsync();
Console.WriteLine("Contrato Chatwoot validado com dados sintéticos; nenhum servidor real foi consultado.");

sealed class ApiSimulada(JsonElement[] mensagens, bool vazia, bool paginar = false, long? agenteId = 42, string? empresaPadrao = null, string? emailContato = null) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get) throw new InvalidOperationException("Somente GET é permitido no teste.");
        var caminho = request.RequestUri!.PathAndQuery;
        string json;
        if (caminho.Contains("/messages"))
        {
            var pagina = paginar
                ? mensagens.Where(x => caminho.Contains("before=") ? x.GetProperty("id").GetInt32() <= 3 : x.GetProperty("id").GetInt32() is 4 or 5).ToArray()
                : caminho.Contains("before=") ? [] : mensagens;
            json = JsonSerializer.Serialize(new { payload = pagina });
        }
        else if (caminho.Contains("status=open") && caminho.Contains("page=1&"))
        {
            object conversa = new { id = 19622, meta = new { sender = new { name = "Contato de teste", email = emailContato, additional_attributes = new { company_name = empresaPadrao }, custom_attributes = new { empresa = "Empresa de teste" } }, assignee_type = "User", assignee = agenteId is { } idAgente ? (object)new { id = idAgente } : null }, status = "open", created_at = 1794330000,
                last_activity_at = 1794399999, last_non_activity_message = vazia ? (object?)null : mensagens.First(x => x.GetProperty("id").GetInt32() == 4) };
            json = JsonSerializer.Serialize(new { data = new { payload = new[] { conversa } } });
        }
        else json = """{"data":{"payload":[]}}""";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
