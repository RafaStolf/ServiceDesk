using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using ServiceDesk.Models;
using ServiceDesk.Services;
using Windows.System;

namespace ServiceDesk;

public sealed partial class MainPage : Page
{
    private readonly DispatcherTimer _relogio = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer _sincronizador = new();
    private readonly DispatcherTimer _aviso = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly MonitoramentoService _monitoramento = new();
    private readonly ConfiguracaoAtualizacaoService _configuracaoAtualizacao = new();
    private readonly PreferenciasInterfaceService _preferenciasService = new();
    private readonly ServicoNotificacoes _notificacoes = new();
    private PreferenciasInterface _preferencias = new();
    private IReadOnlyList<AtendimentoMonitorado> _atendimentos = [];
    private bool _sincronizando;
    private bool _exportando;
    private bool _interfaceIniciada;
    private bool _atualizacaoAutomaticaAtiva;
    private int _intervaloAtualizacaoMinutos;
    private bool _notificacoesAtivas;
    private int _limiteNotificacaoMinutos;
    private string? _ultimoErroNotificacaoExibido;
    private int _colunas = 1;
    private string _filtroStatus = "Em aberto";
    private Frame? _quadroMetricas;

    public MainPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        Unloaded += (_, _) => { _relogio.Stop(); _sincronizador.Stop(); _aviso.Stop(); };
        _relogio.Tick += (_, _) => AtualizarTemposExibidos();
        _relogio.Start();
        _sincronizador.Tick += async (_, _) => await AtualizarAsync();
        _aviso.Tick += (_, _) => { _aviso.Stop(); StatusInfo.IsOpen = false; };
        _interfaceIniciada = true;
        AplicarPreferenciasInterface();
        ActualThemeChanged += (_, _) => AtualizarFiltrosVisuais();
        CarregarConfiguracaoAtualizacao();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        AplicarPreferenciasInterface();
        _relogio.Start();
        CarregarConfiguracaoAtualizacao();
        if (Environment.GetEnvironmentVariable("SERVICEDESK_DIAGNOSTICO") == "1") ValidarFiltros();
        await AtualizarAsync();
    }
    private void MetricasButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sincronizando) return;
        try
        {
            var quadro = _quadroMetricas ??= Frame;
            quadro.Content = new MetricasPage(() => quadro.Content = this,
                new OpcoesMetricas(RequestedTheme, CorDestaque(), (Style)Resources["FiltroStyle"]));
        }
        catch (Exception ex)
        {
            if (Environment.GetEnvironmentVariable("SERVICEDESK_DIAGNOSTICO") == "1")
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "ServiceDesk-inicializacao.log"), ex + Environment.NewLine);
            MostrarAviso("Não foi possível abrir as métricas", ex.Message, InfoBarSeverity.Error);
        }
    }
    private async void AtualizarButton_Click(object sender, RoutedEventArgs e) => await AtualizarAsync();

    private async Task AtualizarAsync()
    {
        if (_sincronizando) return;
        _sincronizando = true;
        AtualizarButton.IsEnabled = MetricasButton.IsEnabled = false;
        SincronizacaoText.Text = "Atualizando sua fila…";
        try
        {
            var resultado = await _monitoramento.SincronizarAsync(CancellationToken.None);
            _atendimentos = resultado.Atendimentos;
            AplicarFiltros();
            NotificarAtendimentosPendentes();
            _aviso.Stop();
            StatusInfo.IsOpen = false;
            SincronizacaoText.Text = $"Atualizado às {DateTime.Now:HH:mm} · {_atendimentos.Count} ticket(s)";
        }
        catch (Exception ex)
        {
            SincronizacaoText.Text = "Atualização indisponível · a fila anterior pode estar desatualizada";
            MostrarAviso("Não foi possível atualizar", ex.Message, InfoBarSeverity.Error);
        }
        finally { AtualizarButton.IsEnabled = MetricasButton.IsEnabled = true; _sincronizando = false; }
    }

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
        _intervaloAtualizacaoMinutos = configuracao.IntervaloMinutos;
        _notificacoesAtivas = configuracao.NotificacoesAtivas;
        _limiteNotificacaoMinutos = configuracao.LimiteNotificacaoMinutos;
        _sincronizador.Stop();
        if (_atualizacaoAutomaticaAtiva)
        {
            _sincronizador.Interval = TimeSpan.FromMinutes(_intervaloAtualizacaoMinutos);
            _sincronizador.Start();
        }
    }

    private void AplicarPreferenciasInterface()
    {
        _preferencias = _preferenciasService.Carregar();
        var tema = _preferencias.Tema switch { "Claro" => ElementTheme.Light, "Escuro" => ElementTheme.Dark, _ => ElementTheme.Default };
        RequestedTheme = tema;
        if (XamlRoot?.Content is FrameworkElement raiz) raiz.RequestedTheme = tema;
        AtualizarFiltrosVisuais();
    }

    private SolidColorBrush CorDestaque() => new(_preferencias.Cor switch
    {
        "Verde" => Windows.UI.Color.FromArgb(255, 0, 119, 93),
        "Roxo" => Windows.UI.Color.FromArgb(255, 109, 76, 196),
        "Laranja" => Windows.UI.Color.FromArgb(255, 169, 72, 0),
        "Vermelho" => Windows.UI.Color.FromArgb(255, 180, 50, 50),
        _ => Windows.UI.Color.FromArgb(255, 45, 110, 210)
    });

    private void AtualizarFiltrosVisuais()
    {
        if (!_interfaceIniciada) return;
        AbertosFiltro.Content = $"Em aberto ({_atendimentos.Count(x => !x.EstaAdiado)})";
        AdiadosFiltro.Content = $"Adiados ({_atendimentos.Count(x => x.EstaAdiado)})";
        TodosFiltro.Content = $"Todos ({_atendimentos.Count})";
        foreach (var filtro in new[] { AbertosFiltro, AdiadosFiltro, TodosFiltro })
        {
            var ativo = filtro.Tag as string == _filtroStatus;
            filtro.IsChecked = ativo;
            filtro.Background = ativo ? CorDestaque() : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            filtro.Foreground = new SolidColorBrush(ativo || ActualTheme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black);
        }
    }

    private void ValidarFiltros()
    {
        var anterior = _filtroStatus;
        foreach (var filtro in new[] { AbertosFiltro, AdiadosFiltro, TodosFiltro })
        {
            _filtroStatus = (string)filtro.Tag;
            AtualizarFiltrosVisuais();
            filtro.ApplyTemplate();
            foreach (var estado in new[] { "Checked", "CheckedPointerOver", "CheckedPressed" })
            {
                VisualStateManager.GoToState(filtro, estado, false);
                filtro.UpdateLayout();
                if (VisualTreeHelper.GetChild(filtro, 0) is not Border raiz
                    || raiz.Background is not SolidColorBrush cor || !cor.Color.Equals(CorDestaque().Color))
                    throw new InvalidOperationException("O filtro perdeu a cor de destaque em " + estado);
            }
        }
        _filtroStatus = anterior;
        AtualizarFiltrosVisuais();
    }

    private void Lista_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var colunas = Math.Clamp((int)(e.NewSize.Width / 360), 1, 4);
        if (colunas == _colunas || !_interfaceIniciada) return;
        _colunas = colunas;
        PreencherLista();
    }

    private void PreencherLista()
    {
        AtendimentosList.Children.Clear();
        AtendimentosList.RowDefinitions.Clear();
        AtendimentosList.ColumnDefinitions.Clear();
        for (var i = 0; i < _colunas; i++) AtendimentosList.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var visiveis = FiltrarAtendimentos();
        for (var i = 0; i < visiveis.Count; i++)
        {
            var atendimento = visiveis[i];
            atendimento.AtualizarTempo(DateTimeOffset.Now);
            if (i % _colunas == 0) AtendimentosList.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var card = CriarCardAtendimento(atendimento);
            Grid.SetRow(card, i / _colunas);
            Grid.SetColumn(card, i % _colunas);
            AtendimentosList.Children.Add(card);
        }
        if (visiveis.Count == 0)
        {
            var vazio = new TextBlock { Text = "Nenhum ticket corresponde aos filtros.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 24, 0, 24) };
            Grid.SetColumnSpan(vazio, _colunas);
            AtendimentosList.Children.Add(vazio);
        }
    }

    private Border CriarCardAtendimento(AtendimentoMonitorado atendimento)
    {
        var card = new Border { Style = (Style)Resources["AtendimentoCardStyle"] };
        var conteudo = new StackPanel { Spacing = 8 };
        var cabecalho = new Grid { ColumnSpacing = 8 };
        cabecalho.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cabecalho.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cabecalho.Children.Add(new TextBlock { Text = $"Ticket #{atendimento.Id}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var opcoes = new Button { Content = new FontIcon { Glyph = "\uE712" }, Padding = new Thickness(8), MinWidth = 40, MinHeight = 40 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(opcoes, $"Opções do ticket {atendimento.Id}");
        ToolTipService.SetToolTip(opcoes, "Opções do ticket");
        var menu = new MenuFlyout();
        void Acao(string texto, Func<Task> executar)
        {
            var item = new MenuFlyoutItem { Text = texto };
            item.Click += async (_, _) => await executar();
            menu.Items.Add(item);
        }
        Acao("Copiar texto", () => ExportarAtendimentoAsync(atendimento, copiar: true));
        Acao("Exportar chat", () => ExportarAtendimentoAsync(atendimento, copiar: false));
        Acao("Abrir no Chatwoot", () => AbrirAtendimentoAsync(atendimento));
        opcoes.Flyout = menu;
        Grid.SetColumn(opcoes, 1);
        cabecalho.Children.Add(opcoes);
        conteudo.Children.Add(cabecalho);
        var contato = new TextBlock { Text = atendimento.Cliente, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        ToolTipService.SetToolTip(contato, atendimento.Cliente);
        conteudo.Children.Add(contato);
        var empresa = new TextBlock { Text = string.IsNullOrWhiteSpace(atendimento.Empresa) ? "Empresa não informada" : atendimento.Empresa, FontSize = 16, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
        ToolTipService.SetToolTip(empresa, empresa.Text);
        conteudo.Children.Add(empresa);
        var linhaEmail = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 2, 0, 2) };
        linhaEmail.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        linhaEmail.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        linhaEmail.Children.Add(new FontIcon { Glyph = "\uE715", FontSize = 16, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0) });
        var email = new TextBlock { Text = atendimento.EmailText, FontSize = 14, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = false };
        if (!atendimento.TemEmail)
            email.Foreground = new SolidColorBrush(ActualTheme == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(255, 255, 153, 153)
                : Windows.UI.Color.FromArgb(255, 176, 0, 32));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(email, $"E-mail do contato: {atendimento.EmailText}");
        Grid.SetColumn(email, 1);
        linhaEmail.Children.Add(email);
        conteudo.Children.Add(linhaEmail);
        conteudo.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(50, 128, 128, 128)), Margin = new Thickness(0, 4, 0, 2) });
        conteudo.Children.Add(new TextBlock { Text = "Última interação", Opacity = 0.7 });
        var tempo = new TextBlock { FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        tempo.SetBinding(TextBlock.TextProperty, new Binding { Source = atendimento, Path = new PropertyPath(nameof(AtendimentoMonitorado.TempoUltimaInteracao)), Mode = BindingMode.OneWay });
        conteudo.Children.Add(tempo);
        if (_filtroStatus == "Todos" || atendimento.EstaAdiado)
            conteudo.Children.Add(new TextBlock { Text = atendimento.SituacaoText, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
        card.Child = conteudo;
        return card;
    }

    private List<AtendimentoMonitorado> FiltrarAtendimentos()
    {
        return _atendimentos.Where(item => _filtroStatus switch { "Em aberto" => !item.EstaAdiado, "Adiado" => item.EstaAdiado, _ => true })
            .OrderBy(item => item.ReferenciaInteracao).ThenBy(item => item.Id).ToList();
    }

    private void AplicarFiltros()
    {
        if (!_interfaceIniciada) return;
        AtualizarFiltrosVisuais();
        PreencherLista();
    }
    private void Filtro_Click(object sender, RoutedEventArgs e)
    {
        _filtroStatus = (sender as FrameworkElement)?.Tag as string ?? "Em aberto";
        AplicarFiltros();
    }
    private void AtualizarTemposExibidos()
    {
        foreach (var atendimento in _atendimentos) atendimento.AtualizarTempo(DateTimeOffset.Now);
        NotificarAtendimentosPendentes();
    }
    private void NotificarAtendimentosPendentes()
    {
        if (!_notificacoesAtivas) return;
        var novosAlertas = _notificacoes.NotificarAtendimentosSemInteracao(_atendimentos, _limiteNotificacaoMinutos, _preferencias);
        if (_notificacoes.UltimoErro is { } erro && erro != _ultimoErroNotificacaoExibido)
        {
            _ultimoErroNotificacaoExibido = erro;
            MostrarAviso("Notificações do Windows", erro, InfoBarSeverity.Warning);
        }
        else if (_notificacoes.UltimoErro is null) _ultimoErroNotificacaoExibido = null;
        if (novosAlertas.Count == 0) return;
        var primeiro = novosAlertas[0];
        AlertaInteracaoInfo.Title = novosAlertas.Count == 1 ? $"Ticket #{primeiro.Id}" : $"{novosAlertas.Count} tickets";
        AlertaInteracaoInfo.Message = $"{primeiro.Cliente} · Última interação {primeiro.TempoUltimaInteracaoAgente}";
        AlertaInteracaoInfo.IsOpen = true;
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
