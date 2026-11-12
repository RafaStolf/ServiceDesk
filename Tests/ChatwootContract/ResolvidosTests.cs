using System.Net;
using System.Text.Json;
using ServiceDesk.Models;
using ServiceDesk.Services;

internal static class ResolvidosTests
{
    public static async Task ExecutarAsync()
    {
        using var chatwoot = CriarServico();
        var meus = await chatwoot.BuscarResolvidosAsync(somenteMeus: true, pagina: 1, CancellationToken.None);
        Verificar(meus.Atendimentos.Select(item => item.Id).SequenceEqual([1L, 2L, 3L, 7L]),
            "Meus resolvidos inclui atribuição atual, transferência e resolução própria");
        Verificar(meus.HistoricosIndisponiveis == 1 && meus.TemMais,
            "histórico inacessível mantém os demais resultados e sinaliza consulta parcial");

        using var caixa = CriarServico(caixaCompleta: true);
        var primeira = await caixa.BuscarResolvidosAsync(somenteMeus: false, pagina: 1, CancellationToken.None);
        Verificar(primeira.Atendimentos.Select(item => item.Id).SequenceEqual([1L, 2L, 3L, 4L, 5L, 7L, 8L]),
            "Caixa de entrada inclui os resolvidos acessíveis sem consultar históricos");
        var segunda = await caixa.BuscarResolvidosAsync(somenteMeus: false, pagina: 2, CancellationToken.None);
        Verificar(segunda.Atendimentos.Single().Id == 9, "paginação continua na caixa configurada");
        var fim = await caixa.BuscarResolvidosAsync(somenteMeus: false, pagina: 3, CancellationToken.None);
        Verificar(fim.Atendimentos.Count == 0 && !fim.TemMais, "página vazia encerra a consulta");

        var ticket = new AtendimentoMonitorado { Id = 19673, Cliente = "João da Silva", Empresa = "Comércio Brasil" };
        Verificar(ticket.CorrespondeBusca(" #19673 ") && !ticket.CorrespondeBusca("#1967"),
            "busca aceita o número exato com #");
        Verificar(ticket.CorrespondeBusca("1967") && ticket.CorrespondeBusca("joao") && ticket.CorrespondeBusca("COMERCIO"),
            "busca aceita número parcial, contato e empresa sem distinção de acentos e maiúsculas");
        Verificar(ticket.CorrespondeBusca(" ") && !ticket.CorrespondeBusca("outro contato"),
            "limpar a busca restaura os resultados");
    }

    private static ChatwootSyncService CriarServico(bool caixaCompleta = false) =>
        new(new("https://chatwoot.invalid", "TOKEN-FICTICIO", 1, 99, 12), new HttpClient(new ApiSimulada(caixaCompleta)));

    private static void Verificar(bool condicao, string descricao)
    {
        if (!condicao) throw new InvalidOperationException(descricao);
        Console.WriteLine("OK: " + descricao);
    }

    private sealed class ApiSimulada(bool caixaCompleta) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Get) throw new InvalidOperationException("Somente GET.");
            var caminho = request.RequestUri!.PathAndQuery;
            var resposta = caminho switch
            {
                "/api/v1/accounts/1/conversations?assignee_type=all&status=resolved&page=1&inbox_id=12" =>
                    Conversas(Enumerable.Range(1, 8)),
                "/api/v1/accounts/1/conversations?assignee_type=all&status=resolved&page=2&inbox_id=12" =>
                    Conversas([9]),
                "/api/v1/accounts/1/conversations?assignee_type=all&status=resolved&page=3&inbox_id=12" =>
                    Conversas([]),
                _ when caixaCompleta => throw new InvalidOperationException("A caixa completa só precisa da listagem."),
                "/api/v1/profile" => Json("""{"id":42,"name":"Ana Lima"}"""),
                "/api/v1/accounts/1/conversations/2/messages" =>
                    Mensagem(200, 2, "Atribuído a Bruno por Ana Lima"),
                "/api/v1/accounts/1/conversations/2/messages?before=200" =>
                    Mensagem(199, 2, "Atribuído a Ana Lima por Bruno"),
                "/api/v1/accounts/1/conversations/3/messages" =>
                    Mensagem(300, 2, "Conversa foi marcada como resolvida por Ana Lima"),
                "/api/v1/accounts/1/conversations/4/messages" =>
                    Mensagem(400, 1, "Conversa foi marcada como resolvida por Ana Lima"),
                "/api/v1/accounts/1/conversations/5/messages" =>
                    Mensagem(500, 2, "Atribuído a Ana Lima Santos por Bruno"),
                "/api/v1/accounts/1/conversations/7/messages" =>
                    Mensagem(700, 2, "Ana Lima self-assigned this conversation"),
                "/api/v1/accounts/1/conversations/8/messages" => new HttpResponseMessage(HttpStatusCode.Forbidden),
                _ when caminho.Contains("/messages?before=") => Json("""{"payload":[]}"""),
                _ => throw new InvalidOperationException($"Consulta inesperada: {caminho}")
            };
            return Task.FromResult(resposta);
        }

        private static HttpResponseMessage Conversas(IEnumerable<int> ids) => Json(JsonSerializer.Serialize(new
        {
            data = new
            {
                payload = ids.Select(id => new
                {
                    id,
                    status = id == 6 ? "open" : "resolved",
                    meta = new { assignee_type = "User", assignee = id == 7 ? null : new { id = id == 1 ? 42 : 99 } }
                })
            }
        }));

        private static HttpResponseMessage Mensagem(int id, int tipo, string texto) => Json(JsonSerializer.Serialize(new
        {
            payload = new[] { new { id, message_type = tipo, content = texto } }
        }));

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
    }
}
