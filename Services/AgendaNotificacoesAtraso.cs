namespace ServiceDesk.Services;

internal sealed class AgendaNotificacoesAtraso
{
    private static readonly TimeSpan IntervaloBanners = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan IntervaloTentativas = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan PausaEntreAvaliacoes = TimeSpan.FromSeconds(30);
    private readonly Dictionary<long, Tentativa> _tentativas = new();
    private DateTimeOffset _proximoBanner;
    private DateTimeOffset? _proximoLembrete;
    private DateTimeOffset? _ultimaAvaliacao;
    private int _intervaloLembreteMinutos;
    private string? _erroLembrete;

    internal readonly record struct Envio(long? Episodio);
    private readonly record struct Tentativa(DateTimeOffset? RetentarEm, string? Erro);

    internal Envio? Planejar(IReadOnlyList<long> elegiveis, IReadOnlySet<long> episodiosAbertos,
        DateTimeOffset agora, int intervaloLembreteMinutos, bool ativo)
    {
        foreach (var episodio in _tentativas.Keys.Where(id => !episodiosAbertos.Contains(id)).ToArray())
            _tentativas.Remove(episodio);

        var retomando = _ultimaAvaliacao is { } anterior && agora - anterior > PausaEntreAvaliacoes;
        _ultimaAvaliacao = agora;
        if (retomando || !ativo || elegiveis.Count == 0 || _intervaloLembreteMinutos != intervaloLembreteMinutos)
        {
            _proximoLembrete = null;
            _erroLembrete = null;
        }

        _intervaloLembreteMinutos = intervaloLembreteMinutos;
        if (!ativo || elegiveis.Count == 0) return null;

        if (intervaloLembreteMinutos > 0)
            _proximoLembrete ??= agora.AddMinutes(intervaloLembreteMinutos);
        if (agora < _proximoBanner) return null;

        if (_proximoLembrete <= agora)
        {
            _proximoBanner = agora + IntervaloBanners;
            return new Envio(null);
        }

        foreach (var episodio in elegiveis)
        {
            if (_tentativas.TryGetValue(episodio, out var tentativa)
                && (tentativa.RetentarEm is null || tentativa.RetentarEm > agora))
                continue;

            _proximoBanner = agora + IntervaloBanners;
            return new Envio(episodio);
        }

        return null;
    }

    internal void RegistrarResultado(Envio envio, DateTimeOffset agora, string? erro)
    {
        if (envio.Episodio is { } episodio)
            _tentativas[episodio] = new Tentativa(erro is null ? null : agora + IntervaloTentativas, erro);
        else
        {
            _erroLembrete = erro;
            _proximoLembrete = erro is null ? agora.AddMinutes(_intervaloLembreteMinutos) : agora + IntervaloTentativas;
        }
    }

    internal string? ObterErro(IReadOnlyList<long> elegiveis)
    {
        foreach (var episodio in elegiveis)
            if (_tentativas.TryGetValue(episodio, out var tentativa) && tentativa.Erro is { } erro)
                return erro;
        return _erroLembrete;
    }

    internal void Limpar()
    {
        _tentativas.Clear();
        _proximoLembrete = null;
        _ultimaAvaliacao = null;
        _erroLembrete = null;
    }
}
