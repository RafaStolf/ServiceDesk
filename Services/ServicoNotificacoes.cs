using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

/// <summary>Alerta uma vez por conversa e última interação conhecida do agente atribuído.</summary>
public sealed class ServicoNotificacoes
{
    public const string IdAplicativo = "Centrium.ServiceDesk";
    private readonly Dictionary<long, DateTimeOffset> _referenciasNotificadas = [];
    private readonly Dictionary<long, DateTimeOffset> _referenciasWindows = [];
    private readonly Dictionary<long, DateTimeOffset> _tentativasWindows = [];
    public string? UltimoErro { get; private set; }

    public IReadOnlyList<AtendimentoMonitorado> NotificarAtendimentosSemInteracao(IEnumerable<AtendimentoMonitorado> atendimentos,
        int limiteMinutos, PreferenciasInterface preferencias)
    {
        var novosAlertas = new List<AtendimentoMonitorado>();
        var agora = DateTimeOffset.Now;
        foreach (var atendimento in atendimentos.Where(item => !item.EstaAdiado && item.UltimaInteracaoAgenteEm is { }
            && item.ReferenciaInteracao != DateTimeOffset.MaxValue && agora - item.ReferenciaInteracao >= TimeSpan.FromMinutes(limiteMinutos)))
        {
            atendimento.AtualizarTempo(agora);
            if (preferencias.NotificacoesSistema
                && (!_referenciasWindows.TryGetValue(atendimento.Id, out var enviada) || enviada != atendimento.ReferenciaInteracao)
                && (!_tentativasWindows.TryGetValue(atendimento.Id, out var tentativa) || agora - tentativa >= TimeSpan.FromMinutes(5)))
            {
                _tentativasWindows[atendimento.Id] = agora;
                try
                {
                    Exibir(atendimento, preferencias.SomNotificacoes);
                    _referenciasWindows[atendimento.Id] = atendimento.ReferenciaInteracao;
                    UltimoErro = null;
                }
                catch (Exception ex) { UltimoErro = $"Notificação do Windows não enviada: {ex.Message}"; }
            }
            if (_referenciasNotificadas.TryGetValue(atendimento.Id, out var referencia) && referencia == atendimento.ReferenciaInteracao) continue;
            _referenciasNotificadas[atendimento.Id] = atendimento.ReferenciaInteracao;
            novosAlertas.Add(atendimento);
        }
        return novosAlertas;
    }

    public void Testar(bool som)
    {
        Exibir(new AtendimentoMonitorado { Cliente = "Teste de notificação do ServiceDesk" }, som, teste: true);
    }

    private static void Exibir(AtendimentoMonitorado atendimento, bool som, bool teste = false)
    {
        var titulo = teste ? "ServiceDesk · Teste de notificação" : $"ServiceDesk · Ticket #{atendimento.Id}";
        var identificacao = string.IsNullOrWhiteSpace(atendimento.Empresa) ? atendimento.Cliente : $"{atendimento.Cliente} · {atendimento.Empresa}";
        var mensagem = teste ? "As notificações do ServiceDesk estão chegando ao Windows." : $"Última interação {atendimento.TempoUltimaInteracaoAgente}";
        var audio = som ? "" : "<audio silent=\"true\"/>";
        var xml = new XmlDocument();
        // Sem botões, links, argumentos de lançamento ou ações para o Chatwoot.
        xml.LoadXml($"<toast><visual><binding template=\"ToastGeneric\"><text>{Escapar(titulo)}</text><text>{Escapar(identificacao)}</text><text>{Escapar(mensagem)}</text></binding></visual>{audio}</toast>");
        ToastNotifier notificador;
        try
        {
            _ = Windows.ApplicationModel.Package.Current.Id.FullName;
            notificador = ToastNotificationManager.CreateToastNotifier();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // O instalador registra esse ID no atalho do menu Iniciar da versão portátil.
            notificador = ToastNotificationManager.CreateToastNotifier(IdAplicativo);
        }
        if (notificador.Setting != NotificationSetting.Enabled)
            throw new InvalidOperationException($"Permissão indisponível ({notificador.Setting}). Confira Configurações do Windows → Sistema → Notificações → ServiceDesk.");
        var notificacao = new ToastNotification(xml);
        notificacao.Failed += (_, args) =>
        {
            try
            {
                var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceDesk");
                Directory.CreateDirectory(pasta);
                File.AppendAllText(Path.Combine(pasta, "notificacoes.log"), $"{DateTimeOffset.Now:O} Falha de entrega: {args.ErrorCode}\n");
            }
            catch (Exception) { /* Diagnóstico não deve interromper a aplicação. */ }
        };
        notificador.Show(notificacao);
    }

    private static string Escapar(string texto) => System.Security.SecurityElement.Escape(texto) ?? string.Empty;
}
