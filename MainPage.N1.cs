using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using ServiceDesk.Models;

namespace ServiceDesk;

public sealed partial class MainPage
{
    private readonly List<CardAtendimento> _cardsAtendimento = [];
    private sealed record CardAtendimento(AtendimentoMonitorado Ticket, AcompanhamentoN1? Acompanhamento,
        Button Controle, Button Menu, TextBlock Estado);

    private Grid CriarCardAtendimento(AtendimentoMonitorado atendimento)
    {
        var bloco = new Grid();
        var conteudo = new StackPanel { Spacing = _preferencias.FilaCompacta ? 1 : 3 };
        var card = VisualServiceDesk.Estilizar(new Button
        {
            Content = conteudo,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsTabStop = true,
            UseSystemFocusVisuals = true
        }, "CardAtendimentoServiceDeskStyle");
        AutomationProperties.SetAutomationId(card, $"CardAtendimento_{atendimento.Id}");
        AutomationProperties.SetName(card, $"Ticket #{atendimento.Id} · {atendimento.Cliente}");
        bloco.Children.Add(card);

        var cabecalho = new Grid { Height = 26, Margin = new Thickness(0, 0, 34, 0) };
        var numero = VisualServiceDesk.Estilizar(TextoCompacto($"#{atendimento.Id}", 11), "TextoIdCardServiceDeskStyle");
        numero.VerticalAlignment = VerticalAlignment.Center;
        cabecalho.Children.Add(numero);
        conteudo.Children.Add(cabecalho);

        var opcoes = new Button
        {
            Content = new FontIcon { Glyph = "\uE712", FontSize = 13 },
            Width = 28, Height = 26, MinWidth = 0, MinHeight = 0,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 6, 11, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            UseSystemFocusVisuals = true
        };
        AutomationProperties.SetAutomationId(opcoes, $"OpcoesAtendimento_{atendimento.Id}");
        AutomationProperties.SetName(opcoes, $"Opções do ticket {atendimento.Id}");
        ToolTipService.SetToolTip(opcoes, "Opções do ticket");
        bloco.Children.Add(opcoes);

        var menu = new MenuFlyout();
        void AdicionarOpcao(string texto, Func<Task> executar)
        {
            var item = new MenuFlyoutItem { Text = texto };
            item.Click += async (_, _) => await executar();
            menu.Items.Add(item);
        }
        AdicionarOpcao("Copiar texto", () => ExportarAtendimentoAsync(atendimento, copiar: true));
        AdicionarOpcao("Exportar chat", () => ExportarAtendimentoAsync(atendimento, copiar: false));
        AdicionarOpcao("Abrir no Chatwoot", () => AbrirAtendimentoAsync(atendimento));
        opcoes.Click += (_, _) => menu.ShowAt(opcoes);
        card.Click += (_, _) => menu.ShowAt(opcoes);

        var nome = TextoCompacto(atendimento.Cliente, 15);
        nome.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        ToolTipService.SetToolTip(nome, nome.Text);
        conteudo.Children.Add(nome);

        if (_preferencias.FilaCompacta) card.Padding = new Thickness(6, 3, 6, 3);
        foreach (var campo in _preferencias.CamposFila)
            conteudo.Children.Add(TextoCompacto(atendimento.ValorDoCampo(campo), 12));

        conteudo.Children.Add(VisualServiceDesk.Estilizar(new Border
            { Height = 1, Margin = new Thickness(0, 2, 0, 2) }, "DivisorServiceDeskStyle"));

        var acompanhamento = AcompanhamentoN1.DoTicket(atendimento);
        var estado = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAutomationId(estado, $"EstadoAtendimento_{atendimento.Id}");
        var glyph = acompanhamento?.Estado switch
        {
            EstadoN1.ClienteAguarda => "\uE7BA",
            EstadoN1.AcompanharRetorno => "\uE73E",
            EstadoN1.AdiadoComPrazo or EstadoN1.AdiadoAteResposta => "\uE823",
            _ => atendimento.Status == "resolved" ? "\uE73E" : "\uE946"
        };
        conteudo.Children.Add(LinhaComIcone(glyph, estado));
        if (acompanhamento?.Estado == EstadoN1.AdiadoComPrazo)
        {
            var detalhe = VisualServiceDesk.Estilizar(TextoCompacto("Retomada agendada", 11), "TextoSecundarioServiceDeskStyle");
            detalhe.Margin = new Thickness(17, 0, 0, 0);
            conteudo.Children.Add(detalhe);
        }

        var exibicao = new CardAtendimento(atendimento, acompanhamento, card, opcoes, estado);
        _cardsAtendimento.Add(exibicao);
        AtualizarEstadoDoCard(exibicao, DateTimeOffset.Now);
        return bloco;
    }

    private static TextBlock TextoCompacto(string texto, double tamanho) => new()
    {
        Text = texto, FontSize = tamanho,
        TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis
    };

    private static Grid LinhaComIcone(string glyph, TextBlock texto)
    {
        var linha = new Grid { ColumnSpacing = 5 };
        linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icone = new FontIcon { Glyph = glyph, FontSize = 12, Width = 12, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
        AutomationProperties.SetAccessibilityView(icone, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        icone.SetBinding(IconElement.ForegroundProperty, new Binding { Source = texto, Path = new PropertyPath(nameof(TextBlock.Foreground)) });
        linha.Children.Add(icone);
        Grid.SetColumn(texto, 1);
        linha.Children.Add(texto);
        return linha;
    }

    private void AtualizarEstadosDosCards(DateTimeOffset agora)
    {
        foreach (var card in _cardsAtendimento) AtualizarEstadoDoCard(card, agora);
    }

    private void AtualizarEstadoDoCard(CardAtendimento card, DateTimeOffset agora)
    {
        var indicacao = card.Acompanhamento?.Descrever(agora, _limiteNotificacaoMinutos, _preferencias.AvisoEtapaN1Minutos);
        var emAtraso = card.Acompanhamento?.Estado == EstadoN1.ClienteAguarda && indicacao?.Destaque == DestaqueN1.PrazoAtingido;
        var estiloCard = (Style)Application.Current.Resources[emAtraso ? "CardAtrasadoServiceDeskStyle" : "CardAtendimentoServiceDeskStyle"];
        if (card.Controle.Style != estiloCard) card.Controle.Style = estiloCard;
        var estilo = card.Acompanhamento?.Estado switch
        {
            EstadoN1.ClienteAguarda => "TextoCriticoServiceDeskStyle",
            EstadoN1.AcompanharRetorno when indicacao?.Destaque != DestaqueN1.Normal => "TextoAtivoCardServiceDeskStyle",
            EstadoN1.AcompanharRetorno => "TextoRespondidoCardServiceDeskStyle",
            EstadoN1.AdiadoComPrazo or EstadoN1.AdiadoAteResposta => "TextoAdiadoServiceDeskStyle",
            _ when card.Ticket.Status == "resolved" => "TextoResolvidoCardServiceDeskStyle",
            _ => "TextoSecundarioServiceDeskStyle"
        };
        VisualServiceDesk.Estilizar(card.Estado, estilo);
        var peso = indicacao?.Destaque == DestaqueN1.PrazoAtingido && card.Acompanhamento?.Estado != EstadoN1.AcompanharRetorno
            ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.SemiBold;
        card.Estado.FontWeight = peso;
        var texto = indicacao?.Texto ?? (card.Ticket.Status switch
        {
            "resolved" => AcompanhamentoN1.DescreverResolucao(card.Ticket.ResolvidoEm, agora),
            "open" => "Em aberto",
            _ => card.Ticket.SituacaoText
        });
        var faixa = ConteudoFila.ActualWidth is > 0 and < 240;
        var exibido = faixa ? texto.Replace("Resposta em atraso", "Atraso").Replace("Aguardando resposta", "Espera")
            .Replace(" · há ", " ").Replace(" min", "m").Replace(" h", "h") : texto;
        card.Estado.FontSize = faixa ? 11 : 12;
        card.Estado.MaxLines = faixa ? 2 : 0;
        card.Estado.TextTrimming = TextTrimming.CharacterEllipsis;
        if (card.Estado.Text != exibido)
        {
            card.Estado.Text = exibido;
            ToolTipService.SetToolTip(card.Estado, texto);
            AutomationProperties.SetHelpText(card.Controle, texto);
        }
    }
}
