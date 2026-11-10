using System.Runtime.InteropServices;

namespace ServiceDesk.Services;

/// <summary>Ícone nativo na área de notificação, ligado ao HWND WinUI.</summary>
public sealed class IconeBandeja : IDisposable
{
    private const uint Callback = 0x8001;
    private readonly nint _janela;
    private readonly SubclassProc _procedimento;
    private readonly Action _abrir;
    private readonly Action _sair;
    private readonly uint _explorerReiniciado;
    private DadosIcone _dados;
    private bool _descartado;
    private readonly bool _iconeProprio;
    public bool Disponivel { get; private set; }

    public IconeBandeja(nint janela, Action abrir, Action sair)
    {
        _janela = janela;
        _abrir = abrir;
        _sair = sair;
        _procedimento = Processar;
        _explorerReiniciado = RegisterWindowMessage("TaskbarCreated");
        var icone = LoadImage(0, Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"), 1, 16, 16, 0x10);
        _iconeProprio = icone != 0;
        if (icone == 0) icone = LoadIcon(0, (nint)32512);
        _dados = new DadosIcone { Tamanho = (uint)Marshal.SizeOf<DadosIcone>(), Janela = janela, Id = 1, Flags = 1 | 2 | 4, Mensagem = Callback, Icone = icone, Dica = "ServiceDesk — clique para abrir", Info = "", Titulo = "" };
        if (!SetWindowSubclass(janela, _procedimento, 1, 0)) throw new InvalidOperationException("Não foi possível configurar o ícone do ServiceDesk.");
        Disponivel = ShellNotifyIcon(0, ref _dados);
    }

    private nint Processar(nint janela, uint mensagem, nuint wParam, nint lParam, nuint id, nuint dados)
    {
        if (mensagem == _explorerReiniciado) Disponivel = ShellNotifyIcon(0, ref _dados);
        if (mensagem == Callback)
        {
            var evento = (uint)(long)lParam;
            if (evento == 0x202 || evento == 0x203) _abrir();
            if (evento == 0x205)
            {
                var menu = CreatePopupMenu();
                try
                {
                    AppendMenu(menu, 0, 1, "Abrir ServiceDesk");
                    AppendMenu(menu, 0, 2, "Sair");
                    GetCursorPos(out var ponto);
                    SetForegroundWindow(janela);
                    var acao = TrackPopupMenu(menu, 0x100 | 2, ponto.X, ponto.Y, 0, janela, 0);
                    PostMessage(janela, 0, 0, 0);
                    if (acao == 1) _abrir();
                    if (acao == 2) _sair();
                }
                finally { DestroyMenu(menu); }
            }
            return 0;
        }
        return DefSubclassProc(janela, mensagem, wParam, lParam);
    }

    public void Dispose()
    {
        if (_descartado) return;
        _descartado = true;
        ShellNotifyIcon(2, ref _dados);
        RemoveWindowSubclass(_janela, _procedimento, 1);
        if (_iconeProprio) DestroyIcon(_dados.Icone);
        GC.KeepAlive(_procedimento);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DadosIcone
    {
        public uint Tamanho; public nint Janela; public uint Id; public uint Flags; public uint Mensagem; public nint Icone;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Dica;
        public uint Estado; public uint MascaraEstado;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Versao;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Titulo;
        public uint InfoFlags; public Guid Guid; public nint IconeBalao;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Ponto { public int X; public int Y; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint SubclassProc(nint hwnd, uint msg, nuint wp, nint lp, nuint id, nuint data);
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShellNotifyIcon(uint acao, ref DadosIcone dados);
    [DllImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint msg, nuint wp, nint lp);
    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode)] private static extern nint LoadImage(nint instancia, string nome, uint tipo, int x, int y, uint flags);
    [DllImport("user32.dll", EntryPoint = "LoadIconW")] private static extern nint LoadIcon(nint instancia, nint nome);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icone);
    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string nome);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AppendMenu(nint menu, uint flags, nuint id, string texto);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reservado, nint hwnd, nint rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out Ponto ponto);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(nint hwnd, uint msg, nuint wp, nint lp);
}
