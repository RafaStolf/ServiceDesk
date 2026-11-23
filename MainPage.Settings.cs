using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk;

public sealed partial class MainPage
{
    private void ConfiguracoesButton_Click(object sender, RoutedEventArgs e)
    {
        if (TelaSecundaria.Content is Grid) AtualizarFiltrosVisuais();
        else MostrarConfiguracoes(_novaVersao is not null);
    }

    private void MostrarConfiguracoes(bool abrirSobre)
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
        var inbox = new TextBox { Header = "IDs da caixa de entrada", Text = Valor("chatwoot.inbox-id"), PlaceholderText = "Ex: 1 ou 1,2,3" };
        var janelaPrincipal = (MainWindow)((App)Application.Current).MainWindow;
        var sempreVisivel = new ToggleSwitch { Header = "Manter acima das outras janelas", IsOn = _preferencias.SempreVisivel };
        var posicaoInicial = _preferencias.JanelaInicial;
        var guardarTamanho = new Button { Content = "Usar tamanho e posição atuais ao abrir" };
        guardarTamanho.Click += (_, _) =>
        {
            var janela = janelaPrincipal.AppWindow;
            posicaoInicial = new PosicaoJanela(janela.Position.X, janela.Position.Y, janela.Size.Width, janela.Size.Height);
            guardarTamanho.Content = $"Usar {janela.Size.Width} × {janela.Size.Height} ao abrir";
        };
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
        var notificacoes = new ToggleSwitch { Header = "Ativar alertas de atraso", IsOn = _notificacoesAtivas };
        var limite = new NumberBox { Header = "Prazo de resposta (minutos)", Minimum = AcompanhamentoN1.PrazoRespostaMinimo,
            Maximum = 1440, Value = _limiteNotificacaoMinutos, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var flutuantes = new ToggleSwitch { Header = "Mostrar cards flutuantes", IsOn = _preferencias.MostrarCardsFlutuantes };
        var cantos = new (PosicaoPainelFlutuante Valor, string Rotulo)[]
        {
            (PosicaoPainelFlutuante.InferiorDireita, "Inferior direito"),
            (PosicaoPainelFlutuante.InferiorEsquerda, "Inferior esquerdo"),
            (PosicaoPainelFlutuante.SuperiorDireita, "Superior direito"),
            (PosicaoPainelFlutuante.SuperiorEsquerda, "Superior esquerdo")
        };
        var posicaoFlutuante = new ComboBox { Header = "Posição do painel flutuante",
            ItemsSource = cantos.Select(canto => canto.Rotulo).ToArray(),
            SelectedIndex = Array.FindIndex(cantos, canto => canto.Valor == _preferencias.PosicaoFlutuante),
            HorizontalAlignment = HorizontalAlignment.Stretch };
        List<PainelFlutuanteAtrasos.OpcaoMonitor> monitores =
        [
            new(null, "Automático (janela em uso)"),
            new(PainelFlutuanteAtrasos.MonitorPrincipal, "Monitor principal"),
            .. PainelFlutuanteAtrasos.ListarMonitores()
        ];
        var monitorSalvo = _preferencias.MonitorFlutuante;
        var indiceMonitor = monitores.FindIndex(monitor => string.Equals(monitor.Id, monitorSalvo, StringComparison.OrdinalIgnoreCase));
        if (indiceMonitor < 0)
        {
            indiceMonitor = monitores.Count;
            monitores.Add(new(monitorSalvo, "Monitor salvo (desconectado)"));
        }
        var monitorFlutuante = new ComboBox { Header = "Monitor do painel flutuante",
            ItemsSource = monitores.Select(monitor => monitor.Rotulo).ToArray(), SelectedIndex = indiceMonitor,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        var escalaFlutuante = new NumberBox { Header = "Tamanho do card flutuante (%)",
            Minimum = PreferenciasInterface.EscalaFlutuanteMinima, Maximum = PreferenciasInterface.EscalaFlutuanteMaxima,
            Value = _preferencias.EscalaFlutuante, SmallChange = 5, LargeChange = 10, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var opacidade = new Slider { Header = "Opacidade do card flutuante (%)",
            Minimum = PreferenciasInterface.OpacidadeMinima, Maximum = 100, StepFrequency = 1, Value = _preferencias.OpacidadeFlutuante };
        opacidade.Header = $"Opacidade do card flutuante ({opacidade.Value:0}%)";
        opacidade.ValueChanged += (_, e) => opacidade.Header = $"Opacidade do card flutuante ({e.NewValue:0}%)";
        var fontesFlutuantes = PainelFlutuanteAtrasos.ListarFontes();
        var fonteFlutuante = new ComboBox { Header = "Fonte do card flutuante", ItemsSource = fontesFlutuantes,
            SelectedItem = fontesFlutuantes.FirstOrDefault(fonte => string.Equals(fonte, _preferencias.FonteFlutuante, StringComparison.OrdinalIgnoreCase))
                ?? PreferenciasInterface.FonteFlutuantePadrao, HorizontalAlignment = HorizontalAlignment.Stretch };
        var tema = new ComboBox { Header = "Tema", ItemsSource = new[] { "Sistema", "Claro", "Escuro" }, SelectedItem = _preferencias.Tema, HorizontalAlignment = HorizontalAlignment.Stretch };
        var cor = new ComboBox { Header = "Cor de destaque", ItemsSource = new[] { "Azul", "Verde", "Roxo", "Laranja", "Vermelho" }, SelectedItem = _preferencias.Cor, HorizontalAlignment = HorizontalAlignment.Stretch };
        var avisoEtapaN1 = new NumberBox { Header = "Destacar prazo do card quantos minutos antes",
            Minimum = 0, Maximum = 15, Value = _preferencias.AvisoEtapaN1Minutos, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var sistema = new ToggleSwitch { Header = "Notificações do Windows", IsOn = _preferencias.NotificacoesSistema };
        var repetir = new ToggleSwitch { Header = "Repetir lembrete do Windows", OffContent = "Nunca",
            IsOn = _preferencias.IntervaloLembreteMinutos > 0 };
        var intervaloLembrete = new NumberBox { Header = "Intervalo do lembrete (minutos)", Minimum = 1, Maximum = 60,
            Value = _preferencias.IntervaloLembreteMinutos > 0 ? _preferencias.IntervaloLembreteMinutos : 5,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        void AtualizarOpcoesDeAlertas()
        {
            flutuantes.IsEnabled = sistema.IsEnabled = notificacoes.IsOn;
            posicaoFlutuante.IsEnabled = monitorFlutuante.IsEnabled = notificacoes.IsOn && flutuantes.IsOn;
            opacidade.IsEnabled = escalaFlutuante.IsEnabled = fonteFlutuante.IsEnabled = posicaoFlutuante.IsEnabled;
            repetir.IsEnabled = notificacoes.IsOn && sistema.IsOn;
            intervaloLembrete.IsEnabled = repetir.IsEnabled && repetir.IsOn;
        }
        notificacoes.Toggled += (_, _) => AtualizarOpcoesDeAlertas();
        flutuantes.Toggled += (_, _) => AtualizarOpcoesDeAlertas();
        sistema.Toggled += (_, _) => AtualizarOpcoesDeAlertas();
        repetir.Toggled += (_, _) => AtualizarOpcoesDeAlertas();
        AtualizarOpcoesDeAlertas();
        var toques = new (string Nome, SomNotificacao? Valor)[]
        {
            ("Padrão do Windows", SomNotificacao.Padrao), ("Mensagem", SomNotificacao.Mensagem),
            ("E-mail", SomNotificacao.Email), ("Lembrete", SomNotificacao.Lembrete),
            ("Alarme", SomNotificacao.Alarme), ("Sem som", null)
        };
        var som = new ComboBox { Header = "Som da notificação", ItemsSource = toques.Select(toque => toque.Nome).ToArray(),
            SelectedIndex = Array.FindIndex(toques, toque => toque.Valor == _preferencias.SomSelecionado),
            HorizontalAlignment = HorizontalAlignment.Stretch };
        SomNotificacao? SomEscolhido() => som.SelectedIndex < 0 ? SomNotificacao.Padrao : toques[som.SelectedIndex].Valor;
        foreach (var opcao in new[] { iniciarWindows, segundoPlano, ativa, notificacoes, sistema, flutuantes, repetir, mostrar, tempoReal })
            opcao.Header = new TextBlock { Text = (string)opcao.Header, TextWrapping = TextWrapping.Wrap };
        var testeNotificacao = new Button { Content = "Testar notificação do Windows" };
        var ouvir = new Button { Content = "Ouvir som", IsEnabled = SomEscolhido() is not null };
        var resultadoNotificacao = new InfoBar
        {
            Title = "Notificação de teste", IsClosable = true, MaxWidth = 460,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(resultadoNotificacao, "BannerTesteNotificacao");
        var duracaoBanner = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        duracaoBanner.Tick += (_, _) =>
        {
            duracaoBanner.Stop();
            resultadoNotificacao.IsOpen = false;
        };
        resultadoNotificacao.Closed += (_, _) => duracaoBanner.Stop();
        void MostrarResultado(string mensagem, InfoBarSeverity severidade)
        {
            if (resultadoNotificacao.XamlRoot is null) return;
            duracaoBanner.Stop();
            resultadoNotificacao.Message = mensagem;
            resultadoNotificacao.Severity = severidade;
            resultadoNotificacao.IsOpen = true;
            if (severidade == InfoBarSeverity.Success) duracaoBanner.Start();
        }
        som.SelectionChanged += (_, _) => ouvir.IsEnabled = SomEscolhido() is not null;
        ouvir.Click += (_, _) =>
        {
            if (SomEscolhido() is { } escolhido && !ServicoNotificacoes.OuvirSom(escolhido))
                MostrarResultado("O esquema de sons do Windows não tem áudio para este toque.", InfoBarSeverity.Warning);
        };
        testeNotificacao.Click += async (_, _) =>
        {
            testeNotificacao.IsEnabled = false;
            MostrarResultado("Enviando notificação de teste…", InfoBarSeverity.Informational);
            try
            {
                var confirmada = await ((App)Application.Current).Notificacoes.TestarAsync(SomEscolhido());
                MostrarResultado(confirmada
                    ? "Teste entregue à Central de Notificações (Win + N). O modo Não incomodar pode ocultar o banner e silenciar o som."
                    : "O Windows aceitou o teste. Consulte a Central de Notificações (Win + N) para verificar se apareceu.",
                    confirmada ? InfoBarSeverity.Success : InfoBarSeverity.Informational);
            }
            catch (Exception ex) { MostrarResultado($"Não foi possível enviar: {ex.Message}", InfoBarSeverity.Error); }
            finally { testeNotificacao.IsEnabled = true; }
        };
        var explicacaoAlertas = new TextBlock { Text = "O prazo conta desde que o cliente começou a aguardar resposta. Os avisos acompanham os tickets atribuídos a você.", TextWrapping = TextWrapping.Wrap };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var autoria = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "Desenvolvido por", FontSize = 12 },
                new TextBlock { Text = "Rafael Stolf Silva", FontSize = 12, TextWrapping = TextWrapping.NoWrap }
            }
        };
        var repositorio = new HyperlinkButton
        {
            Content = "Repositório no GitHub",
            NavigateUri = new Uri("https://github.com/RafaStolf/ServiceDesk"),
            HorizontalAlignment = HorizontalAlignment.Left, FontSize = 12, Padding = new Thickness(0, 4, 0, 4)
        };
        var verificar = new Button { Content = "Verificar atualizações" };
        var atualizar = new Button { Content = "Atualizar", IsEnabled = _novaVersao is not null };
        var progresso = new ProgressRing { IsActive = false, Width = 24, Height = 24, HorizontalAlignment = HorizontalAlignment.Left };
        StackPanel Painel(params UIElement[] itens)
        {
            var painel = new StackPanel { Spacing = 12, Margin = new Thickness(0) };
            foreach (var item in itens) painel.Children.Add(item);
            return painel;
        }
        var previsao = new ToggleButton { Content = VisualServiceDesk.ItemLateral(VisualServiceDesk.Icone("\uE8BD"), "Meus tickets"),
            Style = (Style)Resources["FiltroStyle"], HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false, IsTabStop = false };
        var amostraTicket = VisualServiceDesk.Painel(new StackPanel());
        var previa = VisualServiceDesk.Painel(Painel(new TextBlock { Text = "Prévia", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
            previsao, amostraTicket));
        void AtualizarPrevia()
        {
            var temaEscolhido = TemaDaPreferencia(tema.SelectedItem as string);
            var temaPrevia = temaEscolhido != ElementTheme.Default ? temaEscolhido
                : Application.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
            previa.RequestedTheme = temaPrevia;
            VisualServiceDesk.Selecionar(previsao, true, cor.SelectedItem as string, temaPrevia);
        }
        tema.SelectionChanged += (_, _) => AtualizarPrevia();
        cor.SelectionChanged += (_, _) => AtualizarPrevia();
        previa.ActualThemeChanged += (_, _) => AtualizarPrevia();
        AtualizarPrevia();

        Grid LinhaAparencia(string titulo, string ajuda, ComboBox controle)
        {
            var linha = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
            linha.ColumnDefinitions.Add(new ColumnDefinition());
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            linha.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            linha.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var rotulo = new TextBlock { Text = titulo, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            var texto = VisualServiceDesk.Estilizar(new TextBlock { Text = ajuda }, "TextoSecundarioServiceDeskStyle");
            linha.Children.Add(Painel(rotulo, texto));
            controle.Header = null;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetLabeledBy(controle, rotulo);
            linha.Children.Add(controle);
            linha.SizeChanged += (_, e) =>
            {
                var compacto = e.NewSize.Width < 440;
                Grid.SetRow(controle, compacto ? 1 : 0);
                Grid.SetColumn(controle, compacto ? 0 : 1);
                controle.Width = compacto ? double.NaN : 185;
            };
            return linha;
        }

        var camposFila = _preferencias.CamposFila.ToList();
        var camposPainel = _preferencias.CamposPainel.ToList();
        var compactoFila = _preferencias.FilaCompacta;
        var compactoPainel = _preferencias.PainelCompacto;
        var destino = new ComboBox { Header = "Editar", ItemsSource = new[] { "Cards da fila", "Painel flutuante" }, SelectedIndex = 0 };
        var campos = Painel();
        var densidade = new CheckBox { Content = "Densidade compacta" };
        var restaurar = new Button { Content = "Restaurar padrão deste editor" };
        var extras = Painel(posicaoFlutuante, monitorFlutuante, escalaFlutuante, opacidade, fonteFlutuante);
        var amostra = new AtendimentoMonitorado { Id = 28451, Cliente = "Ana Martins", Empresa = "Centrium", Email = "ana@example.com", Telefone = "(11) 5555-0100" };
        var exemplo = Painel();
        amostraTicket.Child = exemplo;
        var controles = Painel(destino, campos, densidade, extras, restaurar);
        var editor = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        editor.ColumnDefinitions.Add(new ColumnDefinition());
        editor.ColumnDefinitions.Add(new ColumnDefinition());
        editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editor.Children.Add(previa);
        editor.Children.Add(controles);
        editor.SizeChanged += (_, e) =>
        {
            var estreito = e.NewSize.Width < 620;
            Grid.SetColumnSpan(controles, estreito ? 2 : 1);
            Grid.SetColumnSpan(previa, estreito ? 2 : 1);
            Grid.SetColumn(previa, estreito ? 0 : 1);
            Grid.SetRow(controles, estreito ? 1 : 0);
        };
        void DesenharEditor()
        {
            var painel = destino.SelectedIndex == 1;
            var ativos = painel ? camposPainel : camposFila;
            var compacto = painel ? compactoPainel : compactoFila;
            extras.Visibility = painel ? Visibility.Visible : Visibility.Collapsed;
            densidade.IsChecked = compacto;
            exemplo.Children.Clear();
            exemplo.Spacing = compacto ? 1 : 4;
            if (!painel) exemplo.Children.Add(new TextBlock { Text = "#28451" });
            exemplo.Children.Add(new TextBlock { Text = painel ? amostra.TituloPainel(ativos) : amostra.Cliente, TextWrapping = TextWrapping.Wrap });
            if (!painel) foreach (var campo in ativos) exemplo.Children.Add(new TextBlock { Text = amostra.ValorDoCampo(campo), TextWrapping = TextWrapping.Wrap });
            exemplo.Children.Add(new TextBlock { Text = "Resposta em atraso · há 5 min", TextWrapping = TextWrapping.Wrap });
            campos.Children.Clear();
            foreach (var campo in ativos.Union(new[] { "Empresa", "E-mail", "Telefone" }).ToArray())
            {
                var visivel = new CheckBox { Content = campo, IsChecked = ativos.Contains(campo) };
                visivel.Click += (_, _) => { if (visivel.IsChecked == true) ativos.Add(campo); else ativos.Remove(campo); DesenharEditor(); AtualizarPreviaFlutuante(); };
                var acima = new Button { Content = "↑", IsEnabled = ativos.IndexOf(campo) > 0 };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(acima, $"Mover {campo} para cima");
                acima.Click += (_, _) => { var indice = ativos.IndexOf(campo); ativos.RemoveAt(indice); ativos.Insert(indice - 1, campo); DesenharEditor(); AtualizarPreviaFlutuante(); };
                campos.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { visivel, acima } });
            }
        }
        var aparencia = Painel(VisualServiceDesk.Painel(Painel(
            LinhaAparencia("Tema do aplicativo", "Tema usado em todas as telas.", tema),
            LinhaAparencia("Cor de destaque", "Cor de seleção em todo o aplicativo.", cor))), editor);
        var secoes = new (string Nome, StackPanel Painel)[]
        {
            ("Atendimento", Painel(ativa, tempoReal, intervalo)),
            ("Personalização", aparencia),
            ("Alertas", Painel(notificacoes, limite, avisoEtapaN1, flutuantes, sistema, repetir, intervaloLembrete, som, ouvir, testeNotificacao, explicacaoAlertas)),
            ("Conexão", Painel(url, conta, chave, mostrar, inbox)),
            ("Aplicativo", Painel(sempreVisivel, guardarTamanho, iniciarWindows, segundoPlano, instrucaoGeral, verificar, atualizar, progresso, status, autoria, repositorio))
        };
        var pagina = new Grid();
        var corpo = new Grid { Padding = new Thickness(20, 16, 20, 16), RowSpacing = 12 };
        corpo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        corpo.RowDefinitions.Add(new RowDefinition());
        corpo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        pagina.Children.Add(corpo);
        var menuSecoes = new MenuFlyout();
        var menuCompacto = new Button { Flyout = menuSecoes, Visibility = Visibility.Collapsed };

        corpo.Children.Add(menuCompacto);
        var tituloSecao = new TextBlock { FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var secaoAtual = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var conteudo = new ScrollViewer
        {
            Content = Painel(tituloSecao, secaoAtual), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled, HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        Grid.SetRow(conteudo, 1);
        corpo.Children.Add(conteudo);
        var botoes = new List<ToggleButton>();
        var navegacao = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        corpo.Children.Add(navegacao);
        pagina.SizeChanged += (_, e) =>
        {
            var compacto = e.NewSize.Width < 720;
            navegacao.Visibility = compacto ? Visibility.Collapsed : Visibility.Visible;
            menuCompacto.Visibility = compacto ? Visibility.Visible : Visibility.Collapsed;
            corpo.Padding = new Thickness(compacto ? 8 : 16);
        };
        var x = _preferencias.PosicaoRelativaX;
        var y = _preferencias.PosicaoRelativaY;
        var emAlertas = false;
        PainelFlutuanteAtrasos? previaFlutuante = null;
        void AtualizarPreviaFlutuante()
        {
            var painel = destino.SelectedIndex == 1;
            exemplo.Opacity = painel ? opacidade.Value / 100 : 1;
            exemplo.Padding = new Thickness(painel && !double.IsNaN(escalaFlutuante.Value) ? 6 * escalaFlutuante.Value / 100 : 6);
            foreach (var linha in exemplo.Children.OfType<TextBlock>())
            {
                linha.FontSize = painel && !double.IsNaN(escalaFlutuante.Value) ? 14 * escalaFlutuante.Value / 100 : 14;
                if (painel && fonteFlutuante.SelectedItem is string fonte)
                    linha.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(fonte);
            }
            if (!emAlertas || destino.SelectedIndex != 1 || !notificacoes.IsOn || !flutuantes.IsOn)
            {
                previaFlutuante?.Ocultar();
                _previaFlutuanteAtiva = false;
                return;
            }
            _previaFlutuanteAtiva = true;
            ((App)Application.Current).PainelDeAtrasos.Ocultar();
            previaFlutuante ??= new PainelFlutuanteAtrasos((novoX, novoY) => { x = novoX; y = novoY; AtualizarPreviaFlutuante(); });
            previaFlutuante.Atualizar([(amostra.TituloPainel(camposPainel), "Atraso · há 5 min")], 0, ActualTheme == ElementTheme.Light,
                _preferencias with { PainelCompacto = compactoPainel, MonitorFlutuante = monitores[monitorFlutuante.SelectedIndex].Id,
                    PosicaoFlutuante = cantos[posicaoFlutuante.SelectedIndex].Valor, OpacidadeFlutuante = (int)opacidade.Value,
                    PosicaoRelativaX = x, PosicaoRelativaY = y,
                    EscalaFlutuante = double.IsNaN(escalaFlutuante.Value) ? 100 : (int)escalaFlutuante.Value,
                    FonteFlutuante = fonteFlutuante.SelectedItem as string ?? PreferenciasInterface.FonteFlutuantePadrao });
        }
        destino.SelectionChanged += (_, _) => { DesenharEditor(); AtualizarPreviaFlutuante(); };
        densidade.Click += (_, _) =>
        {
            if (destino.SelectedIndex == 1) compactoPainel = densidade.IsChecked == true;
            else compactoFila = densidade.IsChecked == true;
            DesenharEditor();
            AtualizarPreviaFlutuante();
        };
        restaurar.Click += (_, _) =>
        {
            if (destino.SelectedIndex == 1)
            {
                camposPainel.Clear(); compactoPainel = false;
                posicaoFlutuante.SelectedIndex = monitorFlutuante.SelectedIndex = 0;
                x = y = null;
                escalaFlutuante.Value = opacidade.Value = 100;
                fonteFlutuante.SelectedItem = PreferenciasInterface.FonteFlutuantePadrao;
            }
            else { camposFila.Clear(); camposFila.AddRange(["Empresa", "E-mail"]); compactoFila = false; }
            DesenharEditor();
            AtualizarPreviaFlutuante();
        };
        DesenharEditor();
        opacidade.ValueChanged += (_, _) => AtualizarPreviaFlutuante();
        posicaoFlutuante.SelectionChanged += (_, _) => { x = y = null; AtualizarPreviaFlutuante(); };
        monitorFlutuante.SelectionChanged += (_, _) => AtualizarPreviaFlutuante();
        escalaFlutuante.ValueChanged += (_, _) => AtualizarPreviaFlutuante();
        fonteFlutuante.SelectionChanged += (_, _) => AtualizarPreviaFlutuante();
        flutuantes.Toggled += (_, _) => AtualizarPreviaFlutuante();
        notificacoes.Toggled += (_, _) => AtualizarPreviaFlutuante();
        pagina.Unloaded += (_, _) => { emAlertas = _previaFlutuanteAtiva = false; previaFlutuante?.Dispose(); AtualizarTemposExibidos(); };
        var selecionado = 0;
        void Selecionar(int indice)
        {
            selecionado = indice;
            emAlertas = secoes[indice].Nome == "Personalização";
            AtualizarPreviaFlutuante();
            menuCompacto.Content = $"{secoes[indice].Nome} ▾";
            tituloSecao.Text = secoes[indice].Nome;
            secaoAtual.Content = secoes[indice].Painel;
            conteudo.ChangeView(null, 0, null, disableAnimation: true);
            for (var j = 0; j < botoes.Count; j++)
                VisualServiceDesk.Selecionar(botoes[j], j == indice, _preferencias.Cor, ActualTheme);
        }
        for (var i = 0; i < secoes.Length; i++)
        {
            var indice = i;
            var nome = secoes[i].Nome;
            var botao = new ToggleButton { Content = nome, Style = (Style)Resources["FiltroStyle"] };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(botao, nome);
            botao.Click += (_, _) => Selecionar(indice);
            navegacao.Children.Add(botao);
            botoes.Add(botao);
            var opcao = new MenuFlyoutItem { Text = nome };
            opcao.Click += (_, _) => Selecionar(indice);
            menuSecoes.Items.Add(opcao);
        }
        pagina.ActualThemeChanged += (_, _) => Selecionar(selecionado);
        Selecionar(abrirSobre ? secoes.Length - 1 : 0);
        var erro = VisualServiceDesk.Estilizar(new TextBlock(), "TextoCriticoServiceDeskStyle");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(erro, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        var salvar = new Button { Content = "Salvar", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        var cancelar = new Button { Content = "Cancelar" };
        var rodape = Painel(VisualServiceDesk.Estilizar(new Border { Height = 1 }, "DivisorServiceDeskStyle"), erro,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right,
                Children = { cancelar, salvar } });
        Grid.SetRow(rodape, 2);
        corpo.Children.Add(rodape);
        Grid.SetRowSpan(resultadoNotificacao, 3);
        corpo.Children.Add(resultadoNotificacao);
        pagina.Unloaded += (_, _) => duracaoBanner.Stop();

        var anterior = TelaSecundaria.Content as MetricasPage;
        cancelar.Click += (_, _) => MostrarTelaSecundaria(anterior);
        MostrarTelaSecundaria(pagina);
        AtualizacaoAplicativo? disponivel = _novaVersao;
        if (disponivel is not null)
            status.Text = $"Versão {disponivel.Versao} disponível.";


        verificar.Click += async (_, _) =>
        {

            verificar.IsEnabled = atualizar.IsEnabled = salvar.IsEnabled = cancelar.IsEnabled = false;
            progresso.IsActive = true;
            try
            {
                disponivel = await _atualizador.VerificarAsync(CancellationToken.None);
                RegistrarVersaoDisponivel(disponivel, notificar: false);
                status.Text = disponivel is null ? "O ServiceDesk está atualizado." : $"Versão {disponivel.Versao} disponível.";
            }
            catch { status.Text = "Não foi possível consultar o GitHub. Confira sua conexão e tente novamente."; disponivel = null; }
            finally { progresso.IsActive = false; verificar.IsEnabled = salvar.IsEnabled = cancelar.IsEnabled = true; atualizar.IsEnabled = disponivel is not null; }
        };
        atualizar.Click += async (_, _) =>
        {
            if (disponivel is null) return;

            verificar.IsEnabled = atualizar.IsEnabled = salvar.IsEnabled = cancelar.IsEnabled = false;
            progresso.IsActive = true;
            status.Text = "Baixando e verificando a atualização…";
            try
            {
                await _atualizador.PrepararAsync(disponivel, CancellationToken.None);

                ((App)Application.Current).Sair();
            }
            catch (Exception ex) { status.Text = $"Não foi possível atualizar: {ex.Message}"; }
            finally { progresso.IsActive = false; verificar.IsEnabled = salvar.IsEnabled = cancelar.IsEnabled = true; atualizar.IsEnabled = disponivel is not null; }
        };
        salvar.Click += async (_, _) =>
        {
            var temChatwoot = new[] { url.Text, conta.Text, chave.Password, inbox.Text }.Any(x => !string.IsNullOrWhiteSpace(x));
            if (temChatwoot && (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out var endereco) || endereco.Scheme != "https" || !string.IsNullOrEmpty(endereco.UserInfo)
                || string.IsNullOrWhiteSpace(chave.Password) || !long.TryParse(conta.Text, out var contaId) || contaId <= 0
                || CarregadorConfiguracaoChatwoot.ParseInboxIds(inbox.Text).Count == 0))
            { erro.Text = "Informe endereço HTTPS, chave da API, ID positivo da conta e ao menos um ID de caixa de entrada (separados por vírgula)."; return; }
            if ((intervalo.IsEnabled && double.IsNaN(intervalo.Value)) || double.IsNaN(limite.Value) || double.IsNaN(avisoEtapaN1.Value)
                || (intervaloLembrete.IsEnabled && double.IsNaN(intervaloLembrete.Value)))
            { erro.Text = "Informe os intervalos em minutos."; return; }
            if (escalaFlutuante.IsEnabled && double.IsNaN(escalaFlutuante.Value))
            { erro.Text = "Informe o tamanho do card flutuante em porcentagem."; return; }
            var intervaloFixo = double.IsNaN(intervalo.Value) ? _intervaloAtualizacaoMinutos : (int)Math.Round(intervalo.Value);
            try
            {
                InicializacaoWindowsService.Configurar(iniciarWindows.IsOn, segundoPlano.IsOn);
                if (temChatwoot) ConfiguracaoLocal.SalvarPropriedades(new Dictionary<string, string>
                {
                    ["chatwoot.base-url"] = url.Text.Trim().TrimEnd('/'), ["chatwoot.account-id"] = conta.Text.Trim(),
                    ["chatwoot.api-access-token"] = chave.Password, ["chatwoot.inbox-id"] = inbox.Text.Trim()
                });
                var toque = SomEscolhido();
                _preferenciasService.Salvar(new PreferenciasInterface { SempreVisivel = sempreVisivel.IsOn, JanelaInicial = posicaoInicial, CamposFila = camposFila.ToArray(), CamposPainel = camposPainel.ToArray(), FilaCompacta = compactoFila, PainelCompacto = compactoPainel, Tema = tema.SelectedItem as string ?? "Sistema", Cor = cor.SelectedItem as string ?? "Azul",
                    NotificacoesSistema = sistema.IsOn, SomNotificacoes = toque is not null, ToqueNotificacoes = toque ?? SomNotificacao.Padrao,
                    AvisoEtapaN1Minutos = (int)Math.Round(avisoEtapaN1.Value), MostrarCardsFlutuantes = flutuantes.IsOn,
                    PosicaoFlutuante = cantos[posicaoFlutuante.SelectedIndex].Valor,
                    MonitorFlutuante = monitores[monitorFlutuante.SelectedIndex].Id,
                    PosicaoRelativaX = x, PosicaoRelativaY = y,
                    OpacidadeFlutuante = (int)Math.Round(opacidade.Value),
                    EscalaFlutuante = double.IsNaN(escalaFlutuante.Value) ? _preferencias.EscalaFlutuante : (int)Math.Round(escalaFlutuante.Value),
                    FonteFlutuante = fonteFlutuante.SelectedItem as string ?? PreferenciasInterface.FonteFlutuantePadrao,
                    IntervaloLembreteMinutos = !repetir.IsOn ? 0 : double.IsNaN(intervaloLembrete.Value) ? _preferencias.IntervaloLembreteMinutos : (int)Math.Round(intervaloLembrete.Value) });
                _configuracaoAtualizacao.Salvar(new ConfiguracaoAtualizacao(ativa.IsOn, intervaloFixo, notificacoes.IsOn, (int)Math.Round(limite.Value), tempoReal.IsOn));
            }
            catch (Exception ex) { erro.Text = $"Não foi possível salvar: {ex.Message}"; return; }

            janelaPrincipal.FixarJanela(sempreVisivel.IsOn);
            var mudouChatwoot = !string.Equals(Valor("chatwoot.base-url"), url.Text.Trim().TrimEnd('/'), StringComparison.Ordinal)
                || Valor("chatwoot.account-id") != conta.Text.Trim() || Valor("chatwoot.api-access-token") != chave.Password
                || Valor("chatwoot.inbox-id") != inbox.Text.Trim();
            if (mudouChatwoot)
            {
                _versaoConfiguracaoFila++;
                LimparFila();
            }
            MostrarTelaSecundaria(anterior is null ? null : new MetricasPage());
            AplicarPreferenciasInterface();
            CarregarConfiguracaoAtualizacao();
            AtualizarTemposExibidos();
            ReiniciarResolvidos();
            await Task.WhenAll(AtualizarAsync(), ExibindoResolvidos ? CarregarResolvidosAsync() : Task.CompletedTask);
        };
    }
}
