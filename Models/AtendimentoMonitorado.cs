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
    public DateTimeOffset? UltimaInteracaoChatEm { get; set; }
    public DateTimeOffset? AdiadoAte { get; set; }

    /// <summary>Referência para prioridade e alertas: a última interação humana relevante no chat.</summary>
    public DateTimeOffset ReferenciaInteracao => DataValida(UltimaInteracaoChatEm) ?? CriadoEm;
    public bool EstaAdiado => string.Equals(Status, "snoozed", StringComparison.OrdinalIgnoreCase)
        || AdiadoAte is { } adiadoAte && adiadoAte > DateTimeOffset.UtcNow;
    public string SituacaoText => EstaAdiado
        ? AdiadoAte is null ? "Adiado" : $"Adiado até {AdiadoAte.Value.LocalDateTime:dd/MM HH:mm}"
        : "Em aberto";

    private string _tempoSemInteracaoChat = string.Empty;
    public string TempoSemInteracaoChat
    {
        get => _tempoSemInteracaoChat;
        private set => SetField(ref _tempoSemInteracaoChat, value);
    }

    private string _tempoChatAberto = string.Empty;
    public string TempoChatAberto
    {
        get => _tempoChatAberto;
        private set => SetField(ref _tempoChatAberto, value);
    }

    public string UltimaInteracaoChatText => DataValida(UltimaInteracaoChatEm) is null
        ? "Sem interação desde a abertura"
        : $"Última interação: {UltimaInteracaoChatEm!.Value.LocalDateTime:dd/MM HH:mm}";

    public string UltimaMensagemClienteText => DataValida(UltimaMensagemClienteEm) is null
        ? "Cliente: sem mensagem registrada"
        : $"Cliente: {UltimaMensagemClienteEm!.Value.LocalDateTime:dd/MM HH:mm}";

    public string MinhaUltimaAcaoText => DataValida(UltimaInteracaoMinhaEm) is null
        ? "Você: sem ação registrada"
        : $"Você: {UltimaInteracaoMinhaEm!.Value.LocalDateTime:dd/MM HH:mm}";

    public void AtualizarTempo(DateTimeOffset agora)
    {
        TempoChatAberto = FormatarDuracao(agora - CriadoEm);
        TempoSemInteracaoChat = FormatarDuracao(agora - ReferenciaInteracao);
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
