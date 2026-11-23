using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

internal sealed class ServicoNotificacoes : IDisposable
{
    private const string GrupoTeste = "diagnostico";
    private const string EtiquetaTeste = "teste";
    private const string GrupoTickets = "tickets";
    private const string GrupoLembretes = "atrasos";
    private const string EtiquetaLembrete = "resumo";
    private readonly AgendaNotificacoesAtraso _agenda = new();
    private readonly Dictionary<long, AvisoRecebido> _avisosIndividuais = new();
    private IReadOnlyList<AtrasoAtendimento> _elegiveis = [];
    private AvisoRecebido? _resumo;
    private DateTimeOffset _agora;
    private DateTimeOffset _proximaAtualizacao;
    private long _versaoAlertas;
    private bool _atualizandoAvisos;
    private AppNotificationManager? _notificador;
    private Action? _abrirAplicativo;
    private string? _erroRegistro;

    internal string? UltimoErro { get; private set; }

    private readonly record struct ConteudoAviso(string Titulo, string Detalhe, string? Estado,
        string Grupo, string Etiqueta);
    private readonly record struct AvisoRecebido(uint Id, ConteudoAviso Conteudo);

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

    internal void NotificarAtendimentos(IReadOnlyList<AtrasoAtendimento> elegiveis, IReadOnlySet<long> episodiosAbertos,
        DateTimeOffset agora, SomNotificacao? som, int intervaloLembreteMinutos, bool ativo)
    {
        var erroPermissao = ativo && elegiveis.Count > 0 ? VerificarPermissao() : null;
        ativo = ativo && erroPermissao is null;

        if (_elegiveis.Count > 0 && (!ativo || elegiveis.Count == 0)) _versaoAlertas++;
        _elegiveis = ativo ? elegiveis : [];
        _agora = agora;
        if (_elegiveis.Count == 0 || intervaloLembreteMinutos <= 0) _resumo = null;
        foreach (var episodio in _avisosIndividuais.Keys.Where(id => !episodiosAbertos.Contains(id)).ToArray())
            _avisosIndividuais.Remove(episodio);

        var episodiosElegiveis = _elegiveis.Select(atraso => atraso.Episodio).ToArray();
        if (_agenda.Planejar(episodiosElegiveis, episodiosAbertos, agora, intervaloLembreteMinutos, ativo) is { } envio
            && ObterConteudo(envio.Episodio) is { } conteudo)
        {
            string? erro = null;
            try { GuardarAviso(envio.Episodio, new AvisoRecebido(Exibir(conteudo, som, silencioso: false), conteudo)); }
            catch (Exception ex)
            {
                erro = $"Notificação do Windows não enviada: {ex.Message}";
                RegistrarFalha(ex);
            }
            _agenda.RegistrarResultado(envio, agora, erro);
        }

        UltimoErro = erroPermissao ?? (ativo ? _agenda.ObterErro(episodiosElegiveis) : null);
        PrepararAtualizacaoDosAvisos();
    }

    internal void LimparAlertasAtendimento()
    {
        _versaoAlertas++;
        _elegiveis = [];
        _avisosIndividuais.Clear();
        _resumo = null;
        _proximaAtualizacao = default;
        UltimoErro = null;
        _agenda.Limpar();
    }

    private void PrepararAtualizacaoDosAvisos()
    {
        if (_notificador is not { } notificador || _atualizandoAvisos || _agora < _proximaAtualizacao) return;
        List<(long? Episodio, AvisoRecebido Anterior)>? atualizacoes = null;
        foreach (var atraso in _elegiveis)
        {
            if (_avisosIndividuais.TryGetValue(atraso.Episodio, out var anterior) && ConteudoDoTicket(atraso) != anterior.Conteudo)
                (atualizacoes ??= []).Add((atraso.Episodio, anterior));
        }
        if (_resumo is { } resumo && ConteudoDoLembrete() != resumo.Conteudo)
            (atualizacoes ??= []).Add((null, resumo));
        if (atualizacoes is not null) _ = AtualizarAvisosAsync(notificador, atualizacoes, _versaoAlertas);
    }

    private async Task AtualizarAvisosAsync(AppNotificationManager notificador,
        List<(long? Episodio, AvisoRecebido Anterior)> atualizacoes, long versao)
    {
        _atualizandoAvisos = true;
        try
        {
            var recebidas = await notificador.GetAllAsync();
            if (_notificador != notificador || versao != _versaoAlertas
                || DateTimeOffset.Now - _agora > AgendaNotificacoesAtraso.PausaEntreAvaliacoes)
                return;

            var ids = recebidas.Select(aviso => aviso.Id).ToHashSet();
            foreach (var (episodio, anterior) in atualizacoes)
            {
                // A fila ou o aviso com esta etiqueta pode ter mudado enquanto o Windows consultava a Central.
                if (ObterAviso(episodio) != anterior || ObterConteudo(episodio) is not { } conteudo) continue;
                if (!ids.Contains(anterior.Id))
                {
                    GuardarAviso(episodio, null);
                    continue;
                }
                if (conteudo == anterior.Conteudo) continue;

                var id = Exibir(conteudo, som: null, silencioso: true);
                GuardarAviso(episodio, new AvisoRecebido(id, conteudo));
            }
        }
        catch (Exception ex)
        {
            if (versao == _versaoAlertas) _proximaAtualizacao = _agora.AddSeconds(30);
            RegistrarFalha(ex);
        }
        finally { _atualizandoAvisos = false; }
    }

    private AvisoRecebido? ObterAviso(long? episodio)
    {
        if (episodio is not { } id) return _resumo;
        return _avisosIndividuais.TryGetValue(id, out var aviso) ? aviso : null;
    }

    private void GuardarAviso(long? episodio, AvisoRecebido? aviso)
    {
        if (episodio is not { } id) _resumo = aviso;
        else if (aviso is { } recebido) _avisosIndividuais[id] = recebido;
        else _avisosIndividuais.Remove(id);
    }

    private ConteudoAviso? ObterConteudo(long? episodio)
    {
        if (episodio is not { } id) return _elegiveis.Count == 0 ? null : ConteudoDoLembrete();

        var atraso = _elegiveis.FirstOrDefault(atraso => atraso.Episodio == id);
        return atraso is null ? null : ConteudoDoTicket(atraso);
    }

    private ConteudoAviso ConteudoDoTicket(AtrasoAtendimento atraso)
    {
        var ticket = atraso.Ticket;
        var identificacao = string.IsNullOrWhiteSpace(ticket.Empresa)
            ? ticket.Cliente : $"{ticket.Cliente} · {ticket.Empresa}";
        return new ConteudoAviso($"ServiceDesk · Ticket #{ticket.Id}", identificacao, atraso.Texto(_agora),
            GrupoTickets, ticket.Id.ToString("x", System.Globalization.CultureInfo.InvariantCulture));
    }

    private ConteudoAviso ConteudoDoLembrete()
    {
        var quantidade = _elegiveis.Count == 1 ? "1 ticket em atraso" : $"{_elegiveis.Count} tickets em atraso";
        return new ConteudoAviso($"ServiceDesk · {quantidade}", $"Espera mais antiga · {_elegiveis[0].Tempo(_agora)}",
            null, GrupoLembretes, EtiquetaLembrete);
    }

    private uint Exibir(ConteudoAviso conteudo, SomNotificacao? som, bool silencioso)
    {
        string[] linhas = conteudo.Estado is { } estado
            ? [conteudo.Titulo, conteudo.Detalhe, estado] : [conteudo.Titulo, conteudo.Detalhe];
        return Enviar(som, linhas, (conteudo.Grupo, conteudo.Etiqueta), silencioso);
    }

    internal void AvisarNovaVersao(Version versao, SomNotificacao? som)
    {
        try { Enviar(som, ["Nova versão do ServiceDesk", $"A versão {versao} está disponível em Configurações → Sobre."]); }
        catch (Exception ex) { RegistrarFalha(ex); }
    }

    internal async Task<bool> TestarAsync(SomNotificacao? som)
    {
        try
        {
            var notificador = ObterNotificadorAtivo();
            try { await notificador.RemoveByTagAndGroupAsync(EtiquetaTeste, GrupoTeste); }
            catch (COMException ex) { RegistrarFalha(ex); }
            var id = Enviar(som, ["ServiceDesk", "Notificação de teste"], (GrupoTeste, EtiquetaTeste));
            var limite = DateTimeOffset.UtcNow.AddSeconds(3);
            do
            {
                try
                {
                    var recebidas = await notificador.GetAllAsync();
                    if (recebidas.Any(aviso => aviso.Id == id)) return true;
                }
                catch (COMException ex)
                {
                    RegistrarFalha(ex);
                    return false;
                }
                await Task.Delay(200);
            } while (DateTimeOffset.UtcNow < limite && _notificador == notificador);

            // O Windows pode aceitar o envio antes de o aviso aparecer na Central.
            return false;
        }
        catch (Exception ex)
        {
            RegistrarFalha(ex);
            throw;
        }
    }

    internal static bool OuvirSom(SomNotificacao som)
    {
        var alias = som switch
        {
            SomNotificacao.Mensagem => "Notification.IM",
            SomNotificacao.Email => "Notification.Mail",
            SomNotificacao.Lembrete => "Notification.Reminder",
            SomNotificacao.Alarme => "Notification.Looping.Alarm",
            _ => "Notification.Default"
        };
        const uint aliasAssincronoSemSomAlternativo = 0x00010000 | 0x0001 | 0x0002;
        return PlaySound(alias, IntPtr.Zero, aliasAssincronoSemSomAlternativo);
    }

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string som, IntPtr modulo, uint opcoes);

    private string? VerificarPermissao()
    {
        if (_notificador is not { } notificador) return _erroRegistro ?? "Notificações do Windows indisponíveis.";
        try { return ErroPermissao(notificador.Setting); }
        catch (Exception ex)
        {
            var erro = $"Não foi possível verificar a permissão de notificação do Windows: {ex.Message}";
            if (UltimoErro != erro) RegistrarFalha(ex);
            return erro;
        }
    }

    private static string? ErroPermissao(AppNotificationSetting permissao) =>
        permissao == AppNotificationSetting.Enabled ? null
            : $"Permissão indisponível ({permissao}). Confira Configurações do Windows → Sistema → Notificações → ServiceDesk.";

    private AppNotificationManager ObterNotificadorAtivo()
    {
        var notificador = _notificador ?? throw new InvalidOperationException(_erroRegistro ?? "Notificações do Windows indisponíveis.");
        if (ErroPermissao(notificador.Setting) is { } erro) throw new InvalidOperationException(erro);
        return notificador;
    }

    private uint Enviar(SomNotificacao? som, string[] linhas, (string Grupo, string Etiqueta)? identificador = null,
        bool silencioso = false)
    {
        var notificador = ObterNotificadorAtivo();
        var construtor = new AppNotificationBuilder();
        foreach (var linha in linhas) construtor.AddText(linha);
        if (som is null) construtor.MuteAudio();
        else if (som != SomNotificacao.Padrao)
            construtor.SetAudioEvent(som switch
            {
                SomNotificacao.Mensagem => AppNotificationSoundEvent.IM,
                SomNotificacao.Email => AppNotificationSoundEvent.Mail,
                SomNotificacao.Lembrete => AppNotificationSoundEvent.Reminder,
                SomNotificacao.Alarme => AppNotificationSoundEvent.Alarm,
                _ => AppNotificationSoundEvent.Default
            });
        var aviso = construtor.BuildNotification();
        if (identificador is { } chave)
        {
            aviso.Group = chave.Grupo;
            aviso.Tag = chave.Etiqueta;
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
        LimparAlertasAtendimento();
        notificador.NotificationInvoked -= NotificacaoAcionada;
        notificador.Unregister();
    }
}
