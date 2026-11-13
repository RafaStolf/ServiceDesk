using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ServiceDesk.Services;

namespace ServiceDesk;

public sealed partial class MainPage
{
    private async void ConfiguracoesButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyDictionary<string, string> valores;
        try { valores = ConfiguracaoLocal.CarregarPropriedades(); }
        catch (InvalidOperationException) { valores = new Dictionary<string, string>(); }
        string Valor(string chave) => valores.TryGetValue(chave, out var valor) ? valor : "";
        var url = new TextBox { Header = "Endereço do Chatwoot", Text = Valor("chatwoot.base-url"), PlaceholderText = "https://chatwoot.suaempresa.com" };
        var conta = new TextBox { Header = "ID da conta", Text = Valor("chatwoot.account-id") };
        var chave = new PasswordBox { Header = "Chave da API", Password = Valor("chatwoot.api-access-token"), PasswordRevealMode = PasswordRevealMode.Hidden };
        var mostrar = new ToggleSwitch { Header = "Mostrar chave da API" };
        mostrar.Toggled += (_, _) => chave.PasswordRevealMode = mostrar.IsOn ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
        var inbox = new TextBox { Header = "ID da caixa de entrada", Text = Valor("chatwoot.inbox-id") };
        var iniciarWindows = new ToggleSwitch { Header = "Iniciar com o Windows", IsOn = InicializacaoWindowsService.IniciarComWindows };
        var segundoPlano = new ToggleSwitch { Header = "Continuar em segundo plano ao fechar a janela", IsOn = InicializacaoWindowsService.ManterEmSegundoPlano };
        var instrucaoGeral = new TextBlock { Text = "Ao entrar no Windows, o ServiceDesk inicia junto ao relógio. Clique no ícone para abrir. Use o menu do ícone → Sair para encerrar completamente.", TextWrapping = TextWrapping.Wrap };
        var ativa = new ToggleSwitch { Header = "Atualizar tickets automaticamente", IsOn = _atualizacaoAutomaticaAtiva };
        var tempoReal = new ToggleSwitch { Header = "Atualização em tempo real", IsOn = _preferirTempoReal, IsEnabled = ativa.IsOn };
        var intervalo = new NumberBox { Header = "Intervalo fixo (minutos)", Minimum = 1, Maximum = 120, Value = _intervaloAtualizacaoMinutos,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, IsEnabled = ativa.IsOn && !tempoReal.IsOn };
        ativa.Toggled += (_, _) =>
        {
            tempoReal.IsEnabled = ativa.IsOn;
            intervalo.IsEnabled = ativa.IsOn && !tempoReal.IsOn;
        };
        tempoReal.Toggled += (_, _) => intervalo.IsEnabled = ativa.IsOn && !tempoReal.IsOn;
        var notificacoes = new ToggleSwitch { Header = "Ativar alertas de última interação", IsOn = _notificacoesAtivas };
        var limite = new NumberBox { Header = "Alertar após quantos minutos", Minimum = 1, Maximum = 1440, Value = _limiteNotificacaoMinutos, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var tema = new ComboBox { Header = "Tema", ItemsSource = new[] { "Sistema", "Claro", "Escuro" }, SelectedItem = _preferencias.Tema, HorizontalAlignment = HorizontalAlignment.Stretch };
        var cor = new ComboBox { Header = "Cor de destaque", ItemsSource = new[] { "Azul", "Verde", "Roxo", "Laranja", "Vermelho" }, SelectedItem = _preferencias.Cor, HorizontalAlignment = HorizontalAlignment.Stretch };
        var sistema = new ToggleSwitch { Header = "Notificações do Windows", IsOn = _preferencias.NotificacoesSistema };
        var som = new ToggleSwitch { Header = "Som nas notificações do Windows", IsOn = _preferencias.SomNotificacoes, IsEnabled = sistema.IsOn };
        foreach (var opcao in new[] { iniciarWindows, segundoPlano, ativa, notificacoes, sistema, som, mostrar, tempoReal })
            opcao.Header = new TextBlock { Text = (string)opcao.Header, TextWrapping = TextWrapping.Wrap };
        var testeNotificacao = new Button { Content = "Testar notificação do Windows", IsEnabled = sistema.IsOn };
        var resultadoNotificacao = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var testandoNotificacao = false;
        sistema.Toggled += (_, _) =>
        {
            som.IsEnabled = sistema.IsOn;
            testeNotificacao.IsEnabled = sistema.IsOn && !testandoNotificacao;
        };
        testeNotificacao.Click += async (_, _) =>
        {
            testandoNotificacao = true;
            testeNotificacao.IsEnabled = false;
            resultadoNotificacao.Text = "Enviando notificação…";
            try
            {
                await ((App)Application.Current).Notificacoes.TestarAsync(som.IsOn);
                resultadoNotificacao.Text = "Notificação entregue à Central de Notificações do Windows. O modo Não incomodar pode ocultar o banner.";
            }
            catch (Exception ex) { resultadoNotificacao.Text = $"Não foi possível enviar: {ex.Message}"; }
            finally
            {
                testandoNotificacao = false;
                testeNotificacao.IsEnabled = sistema.IsOn;
            }
        };
        var explicacaoAlertas = new TextBlock { Text = "O limite conta desde a última interação do agente. Se ele ainda não respondeu, conta desde a última mensagem do cliente.", TextWrapping = TextWrapping.Wrap };
        var status = new TextBlock { Text = $"Versão instalada: {AtualizacaoAplicativoService.VersaoAtual}", TextWrapping = TextWrapping.Wrap };
        var autor = new TextBlock { Text = "Desenvolvido por Rafael Stolf Silva", TextWrapping = TextWrapping.Wrap };
        var repositorio = new HyperlinkButton
        {
            Content = "Repositório do ServiceDesk no GitHub",
            NavigateUri = new Uri("https://github.com/RafaStolf/ServiceDesk"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var verificar = new Button { Content = "Verificar atualizações" };
        var atualizar = new Button { Content = "Atualizar", IsEnabled = false };
        var progresso = new ProgressRing { IsActive = false, Width = 24, Height = 24, HorizontalAlignment = HorizontalAlignment.Left };
        var informacao = new TextBlock { Text = "Ao atualizar, o ServiceDesk será fechado, terá os arquivos substituídos e abrirá novamente. As configurações são preservadas. Salve suas alterações antes de atualizar.", TextWrapping = TextWrapping.Wrap };
        StackPanel Painel(params UIElement[] itens)
        {
            var painel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 12, 12, 0) };
            foreach (var item in itens) painel.Children.Add(item);
            return painel;
        }
        var secoes = new (string Nome, StackPanel Painel)[]
        {
            ("Geral", Painel(iniciarWindows, segundoPlano, instrucaoGeral)),
            ("Chatwoot", Painel(url, conta, chave, mostrar, inbox)),
            ("Fila", Painel(ativa, tempoReal, intervalo)),
            ("Aparência", Painel(tema, cor)),
            ("Alertas", Painel(notificacoes, limite, sistema, som, testeNotificacao, resultadoNotificacao, explicacaoAlertas)),
            ("Sobre", Painel(autor, repositorio, status, verificar, atualizar, progresso, informacao))
        };
        var abas = new Grid { Height = Math.Clamp(ActualHeight - 240, 180, 500), RowSpacing = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
        abas.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        abas.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var navegacao = new Grid { ColumnSpacing = 6, RowSpacing = 6 };
        for (var i = 0; i < 3; i++) navegacao.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < 2; i++) navegacao.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var conteudos = secoes.Select(secao => new ScrollViewer { Content = secao.Painel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Disabled, HorizontalContentAlignment = HorizontalAlignment.Stretch, Visibility = Visibility.Collapsed }).ToArray();
        var botoes = new List<Microsoft.UI.Xaml.Controls.Primitives.ToggleButton>();
        void Selecionar(int indice)
        {
            for (var j = 0; j < botoes.Count; j++)
            {
                botoes[j].IsChecked = j == indice;
                botoes[j].Background = j == indice ? CorDestaque() : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                botoes[j].Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(j == indice || ActualTheme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black);
                conteudos[j].Visibility = j == indice ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        for (var i = 0; i < secoes.Length; i++)
        {
            var indice = i;
            var botao = new Microsoft.UI.Xaml.Controls.Primitives.ToggleButton { Content = new TextBlock { Text = secoes[i].Nome, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center }, Style = (Style)Resources["FiltroStyle"], MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
            botao.Click += (_, _) => Selecionar(indice);
            Grid.SetRow(botao, i / 3);
            Grid.SetColumn(botao, i % 3);
            navegacao.Children.Add(botao);
            botoes.Add(botao);
            Grid.SetRow(conteudos[i], 1);
            abas.Children.Add(conteudos[i]);
        }
        abas.Children.Add(navegacao);
        Selecionar(0);
        var erro = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var conteudoDialogo = Painel(abas, erro);
        conteudoDialogo.Margin = new Thickness(0);
        var dialogo = new ContentDialog { XamlRoot = XamlRoot, Title = "Configurações", Content = conteudoDialogo, HorizontalContentAlignment = HorizontalAlignment.Stretch, PrimaryButtonText = "Salvar", CloseButtonText = "Fechar" };
        AtualizacaoAplicativo? disponivel = null;
        var ocupado = false;
        dialogo.Closing += (_, args) => args.Cancel = ocupado;
        verificar.Click += async (_, _) =>
        {
            ocupado = true;
            verificar.IsEnabled = atualizar.IsEnabled = dialogo.IsPrimaryButtonEnabled = false;
            progresso.IsActive = true;
            try
            {
                disponivel = await new AtualizacaoAplicativoService().VerificarAsync(CancellationToken.None);
                status.Text = disponivel is null ? $"Versão {AtualizacaoAplicativoService.VersaoAtual}: não há atualização disponível." : $"Instalada: {AtualizacaoAplicativoService.VersaoAtual}. Disponível: {disponivel.Versao}. Clique em Atualizar para instalar.";
            }
            catch { status.Text = "Não foi possível consultar o GitHub. Confira sua conexão e tente novamente."; disponivel = null; }
            finally { ocupado = false; progresso.IsActive = false; verificar.IsEnabled = dialogo.IsPrimaryButtonEnabled = true; atualizar.IsEnabled = disponivel is not null; }
        };
        atualizar.Click += async (_, _) =>
        {
            if (disponivel is null) return;
            ocupado = true;
            verificar.IsEnabled = atualizar.IsEnabled = dialogo.IsPrimaryButtonEnabled = false;
            progresso.IsActive = true;
            status.Text = "Baixando e verificando a atualização. Aguarde; o aplicativo reiniciará ao concluir.";
            try
            {
                await new AtualizacaoAplicativoService().PrepararAsync(disponivel, CancellationToken.None);
                ocupado = false;
                ((App)Application.Current).Sair();
            }
            catch (Exception ex) { status.Text = $"Não foi possível atualizar: {ex.Message}"; }
            finally { ocupado = false; progresso.IsActive = false; verificar.IsEnabled = dialogo.IsPrimaryButtonEnabled = true; atualizar.IsEnabled = disponivel is not null; }
        };
        dialogo.PrimaryButtonClick += (_, args) =>
        {
            var temChatwoot = new[] { url.Text, conta.Text, chave.Password, inbox.Text }.Any(x => !string.IsNullOrWhiteSpace(x));
            if (temChatwoot && (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out var endereco) || endereco.Scheme != "https" || !string.IsNullOrEmpty(endereco.UserInfo)
                || string.IsNullOrWhiteSpace(chave.Password) || !long.TryParse(conta.Text, out var contaId) || contaId <= 0
                || !long.TryParse(inbox.Text, out var inboxId) || inboxId <= 0))
            { erro.Text = "Informe endereço HTTPS, chave da API e IDs positivos da conta e caixa de entrada."; args.Cancel = true; return; }
            if ((intervalo.IsEnabled && double.IsNaN(intervalo.Value)) || double.IsNaN(limite.Value))
            { erro.Text = "Informe os intervalos em minutos."; args.Cancel = true; return; }
            var intervaloFixo = double.IsNaN(intervalo.Value) ? _intervaloAtualizacaoMinutos : (int)Math.Round(intervalo.Value);
            try
            {
                InicializacaoWindowsService.Configurar(iniciarWindows.IsOn, segundoPlano.IsOn);
                if (temChatwoot) ConfiguracaoLocal.SalvarPropriedades(new Dictionary<string, string>
                {
                    ["chatwoot.base-url"] = url.Text.Trim().TrimEnd('/'), ["chatwoot.account-id"] = conta.Text.Trim(),
                    ["chatwoot.api-access-token"] = chave.Password, ["chatwoot.inbox-id"] = inbox.Text.Trim()
                });
                _preferenciasService.Salvar(new PreferenciasInterface { Tema = tema.SelectedItem as string ?? "Sistema", Cor = cor.SelectedItem as string ?? "Azul", NotificacoesSistema = sistema.IsOn, SomNotificacoes = som.IsOn });
                _configuracaoAtualizacao.Salvar(new ConfiguracaoAtualizacao(ativa.IsOn, Math.Clamp(intervaloFixo, 1, 120), notificacoes.IsOn, Math.Clamp((int)Math.Round(limite.Value), 1, 1440), tempoReal.IsOn));
            }
            catch (Exception ex) { erro.Text = $"Não foi possível salvar: {ex.Message}"; args.Cancel = true; }
        };
        if (await dialogo.ShowAsync() == ContentDialogResult.Primary)
        {
            AplicarPreferenciasInterface();
            CarregarConfiguracaoAtualizacao();
            ReiniciarResolvidos();
            await Task.WhenAll(AtualizarAsync(), ExibindoResolvidos ? CarregarResolvidosAsync() : Task.CompletedTask);
        }
    }
}
