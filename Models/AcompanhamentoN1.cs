namespace ServiceDesk.Models;

internal enum EstadoN1 { ClienteAguarda, AcompanharRetorno, AdiadoComPrazo, AdiadoAteResposta }
internal enum DestaqueN1 { Normal, Aviso, PrazoAtingido }
internal sealed record IndicacaoN1(string Texto, DestaqueN1 Destaque);

internal sealed record AcompanhamentoN1(EstadoN1 Estado, DateTimeOffset? Referencia)
{
    internal const int PrazoRespostaMinimo = 10;
    private static readonly TimeSpan PrazoRevisaoRetorno = TimeSpan.FromMinutes(10);

    public static AcompanhamentoN1? DoTicket(AtendimentoMonitorado ticket)
    {
        if (ticket.EstaAdiado)
            return DataValida(ticket.AdiadoAte) is { } prazo
                ? new(EstadoN1.AdiadoComPrazo, prazo) : new(EstadoN1.AdiadoAteResposta, null);
        if (ticket.Status != "open") return null;

        var ultimaPublica = DataValida(ticket.UltimaMensagemPublicaEm);
        var ultimaCliente = DataValida(ticket.UltimaMensagemClienteEm);
        var ultimaAgente = DataValida(ticket.UltimaRespostaPublicaEm);
        var inicioEspera = DataValida(ticket.ClienteAguardaDesde);
        if (inicioEspera is null && !ticket.EsperaInformadaPeloChatwoot && ultimaCliente is { } cliente
            && cliente == ultimaPublica && (ultimaAgente is null || cliente > ultimaAgente))
            inicioEspera = cliente;
        if (inicioEspera is not null) return new(EstadoN1.ClienteAguarda, inicioEspera);
        if (ultimaAgente is { } resposta && resposta == ultimaPublica && (ultimaCliente is null || resposta > ultimaCliente))
            return new(EstadoN1.AcompanharRetorno, resposta);
        return null;
    }

    public IndicacaoN1 Descrever(DateTimeOffset agora, int limiteRespostaMinutos, int avisoMinutos)
    {
        if (Referencia is not { } referencia)
            return new("Adiado até a próxima resposta", DestaqueN1.Normal);

        return Estado switch
        {
            EstadoN1.ClienteAguarda => new(DescreverEspera(referencia, agora, RespostaEmAtraso(agora, limiteRespostaMinutos)),
                Destaque(LimiteDeResposta(limiteRespostaMinutos) - (agora - referencia), avisoMinutos)),
            EstadoN1.AcompanharRetorno => new($"Respondido · há {Duracao(agora - referencia)}",
                Destaque(referencia + PrazoRevisaoRetorno - agora, avisoMinutos)),
            _ => new($"Adiado · Retoma em {referencia.ToLocalTime():dd'/'MM 'às' HH:mm}",
                Destaque(referencia - agora, avisoMinutos))
        };
    }

    internal bool RespostaEmAtraso(DateTimeOffset agora, int limiteRespostaMinutos) =>
        Estado == EstadoN1.ClienteAguarda && Referencia is { } inicio && agora - inicio >= LimiteDeResposta(limiteRespostaMinutos);

    private static TimeSpan LimiteDeResposta(int limiteRespostaMinutos) =>
        TimeSpan.FromMinutes(Math.Max(PrazoRespostaMinimo, limiteRespostaMinutos));

    internal static string DescreverEspera(DateTimeOffset inicio, DateTimeOffset agora, bool emAtraso) =>
        $"{(emAtraso ? "Resposta em atraso" : "Aguardando resposta")} · {TempoDeEspera(inicio, agora)}";

    internal static string TempoDeEspera(DateTimeOffset inicio, DateTimeOffset agora) => $"há {Duracao(agora - inicio)}";

    internal static void PreservarEsperaObservada(IReadOnlyList<AtendimentoMonitorado> novos, IReadOnlyList<AtendimentoMonitorado> anteriores)
    {
        var porId = anteriores.ToDictionary(ticket => ticket.Id);
        foreach (var novo in novos)
        {
            if (novo.EsperaInformadaPeloChatwoot || DoTicket(novo) is not { Estado: EstadoN1.ClienteAguarda, Referencia: { } inicio }
                || !porId.TryGetValue(novo.Id, out var anterior)
                || anterior.AgenteAtribuidoId != novo.AgenteAtribuidoId
                || anterior.UltimaRespostaPublicaEm != novo.UltimaRespostaPublicaEm
                || DoTicket(anterior) is not { Estado: EstadoN1.ClienteAguarda, Referencia: { } inicioAnterior }
                || inicioAnterior >= inicio) continue;
            novo.ClienteAguardaDesde = inicioAnterior;
        }
    }

    private static DestaqueN1 Destaque(TimeSpan restante, int avisoMinutos)
    {
        if (restante <= TimeSpan.Zero) return DestaqueN1.PrazoAtingido;
        return avisoMinutos > 0 && restante <= TimeSpan.FromMinutes(avisoMinutos) ? DestaqueN1.Aviso : DestaqueN1.Normal;
    }

    internal static string DescreverResolucao(DateTimeOffset? resolvidoEm, DateTimeOffset agora) =>
        DataValida(resolvidoEm) is { } data ? $"Resolvido · há {Duracao(agora - data)}" : "Resolvido";

    private static string Duracao(TimeSpan intervalo)
    {
        var minutos = Math.Max(0, (long)Math.Floor(intervalo.TotalMinutes));
        if (minutos >= 1440) return $"{minutos / 1440} d {minutos / 60 % 24} h";
        return minutos >= 60 ? $"{minutos / 60} h {minutos % 60} min" : $"{minutos} min";
    }

    private static DateTimeOffset? DataValida(DateTimeOffset? data) => data is { Year: > 2000 } ? data : null;
}
