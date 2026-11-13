using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk;

public sealed partial class MainPage
{
    private readonly Dictionary<long, AtendimentoMonitorado> _resolvidos = new();
    private CancellationTokenSource? _consultaResolvidos;
    private int _proximaPaginaResolvidos = 1;
    private int _historicosIndisponiveis;
    private bool _temMaisResolvidos = true;
    private string? _erroResolvidos;

    private bool ExibindoResolvidos => _filtroStatus == "Resolvidos";

    private void NavegacaoTickets_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_interfaceIniciada) return;

        const double larguraMinimaParaMesmaLinha = 880;
        var mesmaLinha = e.NewSize.Width >= larguraMinimaParaMesmaLinha;
        var linha = mesmaLinha ? 0 : 1;
        if (Grid.GetRow(BarraResolvidos) == linha) return;

        var colunas = mesmaLinha ? 1 : 2;
        Grid.SetColumnSpan(AbasTickets, colunas);
        Grid.SetRow(BarraResolvidos, linha);
        Grid.SetColumn(BarraResolvidos, mesmaLinha ? 1 : 0);
        Grid.SetColumnSpan(BarraResolvidos, colunas);
        BarraResolvidos.Margin = mesmaLinha ? new Thickness(0) : new Thickness(0, 8, 0, 0);
    }

    private async void EscopoResolvidos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_interfaceIniciada || !ExibindoResolvidos) return;
        ReiniciarResolvidos();
        await CarregarResolvidosAsync();
    }

    private async void MaisResolvidosButton_Click(object sender, RoutedEventArgs e) => await CarregarResolvidosAsync();

    private void BuscaResolvidos_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_interfaceIniciada || !ExibindoResolvidos) return;
        PreencherLista();
        ListaScroll.ChangeView(null, 0, null, disableAnimation: true);
        if (_consultaResolvidos is null && _erroResolvidos is null)
            ResumoResolvidos.Text = DescreverResolvidos();
    }

    private void ReiniciarResolvidos()
    {
        CancelarConsultaResolvidos();
        _resolvidos.Clear();
        _proximaPaginaResolvidos = 1;
        _historicosIndisponiveis = 0;
        _temMaisResolvidos = true;
        _erroResolvidos = null;
        if (!ExibindoResolvidos) return;
        PreencherLista();
        ListaScroll.ChangeView(null, 0, null, disableAnimation: true);
    }

    private void CancelarConsultaResolvidos()
    {
        var consulta = _consultaResolvidos;
        if (consulta is null) return;
        _consultaResolvidos = null;
        consulta.Cancel();
        AtualizarControlesResolvidos();
        ResumoResolvidos.Text = DescreverResolvidos();
    }

    private async Task CarregarResolvidosAsync()
    {
        if (_consultaResolvidos is not null || !_temMaisResolvidos) return;
        using var consulta = new CancellationTokenSource();
        _consultaResolvidos = consulta;
        _erroResolvidos = null;
        AtualizarControlesResolvidos();
        ResumoResolvidos.Text = "Consultando tickets resolvidos…";
        try
        {
            using var chatwoot = new ChatwootSyncService(CarregadorConfiguracaoChatwoot.Carregar());
            var pagina = await chatwoot.BuscarResolvidosAsync(EscopoResolvidos.SelectedIndex == 0, _proximaPaginaResolvidos, consulta.Token);
            if (_consultaResolvidos != consulta) return;

            foreach (var atendimento in pagina.Atendimentos) _resolvidos[atendimento.Id] = atendimento;
            _historicosIndisponiveis += pagina.HistoricosIndisponiveis;
            _proximaPaginaResolvidos++;
            _temMaisResolvidos = pagina.TemMais;
            PreencherLista();
            ResumoResolvidos.Text = DescreverResolvidos();
        }
        catch (Exception ex)
        {
            if (_consultaResolvidos == consulta)
            {
                _erroResolvidos = $"Não foi possível carregar os resolvidos. {ex.Message}";
                ResumoResolvidos.Text = _erroResolvidos;
            }
        }
        finally
        {
            if (_consultaResolvidos == consulta)
            {
                _consultaResolvidos = null;
                AtualizarControlesResolvidos();
            }
        }
    }

    private void AtualizarControlesResolvidos()
    {
        AtualizarDisponibilidadeDaAtualizacao();
        MaisResolvidosButton.IsEnabled = _consultaResolvidos is null;
        MaisResolvidosButton.Visibility = _temMaisResolvidos && _proximaPaginaResolvidos > 1
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private string DescreverResolvidos()
    {
        var texto = _resolvidos.Count switch
        {
            0 when _temMaisResolvidos => "Nenhum ticket encontrado nas páginas consultadas.",
            0 => "Nenhum ticket resolvido encontrado.",
            1 => "1 ticket resolvido carregado.",
            _ => $"{_resolvidos.Count} tickets resolvidos carregados."
        };
        var termo = BuscaResolvidos.Text;
        if (_resolvidos.Count > 0 && !string.IsNullOrWhiteSpace(termo))
        {
            var encontrados = _resolvidos.Values.Count(item => item.CorrespondeBusca(termo));
            texto = encontrados == 0
                ? "Nenhum resultado nos tickets carregados."
                : $"{encontrados} de {_resolvidos.Count} tickets carregados.";
        }
        if (_historicosIndisponiveis > 0)
            texto += $" Históricos indisponíveis no Chatwoot: {_historicosIndisponiveis}.";
        return texto;
    }
}
