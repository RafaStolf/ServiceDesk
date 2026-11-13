using System.Net.WebSockets;
using System.Text.Json;

namespace ServiceDesk.Services;

internal sealed class ChatwootTempoReal(ConfiguracaoChatwoot configuracao)
{
    internal readonly record struct Alteracao(long? CaixaId, long? ConversaId);

    internal async Task AcompanharAsync(Action<bool> conexao, Action<Alteracao> alteracao, CancellationToken token)
    {
        var espera = 2;
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var chatwoot = new ChatwootSyncService(configuracao);
                var perfil = await chatwoot.ObterPerfilTempoRealAsync(token).ConfigureAwait(false);
                using var socket = new ClientWebSocket();
                socket.Options.AddSubProtocol("actioncable-v1-json");
                var endereco = new Uri(configuracao.BaseUrl.TrimEnd('/') + "/cable");
                if (endereco.Scheme != Uri.UriSchemeHttps && !(endereco.Scheme == Uri.UriSchemeHttp && endereco.IsLoopback))
                    throw new InvalidOperationException("O WebSocket do Chatwoot precisa de HTTPS.");

                socket.Options.SetRequestHeader("Origin", endereco.GetLeftPart(UriPartial.Authority));
                var destino = new UriBuilder(endereco) { Scheme = endereco.Scheme == Uri.UriSchemeHttps ? "wss" : "ws" }.Uri;
                using (var abertura = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    abertura.CancelAfter(TimeSpan.FromSeconds(15));
                    await socket.ConnectAsync(destino, abertura.Token).ConfigureAwait(false);
                    var identificador = JsonSerializer.Serialize(new
                    {
                        channel = "RoomChannel",
                        pubsub_token = perfil.Token,
                        user_id = perfil.UsuarioId,
                        account_id = configuracao.ContaId
                    });
                    var inscricao = JsonSerializer.SerializeToUtf8Bytes(new { command = "subscribe", identifier = identificador });
                    await socket.SendAsync(inscricao.AsMemory(), WebSocketMessageType.Text, true, abertura.Token).ConfigureAwait(false);
                    while (true)
                    {
                        using var quadro = await ReceberAsync(socket, abertura.Token).ConfigureAwait(false);
                        var tipo = MensagemChatwoot.Texto(quadro.RootElement, "type");
                        if (tipo == "confirm_subscription") break;
                        if (tipo is "reject_subscription" or "disconnect")
                            throw new InvalidOperationException("O Chatwoot recusou a inscrição.");
                    }
                }

                espera = 2;
                conexao(true);
                while (!token.IsCancellationRequested)
                {
                    using var leitura = CancellationTokenSource.CreateLinkedTokenSource(token);
                    leitura.CancelAfter(TimeSpan.FromSeconds(30));
                    using var quadro = await ReceberAsync(socket, leitura.Token).ConfigureAwait(false);
                    if (MensagemChatwoot.Texto(quadro.RootElement, "type") == "disconnect") break;
                    if (LerAlteracao(quadro.RootElement, configuracao.ContaId) is { } evento) alteracao(evento);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException or IOException
                or InvalidOperationException or JsonException or FormatException or ArgumentException or OperationCanceledException)
            {
                // A fila continua consultável pelo intervalo de contingência.
            }
            finally { conexao(false); }

            try { await Task.Delay(TimeSpan.FromSeconds(espera), token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            espera = Math.Min(60, espera * 2);
        }
    }

    internal static Alteracao? LerAlteracao(JsonElement quadro, long contaId)
    {
        var mensagem = Objeto(quadro, "message");
        var evento = MensagemChatwoot.Texto(mensagem, "event");
        var dados = Objeto(mensagem, "data");
        if (Numero(dados, "account_id") != contaId) return null;

        return evento switch
        {
            "message.created" or "message.updated" or "first_reply.created" =>
                new Alteracao(Numero(dados, "inbox_id") ?? Numero(Objeto(dados, "conversation"), "inbox_id"),
                    Numero(dados, "conversation_id") ?? Numero(Objeto(dados, "conversation"), "id")),
            "conversation.created" or "conversation.updated" or "conversation.status_changed" or
            "conversation.contact_changed" or "conversation.bot_handoff" or "assignee.changed" or "team.changed" =>
                new Alteracao(Numero(dados, "inbox_id"), Numero(dados, "id")),
            _ => null
        };
    }

    private static async Task<JsonDocument> ReceberAsync(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[8192];
        using var mensagem = new MemoryStream();
        ValueWebSocketReceiveResult parte;
        do
        {
            parte = await socket.ReceiveAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            if (parte.MessageType != WebSocketMessageType.Text || mensagem.Length + parte.Count > 1024 * 1024)
                throw new IOException("O Chatwoot encerrou a transmissão ou enviou um quadro inválido.");
            mensagem.Write(buffer, 0, parte.Count);
        } while (!parte.EndOfMessage);

        return JsonDocument.Parse(mensagem.GetBuffer().AsMemory(0, (int)mensagem.Length));
    }

    private static JsonElement Objeto(JsonElement origem, string nome) =>
        origem.ValueKind == JsonValueKind.Object && origem.TryGetProperty(nome, out var valor) ? valor : default;

    private static long? Numero(JsonElement origem, string nome) =>
        Objeto(origem, nome) is { ValueKind: JsonValueKind.Number } valor && valor.TryGetInt64(out var id) ? id : null;
}
