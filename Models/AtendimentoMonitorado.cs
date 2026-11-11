using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ServiceDesk.Models;

public sealed class AtendimentoMonitorado : INotifyPropertyChanged
{
    public long Id { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool TemEmail => !string.IsNullOrWhiteSpace(Email);
    public string EmailText => TemEmail ? Email.Trim() : "E-mail não informado";
    public string Empresa { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? UltimaInteracaoAgenteEm { get; set; }
    public DateTimeOffset? UltimaMensagemClienteEm { get; set; }
    public DateTimeOffset? UltimaMensagemPublicaEm { get; set; }
    public DateTimeOffset? AdiadoAte { get; set; }

    public long? AgenteAtribuidoId { get; set; }

    /// <summary>Ordena pela última interação conhecida do agente; valores desconhecidos ficam ao final.</summary>
    public DateTimeOffset ReferenciaInteracao => DataValida(UltimaInteracaoAgenteEm) ?? DateTimeOffset.MaxValue;
    public bool EstaAdiado => string.Equals(Status, "snoozed", StringComparison.OrdinalIgnoreCase);
    public string SituacaoText => Status.ToLowerInvariant() switch
    {
        "snoozed" => AdiadoAte is null ? "Adiada até a próxima resposta" : $"Adiada até {AdiadoAte.Value.LocalDateTime:dd/MM HH:mm}",
        "open" => "Aberta",
        "pending" => "Pendente",
        "resolved" => "Resolvida",
        _ => "Status desconhecido"
    };

    private string _tempoUltimaInteracaoAgente = "Última interação não identificada";
    public string TempoUltimaInteracaoAgente
    {
        get => _tempoUltimaInteracaoAgente;
        private set => SetField(ref _tempoUltimaInteracaoAgente, value);
    }

    private string _tempoUltimaInteracao = "Última interação não identificada";
    public string TempoUltimaInteracao
    {
        get => _tempoUltimaInteracao;
        private set => SetField(ref _tempoUltimaInteracao, value);
    }

    public void AtualizarTempo(DateTimeOffset agora)
    {
        var ultimaAgente = DataValida(UltimaInteracaoAgenteEm);
        var ultimaCliente = DataValida(UltimaMensagemClienteEm);
        TempoUltimaInteracaoAgente = ultimaAgente is { } resposta
            ? FormatarDuracao(agora - resposta) : "Última interação não identificada";
        TempoUltimaInteracao = ultimaCliente is { } cliente && (ultimaAgente is null || cliente > ultimaAgente)
            ? $"Cliente: {FormatarDuracao(agora - cliente)}"
            : ultimaAgente is { } agente ? $"Agente: {FormatarDuracao(agora - agente)}" : "Última interação não identificada";
    }

    private static string FormatarDuracao(TimeSpan intervalo) => intervalo.TotalDays >= 1
            ? $"há {(int)intervalo.TotalDays}d {intervalo.Hours}h"
            : intervalo.TotalHours >= 1
                ? $"há {(int)intervalo.TotalHours}h {intervalo.Minutes}min"
                : $"há {Math.Max(0, (int)intervalo.TotalMinutes)}min";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static DateTimeOffset? DataValida(DateTimeOffset? data) => data is { Year: > 2000 } ? data : null;
}
