using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using ServiceDesk.Services;

namespace ServiceDesk;

public sealed partial class MainPage
{
    private readonly AtualizacaoAplicativoService _atualizador = new();
    private readonly DispatcherTimer _consultaDeVersao = new() { Interval = TimeSpan.FromHours(4) };
    private AtualizacaoAplicativo? _novaVersao;
    private Version? _ultimaVersaoNotificada;

    private void IniciarVerificacaoDeVersao()
    {
        if (_consultaDeVersao.IsEnabled || Environment.GetEnvironmentVariable("SERVICEDESK_DIAGNOSTICO") == "1") return;
        _consultaDeVersao.Tick += async (_, _) => await VerificarNovaVersaoAsync();
        _consultaDeVersao.Start();
        _ = VerificarNovaVersaoAsync();
    }

    private async Task VerificarNovaVersaoAsync()
    {
        AtualizacaoAplicativo? disponivel;
        try { disponivel = await _atualizador.VerificarAsync(CancellationToken.None); }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException
            or InvalidOperationException or System.Text.Json.JsonException or KeyNotFoundException) { return; }

        RegistrarVersaoDisponivel(disponivel, notificar: true);
    }

    private void RegistrarVersaoDisponivel(AtualizacaoAplicativo? disponivel, bool notificar)
    {
        _novaVersao = disponivel;
        if (_indicadorNovaVersao is { } indicador)
            indicador.Visibility = disponivel is null ? Visibility.Collapsed : Visibility.Visible;
        var rotulo = disponivel is null ? "Configurações" : $"Configurações · versão {disponivel.Versao} disponível";
        AutomationProperties.SetName(ConfiguracoesButton, rotulo);
        ToolTipService.SetToolTip(ConfiguracoesButton, rotulo);

        if (disponivel is not null && disponivel.Versao != _ultimaVersaoNotificada)
        {
            _ultimaVersaoNotificada = disponivel.Versao;
            if (notificar && _preferencias.NotificacoesSistema)
                ((App)Application.Current).Notificacoes.AvisarNovaVersao(disponivel.Versao, _preferencias.SomSelecionado);
        }
    }
}
