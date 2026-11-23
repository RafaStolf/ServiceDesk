using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ServiceDesk.Models;
using ServiceDesk.Services;
using System.Net;

namespace ServiceDesk;


/// <summary>Tela sob demanda: não participa da sincronização periódica da fila.</summary>
public sealed class MetricasPage : Page
{
    private readonly CalendarDatePicker _inicio = new() { Header = "Início", DateFormat = "{day.integer(2)}/{month.integer(2)}/{year.full}" };
    private readonly CalendarDatePicker _fim = new() { Header = "Fim", DateFormat = "{day.integer(2)}/{month.integer(2)}/{year.full}" };
    private readonly ComboBox _baseTempo = new() { Header = "Base de tempo", ItemsSource = new[] { "Tempo corrido", "Horário comercial do Chatwoot" }, SelectedIndex = 0 };
    private readonly Button _atualizar = new() { Content = "Atualizar" };
    private readonly Button _cancelar = new() { Content = "Cancelar consulta", Visibility = Visibility.Collapsed };
    private readonly TextBlock _status = Texto("Preparando a consulta…");
    private readonly InfoBar _aviso = new() { IsOpen = false, IsClosable = true };
    private readonly ProgressBar _progresso = new() { IsIndeterminate = true, Visibility = Visibility.Collapsed };
    private readonly StackPanel _conteudo = new() { Spacing = 16 };
    private CancellationTokenSource? _consulta;
    private ResultadoMetricas? _resultado;
    private string _contexto = "";
    private bool _ativa;
    private int _colunasResumo = 3;

    internal MetricasPage()
    {
        var hoje = PeriodoMetricas.Hoje;
        _inicio.Date = DataCalendario(hoje.AddDays(-6));
        _fim.Date = DataCalendario(hoje);
        var raiz = new Grid { Padding = new Thickness(20, 16, 20, 16), RowSpacing = 12 };
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition());
        var cabecalho = new Grid { ColumnSpacing = 12 };
        cabecalho.ColumnDefinitions.Add(new ColumnDefinition());
        cabecalho.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titulo = new StackPanel { Spacing = 4 };
        titulo.Children.Add(new TextBlock { Text = "Métricas e SLA", Style = (Style)Application.Current.Resources["TitleTextBlockStyle"] });
        titulo.Children.Add(_status);
        cabecalho.Children.Add(titulo);
        var acoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Top };
        _atualizar.Content = VisualServiceDesk.ItemLateral(VisualServiceDesk.Icone("\uE72C"), "Atualizar");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_atualizar, "Atualizar");
        acoes.Children.Add(_cancelar);
        acoes.Children.Add(_atualizar);
        Grid.SetColumn(acoes, 1);
        cabecalho.Children.Add(acoes);
        raiz.Children.Add(cabecalho);

        var corpo = new StackPanel { Spacing = 14 };
        var filtros = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        for (var i = 0; i < 3; i++) filtros.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < 3; i++) filtros.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _inicio.HorizontalAlignment = _fim.HorizontalAlignment = _baseTempo.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(_fim, 1);
        filtros.Children.Add(_inicio);
        filtros.Children.Add(_fim);
        filtros.Children.Add(_baseTempo);
        corpo.Children.Add(VisualServiceDesk.Painel(filtros));
        corpo.Children.Add(_progresso);
        corpo.Children.Add(_aviso);
        corpo.Children.Add(_conteudo);

        var guia = new StackPanel { Spacing = 10 };
        guia.Children.Add(Texto("As médias usam os eventos oficiais do Chatwoot. A seleção da base de tempo altera as médias; as metas e os estados do SLA vêm da política aplicada no Chatwoot."));
        guia.Children.Add(Texto(OrientacoesN1.Contexto));
        foreach (var (tituloEtapa, descricao) in OrientacoesN1.Etapas)
        {
            guia.Children.Add(Texto(tituloEtapa, 14, true));
            guia.Children.Add(Texto(descricao));
        }
        guia.Children.Add(Texto(OrientacoesN1.Limites));
        corpo.Children.Add(new Expander
        {
            Header = "Entender as métricas e consultar o fluxo N1",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = guia
        });

        var scroll = new ScrollViewer { Content = corpo, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Disabled };
        raiz.Children.Remove(cabecalho);
        corpo.Children.Insert(0, cabecalho);
        Grid.SetRow(scroll, 1);
        raiz.Children.Add(scroll);
        Content = raiz;
        SizeChanged += (_, e) =>
        {
            var colunas = e.NewSize.Width < 690 ? 1 : 3;
            var compacto = colunas == 1;
            if (cabecalho.RowDefinitions.Count == 0)
            {
                cabecalho.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                cabecalho.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            Grid.SetColumnSpan(titulo, compacto ? 2 : 1);
            Grid.SetColumn(acoes, compacto ? 0 : 1);
            Grid.SetColumnSpan(acoes, compacto ? 2 : 1);
            Grid.SetRow(acoes, compacto ? 1 : 0);
            acoes.Orientation = e.NewSize.Width < 330 ? Orientation.Vertical : Orientation.Horizontal;
            raiz.Padding = new Thickness(compacto ? 8 : 20);
            Grid.SetColumnSpan(_inicio, compacto ? 3 : 1);
            Grid.SetColumn(_fim, compacto ? 0 : 1);
            Grid.SetRow(_fim, compacto ? 1 : 0);
            Grid.SetColumnSpan(_fim, compacto ? 3 : 1);
            Grid.SetColumn(_baseTempo, compacto ? 0 : 2);
            Grid.SetColumnSpan(_baseTempo, compacto ? 3 : 1);
            Grid.SetRow(_baseTempo, compacto ? 2 : 0);
            if (colunas != _colunasResumo) { _colunasResumo = colunas; Renderizar(); }
        };
        _atualizar.Click += async (_, _) => await CarregarAsync();
        _cancelar.Click += (_, _) => _consulta?.Cancel();
        _baseTempo.SelectionChanged += (_, _) => Renderizar();
        Loaded += async (_, _) => { _ativa = true; if (_resultado is null) await CarregarAsync(); };
        Unloaded += (_, _) => { _ativa = false; _consulta?.Cancel(); };
    }

    private static DateTimeOffset DataCalendario(DateOnly data) => new(data.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    private async Task CarregarAsync()
    {
        if (_consulta is not null || !_ativa) return;
        using var cts = new CancellationTokenSource();
        _consulta = cts;
        Ocupado(true);
        _aviso.IsOpen = false;
        try
        {
            if (_inicio.Date is not { } inicioSelecionado || _fim.Date is not { } fimSelecionado)
                throw new ArgumentException("Informe as datas de início e fim.");
            var inicio = DateOnly.FromDateTime(inicioSelecionado.DateTime);
            var fim = DateOnly.FromDateTime(fimSelecionado.DateTime);
            var contexto = $"{inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy} · Brasília";
            var progresso = new Progress<string>(texto => { if (_ativa && _consulta == cts && !cts.IsCancellationRequested) _status.Text = texto; });
            using var servico = new MetricasChatwootService(CarregadorConfiguracaoChatwoot.Carregar());
            var resultado = await servico.CarregarPeriodoAsync(inicio, fim, progresso, cts.Token);
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
        _atualizar.IsEnabled = _inicio.IsEnabled = _fim.IsEnabled = !valor;
        _progresso.Visibility = _cancelar.Visibility = valor ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Renderizar()
    {
        if (_resultado is not { } resultado) return;
        var uteis = _baseTempo.SelectedIndex == 1;
        _conteudo.Children.Clear();
        _conteudo.Children.Add(VisualServiceDesk.Estilizar(Texto(_contexto), "TextoSecundarioServiceDeskStyle"));
        if (resultado.Parcial)
            _conteudo.Children.Add(new InfoBar { Title = "Resultado parcial", Message = "Alguns dados da consulta estão indisponíveis.",
                IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Warning });

        var resumo = ResumoMetricas.Calcular(resultado.Tickets, uteis);
        var indicadores = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        var cartoes = new[] { ("Primeira resposta", resumo.PrimeiraResposta), ("Espera do cliente", resumo.RespostasSeguintes), ("Resolução", resumo.Resolucao) };
        for (var i = 0; i < _colunasResumo; i++) indicadores.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < cartoes.Length; i++)
        {
            if (i % _colunasResumo == 0) indicadores.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var (nome, valor) = cartoes[i];
            var painel = new StackPanel { Spacing = 5 };
            painel.Children.Add(Texto(nome, 16, true));
            painel.Children.Add(Texto(PeriodoMetricas.Duracao(valor.MediaSegundos), 28, true));
            painel.Children.Add(VisualServiceDesk.Estilizar(Texto($"{valor.Quantidade} registro(s) com duração"), "TextoSecundarioServiceDeskStyle"));
            var card = VisualServiceDesk.Painel(painel);
            Grid.SetColumn(card, i % _colunasResumo);
            Grid.SetRow(card, i / _colunasResumo);
            indicadores.Children.Add(card);
        }
        _conteudo.Children.Add(indicadores);
        _conteudo.Children.Add(GraficoSla(SituacaoSla.Calcular(resultado.Tickets)));
        foreach (var aviso in resultado.Avisos) _conteudo.Children.Add(VisualServiceDesk.Estilizar(Texto(aviso), "TextoSecundarioServiceDeskStyle"));

        _conteudo.Children.Add(Texto("Detalhes por ticket", 20, true));
        if (resultado.Tickets.Count == 0)
        {
            _conteudo.Children.Add(Texto("Nenhum ticket com dados para o período selecionado."));
            return;
        }
        var tabela = new StackPanel();
        var largo = _colunasResumo == 3;
        if (largo)
            tabela.Children.Add(LinhaTicket(Texto("Ticket", 14, true), Texto("Contato", 14, true), Texto("SLA", 14, true), null));
        foreach (var ticket in resultado.Tickets)
        {
            if (tabela.Children.Count > 0)
                tabela.Children.Add(VisualServiceDesk.Estilizar(new Border { Height = 1 }, "DivisorServiceDeskStyle"));
            var detalhes = new Button { Content = "Ver detalhes", HorizontalAlignment = HorizontalAlignment.Left };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(detalhes, $"Ver detalhes do ticket {ticket.Id}");
            detalhes.Click += async (_, _) => await MostrarDetalhesAsync(ticket);
            var contato = new StackPanel { Spacing = 2 };
            contato.Children.Add(Texto(ticket.Contato, 15, true));
            contato.Children.Add(VisualServiceDesk.Estilizar(Texto(ticket.Empresa, 12), "TextoSecundarioServiceDeskStyle"));
            var situacao = ticket.PeriodoConfirmado ? ticket.Sla?.Situacao ?? "Sem política ou dados" : "Presença no período não confirmada";
            var id = Texto($"#{ticket.Id}", 14, true);
            if (largo)
                tabela.Children.Add(LinhaTicket(id, contato, Texto(situacao), detalhes));
            else
                tabela.Children.Add(new StackPanel { Spacing = 6, Padding = new Thickness(0, 8, 0, 8), Children = { id, contato, Texto(situacao), detalhes } });
        }
        _conteudo.Children.Add(VisualServiceDesk.Painel(tabela));
    }

    private static Grid LinhaTicket(FrameworkElement id, FrameworkElement contato, FrameworkElement situacao, FrameworkElement? detalhes)
    {
        var linha = new Grid { Padding = new Thickness(0, 8, 0, 8), ColumnSpacing = 12 };
        linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
        linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) });
        linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) });
        linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
        linha.Children.Add(id);
        Grid.SetColumn(contato, 1);
        linha.Children.Add(contato);
        Grid.SetColumn(situacao, 2);
        linha.Children.Add(situacao);
        if (detalhes is not null)
        {
            Grid.SetColumn(detalhes, 3);
            linha.Children.Add(detalhes);
        }
        return linha;
    }

    private static Border GraficoSla(SituacaoSla sla)
    {
        var painel = new StackPanel { Spacing = 8 };
        painel.Children.Add(Texto("Situação do SLA", 20, true));
        painel.Children.Add(VisualServiceDesk.Estilizar(Texto("Status informado pelo Chatwoot na consulta"), "TextoSecundarioServiceDeskStyle"));
        var dados = new[]
        {
            ("active", sla.Ativos, "BarraAtivaServiceDeskStyle"),
            ("hit", sla.Cumpridos, "BarraCumpridaServiceDeskStyle"),
            ("missed", sla.Violados, "BarraVioladaServiceDeskStyle"),
            ("active_with_misses", sla.AtivosComViolacoes, "BarraAdiadaServiceDeskStyle")
        };
        var maximo = Math.Max(1, dados.Max(estado => estado.Item2));
        var barras = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        barras.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        barras.ColumnDefinitions.Add(new ColumnDefinition());
        barras.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (var i = 0; i < dados.Length; i++)
        {
            var (codigo, quantidade, estilo) = dados[i];
            barras.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var legenda = Texto(PoliticaSla.RotuloEstado(codigo));
            legenda.MaxWidth = 152;
            Grid.SetRow(legenda, i);
            barras.Children.Add(legenda);
            var trilho = new Grid { Height = 12, VerticalAlignment = VerticalAlignment.Center };
            trilho.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(quantidade, GridUnitType.Star) });
            trilho.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(maximo - quantidade, GridUnitType.Star) });
            var fundo = VisualServiceDesk.Estilizar(new Border { Opacity = 0.35, CornerRadius = new CornerRadius(3) }, "DivisorServiceDeskStyle");
            Grid.SetColumnSpan(fundo, 2);
            trilho.Children.Add(fundo);
            if (quantidade > 0)
                trilho.Children.Add(VisualServiceDesk.Estilizar(new Border { CornerRadius = new CornerRadius(3) }, estilo));
            Grid.SetColumn(trilho, 1);
            Grid.SetRow(trilho, i);
            barras.Children.Add(trilho);
            var total = Texto(quantidade.ToString(), 14, true);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(total, $"{legenda.Text}: {quantidade}");
            Grid.SetColumn(total, 2);
            Grid.SetRow(total, i);
            barras.Children.Add(total);
        }
        painel.Children.Add(barras);
        painel.Children.Add(VisualServiceDesk.Estilizar(Texto($"Sem política ou dados: {sla.SemDados}"), "TextoSecundarioServiceDeskStyle"));
        return VisualServiceDesk.Painel(painel);
    }

    private async Task MostrarDetalhesAsync(TicketMetricas ticket)
    {
        var painel = new StackPanel { Spacing = 10 };
        painel.Children.Add(Texto($"{ticket.Contato} · {ticket.Empresa}"));
        painel.Children.Add(Texto(Situacao(ticket.Status)));
        if (ticket.Aviso is { } aviso) painel.Children.Add(Texto(aviso));
        var resumo = ResumoMetricas.Calcular([ticket], _baseTempo.SelectedIndex == 1);
        painel.Children.Add(Texto($"Médias · Primeira resposta: {PeriodoMetricas.Duracao(resumo.PrimeiraResposta.MediaSegundos)} · Espera do cliente: {PeriodoMetricas.Duracao(resumo.RespostasSeguintes.MediaSegundos)} · Resolução: {PeriodoMetricas.Duracao(resumo.Resolucao.MediaSegundos)}"));
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
        painel.Children.Add(Texto("Eventos do período consultado", 16, true));
        foreach (var evento in ticket.Eventos) painel.Children.Add(Texto($"{NomeIndicador(evento.Nome)} · {PeriodoMetricas.Data(evento.RegistradoEm)} · corrido: {PeriodoMetricas.Duracao(evento.Segundos)} · útil: {PeriodoMetricas.Duracao(evento.SegundosUteis)}"));
        if (ticket.Eventos.Count == 0) painel.Children.Add(Texto(ticket.Aviso ?? "Sem eventos retornados para o período."));
        await new ContentDialog { XamlRoot = XamlRoot, Title = $"Ticket #{ticket.Id}", Content = new ScrollViewer { Content = painel, MaxHeight = 450 }, CloseButtonText = "Fechar" }.ShowAsync();
    }
    private static string Situacao(string estado) => estado switch { "open" => "Em aberto", "snoozed" => "Adiado", "resolved" => "Resolvido", "pending" => "Pendente", _ => "Situação não informada" };
    private static string NomeIndicador(string nome) => nome switch { "first_response" or "frt" => "Primeira resposta (FRT)", "reply_time" => "Espera do cliente", "nrt" => "Próxima resposta (NRT)", "conversation_resolved" or "rt" => "Resolução", _ => "Tipo não reconhecido" };
    private static TextBlock Texto(string texto, double tamanho = 14, bool destaque = false) => new() { Text = texto, TextWrapping = TextWrapping.Wrap, FontSize = tamanho, FontWeight = destaque ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal };
}
