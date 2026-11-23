using System.Net;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

public sealed record ResultadoMonitoramento(IReadOnlyList<AtendimentoMonitorado> Atendimentos,
    IReadOnlyList<AtendimentoMonitorado> NaoAtribuidos, bool PodeVerNaoAtribuidos, string? ErroNaoAtribuidos);

/// <summary>Orquestra somente leituras da fila atribuída no Chatwoot.</summary>
public sealed class MonitoramentoService
{
    public async Task<ResultadoMonitoramento> SincronizarAsync(ConfiguracaoChatwoot configuracao, CancellationToken cancellationToken)
    {
        using var chatwoot = new ChatwootSyncService(configuracao);
        return await SincronizarAsync(chatwoot, cancellationToken);
    }

    internal static async Task<ResultadoMonitoramento> SincronizarAsync(ChatwootSyncService chatwoot, CancellationToken cancellationToken)
    {
        var filaPessoal = chatwoot.SincronizarAsync(cancellationToken);
        var filaNaoAtribuida = ConsultarNaoAtribuidosAsync(chatwoot, cancellationToken);
        await Task.WhenAll(filaPessoal, filaNaoAtribuida);
        var (tickets, visivel, erro) = await filaNaoAtribuida;
        return new(await filaPessoal, tickets, visivel, erro);
    }

    private static async Task<(IReadOnlyList<AtendimentoMonitorado> Tickets, bool Visivel, string? Erro)> ConsultarNaoAtribuidosAsync(
        ChatwootSyncService chatwoot, CancellationToken cancellationToken)
    {
        var visivel = false;
        try
        {
            var permissoes = await chatwoot.ObterPermissoesAsync(cancellationToken);
            if (!permissoes.PodeVerNaoAtribuidos) return ([], false, null);
            visivel = true;
            return (await chatwoot.SincronizarNaoAtribuidosAsync(cancellationToken), true, null);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            return ([], false, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Text.Json.JsonException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return ([], visivel, visivel ? "Tente atualizar novamente." : null);
        }
    }
}
