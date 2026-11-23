using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ServiceDesk.Models;
using ServiceDesk.Services;
using Windows.System;

namespace ServiceDesk;

public sealed partial class MainPage : Page
{
    private readonly DispatcherTimer _relogio = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _sincronizador = new();
    private readonly DispatcherTimer _atualizacaoPorEvento = new() { Interval = TimeSpan.FromSeconds(2) };
    private CancellationTokenSource? _tempoReal;
    private ConfiguracaoChatwoot? _configuracaoTempoReal;
    private bool _conectadoTempoReal;
    private bool _atualizacaoPendente;
    private readonly DispatcherTimer _aviso = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly MonitoramentoService _monitoramento = new();
    private readonly ConfiguracaoAtualizacaoService _configuracaoAtualizacao = new();
    private readonly PreferenciasInterfaceService _preferenciasService = new();
    private readonly FilaAlertasAtendimento _alertas = new();
    private PreferenciasInterface _preferencias = new();
    private IReadOnlyList<AtendimentoMonitorado> _atendimentos = [];
    private IReadOnlyList<AtendimentoMonitorado> _naoAtribuidos = [];
    private string? _erroNaoAtribuidos;
    private bool _podeVerNaoAtribuidos;
    private int _versaoConfiguracaoFila;
    private enum FilaTickets { Meus, Adiados, NaoAtribuidos, Resolvidos }
    private FilaTickets _fila;
    private bool ExibindoNaoAtribuidos => _fila == FilaTickets.NaoAtribuidos;
    private bool _sincronizando;
    private DateTimeOffset? _ultimaSincronizacao;
    private bool _exportando;
    private bool _interfaceIniciada;
    private bool _atualizacaoAutomaticaAtiva;
    private bool _preferirTempoReal;
    private int _intervaloAtualizacaoMinutos;
    private bool _notificacoesAtivas;
    private int _limiteNotificacaoMinutos;
    private string? _ultimoErroNotificacaoExibido;
    private int _colunas = 1;
    private Microsoft.UI.Xaml.Shapes.Ellipse? _indicadorNovaVersao;
    private bool _previaFlutuanteAtiva;
    private bool EmMetricas => TelaSecundaria.Content is MetricasPage;

    public MainPage()
    {
        InitializeComponent();
        PrepararNavegacao();
        ListaScroll.ViewChanged += (_, _) => VerificarFimDaLista();
        ListaScroll.SizeChanged += (_, _) => DispatcherQueue.TryEnqueue(VerificarFimDaLista);
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        Unloaded += (_, _) =>
        {
            _aviso.Stop();
            CancelarConsultaResolvidos();
        };
        _relogio.Tick += (_, _) => AtualizarTemposExibidos();
        _sincronizador.Tick += async (_, _) => await AtualizarAsync();
        _atualizacaoPorEvento.Tick += async (_, _) =>
        {
            _atualizacaoPorEvento.Stop();
            if (_sincronizando) _atualizacaoPendente = true;
            else await AtualizarAsync();
        };
        _aviso.Tick += (_, _) => { _aviso.Stop(); StatusInfo.IsOpen = false; };
        _interfaceIniciada = true;
        AplicarPreferenciasInterface();
        ActualThemeChanged += (_, _) => AplicarFiltros();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        AplicarPreferenciasInterface();
        _relogio.Start();
        IniciarVerificacaoDeVersao();
        CarregarConfiguracaoAtualizacao();
        if (Environment.GetEnvironmentVariable("SERVICEDESK_DIAGNOSTICO") == "1") ValidarFiltros();
        await Task.WhenAll(AtualizarAsync(),
            ExibindoResolvidos && _proximaPaginaResolvidos == 1 ? CarregarResolvidosAsync() : Task.CompletedTask);
    }
    private void MetricasButton_Click(object sender, RoutedEventArgs e)
    {
        if (EmMetricas) { AtualizarFiltrosVisuais(); return; }
        try
        {
            MostrarTelaSecundaria(new MetricasPage());
        }
        catch (Exception ex)
        {
            if (Environment.GetEnvironmentVariable("SERVICEDESK_DIAGNOSTICO") == "1")
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "ServiceDesk-inicializacao.log"), ex + Environment.NewLine);
            MostrarAviso("Não foi possível abrir as métricas", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void PrepararNavegacao()
    {
        void Configurar(ButtonBase botao, string nome, FontIcon icone)
        {
            var item = VisualServiceDesk.ItemLateral(icone, nome);
            botao.Content = item;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(botao, nome);
            ToolTipService.SetToolTip(botao, nome);
        }
        Configurar(MeusFiltro, "Meus tickets", VisualServiceDesk.Icone("\uE8BD"));
        Configurar(AdiadosFiltro, "Adiados", VisualServiceDesk.Icone("\uE823"));
        Configurar(ResolvidosFiltro, "Resolvidos", VisualServiceDesk.Icone("\uE73E"));
        Configurar(NaoAtribuidosFiltro, "Não atribuídos", VisualServiceDesk.Icone("\uE8B7"));
        Configurar(MetricasButton, "Métricas", VisualServiceDesk.Icone("\uE9D9"));
        Configurar(ConfiguracoesButton, "Configurações", VisualServiceDesk.Icone("\uE713"));
        var linha = (StackPanel)ConfiguracoesButton.Content;
        ConfiguracoesButton.Content = null;
        _indicadorNovaVersao = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = 8, Height = 8, Visibility = Visibility.Collapsed, IsHitTestVisible = false,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 115, 130))
        };
        var configuracao = new Grid();
        configuracao.Children.Add(linha);
        _indicadorNovaVersao.HorizontalAlignment = HorizontalAlignment.Right;
        _indicadorNovaVersao.VerticalAlignment = VerticalAlignment.Top;
        configuracao.Children.Add(_indicadorNovaVersao);
        ConfiguracoesButton.Content = configuracao;
    }

    private void Estrutura_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_interfaceIniciada) return;
        OrganizarAbas();
    }

    private void OrganizarAbas()
    {
        var ativa = TelaSecundaria.Content is Grid ? (ButtonBase)ConfiguracoesButton :
            EmMetricas ? MetricasButton : AbasDaFila().First(a => a.Fila == _fila).Botao;
        ButtonBase[] abas = [MeusFiltro, AdiadosFiltro, ResolvidosFiltro, NaoAtribuidosFiltro, MetricasButton, ConfiguracoesButton];
        MenuAbas.Items.Clear();
        double Medir(ButtonBase aba)
        {
            aba.Visibility = Visibility.Visible;
            aba.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            return aba.DesiredSize.Width + 3;
        }
        var estreita = Estrutura.ActualWidth < 240;
        var nomeAtivo = Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(ativa);
        MaisAbas.Content = estreita ? new TextBlock { Text = nomeAtivo + " ▾", TextTrimming = TextTrimming.CharacterEllipsis } : "Mais ▾";
        MaisAbas.MaxWidth = estreita ? Math.Max(32, Estrutura.ActualWidth - 8) : double.PositiveInfinity;
        var disponivel = Estrutura.ActualWidth - 8 - Medir(ativa);
        var outras = abas.Where(a => a != ativa && (a != NaoAtribuidosFiltro || _podeVerNaoAtribuidos)).ToArray();
        if (outras.Sum(Medir) > disponivel) disponivel -= Medir(MaisAbas);
        foreach (var aba in outras)
        {
            var largura = Medir(aba);
            if (!estreita && largura <= disponivel) { disponivel -= largura; continue; }
            aba.Visibility = Visibility.Collapsed;
            var item = new MenuFlyoutItem { Text = Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(aba) };
            item.Click += (_, e) =>
            {
                if (aba == ConfiguracoesButton) ConfiguracoesButton_Click(aba, e);
                else if (aba == MetricasButton) MetricasButton_Click(aba, e);
                else Filtro_Click(aba, e);
                DispatcherQueue.TryEnqueue(() => (aba.Visibility == Visibility.Visible ? aba : MaisAbas).Focus(FocusState.Keyboard));
            };
            MenuAbas.Items.Add(item);
        }
        if (estreita && TelaSecundaria.Content is null)
        {
            MenuAbas.Items.Insert(0, new MenuFlyoutSeparator());
            var atualizar = new MenuFlyoutItem { Text = "Atualizar" };
            atualizar.SetBinding(Control.IsEnabledProperty, new Microsoft.UI.Xaml.Data.Binding { Source = AtualizarButton, Path = new PropertyPath("IsEnabled") });
            atualizar.Click += AtualizarButton_Click;
            MenuAbas.Items.Insert(0, atualizar);
            if (_fila != FilaTickets.Adiados)
            {
                var filtros = new MenuFlyoutItem { Text = "Filtros" };
                filtros.Click += (_, _) => DispatcherQueue.TryEnqueue(() => FlyoutFiltros.ShowAt(MaisAbas));
                MenuAbas.Items.Insert(1, filtros);
            }
        }
        if (estreita) ativa.Visibility = Visibility.Collapsed;
        MaisAbas.Visibility = MenuAbas.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MostrarTelaSecundaria(FrameworkElement? tela)
    {
        TelaSecundaria.Content = tela;
        TelaSecundaria.Visibility = tela is null ? Visibility.Collapsed : Visibility.Visible;
        ConteudoFila.Visibility = tela is null ? Visibility.Visible : Visibility.Collapsed;
        AtualizarFiltrosVisuais();
    }

    private void ConteudoFila_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_interfaceIniciada) ReposicionarControles(e.NewSize.Width);
    }


    private void ReposicionarControles(double largura)
    {
        if (largura <= 0) return;
        var enxuto = largura < 440;
        ConteudoFila.Padding = enxuto ? new Thickness(8, 10, 8, 8)
            : largura < 760 ? new Thickness(12, 12, 12, 10) : new Thickness(20, 16, 20, 16);
        ConteudoFila.RowSpacing = enxuto ? 6 : 12;
        CabecalhoFila.ColumnSpacing = enxuto ? 4 : 12;
        Grid.SetColumnSpan((Grid)ResumoResolvidos.Parent, enxuto ? 3 : 1);
        Grid.SetRow(AtualizarButton, enxuto ? 1 : 0);
        Grid.SetRow(MenuFiltros, enxuto ? 1 : 0);
        AtualizarButton.Width = enxuto ? 34 : 44;
        var compacto = largura < (ExibindoResolvidos ? 560 : 350);
        if (compacto && FlyoutFiltros.Content is null)
        {
            CabecalhoFila.Children.Remove(ControlesFila);
            FlyoutFiltros.Content = ControlesFila;
        }
        else if (!compacto && FlyoutFiltros.Content is not null)
        {
            FlyoutFiltros.Hide();
            FlyoutFiltros.Content = null;
            CabecalhoFila.Children.Add(ControlesFila);
        }
        MenuFiltros.Visibility = compacto && _fila != FilaTickets.Adiados ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetRow(ControlesFila, enxuto && !compacto ? 1 : 0);
        Grid.SetColumn(ControlesFila, compacto ? 0 : 2);
        Grid.SetColumnSpan(ControlesFila, compacto ? 3 : 1);
        ControlesFila.HorizontalAlignment = compacto ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        StatusLabel.Visibility = enxuto ? Visibility.Collapsed : Visibility.Visible;
        BarraStatus.HorizontalAlignment = enxuto ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        BarraStatus.Orientation = enxuto ? Orientation.Vertical : Orientation.Horizontal;
        StatusTickets.HorizontalAlignment = enxuto ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        StatusTickets.Width = enxuto ? double.NaN : 150;

        var empilharResolvidos = largura < 475;
        BuscaResolvidos.Width = empilharResolvidos ? double.NaN : 145;
        BarraResolvidos.RowSpacing = empilharResolvidos ? 6 : 0;
        BarraResolvidos.ColumnSpacing = empilharResolvidos ? 0 : 8;
        Grid.SetColumnSpan(BuscaResolvidos, empilharResolvidos ? 3 : 1);
        Grid.SetRow(EscopoResolvidos, empilharResolvidos ? 1 : 0);
        Grid.SetColumn(EscopoResolvidos, empilharResolvidos ? 0 : 1);
        Grid.SetColumnSpan(EscopoResolvidos, empilharResolvidos ? 3 : 1);
        EscopoResolvidos.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetRow(MaisResolvidosButton, empilharResolvidos ? 2 : 0);
        Grid.SetColumn(MaisResolvidosButton, empilharResolvidos ? 0 : 2);
        Grid.SetColumnSpan(MaisResolvidosButton, empilharResolvidos ? 3 : 1);

        var faixa = Estrutura.ActualWidth < 240;
        AtualizarButton.Visibility = faixa ? Visibility.Collapsed : Visibility.Visible;
        if (faixa) MenuFiltros.Visibility = Visibility.Collapsed;
        ConteudoFila.Padding = faixa ? new Thickness(4) : ConteudoFila.Padding;
        MenuFiltros.Content = faixa ? new FontIcon { Glyph = "\uE71C", FontSize = 15 } : "Filtros";
        MenuFiltros.MinWidth = faixa ? 30 : 0;
        ToolTipService.SetToolTip(MenuFiltros, "Filtros");
        var resumoCompacto = largura < 520;
        GradeSituacao.ColumnSpacing = resumoCompacto ? 6 : 14;
        GradeSituacao.ColumnDefinitions[2].Width = resumoCompacto ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        SituacaoAtendimentos.Padding = enxuto ? new Thickness(9) : new Thickness(14);
        TituloSituacao.FontSize = enxuto ? 15 : 18;
        TotalAbertos.FontSize = TotalAdiados.FontSize = enxuto ? 22 : 28;
        Grid.SetColumnSpan(TituloSituacao, resumoCompacto ? 3 : 1);
        Grid.SetRow(ResumoAbertos, resumoCompacto ? 1 : 0);
        Grid.SetColumn(ResumoAbertos, resumoCompacto ? 0 : 1);
        Grid.SetRow(ResumoAdiados, resumoCompacto ? 1 : 0);
        Grid.SetColumn(ResumoAdiados, resumoCompacto ? 1 : 2);
        ResumoFaixa.Visibility = faixa ? Visibility.Visible : Visibility.Collapsed;
        ResumoAbertos.Visibility = ResumoAdiados.Visibility = TituloSituacao.Visibility = faixa ? Visibility.Collapsed : Visibility.Visible;
        if (faixa) SituacaoAtendimentos.Padding = new Thickness(5, 3, 5, 3);
        AtualizarEstadosDosCards(DateTimeOffset.Now);
    }
    private async void AtualizarButton_Click(object sender, RoutedEventArgs e)
    {
        if (ExibindoResolvidos)
        {
            ReiniciarResolvidos();
            await CarregarResolvidosAsync();
        }
        else await AtualizarAsync();
    }

    private async Task AtualizarAsync()
    {
        if (_sincronizando) return;
        _sincronizando = true;
        AtualizarDisponibilidadeDaAtualizacao();
        SincronizacaoText.Text = "Atualizando sua fila…";
        var versao = _versaoConfiguracaoFila;
        try
        {
            var configuracao = CarregadorConfiguracaoChatwoot.Carregar();
            var resultado = await _monitoramento.SincronizarAsync(configuracao, CancellationToken.None);
            if (versao != _versaoConfiguracaoFila) return;
            AcompanhamentoN1.PreservarEsperaObservada(resultado.Atendimentos, _atendimentos);
            AcompanhamentoN1.PreservarEsperaObservada(resultado.NaoAtribuidos, _naoAtribuidos);
            _atendimentos = resultado.Atendimentos;
            _naoAtribuidos = resultado.NaoAtribuidos;
            _erroNaoAtribuidos = resultado.ErroNaoAtribuidos;
            _podeVerNaoAtribuidos = resultado.PodeVerNaoAtribuidos;
            _ultimaSincronizacao = DateTimeOffset.Now;
            if (!_podeVerNaoAtribuidos && ExibindoNaoAtribuidos)
            {
                _fila = FilaTickets.Meus;
                ListaScroll.ChangeView(null, 0, null, disableAnimation: true);
                MeusFiltro.Focus(FocusState.Programmatic);
            }
            if (ExibindoResolvidos) AtualizarFiltrosVisuais();
            else AplicarFiltros();
            _aviso.Stop();
            if (!ExibindoResolvidos) StatusInfo.IsOpen = false;
            AtualizarTemposExibidos();
            AtualizarResumoDaFila();
        }
        catch (Exception ex)
        {
            if (versao != _versaoConfiguracaoFila) return;
            if (ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden })
            {
                LimparFila(preservarEpisodios: true);
            }
            _ultimaSincronizacao = null;
            AtualizarTemposExibidos();
            AtualizarFiltrosVisuais();
            SincronizacaoText.Text = "Atualização indisponível · a fila anterior pode estar desatualizada";
            if (!ExibindoResolvidos) MostrarAviso("Não foi possível atualizar", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            _sincronizando = false;
            AtualizarDisponibilidadeDaAtualizacao();
            if (versao != _versaoConfiguracaoFila)
            {
                _atualizacaoPendente = false;
                await AtualizarAsync();
            }
            else if (_atualizacaoPendente)
            {
                _atualizacaoPendente = false;
                AgendarAtualizacaoPorEvento();
            }
        }
    }

    private void LimparFila(bool preservarEpisodios = false)
    {
        _atendimentos = _naoAtribuidos = [];
        _podeVerNaoAtribuidos = false;
        _erroNaoAtribuidos = null;
        _ultimaSincronizacao = null;
        var app = (App)Application.Current;
        app.PainelDeAtrasos.Ocultar();
        if (!preservarEpisodios)
        {
            _alertas.Limpar();
            app.Notificacoes.LimparAlertasAtendimento();
        }
        AlertaInteracaoInfo.IsOpen = false;
        if (ExibindoNaoAtribuidos) _fila = FilaTickets.Meus;
        SincronizacaoText.Text = "Atualizando sua fila…";
        AplicarFiltros();
    }

    private void AtualizarResumoDaFila()
    {
        if (_ultimaSincronizacao is not { } horario) return;
        SincronizacaoText.Text = ExibindoNaoAtribuidos && _erroNaoAtribuidos is not null
            ? $"Fila de não atribuídos indisponível. {_erroNaoAtribuidos}"
            : $"Atualizado às {horario:HH:mm}";
    }

    private void AtualizarDisponibilidadeDaAtualizacao() =>
        AtualizarButton.IsEnabled = ExibindoResolvidos ? _consultaResolvidos is null : !_sincronizando;

    private void MostrarAviso(string titulo, string mensagem, InfoBarSeverity severidade)
    {
        _aviso.Stop();
        StatusInfo.Title = titulo;
        StatusInfo.Message = mensagem;
        StatusInfo.Severity = severidade;
        StatusInfo.IsOpen = true;
        if (severidade == InfoBarSeverity.Success) _aviso.Start();
    }

    private void CarregarConfiguracaoAtualizacao()
    {
        var configuracao = _configuracaoAtualizacao.Carregar();
        _atualizacaoAutomaticaAtiva = configuracao.Ativa;
        _preferirTempoReal = configuracao.TempoReal;
        _intervaloAtualizacaoMinutos = configuracao.IntervaloMinutos;
        _notificacoesAtivas = configuracao.NotificacoesAtivas;
        _limiteNotificacaoMinutos = configuracao.LimiteNotificacaoMinutos;
        ConfigurarTempoReal();
        AtualizarIntervaloDeSincronizacao();
    }

    private void ConfigurarTempoReal()
    {
        ConfiguracaoChatwoot? configuracao = null;
        if (_atualizacaoAutomaticaAtiva && _preferirTempoReal)
        {
            try { configuracao = CarregadorConfiguracaoChatwoot.Carregar(); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException) { }
        }
        if (_configuracaoTempoReal == configuracao) return;

        var anterior = _tempoReal;
        _tempoReal = null;
        anterior?.Cancel();
        _atualizacaoPorEvento.Stop();
        _atualizacaoPendente = false;
        _conectadoTempoReal = false;
        _configuracaoTempoReal = configuracao;
        if (configuracao is null) return;

        var sessao = new CancellationTokenSource();
        _tempoReal = sessao;
        _ = ObservarChatwootAsync(configuracao, sessao);
    }

    private async Task ObservarChatwootAsync(ConfiguracaoChatwoot configuracao, CancellationTokenSource sessao)
    {
        var dispatcher = DispatcherQueue;
        using (sessao)
        {
            await new ChatwootTempoReal(configuracao).AcompanharAsync(
                conectado => dispatcher.TryEnqueue(() =>
                {
                    if (_tempoReal != sessao) return;
                    _conectadoTempoReal = conectado;
                    AtualizarIntervaloDeSincronizacao();
                    if (conectado) AgendarAtualizacaoPorEvento();
                }),
                alteracao => dispatcher.TryEnqueue(() =>
                {
                    if (_tempoReal != sessao) return;
                    if (configuracao.InboxIds.Count == 0 || alteracao.CaixaId is null
                        || configuracao.InboxIds.Contains(alteracao.CaixaId.Value)
                        || _atendimentos.Any(ticket => ticket.Id == alteracao.ConversaId))
                        AgendarAtualizacaoPorEvento();
                }),
                sessao.Token);
        }
    }

    private void AgendarAtualizacaoPorEvento()
    {
        if (_tempoReal is not null && !_atualizacaoPorEvento.IsEnabled)
            _atualizacaoPorEvento.Start();
    }

    private void AtualizarIntervaloDeSincronizacao()
    {
        if (!_atualizacaoAutomaticaAtiva)
        {
            _sincronizador.Stop();
            return;
        }

        var minutos = _preferirTempoReal ? (_conectadoTempoReal ? 5 : 1) : _intervaloAtualizacaoMinutos;
        var intervalo = TimeSpan.FromMinutes(minutos);
        if (_sincronizador.IsEnabled && _sincronizador.Interval == intervalo) return;

        _sincronizador.Stop();
        _sincronizador.Interval = intervalo;
        _sincronizador.Start();
    }

    private void AplicarPreferenciasInterface()
    {
        _preferencias = _preferenciasService.Carregar();
        var tema = TemaDaPreferencia(_preferencias.Tema);
        RequestedTheme = tema;
        if (XamlRoot?.Content is FrameworkElement raiz) raiz.RequestedTheme = tema;
        AtualizarFiltrosVisuais();
    }

    private static ElementTheme TemaDaPreferencia(string? tema) =>
        tema switch { "Claro" => ElementTheme.Light, "Escuro" => ElementTheme.Dark, _ => ElementTheme.Default };

    private void AtualizarFiltrosVisuais()
    {
        if (!_interfaceIniciada) return;
        NaoAtribuidosFiltro.Visibility = _podeVerNaoAtribuidos ? Visibility.Visible : Visibility.Collapsed;
        BarraStatus.Visibility = ExibindoResolvidos || _fila == FilaTickets.Adiados ? Visibility.Collapsed : Visibility.Visible;
        SituacaoAtendimentos.Visibility = ExibindoResolvidos ? Visibility.Collapsed : Visibility.Visible;
        var tickets = ExibindoNaoAtribuidos ? _naoAtribuidos : _atendimentos;
        var adiados = tickets.Count(ticket => ticket.EstaAdiado);
        var temContagens = _ultimaSincronizacao is not null && (!ExibindoNaoAtribuidos || _erroNaoAtribuidos is null);
        TotalAbertos.Text = temContagens ? (tickets.Count - adiados).ToString() : "—";
        TotalAdiados.Text = temContagens ? adiados.ToString() : "—";
        ResumoFaixa.Text = $"Abertos  {TotalAbertos.Text}\nAdiados  {TotalAdiados.Text}";
        foreach (var (filtro, fila) in AbasDaFila())
            VisualServiceDesk.Selecionar(filtro, fila == _fila && TelaSecundaria.Content is null, _preferencias.Cor, ActualTheme);
        VisualServiceDesk.Selecionar(MetricasButton, EmMetricas, _preferencias.Cor, ActualTheme);
        VisualServiceDesk.Selecionar(ConfiguracoesButton, TelaSecundaria.Content is Grid, _preferencias.Cor, ActualTheme);
        RepetirConsulta.Visibility = ExibindoResolvidos && _erroResolvidos is not null ? Visibility.Visible : Visibility.Collapsed;
        OrganizarAbas();
    }

    private (ToggleButton Botao, FilaTickets Fila)[] AbasDaFila() =>
        [(MeusFiltro, FilaTickets.Meus), (AdiadosFiltro, FilaTickets.Adiados),
         (ResolvidosFiltro, FilaTickets.Resolvidos), (NaoAtribuidosFiltro, FilaTickets.NaoAtribuidos)];

    private void ValidarFiltros()
    {
        var anterior = _fila;
        foreach (var (filtro, fila) in AbasDaFila().Where(aba => aba.Botao.Visibility == Visibility.Visible))
        {
            _fila = fila;
            AtualizarFiltrosVisuais();
            filtro.ApplyTemplate();
            foreach (var estado in new[] { "Checked", "CheckedPointerOver", "CheckedPressed" })
            {
                VisualStateManager.GoToState(filtro, estado, false);
                filtro.UpdateLayout();
                if (VisualTreeHelper.GetChild(filtro, 0) is not Border raiz
                    || raiz.Background is not SolidColorBrush cor || !cor.Color.Equals(((SolidColorBrush)filtro.Background).Color))
                    throw new InvalidOperationException("O filtro perdeu a cor de destaque em " + estado);
            }
        }
        _fila = anterior;
        AtualizarFiltrosVisuais();
    }

    private void Lista_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_interfaceIniciada || e.NewSize.Width <= 0) return;
        const double larguraMinimaCard = 260;
        var espaco = AtendimentosList.ColumnSpacing;
        var colunas = Math.Clamp((int)((e.NewSize.Width + espaco) / (larguraMinimaCard + espaco)), 1, 5);
        if (colunas == _colunas) return;
        _colunas = colunas;
        PreencherLista();
    }

    private void PreencherLista()
    {
        var focado = XamlRoot is { } raiz ? FocusManager.GetFocusedElement(raiz) as Control : null;
        var idFocado = focado?.FocusState == FocusState.Keyboard
            ? AutomationProperties.GetAutomationId(focado) : null;
        ReconstruirLista();
        if (string.IsNullOrEmpty(idFocado)) return;

        foreach (var card in _cardsAtendimento)
        {
            var alvo = AutomationProperties.GetAutomationId(card.Controle) == idFocado ? (Control)card.Controle
                : AutomationProperties.GetAutomationId(card.Menu) == idFocado ? card.Menu : null;
            if (alvo is null) continue;
            alvo.Focus(FocusState.Keyboard);
            break;
        }
    }

    private void ReconstruirLista()
    {
        _cardsAtendimento.Clear();
        AtendimentosList.Children.Clear();
        AtendimentosList.RowDefinitions.Clear();
        AtendimentosList.ColumnDefinitions.Clear();
        for (var i = 0; i < _colunas; i++) AtendimentosList.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (ExibindoResolvidos)
        {
            var visiveis = _resolvidos.Values.Where(item => item.CorrespondeBusca(BuscaResolvidos.Text))
                .OrderByDescending(item => item.UltimaAtividadeEm).ThenByDescending(item => item.Id).ToList();
            AdicionarCards(visiveis);
            return;
        }

        if (ExibindoNaoAtribuidos && _erroNaoAtribuidos is not null) return;
        var tickets = ExibindoNaoAtribuidos ? _naoAtribuidos : _atendimentos;
        var porStatus = tickets.ToLookup(item => item.EstaAdiado);
        var adiados = porStatus[true].OrderBy(item => item.AdiadoAte ?? DateTimeOffset.MaxValue).ThenBy(item => item.Id).ToList();
        if (_fila == FilaTickets.Adiados)
        {
            AdicionarBlocoDeStatus("Adiados", adiados, "Nenhum ticket adiado.");
            return;
        }
        if (StatusTickets.SelectedIndex != 2)
        {
            var abertos = porStatus[false].Select(ticket => (Ticket: ticket, Acompanhamento: AcompanhamentoN1.DoTicket(ticket)))
                .OrderBy(item => item.Acompanhamento?.Estado != EstadoN1.ClienteAguarda)
                .ThenBy(item => item.Acompanhamento?.Referencia ?? item.Ticket.ReferenciaInteracao)
                .ThenBy(item => item.Ticket.Id).Select(item => item.Ticket).ToList();
            AdicionarBlocoDeStatus("Em aberto", abertos, "Nenhum ticket em aberto.");
        }
        if (StatusTickets.SelectedIndex != 1) AdicionarBlocoDeStatus("Adiados", adiados, "Nenhum ticket adiado.");
    }

    private void AdicionarBlocoDeStatus(string titulo, IReadOnlyList<AtendimentoMonitorado> tickets, string vazio)
    {
        var cabecalho = new TextBlock
        {
            Text = titulo,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, AtendimentosList.RowDefinitions.Count == 0 ? 0 : 8, 0, 0)
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHeadingLevel(cabecalho, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level1);
        AdicionarLinhaDaLista(cabecalho);
        if (tickets.Count == 0)
            AdicionarLinhaDaLista(new TextBlock { Text = vazio, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
        else
            AdicionarCards(tickets);
    }

    private void AdicionarLinhaDaLista(FrameworkElement elemento)
    {
        Grid.SetRow(elemento, AtendimentosList.RowDefinitions.Count);
        Grid.SetColumnSpan(elemento, _colunas);
        AtendimentosList.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AtendimentosList.Children.Add(elemento);
    }

    private void AdicionarCards(IReadOnlyList<AtendimentoMonitorado> tickets)
    {
        var primeiraLinha = AtendimentosList.RowDefinitions.Count;
        var agora = DateTimeOffset.Now;
        for (var i = 0; i < tickets.Count; i++)
        {
            var atendimento = tickets[i];
            if (!ExibindoResolvidos) atendimento.AtualizarTempo(agora);
            if (i % _colunas == 0) AtendimentosList.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var card = CriarCardAtendimento(atendimento);
            Grid.SetRow(card, primeiraLinha + i / _colunas);
            Grid.SetColumn(card, i % _colunas);
            AtendimentosList.Children.Add(card);
        }
    }

    private void AplicarFiltros()
    {
        if (!_interfaceIniciada) return;
        AtualizarFiltrosVisuais();
        PreencherLista();
    }
    private async void Filtro_Click(object sender, RoutedEventArgs e)
    {
        var anterior = _fila;
        MostrarTelaSecundaria(null);
        _fila = AbasDaFila().First(aba => ReferenceEquals(aba.Botao, sender)).Fila;
        BarraResolvidos.Visibility = ResumoResolvidos.Visibility = ExibindoResolvidos ? Visibility.Visible : Visibility.Collapsed;
        ReposicionarControles(ConteudoFila.ActualWidth);
        SincronizacaoText.Visibility = ExibindoResolvidos ? Visibility.Collapsed : Visibility.Visible;
        if (!ExibindoResolvidos) CancelarConsultaResolvidos();
        AtualizarDisponibilidadeDaAtualizacao();
        AplicarFiltros();
        if (anterior != _fila)
        {
            AtualizarResumoDaFila();
            ListaScroll.ChangeView(null, 0, null, disableAnimation: true);
            if (ExibindoResolvidos)
            {
                StatusInfo.IsOpen = false;
                if (_proximaPaginaResolvidos == 1) await CarregarResolvidosAsync();
            }
        }
    }

    private void StatusTickets_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_interfaceIniciada || ExibindoResolvidos) return;
        PreencherLista();
        ListaScroll.ChangeView(null, 0, null, disableAnimation: true);
    }
    private void AtualizarTemposExibidos()
    {
        var agora = DateTimeOffset.Now;
        AtualizarEstadosDosCards(agora);
        var dadosRecentes = FilaAlertasAtendimento.DadosRecentes(_ultimaSincronizacao, agora,
            _atualizacaoAutomaticaAtiva ? _sincronizador.Interval : null);
        var atualizacao = _alertas.Atualizar(_atendimentos, agora, _limiteNotificacaoMinutos,
            AlertaInteracaoInfo.IsOpen, dadosRecentes, _notificacoesAtivas);
        if (atualizacao.Atual is { } alerta)
        {
            AlertaInteracaoInfo.Title = $"Ticket #{alerta.Ticket.Id}";
            AlertaInteracaoInfo.Message = $"{alerta.Ticket.Cliente} · {alerta.Texto(agora)}";
        }
        AlertaInteracaoInfo.IsOpen = atualizacao.Atual is not null;

        var app = (App)Application.Current;
        var elegiveis = atualizacao.Elegiveis;
        if (!_previaFlutuanteAtiva && _notificacoesAtivas && _preferencias.MostrarCardsFlutuantes && elegiveis.Count > 0)
        {
            const int maximoCards = 3;
            var cards = elegiveis.Take(maximoCards).Select(atraso => (atraso.Ticket.TituloPainel(_preferencias.CamposPainel), $"Atraso · {atraso.Tempo(agora)}")).ToArray();
            app.PainelDeAtrasos.Atualizar(cards, elegiveis.Count - cards.Length, ActualTheme == ElementTheme.Light, _preferencias);
        }
        else app.PainelDeAtrasos.Ocultar();

        var notificacoes = app.Notificacoes;
        notificacoes.NotificarAtendimentos(elegiveis, atualizacao.EpisodiosAbertos, agora, _preferencias.SomSelecionado,
            _preferencias.IntervaloLembreteMinutos, dadosRecentes && _notificacoesAtivas && _preferencias.NotificacoesSistema);
        if (notificacoes.UltimoErro is { } erro && erro != _ultimoErroNotificacaoExibido)
            MostrarAviso("Notificações do Windows", erro, InfoBarSeverity.Warning);
        _ultimoErroNotificacaoExibido = notificacoes.UltimoErro;
    }

    private async Task ExportarAtendimentoAsync(AtendimentoMonitorado atendimento, bool copiar)
    {
        if (_exportando) return;
        _exportando = true;
        _aviso.Stop();
        try
        {
            using var chatwoot = new ChatwootSyncService(CarregadorConfiguracaoChatwoot.Carregar());
            var exportacao = await chatwoot.ExportarAtendimentoAsync(atendimento, CancellationToken.None);
            if (copiar)
            {
                var dadosCopiados = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dadosCopiados.SetText(exportacao.Texto);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dadosCopiados);
                MostrarAviso("Texto copiado", $"Ticket #{atendimento.Id} copiado.", InfoBarSeverity.Success);
                return;
            }
            var downloads = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(PastaDownloads.Obter());
            var pasta = await downloads.CreateFolderAsync($"Ticket-{atendimento.Id}", Windows.Storage.CreationCollisionOption.GenerateUniqueName);
            try
            {
                MostrarAviso("Exportando chat", $"Baixando histórico e {exportacao.Anexos.Count} anexo(s)...", InfoBarSeverity.Informational);
                await PacoteAtendimento.SalvarPastaAsync(pasta.Path, exportacao, CancellationToken.None, $"Ticket-{atendimento.Id}.md");
            }
            catch
            {
                await pasta.DeleteAsync(Windows.Storage.StorageDeleteOption.PermanentDelete);
                throw;
            }
            MostrarAviso("Chat exportado", $"Salvo em {pasta.Path}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            MostrarAviso("Não foi possível exportar", ex.Message, InfoBarSeverity.Error);
        }
        finally { _exportando = false; }
    }

    private async Task AbrirAtendimentoAsync(AtendimentoMonitorado atendimento)
    {
        try
        {
            var configuracao = CarregadorConfiguracaoChatwoot.Carregar();
            await Launcher.LaunchUriAsync(new Uri($"{configuracao.BaseUrl}/app/accounts/{configuracao.ContaId}/conversations/{atendimento.Id}"));
        }
        catch (Exception ex)
        {
            MostrarAviso("Não foi possível abrir o ticket", ex.Message, InfoBarSeverity.Error);
        }
    }
}
