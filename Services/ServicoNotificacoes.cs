using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

/// <summary>Alerta uma vez por conversa e última interação conhecida do agente atribuído.</summary>
public sealed class ServicoNotificacoes
{
    public const string IdAplicativo = "Centrium.ServiceDesk";
    private readonly Dictionary<long, DateTimeOffset> _referenciasNotificadas = [];

    public IReadOnlyList<AtendimentoMonitorado> NotificarAtendimentosSemInteracao(IEnumerable<AtendimentoMonitorado> atendimentos,
        int limiteMinutos, PreferenciasInterface preferencias)
    {
        var novosAlertas = new List<AtendimentoMonitorado>();
        var agora = DateTimeOffset.Now;
        foreach (var atendimento in atendimentos.Where(item => !item.EstaAdiado && item.UltimaInteracaoAgenteEm is { }
            && item.ReferenciaInteracao != DateTimeOffset.MaxValue && agora - item.ReferenciaInteracao >= TimeSpan.FromMinutes(limiteMinutos)))
        {
            if (_referenciasNotificadas.TryGetValue(atendimento.Id, out var referencia) && referencia == atendimento.ReferenciaInteracao) continue;
            atendimento.AtualizarTempo(agora);
            if (preferencias.NotificacoesSistema)
            {
                try { Exibir(atendimento, preferencias.SomNotificacoes); }
                catch (Exception) { /* O alerta dentro do aplicativo permanece disponível. */ }
            }
            _referenciasNotificadas[atendimento.Id] = atendimento.ReferenciaInteracao;
            novosAlertas.Add(atendimento);
        }
        return novosAlertas;
    }

    private static void Exibir(AtendimentoMonitorado atendimento, bool som)
    {
        var titulo = $"ServiceDesk · Atendimento #{atendimento.Id}";
        var identificacao = string.IsNullOrWhiteSpace(atendimento.Empresa) ? atendimento.Cliente : $"{atendimento.Cliente} · {atendimento.Empresa}";
        var mensagem = $"Última interação {atendimento.TempoUltimaInteracaoAgente}";
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
        notificador.Show(new ToastNotification(xml));
    }

    private static string Escapar(string texto) => System.Security.SecurityElement.Escape(texto) ?? string.Empty;
}
