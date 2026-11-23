using ServiceDesk.Models;

namespace ServiceDesk.Services;

internal sealed record AtrasoAtendimento(AtendimentoMonitorado Ticket, DateTimeOffset Inicio, long Episodio)
{
    internal string Texto(DateTimeOffset agora) => AcompanhamentoN1.DescreverEspera(Inicio, agora, emAtraso: true);

    internal string Tempo(DateTimeOffset agora) => AcompanhamentoN1.TempoDeEspera(Inicio, agora);
}

internal sealed class FilaAlertasAtendimento
{
    private static readonly TimeSpan DuracaoAviso = TimeSpan.FromSeconds(8);
    private readonly Dictionary<long, Espera> _esperas = new();
    private long _ultimoEpisodio;
    private long? _emExibicao;
    private DateTimeOffset _expiraEm;

    internal sealed record Atualizacao(AtrasoAtendimento? Atual,
        IReadOnlyList<AtrasoAtendimento> Elegiveis, IReadOnlySet<long> EpisodiosAbertos);

    private sealed record Espera(long? Agente, DateTimeOffset Inicio, long Episodio)
    {
        internal bool AvisoExibido { get; set; }
    }

    internal static bool DadosRecentes(DateTimeOffset? ultimaSincronizacao, DateTimeOffset agora, TimeSpan? intervaloAutomatico) =>
        ultimaSincronizacao is { } ultima && agora < ultima + (intervaloAutomatico?.Add(TimeSpan.FromMinutes(1)) ?? TimeSpan.FromMinutes(5));

    internal void Limpar()
    {
        _esperas.Clear();
        _emExibicao = null;
    }

    internal Atualizacao Atualizar(IReadOnlyList<AtendimentoMonitorado> atendimentos, DateTimeOffset agora,
        int limiteMinutos, bool manterAtual, bool dadosRecentes = true, bool alertasAtivos = true)
    {
        if (!dadosRecentes)
        {
            _emExibicao = null;
            return new Atualizacao(null, [], EpisodiosAbertos());
        }

        var ativos = new HashSet<long>();
        var elegiveis = new List<AtrasoAtendimento>();
        foreach (var ticket in atendimentos)
        {
            var acompanhamento = AcompanhamentoN1.DoTicket(ticket);
            if (acompanhamento is not { Estado: EstadoN1.ClienteAguarda, Referencia: { } inicio }) continue;
            ativos.Add(ticket.Id);
            if (!_esperas.TryGetValue(ticket.Id, out var espera) || espera.Agente != ticket.AgenteAtribuidoId || espera.Inicio != inicio)
                _esperas[ticket.Id] = espera = new Espera(ticket.AgenteAtribuidoId, inicio, ++_ultimoEpisodio);

            if (acompanhamento.RespostaEmAtraso(agora, limiteMinutos))
                elegiveis.Add(new AtrasoAtendimento(ticket, inicio, espera.Episodio));
        }
        foreach (var id in _esperas.Keys.Where(id => !ativos.Contains(id)).ToArray())
            _esperas.Remove(id);

        var ordenados = elegiveis.OrderBy(atraso => atraso.Inicio).ThenBy(atraso => atraso.Ticket.Id).ToList();
        var episodios = EpisodiosAbertos();
        if (!alertasAtivos)
        {
            _emExibicao = null;
            return new Atualizacao(null, ordenados, episodios);
        }

        if (manterAtual && agora < _expiraEm && _emExibicao is { } atual
            && ordenados.FirstOrDefault(atraso => atraso.Episodio == atual) is { } emExibicao)
            return new Atualizacao(emExibicao, ordenados, episodios);

        var proximo = ordenados.FirstOrDefault(atraso => !_esperas[atraso.Ticket.Id].AvisoExibido);
        _emExibicao = proximo?.Episodio;
        if (proximo is not null)
        {
            _esperas[proximo.Ticket.Id].AvisoExibido = true;
            _expiraEm = agora + DuracaoAviso;
        }
        return new Atualizacao(proximo, ordenados, episodios);
    }

    private HashSet<long> EpisodiosAbertos() => _esperas.Values.Select(espera => espera.Episodio).ToHashSet();
}
