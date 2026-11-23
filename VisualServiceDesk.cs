using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace ServiceDesk;

internal static class VisualServiceDesk
{
    public static StackPanel ItemLateral(IconElement icone, string texto)
    {
        var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        item.Children.Add(icone);
        item.Children.Add(new TextBlock { Text = texto, VerticalAlignment = VerticalAlignment.Center });
        return item;
    }

    public static FontIcon Icone(string glyph) => new() { Glyph = glyph, FontSize = 18, Width = 20 };

    public static T Estilizar<T>(T elemento, string estilo) where T : FrameworkElement
    {
        elemento.Style = (Style)Application.Current.Resources[estilo];
        return elemento;
    }

    public static Border Painel(UIElement conteudo) =>
        Estilizar(new Border { Child = conteudo }, "PainelServiceDeskStyle");

    public static void Selecionar(ToggleButton botao, bool ativo, string? cor, ElementTheme tema)
    {
        botao.IsChecked = ativo;
        var altoContraste = new AccessibilitySettings().HighContrast;
        var interfaceWindows = new UISettings();
        var escuro = tema == ElementTheme.Dark;
        botao.Background = new SolidColorBrush(!ativo ? Microsoft.UI.Colors.Transparent
            : altoContraste ? interfaceWindows.UIElementColor(UIElementType.Highlight) : Destaque(cor, escuro));
        botao.Foreground = new SolidColorBrush(altoContraste
            ? interfaceWindows.UIElementColor(ativo ? UIElementType.HighlightText : UIElementType.WindowText)
            : ativo ? (escuro && (cor is null or "Azul") ? Color.FromArgb(255, 12, 28, 47) : Microsoft.UI.Colors.White)
            : escuro ? Microsoft.UI.Colors.White : Color.FromArgb(255, 18, 37, 60));
    }

    private static Color Destaque(string? cor, bool escuro) => cor switch
    {
        "Verde" => Color.FromArgb(255, 0, 119, 93),
        "Roxo" => Color.FromArgb(255, 109, 76, 196),
        "Laranja" => Color.FromArgb(255, 169, 72, 0),
        "Vermelho" => Color.FromArgb(255, 180, 50, 50),
        _ => escuro ? Color.FromArgb(255, 109, 173, 255) : Color.FromArgb(255, 33, 102, 176)
    };
}
