using Microsoft.Win32;

namespace ServiceDesk.Services;

public static class InicializacaoWindowsService
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Preferencias = @"Software\ServiceDesk";
    private static string Comando => $"\"{Path.Combine(AppContext.BaseDirectory, "ServiceDesk.exe")}\" --background";

    public static bool IniciarComWindows
    {
        get { using var chave = Registry.CurrentUser.OpenSubKey(Run); return string.Equals(chave?.GetValue("ServiceDesk") as string, Comando, StringComparison.OrdinalIgnoreCase); }
    }

    public static bool ManterEmSegundoPlano
    {
        get { using var chave = Registry.CurrentUser.OpenSubKey(Preferencias); return (int?)chave?.GetValue("SegundoPlano") != 0; }
    }

    public static void Configurar(bool iniciar, bool segundoPlano)
    {
        using var run = Registry.CurrentUser.CreateSubKey(Run);
        if (iniciar) run.SetValue("ServiceDesk", Comando);
        else run.DeleteValue("ServiceDesk", throwOnMissingValue: false);
        using var preferencias = Registry.CurrentUser.CreateSubKey(Preferencias);
        preferencias.SetValue("SegundoPlano", segundoPlano ? 1 : 0);
        preferencias.SetValue("Inicializado", 1);
    }

    public static void Inicializar()
    {
        if (Environment.GetEnvironmentVariable("SERVICEDESK_DIAGNOSTICO") == "1") return;
        using var preferencias = Registry.CurrentUser.OpenSubKey(Preferencias);
        if (preferencias?.GetValue("Inicializado") is null) Configurar(true, true);
        // Atualizar o caminho somente se a inicialização continua habilitada.
        else
        {
            using var run = Registry.CurrentUser.OpenSubKey(Run, writable: true);
            if (run?.GetValue("ServiceDesk") is string) run.SetValue("ServiceDesk", Comando);
        }
    }
}
