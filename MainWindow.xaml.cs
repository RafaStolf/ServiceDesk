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
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
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

    internal void Mostrar()
    {
        AppWindow.Show();
        var janela = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ShowWindow(janela, 9);
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

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint hwnd, int comando);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint hwnd);
}
