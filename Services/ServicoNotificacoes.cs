using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

internal sealed class ServicoNotificacoes : IDisposable
{
    private static readonly TimeSpan IntervaloTentativas = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IntervaloBanners = TimeSpan.FromSeconds(8);
    private readonly Dictionary<long, Tentativa> _tentativas = new();
    private const string GrupoTickets = "tickets";
    private DateTimeOffset _proximoBanner;
    private DateTimeOffset _proximaAtualizacao;
    private bool _atualizandoAvisos;
    private AppNotificationManager? _notificador;
    private Action? _abrirAplicativo;
    private string? _erroRegistro;

    internal string? UltimoErro { get; private set; }
    private readonly record struct Tentativa(long? Agente, DateTimeOffset Interacao, DateTimeOffset Horario,
        string? Erro, uint? AvisoId = null, string? Texto = null);

    // O identificador deve corresponder ao AppUserModelID do atalho criado pelo Inno Setup.
    internal static void UsarIdentidadeDaInstalacao()
    {
        const string chave = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\{8EB83B1F-CE63-48F5-B7E0-5179A5B79D51}_is1";
        try
        {
            if (Registry.GetValue(chave, "InstallLocation", null) is not string pasta || string.IsNullOrWhiteSpace(pasta)) return;
            if (string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(pasta)),
                Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase))
                _ = SetCurrentProcessExplicitAppUserModelID("Centrium.ServiceDesk");
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException
            or System.Security.SecurityException) { }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    internal void Registrar(Action abrirAplicativo)
    {
        _abrirAplicativo = abrirAplicativo;
        try
        {
            if (!AppNotificationManager.IsSupported())
                throw new InvalidOperationException("O Windows não disponibilizou notificações para este processo.");

            var notificador = AppNotificationManager.Default;
            notificador.NotificationInvoked += NotificacaoAcionada;
            try { notificador.Register(); }
            catch
            {
                notificador.NotificationInvoked -= NotificacaoAcionada;
                throw;
            }
            _notificador = notificador;
        }
        catch (Exception ex)
        {
            _erroRegistro = $"Falha ao registrar notificações do Windows (0x{ex.HResult:X8}). {ex.Message}";
        }
    }

    internal void NotificarAtendimentos(IEnumerable<AtendimentoMonitorado> elegiveis, DateTimeOffset agora, bool som)
    {
        UltimoErro = null;
        var podeAtualizar = !_atualizandoAvisos && agora >= _proximaAtualizacao;
        List<(AtendimentoMonitorado Ticket, Tentativa Anterior, string Texto)>? atualizacoes = null;
        foreach (var atendimento in elegiveis)
        {
            if (atendimento.ReferenciaAlerta is not { } referencia) continue;
            var tentativa = new Tentativa(atendimento.AgenteAtribuidoId, referencia, agora, null);
            if (_tentativas.TryGetValue(atendimento.Id, out var anterior)
                && anterior.Agente == tentativa.Agente && anterior.Interacao == tentativa.Interacao)
            {
                if (anterior.Erro is null)
                {
                    if (podeAtualizar && anterior.AvisoId is not null)
                    {
                        var texto = atendimento.DescreverAlerta(agora);
                        if (texto != anterior.Texto) (atualizacoes ??= []).Add((atendimento, anterior, texto));
                    }
                    continue;
                }
                if (agora - anterior.Horario < IntervaloTentativas)
                {
                    UltimoErro ??= anterior.Erro;
                    continue;
                }
            }

            if (agora < _proximoBanner) continue;

            // Dá tempo para o Windows apresentar cada aviso quando vários tickets vencem juntos.
            _proximoBanner = agora + IntervaloBanners;
            try
            {
                var texto = atendimento.DescreverAlerta(agora);
                var id = Exibir(atendimento, texto, som, silencioso: false);
                tentativa = tentativa with { AvisoId = id, Texto = texto };
            }
            catch (Exception ex)
            {
                var mensagem = $"Notificação do Windows não enviada: {ex.Message}";
                tentativa = tentativa with { Erro = mensagem };
                UltimoErro ??= mensagem;
                RegistrarFalha(ex);
            }
            _tentativas[atendimento.Id] = tentativa;
        }
        if (atualizacoes is not null) _ = AtualizarAvisosAsync(atualizacoes);
    }

    private async Task AtualizarAvisosAsync(List<(AtendimentoMonitorado Ticket, Tentativa Anterior, string Texto)> atualizacoes)
    {
        if (_notificador is not { } notificador) return;
        _atualizandoAvisos = true;
        try
        {
            var recebidas = await notificador.GetAllAsync();
            if (_notificador != notificador) return;

            var ids = recebidas.Select(aviso => aviso.Id).ToHashSet();
            foreach (var (ticket, anterior, texto) in atualizacoes)
            {
                if (!_tentativas.TryGetValue(ticket.Id, out var atual) || atual != anterior) continue;
                if (!ids.Contains(anterior.AvisoId!.Value))
                {
                    _tentativas[ticket.Id] = anterior with { AvisoId = null };
                    continue;
                }
                var id = Exibir(ticket, texto, som: false, silencioso: true);
                _tentativas[ticket.Id] = anterior with { AvisoId = id, Texto = texto };
            }
        }
        catch (Exception ex)
        {
            _proximaAtualizacao = DateTimeOffset.Now.AddSeconds(30);
            RegistrarFalha(ex);
        }
        finally { _atualizandoAvisos = false; }
    }

    private uint Exibir(AtendimentoMonitorado atendimento, string texto, bool som, bool silencioso)
    {
        var identificacao = string.IsNullOrWhiteSpace(atendimento.Empresa)
            ? atendimento.Cliente : $"{atendimento.Cliente} · {atendimento.Empresa}";
        return Enviar(som, [$"ServiceDesk · Ticket #{atendimento.Id}", identificacao, texto], atendimento.Id, silencioso);
    }

    internal async Task TestarAsync(bool som)
    {
        try
        {
            var id = Enviar(som, ["ServiceDesk", "Notificação de teste"]);
            var recebidas = await _notificador!.GetAllAsync();
            if (!recebidas.Any(aviso => aviso.Id == id))
                throw new InvalidOperationException("O Windows não confirmou a notificação na Central de Notificações.");
        }
        catch (Exception ex)
        {
            RegistrarFalha(ex);
            throw;
        }
    }

    private uint Enviar(bool som, string[] linhas, long? ticketId = null, bool silencioso = false)
    {
        var notificador = _notificador ?? throw new InvalidOperationException(_erroRegistro ?? "Notificações do Windows indisponíveis.");
        var permissao = notificador.Setting;
        if (permissao != AppNotificationSetting.Enabled)
            throw new InvalidOperationException($"Permissão indisponível ({permissao}). Confira Configurações do Windows → Sistema → Notificações → ServiceDesk.");

        var construtor = new AppNotificationBuilder();
        foreach (var linha in linhas) construtor.AddText(linha);
        if (!som) construtor.MuteAudio();
        var aviso = construtor.BuildNotification();
        if (ticketId is { } id)
        {
            aviso.Group = GrupoTickets;
            aviso.Tag = id.ToString("x", System.Globalization.CultureInfo.InvariantCulture);
        }
        aviso.SuppressDisplay = silencioso;
        notificador.Show(aviso);
        if (aviso.Id == 0) throw new InvalidOperationException("O Windows não aceitou a notificação do ServiceDesk.");
        return aviso.Id;
    }

    private static void RegistrarFalha(Exception erro)
    {
        try
        {
            var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceDesk");
            Directory.CreateDirectory(pasta);
            File.AppendAllText(Path.Combine(pasta, "notificacoes.log"),
                $"{DateTimeOffset.Now:O} Falha de entrega: 0x{erro.HResult:X8}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void NotificacaoAcionada(AppNotificationManager sender, AppNotificationActivatedEventArgs args) =>
        _abrirAplicativo?.Invoke();

    public void Dispose()
    {
        if (_notificador is not { } notificador) return;
        _notificador = null;
        notificador.NotificationInvoked -= NotificacaoAcionada;
        notificador.Unregister();
    }
}
