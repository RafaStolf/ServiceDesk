namespace ServiceDesk.Models;

public sealed record EventoMetrica(string Nome, double? Segundos, double? SegundosUteis, DateTimeOffset? RegistradoEm);
public sealed record OcorrenciaSla(string Tipo, DateTimeOffset? RegistradaEm);
public sealed record PoliticaSla(string Nome, string Estado, double? PrimeiraRespostaMeta, double? ProximaRespostaMeta,
    double? ResolucaoMeta, bool? ApenasHorarioComercial, DateTimeOffset? PrimeiraRespostaVence,
    DateTimeOffset? ProximaRespostaVence, DateTimeOffset? ResolucaoVence, DateTimeOffset? ConcluidoEm)
{
    public string Situacao => Estado switch
    {
        "active" => "Em acompanhamento", "hit" => "Cumprido", "missed" => "Violado",
        "active_with_misses" => "Em acompanhamento · com violação", _ => "Situação não informada"
    };
}
public sealed record TicketMetricas(long Id, string Contato, string Empresa, string Status, IReadOnlyList<EventoMetrica> Eventos,
    PoliticaSla? Sla, IReadOnlyList<OcorrenciaSla> Ocorrencias, string? Aviso, bool PeriodoConfirmado = true);
public sealed record ResultadoMetricas(IReadOnlyList<TicketMetricas> Tickets, IReadOnlyList<string> Avisos, bool Parcial, DateTimeOffset ConsultadoEm);
public sealed record IndicadorMetrica(double? MediaSegundos, int Quantidade);
public sealed record ResumoMetricas(IndicadorMetrica PrimeiraResposta, IndicadorMetrica RespostasSeguintes, IndicadorMetrica Resolucao)
{
    public static ResumoMetricas Calcular(IEnumerable<TicketMetricas> tickets, bool uteis)
    {
        var eventos = tickets.SelectMany(x => x.Eventos).ToArray();
        IndicadorMetrica Media(string nome)
        {
            var valores = eventos.Where(x => x.Nome == nome).Select(x => uteis ? x.SegundosUteis : x.Segundos)
                .Where(x => x is { } valor && double.IsFinite(valor) && valor >= 0).Select(x => x!.Value).ToArray();
            return new(valores.Length == 0 ? null : valores.Average(), valores.Length);
        }
        return new(Media("first_response"), Media("reply_time"), Media("conversation_resolved"));
    }
}
public static class PeriodoMetricas
{
    public static TimeZoneInfo Fuso => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public static DateOnly Hoje => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Fuso).DateTime);
    public static (DateTimeOffset Inicio, DateTimeOffset FimExclusivo) Limites(DateOnly inicio, DateOnly fim)
    {
        if (fim < inicio || fim.DayNumber - inicio.DayNumber > 365) throw new ArgumentException("Escolha um período de até 366 dias, com início anterior ou igual ao fim.");
        DateTimeOffset Utc(DateOnly dia) => new(TimeZoneInfo.ConvertTimeToUtc(dia.ToDateTime(TimeOnly.MinValue), Fuso), TimeSpan.Zero);
        return (Utc(inicio), Utc(fim.AddDays(1)));
    }
    public static string Data(DateTimeOffset? data) => data is { } valor ? TimeZoneInfo.ConvertTime(valor, Fuso).ToString("dd/MM/yyyy HH:mm") : "Não informado";
    public static string Duracao(double? segundos)
    {
        if (segundos is not { } valor || !double.IsFinite(valor) || valor < 0) return "Indisponível";
        var tempo = TimeSpan.FromSeconds(Math.Min(valor, TimeSpan.MaxValue.TotalSeconds / 2));
        return tempo.TotalDays >= 1 ? $"{(long)tempo.TotalDays}d {tempo.Hours}h {tempo.Minutes}min"
            : tempo.TotalHours >= 1 ? $"{(int)tempo.TotalHours}h {tempo.Minutes}min" : tempo.TotalMinutes >= 1 ? $"{(int)tempo.TotalMinutes}min {tempo.Seconds}s" : $"{tempo.Seconds}s";
    }
}
