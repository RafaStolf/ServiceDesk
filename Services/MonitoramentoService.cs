using ServiceDesk.Models;

namespace ServiceDesk.Services;

public sealed record ResultadoMonitoramento(IReadOnlyList<AtendimentoMonitorado> Atendimentos);

/// <summary>Orquestra somente leituras da fila atribuída no Chatwoot.</summary>
public sealed class MonitoramentoService
{
    public async Task<ResultadoMonitoramento> SincronizarAsync(CancellationToken cancellationToken)
    {
        var configuracao = CarregadorConfiguracaoChatwoot.Carregar();
        using var chatwoot = new ChatwootSyncService(configuracao);
        var atendimentos = await chatwoot.SincronizarAsync(cancellationToken);
        return new ResultadoMonitoramento(atendimentos);
    }
}
