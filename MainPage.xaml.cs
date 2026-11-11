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
    private int _colunas = 1;
    private string _filtroStatus = "Em aberto";

    public MainPage()
    {
        InitializeComponent();
        if (Environment.GetEnvironmentVariable("SERVICEDESK_DIAGNOSTICO") == "1"
            && !CriarPreviaExportacao("Cabeçalho\r\nMensagem final").Text.Contains("Mensagem final"))
            throw new InvalidOperationException("A prévia truncou o histórico.");
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
        await AtualizarAsync();
    }
    private async void AtualizarButton_Click(object sender, RoutedEventArgs e) => await AtualizarAsync();

    private async Task AtualizarAsync()
    {
        if (_sincronizando) return;
        _sincronizando = true;
        AtualizarButton.IsEnabled = false;
        SincronizacaoText.Text = "Atualizando sua fila…";
        try
        {
            var resultado = await _monitoramento.SincronizarAsync(CancellationToken.None);
            _atendimentos = resultado.Atendimentos;
            AplicarFiltros();
            NotificarAtendimentosPendentes();
            _aviso.Stop();
            StatusInfo.IsOpen = false;
            SincronizacaoText.Text = $"Atualizado às {DateTime.Now:HH:mm} · {_atendimentos.Count} atendimento(s)";
        }
        catch (Exception ex)
        {
            SincronizacaoText.Text = "Atualização indisponível · a fila anterior pode estar desatualizada";
            MostrarAviso("Não foi possível atualizar", ex.Message, InfoBarSeverity.Error);
        }
        finally { AtualizarButton.IsEnabled = true; _sincronizando = false; }
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
            var vazio = new TextBlock { Text = "Nenhum atendimento corresponde aos filtros.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 24, 0, 24) };
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
        cabecalho.Children.Add(new TextBlock { Text = $"Atendimento #{atendimento.Id}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var opcoes = new Button { Content = new FontIcon { Glyph = "\uE712" }, Padding = new Thickness(8), MinWidth = 32, MinHeight = 32 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(opcoes, $"Opções do atendimento {atendimento.Id}");
        ToolTipService.SetToolTip(opcoes, "Opções do atendimento");
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
        var contato = new TextBlock { Text = atendimento.Cliente, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTipService.SetToolTip(contato, atendimento.Cliente);
        conteudo.Children.Add(contato);
        var empresa = new TextBlock { Text = string.IsNullOrWhiteSpace(atendimento.Empresa) ? "Empresa não informada" : atendimento.Empresa, FontSize = 16, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = 0.8 };
        ToolTipService.SetToolTip(empresa, empresa.Text);
        conteudo.Children.Add(empresa);
        conteudo.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(50, 128, 128, 128)), Margin = new Thickness(0, 4, 0, 2) });
        conteudo.Children.Add(new TextBlock { Text = "Última interação", Opacity = 0.7 });
        var tempo = new TextBlock { FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        tempo.SetBinding(TextBlock.TextProperty, new Binding { Source = atendimento, Path = new PropertyPath(nameof(AtendimentoMonitorado.TempoUltimaInteracaoAgente)), Mode = BindingMode.OneWay });
        conteudo.Children.Add(tempo);
        conteudo.Children.Add(new TextBlock { Text = atendimento.SituacaoText, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
        card.Child = conteudo;
        return card;
    }

    private List<AtendimentoMonitorado> FiltrarAtendimentos()
    {
        var busca = BuscaTextBox.Text?.Trim() ?? string.Empty;
        return _atendimentos.Where(item => _filtroStatus switch { "Em aberto" => !item.EstaAdiado, "Adiado" => item.EstaAdiado, _ => true })
            .Where(item => string.IsNullOrWhiteSpace(busca) || item.Cliente.Contains(busca, StringComparison.OrdinalIgnoreCase)
                || item.Empresa.Contains(busca, StringComparison.OrdinalIgnoreCase) || item.Telefone.Contains(busca, StringComparison.OrdinalIgnoreCase)
                || item.Id.ToString().Contains(busca, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.ReferenciaInteracao).ThenBy(item => item.Id).ToList();
    }

    private void AplicarFiltros()
    {
        if (!_interfaceIniciada) return;
        AtualizarFiltrosVisuais();
        PreencherLista();
    }
    private void BuscaTextBox_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltros();
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
        if (novosAlertas.Count == 0) return;
        var primeiro = novosAlertas[0];
        AlertaInteracaoInfo.Title = novosAlertas.Count == 1 ? $"Atendimento #{primeiro.Id}" : $"{novosAlertas.Count} atendimentos";
        AlertaInteracaoInfo.Message = $"{primeiro.Cliente} · Última interação {primeiro.TempoUltimaInteracaoAgente}";
        AlertaInteracaoInfo.IsOpen = true;
    }

    private static TextBox CriarPreviaExportacao(string texto)
    {
        // Habilite múltiplas linhas antes de atribuir o histórico: o controle
        // em modo de uma linha descarta o conteúdo após a primeira quebra.
        var previa = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 350 };
        previa.Text = texto;
        return previa;
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
                MostrarAviso("Texto copiado", $"Atendimento #{atendimento.Id} copiado.", InfoBarSeverity.Success);
                return;
            }
            var previa = CriarPreviaExportacao(exportacao.Texto);
            var conteudo = new StackPanel { Spacing = 12 };
            conteudo.Children.Add(new TextBlock
            {
                Text = "Revise o histórico e salve como Markdown (.md) para anexar no ChatGPT. Com a opção de mídias marcada, escolha uma pasta para salvar o .md e os arquivos juntos. O .md sozinho contém apenas o texto e as referências aos anexos; áudio/vídeo podem exigir conversão.",
                TextWrapping = TextWrapping.Wrap
            });
            conteudo.Children.Add(previa);
            var incluirMidias = new CheckBox { Content = "Salvar também imagens, vídeos e áudios na mesma pasta do .md", IsChecked = true };
            conteudo.Children.Add(incluirMidias);
            var dialogo = new ContentDialog
            {
                XamlRoot = XamlRoot, Title = $"Exportar atendimento #{atendimento.Id}", Content = conteudo,
                PrimaryButtonText = "Copiar texto", SecondaryButtonText = "Salvar arquivo", CloseButtonText = "Cancelar"
            };
            var resultado = await dialogo.ShowAsync();
            if (resultado == ContentDialogResult.None) return;
            if (resultado == ContentDialogResult.Primary)
            {
                var dados = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dados.SetText(previa.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dados);
            }
            else
            {
                if (incluirMidias.IsChecked == true)
                {
                    var seletor = new Windows.Storage.Pickers.FolderPicker();
                    seletor.FileTypeFilter.Add("*");
                    WinRT.Interop.InitializeWithWindow.Initialize(seletor,
                        WinRT.Interop.WindowNative.GetWindowHandle(((App)Application.Current).MainWindow));
                    var pasta = await seletor.PickSingleFolderAsync();
                    if (pasta is null) return;
                    var pastaAtendimento = await pasta.CreateFolderAsync($"Atendimento-{atendimento.Id}", Windows.Storage.CreationCollisionOption.GenerateUniqueName);
                    var destino = pastaAtendimento.Path;
                    try
                    {
                        StatusInfo.Title = "Salvando histórico e mídias";
                        StatusInfo.Message = $"Baixando {exportacao.Anexos.Count} anexo(s)...";
                        StatusInfo.Severity = InfoBarSeverity.Informational;
                        StatusInfo.IsOpen = true;
                        await PacoteAtendimento.SalvarPastaAsync(destino, exportacao with { Texto = previa.Text }, CancellationToken.None, $"Atendimento-{atendimento.Id}.md");
                    }
                    catch
                    {
                        Directory.Delete(destino, recursive: true);
                        throw;
                    }
                }
                else
                {
                    var seletor = new Windows.Storage.Pickers.FileSavePicker { SuggestedFileName = $"Atendimento-{atendimento.Id}" };
                    seletor.FileTypeChoices.Add("Markdown", new List<string> { ".md" });
                    WinRT.Interop.InitializeWithWindow.Initialize(seletor,
                        WinRT.Interop.WindowNative.GetWindowHandle(((App)Application.Current).MainWindow));
                    var arquivo = await seletor.PickSaveFileAsync();
                    if (arquivo is null) return;
                    await Windows.Storage.FileIO.WriteTextAsync(arquivo, previa.Text);
                }
            }
            MostrarAviso(resultado == ContentDialogResult.Primary ? "Texto copiado" : "Arquivo salvo", $"Atendimento #{atendimento.Id}.", InfoBarSeverity.Success);
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
            MostrarAviso("Não foi possível abrir o atendimento", ex.Message, InfoBarSeverity.Error);
        }
    }
}
