using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ServiceDesk.Models;
using ServiceDesk.Services;
using Windows.System;

namespace ServiceDesk;

public sealed partial class MainPage : Page
{
    private readonly DispatcherTimer _relogio = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer _sincronizador = new();
    private readonly MonitoramentoService _monitoramento = new();
    private readonly ConfiguracaoAtualizacaoService _configuracaoAtualizacao = new();
    private readonly ServicoNotificacoes _notificacoes = new();
    private IReadOnlyList<AtendimentoMonitorado> _atendimentos = [];
    private bool _sincronizando;
    private bool _atualizacaoAutomaticaAtiva;
    private int _intervaloAtualizacaoMinutos;
    private bool _notificacoesAtivas;
    private int _limiteNotificacaoMinutos;
    private string _filtroStatus = "Em aberto";
    private AtendimentoMonitorado? _atendimentoSelecionado;

    public MainPage()
    {
        InitializeComponent();
        StatusComboBox.Items.Add("Todos");
        StatusComboBox.Items.Add("Em aberto");
        StatusComboBox.Items.Add("Adiado");
        StatusComboBox.SelectedItem = _filtroStatus;
        _relogio.Tick += (_, _) => AtualizarTemposExibidos();
        _relogio.Start();
        _sincronizador.Tick += async (_, _) => await AtualizarAsync();
        CarregarConfiguracaoAtualizacao();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e) => await AtualizarAsync();

    private async void AtualizarButton_Click(object sender, RoutedEventArgs e) => await AtualizarAsync();

    private async Task AtualizarAsync()
    {
        if (_sincronizando) return;
        _sincronizando = true;
        AtualizarButton.IsEnabled = false;
        AbrirButton.IsEnabled = false;
        StatusInfo.Severity = InfoBarSeverity.Informational;
        StatusInfo.Title = "Atualizando";
        StatusInfo.Message = "Consultando seus atendimentos no Chatwoot...";
        StatusInfo.IsOpen = true;

        try
        {
            var resultado = await _monitoramento.SincronizarAsync(CancellationToken.None);
            _atendimentos = resultado.Atendimentos;
            AplicarFiltros();
            NotificarAtendimentosPendentes();

            AbertosCountText.Text = _atendimentos.Count(item => !item.EstaAdiado).ToString();
            AdiadosCountText.Text = _atendimentos.Count(item => item.EstaAdiado).ToString();
            StatusInfo.Severity = InfoBarSeverity.Success;
            StatusInfo.Title = "Sincronização concluída";
            StatusInfo.Message = _atendimentos.Count == 0
                ? "Não há atendimentos em aberto ou adiados atribuídos a você."
                : $"{_atendimentos.Count} atendimento(s) atribuído(s) a você foram atualizados.";
        }
        catch (Exception ex)
        {
            StatusInfo.Severity = InfoBarSeverity.Error;
            StatusInfo.Title = "Não foi possível sincronizar";
            StatusInfo.Message = ex.Message;
        }
        finally
        {
            AtualizarButton.IsEnabled = true;
            _sincronizando = false;
        }
    }

    private void CarregarConfiguracaoAtualizacao()
    {
        var configuracao = _configuracaoAtualizacao.Carregar();
        _atualizacaoAutomaticaAtiva = configuracao.Ativa;
        _intervaloAtualizacaoMinutos = configuracao.IntervaloMinutos;
        _notificacoesAtivas = configuracao.NotificacoesAtivas;
        _limiteNotificacaoMinutos = configuracao.LimiteNotificacaoMinutos;
        AplicarConfiguracaoAtualizacao();
    }

    private void AplicarConfiguracaoAtualizacao()
    {
        _sincronizador.Stop();
        if (_atualizacaoAutomaticaAtiva)
        {
            _sincronizador.Interval = TimeSpan.FromMinutes(_intervaloAtualizacaoMinutos);
            _sincronizador.Start();
            AtualizacaoAutomaticaButton.Label = $"Automática: {_intervaloAtualizacaoMinutos} min";
        }
        else
        {
            AtualizacaoAutomaticaButton.Label = "Automática: desligada";
        }
    }

    private async void ConfiguracaoChatwootButton_Click(object sender, RoutedEventArgs e)
    {
        ConfiguracaoChatwoot configuracao;
        try { configuracao = CarregadorConfiguracaoChatwoot.Carregar(); }
        catch (Exception ex)
        {
            StatusInfo.Severity = InfoBarSeverity.Error;
            StatusInfo.Title = "Configuração do Chatwoot indisponível";
            StatusInfo.Message = ex.Message;
            StatusInfo.IsOpen = true;
            return;
        }

        var chave = new PasswordBox { Header = "Chave da API", Password = configuracao.Token, PasswordRevealMode = PasswordRevealMode.Hidden };
        var mostrarChave = new ToggleSwitch { Header = "Mostrar chave da API", IsOn = false };
        mostrarChave.Toggled += (_, _) => chave.PasswordRevealMode = mostrarChave.IsOn ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
        var usuario = new TextBox { Header = "ID do usuário", Text = configuracao.UsuarioId?.ToString() ?? string.Empty, PlaceholderText = "Ex.: 123" };
        var inbox = new TextBox { Header = "ID do Inbox", Text = configuracao.InboxId?.ToString() ?? string.Empty, PlaceholderText = "Ex.: 456" };
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(new TextBlock { Text = "A chave fica salva apenas na configuração local deste Windows. O Inbox restringe a fila consultada.", TextWrapping = TextWrapping.Wrap });
        conteudo.Children.Add(chave);
        conteudo.Children.Add(mostrarChave);
        conteudo.Children.Add(usuario);
        conteudo.Children.Add(inbox);
        var dialogo = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Integração Chatwoot",
            Content = conteudo,
            PrimaryButtonText = "Salvar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(chave.Password) || !long.TryParse(usuario.Text, out _) || !long.TryParse(inbox.Text, out _))
        {
            StatusInfo.Severity = InfoBarSeverity.Error;
            StatusInfo.Title = "Dados inválidos";
            StatusInfo.Message = "Informe uma chave da API e IDs numéricos para usuário e Inbox.";
            StatusInfo.IsOpen = true;
            return;
        }
        ConfiguracaoLocal.SalvarPropriedades(new Dictionary<string, string>
        {
            ["chatwoot.api-access-token"] = chave.Password,
            ["chatwoot.user-id"] = usuario.Text,
            ["chatwoot.inbox-id"] = inbox.Text
        });
        await AtualizarAsync();
    }

    private async void AtualizacaoAutomaticaButton_Click(object sender, RoutedEventArgs e)
    {
        var ativa = new ToggleSwitch { Header = "Atualizar automaticamente", IsOn = _atualizacaoAutomaticaAtiva };
        var intervalo = new NumberBox
        {
            Header = "Intervalo em minutos",
            Minimum = 1,
            Maximum = 120,
            Value = _intervaloAtualizacaoMinutos,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var notificacoes = new ToggleSwitch { Header = "Notificar atendimento sem interação", IsOn = _notificacoesAtivas };
        var limiteNotificacao = new NumberBox
        {
            Header = "Notificar após (minutos)", Minimum = 1, Maximum = 1440, Value = _limiteNotificacaoMinutos,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var conteudo = new StackPanel { Spacing = 16 };
        conteudo.Children.Add(new TextBlock { Text = "O ServiceDesk sincroniza apenas dados de leitura do Chatwoot.", TextWrapping = TextWrapping.Wrap });
        conteudo.Children.Add(ativa);
        conteudo.Children.Add(intervalo);
        conteudo.Children.Add(notificacoes);
        conteudo.Children.Add(limiteNotificacao);

        var dialogo = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Atualização automática",
            Content = conteudo,
            PrimaryButtonText = "Salvar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary) return;

        _atualizacaoAutomaticaAtiva = ativa.IsOn;
        _intervaloAtualizacaoMinutos = Math.Clamp((int)Math.Round(double.IsNaN(intervalo.Value) ? 5 : intervalo.Value), 1, 120);
        _notificacoesAtivas = notificacoes.IsOn;
        _limiteNotificacaoMinutos = Math.Clamp((int)Math.Round(double.IsNaN(limiteNotificacao.Value) ? 10 : limiteNotificacao.Value), 1, 1440);
        _configuracaoAtualizacao.Salvar(new ConfiguracaoAtualizacao(_atualizacaoAutomaticaAtiva, _intervaloAtualizacaoMinutos, _notificacoesAtivas, _limiteNotificacaoMinutos));
        AplicarConfiguracaoAtualizacao();
        NotificarAtendimentosPendentes();
    }

    private void PreencherLista()
    {
        AtendimentosList.Children.Clear();
        var visiveis = FiltrarAtendimentos();
        foreach (var atendimento in visiveis)
        {
            atendimento.AtualizarTempo(DateTimeOffset.Now);
            var botao = new Button
            {
                Tag = atendimento,
                Content = CriarCardAtendimento(atendimento),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Background = null,
                BorderThickness = new Thickness(0)
            };
            botao.Click += AtendimentoCard_Click;
            AtendimentosList.Children.Add(botao);
        }

        if (visiveis.Count == 0)
        {
            AtendimentosList.Children.Add(new TextBlock
            {
                Text = "Nenhum atendimento corresponde aos filtros selecionados.",
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(0, 24, 0, 24)
            });
        }
    }

    private Border CriarCardAtendimento(AtendimentoMonitorado atendimento)
    {
        var card = new Border { Padding = new Thickness(16), Margin = new Thickness(2), CornerRadius = new CornerRadius(8), Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Microsoft.UI.Xaml.Media.Brush };
        var conteudo = new Grid { RowSpacing = 6 };
        conteudo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        conteudo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        conteudo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        conteudo.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var cabecalho = new Grid();
        cabecalho.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cabecalho.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var cliente = new StackPanel { Spacing = 2 };
        cliente.Children.Add(new TextBlock { Text = atendimento.Cliente, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        cliente.Children.Add(new TextBlock { Text = atendimento.Assunto, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.Wrap, MaxLines = 2, Opacity = 0.72 });
        cabecalho.Children.Add(cliente);
        var situacao = new TextBlock { Text = atendimento.SituacaoText, VerticalAlignment = VerticalAlignment.Top, Opacity = 0.8 };
        Grid.SetColumn(situacao, 1);
        cabecalho.Children.Add(situacao);
        conteudo.Children.Add(cabecalho);

        var tempo = new Grid { ColumnSpacing = 16, Margin = new Thickness(0, 4, 0, 2) };
        tempo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tempo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var aberto = new StackPanel { Spacing = 1 };
        aberto.Children.Add(new TextBlock { Text = "CHAT ABERTO", Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] });
        aberto.Children.Add(new TextBlock { Text = atendimento.TempoChatAberto, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 19 });
        tempo.Children.Add(aberto);
        var atividade = new StackPanel { Spacing = 1 };
        atividade.Children.Add(new TextBlock { Text = "ÚLTIMA ATIVIDADE", Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] });
        atividade.Children.Add(new TextBlock { Text = atendimento.TempoSemInteracaoChat, FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 19 });
        Grid.SetColumn(atividade, 1);
        tempo.Children.Add(atividade);
        Grid.SetRow(tempo, 1);
        conteudo.Children.Add(tempo);

        var detalhes = new StackPanel { Spacing = 3, Opacity = 0.76 };
        detalhes.Children.Add(new TextBlock { Text = atendimento.UltimaInteracaoChatText });
        detalhes.Children.Add(new TextBlock { Text = atendimento.UltimaMensagemClienteText });
        detalhes.Children.Add(new TextBlock { Text = atendimento.MinhaUltimaAcaoText });
        Grid.SetRow(detalhes, 2);
        conteudo.Children.Add(detalhes);
        card.Child = conteudo;
        return card;
    }

    private List<AtendimentoMonitorado> FiltrarAtendimentos()
    {
        var busca = BuscaTextBox.Text?.Trim() ?? string.Empty;
        return _atendimentos
            .Where(item => _filtroStatus switch
            {
                "Em aberto" => !item.EstaAdiado,
                "Adiado" => item.EstaAdiado,
                _ => true
            })
            .Where(item => string.IsNullOrWhiteSpace(busca)
                || item.Cliente.Contains(busca, StringComparison.OrdinalIgnoreCase)
                || item.Telefone.Contains(busca, StringComparison.OrdinalIgnoreCase)
                || item.Empresa.Contains(busca, StringComparison.OrdinalIgnoreCase)
                || item.Ticket.Contains(busca, StringComparison.OrdinalIgnoreCase)
                || item.Assunto.Contains(busca, StringComparison.OrdinalIgnoreCase)
                || item.Id.ToString().Contains(busca, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.ReferenciaInteracao)
            .ToList();
    }

    private void AplicarFiltros()
    {
        AbrirButton.IsEnabled = false;
        _atendimentoSelecionado = null;
        PreencherLista();
    }

    private void BuscaTextBox_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltros();

    private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StatusComboBox.SelectedItem is string filtro) _filtroStatus = filtro;
        AplicarFiltros();
    }

    private void FilaTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilaTabs.SelectedItem is not TabViewItem { Tag: string filtro }) return;
        _filtroStatus = filtro;
        StatusComboBox.SelectedItem = filtro;
        AplicarFiltros();
    }

    private void AtualizarTemposExibidos()
    {
        if (_atendimentos.Count == 0) return;
        AplicarFiltros();
        NotificarAtendimentosPendentes();
    }

    private void NotificarAtendimentosPendentes()
    {
        if (!_notificacoesAtivas) return;
        var novosAlertas = _notificacoes.NotificarAtendimentosSemInteracao(_atendimentos, _limiteNotificacaoMinutos);
        if (novosAlertas.Count == 0) return;

        var primeiro = novosAlertas[0];
        AlertaInteracaoInfo.Title = novosAlertas.Count == 1 ? "Atendimento sem interação" : $"{novosAlertas.Count} atendimentos sem interação";
        AlertaInteracaoInfo.Message = novosAlertas.Count == 1
            ? $"{primeiro.Cliente} está sem interação há {primeiro.TempoSemInteracaoChat}."
            : $"{primeiro.Cliente} e outros atendimentos excederam o limite configurado.";
        AlertaInteracaoInfo.IsOpen = true;
    }

    private void AtendimentoCard_Click(object sender, RoutedEventArgs e)
    {
        _atendimentoSelecionado = (sender as FrameworkElement)?.Tag as AtendimentoMonitorado;
        AbrirButton.IsEnabled = _atendimentoSelecionado is not null;
    }

    private async void AbrirButton_Click(object sender, RoutedEventArgs e)
    {
        if (_atendimentoSelecionado is not { } atendimento) return;
        try
        {
            var configuracao = CarregadorConfiguracaoChatwoot.Carregar();
            await Launcher.LaunchUriAsync(new Uri($"{configuracao.BaseUrl}/app/accounts/{configuracao.ContaId}/conversations/{atendimento.Id}"));
        }
        catch (Exception ex)
        {
            StatusInfo.Severity = InfoBarSeverity.Error;
            StatusInfo.Title = "Não foi possível abrir o atendimento";
            StatusInfo.Message = ex.Message;
            StatusInfo.IsOpen = true;
        }
    }
}
