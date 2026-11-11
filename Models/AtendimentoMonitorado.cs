using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ServiceDesk.Models;

public sealed class AtendimentoMonitorado : INotifyPropertyChanged
{
    public long Id { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Ticket { get; set; } = string.Empty;
    public string Assunto { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? UltimaInteracaoMinhaEm { get; set; }
    public DateTimeOffset? UltimaMensagemClienteEm { get; set; }
    public DateTimeOffset? UltimaMensagemPublicaEm { get; set; }
    public DateTimeOffset? AdiadoAte { get; set; }

    /// <summary>Referência para prioridade e alertas: a última mensagem pública da conversa, inclusive bot ou template.</summary>
    public DateTimeOffset ReferenciaInteracao => DataValida(UltimaMensagemPublicaEm) ?? CriadoEm;
    public bool EstaAdiado => string.Equals(Status, "snoozed", StringComparison.OrdinalIgnoreCase);
    public string SituacaoText => Status.ToLowerInvariant() switch
    {
        "snoozed" => AdiadoAte is null ? "Adiada até a próxima resposta" : $"Adiada até {AdiadoAte.Value.LocalDateTime:dd/MM HH:mm}",
        "open" => "Aberta",
        "pending" => "Pendente",
        "resolved" => "Resolvida",
        _ => "Status desconhecido"
    };

    private string _tempoSemMensagemPublica = string.Empty;
    public string TempoSemMensagemPublica
    {
        get => _tempoSemMensagemPublica;
        private set => SetField(ref _tempoSemMensagemPublica, value);
    }

    private string _tempoDesdeCriacao = string.Empty;
    public string TempoDesdeCriacao
    {
        get => _tempoDesdeCriacao;
        private set => SetField(ref _tempoDesdeCriacao, value);
    }

    public string UltimaMensagemPublicaText => DataValida(UltimaMensagemPublicaEm) is null
        ? "Sem mensagem pública registrada"
        : $"Última mensagem pública: {UltimaMensagemPublicaEm!.Value.LocalDateTime:dd/MM HH:mm}";

    public string UltimaMensagemClienteText => DataValida(UltimaMensagemClienteEm) is null
        ? "Contato: sem mensagem recebida registrada"
        : $"Contato: {UltimaMensagemClienteEm!.Value.LocalDateTime:dd/MM HH:mm}";

    public string MinhaUltimaMensagemText => DataValida(UltimaInteracaoMinhaEm) is null
        ? "Você: sem mensagem enviada registrada"
        : $"Última mensagem enviada por você: {UltimaInteracaoMinhaEm!.Value.LocalDateTime:dd/MM HH:mm}";

    public void AtualizarTempo(DateTimeOffset agora)
    {
        TempoDesdeCriacao = FormatarDuracao(agora - CriadoEm);
        TempoSemMensagemPublica = FormatarDuracao(agora - ReferenciaInteracao);
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
