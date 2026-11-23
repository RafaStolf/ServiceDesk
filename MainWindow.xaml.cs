using Microsoft.UI.Xaml;
using ServiceDesk.Services;
using System.Runtime.InteropServices;

namespace ServiceDesk;

public sealed partial class MainWindow : Window
{
    private readonly IconeBandeja _bandeja;
    private bool _encerrando;

    public MainWindow()
    {
        InitializeComponent();
        var versao = $"v{AtualizacaoAplicativoService.VersaoAtual}";
        Title = $"ServiceDesk  {versao}";
        Janela.SizeChanged += (_, e) =>
        {
            MarcaCompacta.Text = e.NewSize.Width < 200 ? $"ServiceDesk\n{versao}" : Title;
            MarcaCompacta.Visibility = e.NewSize.Width < 320 ? Visibility.Visible : Visibility.Collapsed;
        };
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        var preferencias = new PreferenciasInterfaceService().Carregar();
        void AoAtivar(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated) return;
            Activated -= AoAtivar;
            DispatcherQueue.TryEnqueue(() => FixarJanela(preferencias.SempreVisivel));
        }
        Activated += AoAtivar;
        if (preferencias.JanelaInicial is { Width: > 0, Height: > 0 } posicao)
        {
            var retangulo = new Windows.Graphics.RectInt32(posicao.X, posicao.Y, posicao.Width, posicao.Height);
            var monitor = Microsoft.UI.Windowing.DisplayArea.GetFromRect(retangulo, Microsoft.UI.Windowing.DisplayAreaFallback.None);
            if (monitor is not null) AppWindow.MoveAndResize(retangulo);
        }
        Janela.ActualThemeChanged += (_, _) => ColorirTitulo();
        Janela.Loaded += (_, _) => ColorirTitulo();
        RootFrame.Navigate(typeof(MainPage));
        _bandeja = new IconeBandeja(WinRT.Interop.WindowNative.GetWindowHandle(this), Mostrar, () => ((App)Application.Current).Sair());
        AppWindow.Closing += (_, args) =>
        {
            if (!_encerrando && InicializacaoWindowsService.ManterEmSegundoPlano && _bandeja.Disponivel)
            {
                args.Cancel = true;
                Ocultar();
            }
        };
    }

    internal void FixarJanela(bool fixar)
    {
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            presenter.IsAlwaysOnTop = fixar;
    }

    private void ColorirTitulo()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var cor = ((Microsoft.UI.Xaml.Media.SolidColorBrush)Janela.Background).Color;
        int fundo = cor.R | cor.G << 8 | cor.B << 16;
        int escuro = Janela.ActualTheme == ElementTheme.Dark ? 1 : 0;
        int primeiroPlano = escuro == 1 ? 0xFFFFFF : 0x222222;
        if (new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast) fundo = primeiroPlano = -1;
        DwmSetWindowAttribute(hwnd, 20, ref escuro, sizeof(int));
        DwmSetWindowAttribute(hwnd, 35, ref fundo, sizeof(int));
        DwmSetWindowAttribute(hwnd, 36, ref primeiroPlano, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int atributo, ref int valor, int tamanho);

    internal void Mostrar()
    {
        AppWindow.Show();
        var janela = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (IsIconic(janela)) ShowWindow(janela, 9);
        Activate();
        SetForegroundWindow(janela);
    }

    internal void Ocultar()
    {
        // Se o shell não tiver área de notificação, mantenha uma janela acessível.
        if (_bandeja.Disponivel) AppWindow.Hide();
    }

    internal void Encerrar()
    {
        _encerrando = true;
        _bandeja.Dispose();
    }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint hwnd, int comando);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint hwnd);
}
