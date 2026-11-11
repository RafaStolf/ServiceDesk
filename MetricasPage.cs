using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using ServiceDesk.Models;
using ServiceDesk.Services;
using System.Net;

namespace ServiceDesk;

internal sealed record OpcoesMetricas(ElementTheme Tema, SolidColorBrush Destaque, Style EstiloFiltro);

/// <summary>Tela sob demanda: não participa da sincronização periódica da fila.</summary>
public sealed class MetricasPage : Page
{
    private readonly ToggleButton _fila = new() { Content = "Fila atual", IsChecked = true };
    private readonly ToggleButton _historico = new() { Content = "Histórico" };
    private readonly CalendarDatePicker _inicio = new() { Header = "Início", DateFormat = "{day.integer(2)}/{month.integer(2)}/{year.full}" };
    private readonly CalendarDatePicker _fim = new() { Header = "Fim", DateFormat = "{day.integer(2)}/{month.integer(2)}/{year.full}" };
    private readonly ComboBox _baseTempo = new() { Header = "Exibir médias em", ItemsSource = new[] { "Tempo corrido", "Horário comercial do Chatwoot" }, SelectedIndex = 0 };
    private readonly Button _atualizar = new() { Content = "Atualizar" };
    private readonly Button _cancelar = new() { Content = "Cancelar consulta", Visibility = Visibility.Collapsed };
    private readonly TextBlock _status = Texto("Consulte os indicadores oficiais dos seus tickets.");
    private readonly InfoBar _aviso = new() { IsOpen = false, IsClosable = true };
    private readonly ProgressBar _progresso = new() { IsIndeterminate = true, Visibility = Visibility.Collapsed };
    private readonly StackPanel _periodo = new() { Spacing = 8, Visibility = Visibility.Collapsed };
    private readonly StackPanel _conteudo = new() { Spacing = 16 };
    private readonly List<Button> _atalhos = [];
    private SolidColorBrush _destaque = new(Microsoft.UI.Colors.DodgerBlue);
    private CancellationTokenSource? _consulta;
    private ResultadoMetricas? _resultado;
    private string _contexto = "";
    private bool _estaNoHistorico;
    private bool _ativa;
    private bool _carregada;
    private int _colunasResumo = 3;

    internal MetricasPage(Action voltarParaFila, OpcoesMetricas opcoes)
    {
        RequestedTheme = opcoes.Tema;
        _destaque = opcoes.Destaque;
        _fila.Style = _historico.Style = opcoes.EstiloFiltro;
        var hoje = PeriodoMetricas.Hoje;
        _inicio.Date = DataCalendario(hoje.AddDays(-6));
        _fim.Date = DataCalendario(hoje);
        var raiz = new Grid { Padding = new Thickness(20), RowSpacing = 12 };
        for (var i = 0; i < 3; i++) raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var cabecalho = new Grid { ColumnSpacing = 12 };
        cabecalho.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cabecalho.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cabecalho.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var voltar = new Button { Content = new SymbolIcon(Symbol.Back), VerticalAlignment = VerticalAlignment.Top };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(voltar, "Voltar à fila");
        ToolTipService.SetToolTip(voltar, "Voltar à fila");
        voltar.Click += (_, _) => { _consulta?.Cancel(); voltarParaFila(); };
        cabecalho.Children.Add(voltar);
        var titulo = new StackPanel { Spacing = 4 };
        titulo.Children.Add(new TextBlock { Text = "Métricas e SLA", Style = (Style)Application.Current.Resources["TitleTextBlockStyle"] });
        titulo.Children.Add(_status);
        Grid.SetColumn(titulo, 1); cabecalho.Children.Add(titulo);
        var acoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Top };
        acoes.Children.Add(_cancelar); acoes.Children.Add(_atualizar);
        Grid.SetColumn(acoes, 2); cabecalho.Children.Add(acoes);
        raiz.Children.Add(cabecalho);
        var controles = new StackPanel { Spacing = 10 };
        var modos = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        modos.Children.Add(_fila); modos.Children.Add(_historico);
        controles.Children.Add(modos);
        var guia = new StackPanel { Spacing = 10 };
        guia.Children.Add(Texto("CCChatwoot v4.5.0 · build 75d3569. A disponibilidade dos dados depende desta instalação."));
        guia.Children.Add(Texto("Primeira resposta (FRT): duração oficial até a primeira resposta. Espera do cliente (reply_time): duração oficial de espera por respostas subsequentes. Resolução: duração oficial até resolver; reaberturas podem gerar novos registros."));
        guia.Children.Add(Texto("SLA: política aplicada pelo Chatwoot, com metas de primeira resposta, próxima resposta (NRT) e resolução. Última interação e alertas locais não medem cumprimento de SLA. Mensagem privada registra a análise interna; mensagem para o cliente integra a conversa pública."));
        guia.Children.Add(Texto(OrientacoesN1.Contexto));
        foreach (var (tituloEtapa, descricao) in OrientacoesN1.Etapas)
        {
            guia.Children.Add(Texto(tituloEtapa, 14, true));
            guia.Children.Add(Texto(descricao));
        }
        guia.Children.Add(Texto(OrientacoesN1.Limites));
        controles.Children.Add(new Expander
        {
            Header = "Entender as métricas e consultar o fluxo N1",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new ScrollViewer { Content = guia, MaxHeight = 280, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }
        });
        var datas = new Grid { ColumnSpacing = 12 };
        datas.ColumnDefinitions.Add(new ColumnDefinition()); datas.ColumnDefinitions.Add(new ColumnDefinition());
        datas.Children.Add(_inicio); Grid.SetColumn(_fim, 1); datas.Children.Add(_fim);
        _periodo.Children.Add(datas);
        var atalhos = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var (rotulo, dias) in new[] { ("Hoje", 1), ("7 dias", 7), ("30 dias", 30) })
        {
            var botao = new Button { Content = rotulo };
            botao.Click += async (_, _) => { var dia = PeriodoMetricas.Hoje; _inicio.Date = DataCalendario(dia.AddDays(1 - dias)); _fim.Date = DataCalendario(dia); await CarregarAsync(); };
            atalhos.Children.Add(botao); _atalhos.Add(botao);
        }
        _periodo.Children.Add(atalhos);
        _periodo.Children.Add(Texto("Datas inclusivas · Brasília. Se alterar o período, clique em Atualizar."));
        controles.Children.Add(_periodo); controles.Children.Add(_baseTempo); controles.Children.Add(_progresso);
        Grid.SetRow(controles, 1); raiz.Children.Add(controles);
        Grid.SetRow(_aviso, 2); raiz.Children.Add(_aviso);
        var scroll = new ScrollViewer { Content = _conteudo, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 3); raiz.Children.Add(scroll);
        Content = raiz;
        AtualizarModo();
        SizeChanged += (_, e) =>
        {
            var colunas = e.NewSize.Width < 760 ? 1 : 3;
            if (colunas != _colunasResumo) { _colunasResumo = colunas; Renderizar(); }
        };
        _fila.Click += async (_, _) => { _estaNoHistorico = false; AtualizarModo(); await CarregarAsync(); };
        _historico.Click += async (_, _) => { _estaNoHistorico = true; AtualizarModo(); await CarregarAsync(); };
        _atualizar.Click += async (_, _) => await CarregarAsync();
        _cancelar.Click += (_, _) => _consulta?.Cancel();
        _baseTempo.SelectionChanged += (_, _) => Renderizar();
        ActualThemeChanged += (_, _) => AtualizarModo();
        Loaded += async (_, _) => { _ativa = true; if (!_carregada) { _carregada = true; await CarregarAsync(); } };
        Unloaded += (_, _) => { _ativa = false; _consulta?.Cancel(); };
    }

    private static DateTimeOffset DataCalendario(DateOnly data) => new(data.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
    private void AtualizarModo()
    {
        _periodo.Visibility = _estaNoHistorico ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (botao, ativo) in new[] { (_fila, !_estaNoHistorico), (_historico, _estaNoHistorico) })
        {
            botao.IsChecked = ativo;
            botao.Background = ativo ? _destaque : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            botao.Foreground = new SolidColorBrush(ativo || ActualTheme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black);
        }
    }
    private async Task CarregarAsync()
    {
        if (_consulta is not null || !_ativa) return;
        using var cts = new CancellationTokenSource();
        _consulta = cts;
        Ocupado(true);
        _aviso.IsOpen = false;
        try
        {
            var inicio = PeriodoMetricas.Hoje;
            var fim = inicio;
            if (_estaNoHistorico)
            {
                if (_inicio.Date is not { } inicioSelecionado || _fim.Date is not { } fimSelecionado)
                    throw new ArgumentException("Informe as datas de início e fim.");
                inicio = DateOnly.FromDateTime(inicioSelecionado.DateTime);
                fim = DateOnly.FromDateTime(fimSelecionado.DateTime);
            }
            var contexto = _estaNoHistorico ? $"Histórico · {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy} · Brasília" : "Fila atual · abertos e adiados · eventos disponíveis desses tickets";
            var progresso = new Progress<string>(texto => { if (_ativa && _consulta == cts && !cts.IsCancellationRequested) _status.Text = texto; });
            using var servico = new MetricasChatwootService(CarregadorConfiguracaoChatwoot.Carregar());
            var resultado = await servico.CarregarAsync(_estaNoHistorico, inicio, fim, progresso, cts.Token);
            if (!_ativa || cts.IsCancellationRequested) return;
            _resultado = resultado; _contexto = contexto;
            _status.Text = $"Consultado em {PeriodoMetricas.Data(resultado.ConsultadoEm)} · Brasília";
            Renderizar();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        { if (_ativa) _status.Text = "Consulta cancelada. Os resultados anteriores, se houver, permanecem identificados abaixo."; }
        catch (Exception ex)
        {
            if (!_ativa) return;
            _status.Text = "Consulta não concluída. Os resultados anteriores, se houver, permanecem identificados abaixo.";
            _aviso.Title = "Não foi possível consultar as métricas";
            _aviso.Message = ex switch
            {
                HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } => "A chave não foi aceita (401). Confira sua configuração do Chatwoot.",
                HttpRequestException { StatusCode: HttpStatusCode.Forbidden } => "A API não permitiu esta consulta (403). As permissões permanecem as do seu usuário.",
                HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "O recurso consultado não está disponível nesta instalação ou conta (404).",
                HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => "O Chatwoot limitou as consultas (429). Aguarde antes de tentar novamente.",
                OperationCanceledException => "O Chatwoot não respondeu dentro do tempo limite. Tente novamente mais tarde.",
                ArgumentException or InvalidOperationException => ex.Message,
                _ => "Não foi possível ler os dados. Confira a conexão e tente novamente."
            };
            _aviso.Severity = InfoBarSeverity.Error; _aviso.IsOpen = true;
        }
        finally { _consulta = null; Ocupado(false); }
    }
    private void Ocupado(bool valor)
    {
        _atualizar.IsEnabled = _fila.IsEnabled = _historico.IsEnabled = _inicio.IsEnabled = _fim.IsEnabled = !valor;
        foreach (var botao in _atalhos) botao.IsEnabled = !valor;
        _progresso.Visibility = _cancelar.Visibility = valor ? Visibility.Visible : Visibility.Collapsed;
    }

    internal void RenderizarResultado(ResultadoMetricas resultado, string contexto)
    { _resultado = resultado; _contexto = contexto; Renderizar(); }

    private void Renderizar()
    {
        if (_resultado is not { } resultado) return;
        var uteis = _baseTempo.SelectedIndex == 1;
        var baseTexto = uteis ? "horário comercial do Chatwoot" : "tempo corrido";
        _conteudo.Children.Clear();
        _conteudo.Children.Add(Texto($"{_contexto} · {resultado.Tickets.Count} ticket(s) listados{(resultado.Parcial ? " · RESULTADO PARCIAL" : "")}", 16, true));
        _conteudo.Children.Add(Texto($"Médias dos seus eventos oficiais · {baseTexto}. Quantidades representam registros, não tickets distintos. Horas úteis seguem o cadastro do Chatwoot."));
        var resumo = ResumoMetricas.Calcular(resultado.Tickets, uteis);
        var indicadores = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        var cartoes = new[] { ("Primeira resposta", resumo.PrimeiraResposta), ("Espera do cliente", resumo.RespostasSeguintes), ("Resolução", resumo.Resolucao) };
        // Empilha em janelas estreitas para manter valores e rótulos legíveis.
        var colunas = _colunasResumo;
        for (var i = 0; i < colunas; i++) indicadores.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < cartoes.Length; i++)
        {
            if (i % colunas == 0) indicadores.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var (nome, valor) = cartoes[i];
            var painel = new StackPanel { Spacing = 6 };
            painel.Children.Add(Texto(nome, 14, true));
            painel.Children.Add(Texto(PeriodoMetricas.Duracao(valor.MediaSegundos), 24, true));
            painel.Children.Add(Texto($"{valor.Quantidade} registro(s) com duração disponível"));
            var card = Card(painel); Grid.SetColumn(card, i % colunas); Grid.SetRow(card, i / colunas); indicadores.Children.Add(card);
        }
        _conteudo.Children.Add(indicadores);
        var confirmados = resultado.Tickets.Where(x => x.PeriodoConfirmado).ToArray();
        var sla = new StackPanel { Spacing = 6 };
        sla.Children.Add(Texto("Situação atual do SLA dos tickets do recorte", 18, true));
        sla.Children.Add(Texto($"Em acompanhamento: {confirmados.Count(x => x.Sla?.Estado == "active")} · Cumpridos: {confirmados.Count(x => x.Sla?.Estado == "hit")} · Com violação: {confirmados.Count(x => x.Sla?.Estado is "missed" or "active_with_misses")} · Sem política/dados: {confirmados.Count(x => x.Sla is null || x.Sla.Estado is not ("active" or "hit" or "missed" or "active_with_misses"))}"));
        sla.Children.Add(Texto("O estado é informado pelo Chatwoot no momento da consulta. Em acompanhamento não significa meta já cumprida; reaberturas podem gerar mais de uma resolução."));
        _conteudo.Children.Add(Card(sla));
        foreach (var aviso in resultado.Avisos) _conteudo.Children.Add(Texto(aviso));
        if (resultado.Tickets.Count == 0) _conteudo.Children.Add(Texto("Nenhum ticket com dados retornados para este recorte. Isso não confirma ausência de atendimentos transferidos ou de registros inacessíveis."));
        foreach (var ticket in resultado.Tickets)
        {
            var painel = new StackPanel { Spacing = 6 };
            painel.Children.Add(Texto($"Ticket #{ticket.Id} · {Situacao(ticket.Status)}", 14, true));
            painel.Children.Add(Texto(ticket.Contato, 20, true)); painel.Children.Add(Texto(ticket.Empresa));
            if (!ticket.PeriodoConfirmado) painel.Children.Add(Texto("Presença no período não confirmada · fora do resumo de SLA"));
            var valores = ResumoMetricas.Calcular(new[] { ticket }, uteis);
            painel.Children.Add(Texto($"Médias · Primeira resposta: {PeriodoMetricas.Duracao(valores.PrimeiraResposta.MediaSegundos)} · Espera do cliente: {PeriodoMetricas.Duracao(valores.RespostasSeguintes.MediaSegundos)} · Resolução: {PeriodoMetricas.Duracao(valores.Resolucao.MediaSegundos)}"));
            painel.Children.Add(Texto(ticket.Sla is { } politica ? $"SLA: {politica.Nome} · {politica.Situacao}" : "SLA: política não retornada"));
            if (ticket.Aviso is { } motivo) painel.Children.Add(Texto(motivo));
            else if (ticket.Eventos.Count == 0) painel.Children.Add(Texto("Sem eventos pessoais de resposta/resolução retornados para este recorte."));
            else if (valores.PrimeiraResposta.Quantidade + valores.RespostasSeguintes.Quantidade + valores.Resolucao.Quantidade == 0)
                painel.Children.Add(Texto("Os eventos retornados não informam duração nesta base de tempo."));
            var detalhes = new Button { Content = "Ver detalhes" };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(detalhes, $"Ver detalhes do ticket {ticket.Id}");
            detalhes.Click += async (_, _) => await MostrarDetalhesAsync(ticket);
            painel.Children.Add(detalhes); _conteudo.Children.Add(Card(painel));
        }
    }
    private async Task MostrarDetalhesAsync(TicketMetricas ticket)
    {
        var painel = new StackPanel { Spacing = 10 };
        painel.Children.Add(Texto($"{ticket.Contato} · {ticket.Empresa}"));
        painel.Children.Add(Texto("Datas em Brasília. Metas e vencimentos abaixo são os retornados pelo Chatwoot; a seleção de tempo das médias não altera a política."));
        if (ticket.Sla is { } sla)
        {
            painel.Children.Add(Texto($"{sla.Nome} · {sla.Situacao}", 18, true));
            painel.Children.Add(Texto(sla.ApenasHorarioComercial switch { true => "Política em horário comercial do Chatwoot", false => "Política em tempo corrido", _ => "Base de tempo da política não informada" }));
            painel.Children.Add(Texto($"Primeira resposta · meta: {PeriodoMetricas.Duracao(sla.PrimeiraRespostaMeta)} · vencimento: {PeriodoMetricas.Data(sla.PrimeiraRespostaVence)}"));
            painel.Children.Add(Texto($"Próxima resposta (NRT) · meta: {PeriodoMetricas.Duracao(sla.ProximaRespostaMeta)} · vencimento: {PeriodoMetricas.Data(sla.ProximaRespostaVence)}"));
            painel.Children.Add(Texto($"Resolução · meta: {PeriodoMetricas.Duracao(sla.ResolucaoMeta)} · vencimento: {PeriodoMetricas.Data(sla.ResolucaoVence)}"));
            painel.Children.Add(Texto($"Conclusão do SLA: {PeriodoMetricas.Data(sla.ConcluidoEm)}"));
        }
        else painel.Children.Add(Texto("A API não retornou política aplicada. Isso não informa quais regras internas de suporte existem."));
        painel.Children.Add(Texto("Ocorrências de SLA retornadas · histórico completo do ticket", 16, true));
        foreach (var ocorrencia in ticket.Ocorrencias) painel.Children.Add(Texto($"{NomeIndicador(ocorrencia.Tipo)} · {PeriodoMetricas.Data(ocorrencia.RegistradaEm)}"));
        if (ticket.Ocorrencias.Count == 0) painel.Children.Add(Texto("Nenhuma ocorrência retornada; ausência de dados não comprova cumprimento."));
        painel.Children.Add(Texto("Eventos pessoais do recorte consultado", 16, true));
        foreach (var evento in ticket.Eventos) painel.Children.Add(Texto($"{NomeIndicador(evento.Nome)} · {PeriodoMetricas.Data(evento.RegistradoEm)} · corrido: {PeriodoMetricas.Duracao(evento.Segundos)} · útil: {PeriodoMetricas.Duracao(evento.SegundosUteis)}"));
        if (ticket.Eventos.Count == 0) painel.Children.Add(Texto(ticket.Aviso ?? "Sem eventos pessoais retornados para o recorte."));
        await new ContentDialog { XamlRoot = XamlRoot, Title = $"Ticket #{ticket.Id}", Content = new ScrollViewer { Content = painel, MaxHeight = 450 }, CloseButtonText = "Fechar" }.ShowAsync();
    }
    private static string Situacao(string estado) => estado switch { "open" => "Em aberto", "snoozed" => "Adiado", "resolved" => "Resolvido", "pending" => "Pendente", _ => "Situação não informada" };
    private static string NomeIndicador(string nome) => nome switch { "first_response" or "frt" => "Primeira resposta (FRT)", "reply_time" => "Espera do cliente", "nrt" => "Próxima resposta (NRT)", "conversation_resolved" or "rt" => "Resolução", _ => "Tipo não reconhecido" };
    private static TextBlock Texto(string texto, double tamanho = 14, bool destaque = false) => new() { Text = texto, TextWrapping = TextWrapping.Wrap, FontSize = tamanho, FontWeight = destaque ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal };
    private static Border Card(UIElement conteudo) => new() { Child = conteudo, Padding = new Thickness(16), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(60,128,128,128)), Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] };
}
