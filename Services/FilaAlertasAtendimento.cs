using ServiceDesk.Models;

namespace ServiceDesk.Services;

internal sealed class FilaAlertasAtendimento
{
    private static readonly TimeSpan DuracaoAviso = TimeSpan.FromSeconds(8);
    private readonly Dictionary<long, Referencia> _notificados = new();
    private readonly Queue<Referencia> _pendentes = new();
    private Referencia? _atual;
    private DateTimeOffset _expiraEm;

    internal sealed record Atualizacao(AtendimentoMonitorado? Atual,
        IReadOnlyCollection<AtendimentoMonitorado> Elegiveis);
    private readonly record struct Referencia(long Ticket, long? Agente, DateTimeOffset Interacao);

    internal Atualizacao Atualizar(IReadOnlyList<AtendimentoMonitorado> atendimentos, DateTimeOffset agora,
        int? limiteMinutos, bool manterAtual)
    {
        foreach (var atendimento in atendimentos) atendimento.AtualizarTempo(agora);
        if (limiteMinutos is null)
        {
            _pendentes.Clear();
            _atual = null;
            return new Atualizacao(null, []);
        }

        var limite = agora.AddMinutes(-limiteMinutos.Value);
        var elegiveis = new Dictionary<Referencia, AtendimentoMonitorado>();
        foreach (var atendimento in atendimentos)
        {
            if (atendimento.Status != "open" || atendimento.ReferenciaAlerta is not { } interacao || interacao > limite)
                continue;

            var referencia = new Referencia(atendimento.Id, atendimento.AgenteAtribuidoId, interacao);
            elegiveis[referencia] = atendimento;
            if (_notificados.TryGetValue(atendimento.Id, out var notificado) && notificado == referencia) continue;

            _notificados[atendimento.Id] = referencia;
            _pendentes.Enqueue(referencia);
        }

        if (manterAtual && agora < _expiraEm && _atual is { } atual && elegiveis.TryGetValue(atual, out var emExibicao))
            return new Atualizacao(emExibicao, elegiveis.Values);

        _atual = null;
        while (_pendentes.TryDequeue(out var proximo))
        {
            if (!elegiveis.TryGetValue(proximo, out var atendimento)) continue;
            _atual = proximo;
            _expiraEm = agora + DuracaoAviso;
            return new Atualizacao(atendimento, elegiveis.Values);
        }
        return new Atualizacao(null, elegiveis.Values);
    }
}
