using Microsoft.UI.Xaml;
using ServiceDesk.Services;

namespace ServiceDesk;

public partial class App : Application
{
    private MainWindow? _window;
    private Mutex? _instancia;
    private EventWaitHandle? _abrir;
    private RegisteredWaitHandle? _espera;
    private bool _saindo;
    internal Window MainWindow => _window ?? throw new InvalidOperationException("Janela indisponível.");
    internal ServicoNotificacoes Notificacoes { get; } = new();

    public App()
    {
        UnhandledException += (_, e) =>
        {
            if (Environment.GetEnvironmentVariable("SERVICEDESK_DIAGNOSTICO") == "1")
                File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ServiceDesk-inicializacao.log"), e.Exception + Environment.NewLine);
        };
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var diagnostico = Environment.GetEnvironmentVariable("SERVICEDESK_DIAGNOSTICO") == "1";
        var nome = @"Local\ServiceDesk";
        if (diagnostico) nome += "-Teste-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(AppContext.BaseDirectory)))[..16];
        _abrir = new EventWaitHandle(false, EventResetMode.AutoReset, nome + "-Abrir");
        _instancia = new Mutex(true, nome, out var primeira);
        if (!primeira)
        {
            // Um segundo início pelo Windows não deve abrir outra janela.
            if (!Environment.GetCommandLineArgs().Contains("--background")) _abrir.Set();
            _instancia.Dispose();
            _abrir.Dispose();
            Exit();
            return;
        }
        try { InicializacaoWindowsService.Inicializar(); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        { /* As configurações permitem tentar habilitar novamente sem impedir a abertura. */ }
        ServicoNotificacoes.UsarIdentidadeDaInstalacao();
        _window = new MainWindow();
        Notificacoes.Registrar(() => _window.DispatcherQueue.TryEnqueue(() => _window.Mostrar()));
        _espera = ThreadPool.RegisterWaitForSingleObject(_abrir, (_, _) => _window.DispatcherQueue.TryEnqueue(() => _window.Mostrar()), null, Timeout.Infinite, false);
        _window.Closed += (_, _) => Sair();
        _window.Activate();
        if (Environment.GetCommandLineArgs().Contains("--background")) _window.Ocultar();
        if (diagnostico)
            File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ServiceDesk-window-{Environment.ProcessId}.txt"), WinRT.Interop.WindowNative.GetWindowHandle(_window).ToString());
    }

    internal void Sair()
    {
        if (_saindo) return;
        _saindo = true;
        try { Notificacoes.Dispose(); }
        catch (System.Runtime.InteropServices.COMException) { }
        _window?.Encerrar();
        _espera?.Unregister(null);
        _abrir?.Dispose();
        _instancia?.ReleaseMutex();
        _instancia?.Dispose();
        Exit();
    }
}
