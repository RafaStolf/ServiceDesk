using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

/// <summary>Exibe alertas locais uma vez por atendimento até a próxima interação relevante.</summary>
public sealed class ServicoNotificacoes
{
    private readonly Dictionary<long, DateTimeOffset> _referenciasNotificadas = [];

    public IReadOnlyList<AtendimentoMonitorado> NotificarAtendimentosSemInteracao(IEnumerable<AtendimentoMonitorado> atendimentos, int limiteMinutos)
    {
        var novosAlertas = new List<AtendimentoMonitorado>();
        var agora = DateTimeOffset.Now;
        foreach (var atendimento in atendimentos.Where(item => !item.EstaAdiado && agora - item.ReferenciaInteracao >= TimeSpan.FromMinutes(limiteMinutos)))
        {
            if (_referenciasNotificadas.TryGetValue(atendimento.Id, out var referencia) && referencia == atendimento.ReferenciaInteracao) continue;
            try
            {
                Exibir("ServiceDesk: conversa sem mensagem", $"Última mensagem de {atendimento.Cliente}: {atendimento.TempoSemMensagemPublica}.");
            }
            catch (Exception)
            {
                // O painel permanece funcional se o Windows bloquear notificações para esta instalação.
            }
            _referenciasNotificadas[atendimento.Id] = atendimento.ReferenciaInteracao;
            novosAlertas.Add(atendimento);
        }
        return novosAlertas;
    }

    private static void Exibir(string titulo, string mensagem)
    {
        var xml = new XmlDocument();
        xml.LoadXml($"<toast><visual><binding template=\"ToastGeneric\"><text>{Escapar(titulo)}</text><text>{Escapar(mensagem)}</text></binding></visual></toast>");
        ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(xml));
    }

    private static string Escapar(string texto) => System.Security.SecurityElement.Escape(texto) ?? string.Empty;
}
