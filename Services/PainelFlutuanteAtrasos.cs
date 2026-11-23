using System.Runtime.InteropServices;

namespace ServiceDesk.Services;

internal sealed class PainelFlutuanteAtrasos : IDisposable
{
    internal const string MonitorPrincipal = "Principal";
    private const uint WsPopup = 0x80000000;
    private const uint WsExTopmost = 0x00000008;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpHideWindow = 0x0080;
    private const uint MonitorDefaultToPrimary = 1;
    private const uint MonitorDefaultToNearest = 2;
    private const uint SpiGetHighContrast = 0x0042;
    private const uint HcfHighContrastOn = 1;
    private const int ColorWindow = 5;
    private const int ColorWindowText = 8;
    private const uint Blackness = 0x00000042;
    private const uint UlwAlpha = 2;
    private const uint FormatoTexto = 0x0004 | 0x0020 | 0x0800 | 0x8000; // DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX | DT_END_ELLIPSIS
    private const int MaxCartoes = 3;
    private const int AlinharDireita = 0x0002;

    private readonly (string Nome, string Tempo)[] _cartoesExibidos = new (string, string)[MaxCartoes];
    private nint _janela;
    private nint _monitor;
    private string? _nomeMonitor;
    private string? _monitorSolicitado;
    private long _proximaBuscaMonitor;
    private string? _fonteSolicitada;
    private string _fonteDisponivel = PreferenciasInterface.FonteFlutuantePadrao;
    private Exibicao? _exibicao;
    private readonly Action<double, double>? _aoMover;
    private readonly Procedimento _procedimento;
    internal PainelFlutuanteAtrasos(Action<double, double>? aoMover = null)
    {
        _aoMover = aoMover;
        _procedimento = ProcessarMensagem;
    }

    private delegate nint Procedimento(nint janela, uint msg, nuint w, nint l, nuint id, nuint dados);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint janela, Procedimento proc, nuint id, nuint dados);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint janela, uint msg, nuint w, nint l);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint janela, out Retangulo limites);

    private nint ProcessarMensagem(nint janela, uint msg, nuint w, nint l, nuint id, nuint dados)
    {
        if (msg == 0x84) return 2; // WM_NCHITTEST: arrastar pela área do card.
        if (msg == 0x232 && _exibicao is { } exibicao && GetWindowRect(janela, out var r))
        {
            var area = exibicao.Area;
            _aoMover?.Invoke(Math.Clamp((r.Esquerda - area.Esquerda) / (double)Math.Max(1, area.Largura - r.Largura), 0, 1),
                Math.Clamp((r.Superior - area.Superior) / (double)Math.Max(1, area.Altura - r.Altura), 0, 1));
        }
        return DefSubclassProc(janela, msg, w, l);
    }

    private bool _visivel;
    private bool _descartado;

    internal void Atualizar(IReadOnlyList<(string Nome, string Tempo)> cards, int restantes, bool temaClaro,
        PreferenciasInterface preferencias)
    {
        if (_descartado) return;

        if (cards.Count == 0 || !ObterMonitor(preferencias.MonitorFlutuante, out var area) || !PosicionarNoMonitor(area))
        {
            Ocultar();
            return;
        }

        int dpi = (int)GetDpiForWindow(_janela);
        if (dpi == 0)
        {
            Ocultar();
            return;
        }

        var medidas = new Medidas(area, dpi, preferencias.EscalaFlutuante, cards.Count, restantes, preferencias.PainelCompacto);
        if (medidas.LarguraMaxima < Escalar(100, dpi) || medidas.Altura > medidas.AlturaDisponivel)
        {
            Ocultar();
            return;
        }

        var exibicao = new Exibicao(area, dpi, preferencias.EscalaFlutuante, preferencias.OpacidadeFlutuante, ResolverFonte(preferencias.FonteFlutuante),
            preferencias.PosicaoFlutuante, preferencias.PosicaoRelativaX, preferencias.PosicaoRelativaY, ObterPaleta(temaClaro), medidas.Quantidade, medidas.Restantes, preferencias.PainelCompacto);
        if (_visivel && _exibicao == exibicao && CartoesIguais(cards, medidas.Quantidade)) return;

        if (!Desenhar(cards, exibicao, medidas) || (!_visivel && !SetWindowPos(_janela, -1, 0, 0, 0, 0,
            SwpNoSize | SwpNoMove | SwpNoActivate | SwpShowWindow)))
        {
            Ocultar();
            return;
        }

        _visivel = true;
        _exibicao = exibicao;
        Array.Clear(_cartoesExibidos);
        for (int i = 0; i < medidas.Quantidade; i++) _cartoesExibidos[i] = cards[i];
    }

    internal void Ocultar()
    {
        if (_visivel && !SetWindowPos(_janela, 0, 0, 0, 0, 0,
            SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpHideWindow))
        {
            DestroyWindow(_janela);
            _janela = 0;
        }

        _visivel = false;
        if (_exibicao is not null) Array.Clear(_cartoesExibidos);
        _exibicao = null;
        _monitor = 0;
        _nomeMonitor = null;
        _proximaBuscaMonitor = 0;
    }

    public void Dispose()
    {
        if (_descartado) return;

        Ocultar();
        if (_janela != 0) DestroyWindow(_janela);
        _janela = 0;
        _descartado = true;
    }

    internal sealed record OpcaoMonitor(string? Id, string Rotulo);

    internal static string[] ListarFontes()
    {
        var fontes = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase) { PreferenciasInterface.FonteFlutuantePadrao };
        var dc = CreateCompatibleDC(0);
        if (dc == 0) return fontes.ToArray();
        try
        {
            var filtro = new LogFont { ConjuntoCaracteres = 1, Nome = "" }; // DEFAULT_CHARSET
            EnumFontFamiliesEx(dc, ref filtro, (font, _, tipo, _) =>
            {
                const uint fonteRaster = 1;
                const byte conjuntoSimbolos = 2;
                var familia = Marshal.PtrToStructure<LogFont>(font);
                if ((tipo & fonteRaster) == 0 && familia.ConjuntoCaracteres != conjuntoSimbolos
                    && familia.Nome is { Length: > 0 } nome && nome[0] != '@')
                    fontes.Add(nome);
                return 1;
            }, 0, 0);
            return fontes.ToArray();
        }
        finally { DeleteDC(dc); }
    }

    private string ResolverFonte(string fonte)
    {
        if (_fonteSolicitada != fonte)
        {
            _fonteSolicitada = fonte;
            _fonteDisponivel = fonte == PreferenciasInterface.FonteFlutuantePadrao ? fonte
                : ListarFontes().FirstOrDefault(familia => string.Equals(familia, fonte, StringComparison.OrdinalIgnoreCase))
                    ?? PreferenciasInterface.FonteFlutuantePadrao;
        }
        return _fonteDisponivel;
    }

    internal static IReadOnlyList<OpcaoMonitor> ListarMonitores() =>
        EnumerarMonitores().OrderBy(tela => tela.Dados.Nome, StringComparer.Ordinal)
            .Select(tela =>
            {
                const string prefixo = @"\\.\DISPLAY";
                var dados = tela.Dados;
                var numero = dados.Nome.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase) ? dados.Nome[prefixo.Length..] : dados.Nome;
                var principal = (dados.Flags & 1) != 0 ? " (principal)" : "";
                return new OpcaoMonitor(tela.Id, $"Monitor {numero} · {dados.Area.Largura} × {dados.Area.Altura}{principal}");
            }).ToArray();

    private static List<(nint Handle, InformacoesMonitor Dados, string Id)> EnumerarMonitores()
    {
        var monitores = new List<(nint, InformacoesMonitor, string)>();
        EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
        {
            if (LerMonitor(monitor, out var dados)) monitores.Add((monitor, dados, IdDoMonitor(dados.Nome)));
            return true;
        }, 0);
        return monitores;
    }

    private static string IdDoMonitor(string nome)
    {
        var dispositivo = new DispositivoTela { Tamanho = (uint)Marshal.SizeOf<DispositivoTela>() };
        return EnumDisplayDevices(nome, 0, ref dispositivo, 1) && !string.IsNullOrWhiteSpace(dispositivo.Id)
            ? dispositivo.Id : nome;
    }

    private bool ObterMonitor(string? monitorEscolhido, out Retangulo area)
    {
        if (!string.Equals(_monitorSolicitado, monitorEscolhido, StringComparison.OrdinalIgnoreCase))
        {
            _monitorSolicitado = monitorEscolhido;
            _monitor = 0;
            _nomeMonitor = null;
            _proximaBuscaMonitor = 0;
        }

        if (monitorEscolhido is null) return ObterMonitorAutomatico(out area);
        if (monitorEscolhido == MonitorPrincipal)
            _monitor = MonitorFromPoint(default, MonitorDefaultToPrimary);
        else if (_monitor == 0 || Environment.TickCount64 >= _proximaBuscaMonitor
            || !LerMonitor(_monitor, out var atual) || atual.Nome != _nomeMonitor)
        {
            _monitor = EnumerarMonitores().FirstOrDefault(tela =>
                string.Equals(tela.Id, monitorEscolhido, StringComparison.OrdinalIgnoreCase)).Handle;
            if (_monitor == 0) _monitor = MonitorFromPoint(default, MonitorDefaultToPrimary);
            _proximaBuscaMonitor = Environment.TickCount64 + 3000;
        }
        return LerAreaSelecionada(out area);
    }

    private bool ObterMonitorAutomatico(out Retangulo area)
    {
        // Quando o Windows troca o HMONITOR durante uma mudança de exibição,
        // o nome do dispositivo permite manter o painel no mesmo monitor.
        if (_nomeMonitor is not null &&
            (!LerMonitor(_monitor, out var atual) || atual.Nome != _nomeMonitor))
        {
            _monitor = 0;
            EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
            {
                if (!LerMonitor(monitor, out var candidato) || candidato.Nome != _nomeMonitor) return true;
                _monitor = monitor;
                return false;
            }, 0);
        }

        if (_monitor == 0)
        {
            var primeiroPlano = GetForegroundWindow();
            _monitor = primeiroPlano != 0
                ? MonitorFromWindow(primeiroPlano, MonitorDefaultToNearest)
                : MonitorFromPoint(default, MonitorDefaultToPrimary);
        }

        return LerAreaSelecionada(out area);
    }

    private bool LerAreaSelecionada(out Retangulo area)
    {
        var encontrado = LerMonitor(_monitor, out var informacoes);
        _nomeMonitor = encontrado ? informacoes.Nome : null;
        area = informacoes.Trabalho;
        return encontrado && area.Largura > 0 && area.Altura > 0;
    }

    private static bool LerMonitor(nint monitor, out InformacoesMonitor informacoes)
    {
        informacoes = new InformacoesMonitor { Tamanho = (uint)Marshal.SizeOf<InformacoesMonitor>() };
        return monitor != 0 && GetMonitorInfo(monitor, ref informacoes);
    }

    private bool PosicionarNoMonitor(Retangulo area)
    {
        if (_janela == 0)
        {
            // A classe nativa STATIC dispensa registro próprio e o conteúdo vem do bitmap da janela layered.
            _janela = CreateWindowEx(WsExTopmost | (_aoMover is null ? WsExTransparent : 0) | WsExToolWindow | WsExLayered | WsExNoActivate,
                "STATIC", null, WsPopup, area.Direita - 1, area.Inferior - 1, 1, 1, 0, 0, 0, 0);
            if (_janela != 0 && _aoMover is not null) SetWindowSubclass(_janela, _procedimento, 1, 0);
            return _janela != 0;
        }

        if (MonitorFromWindow(_janela, 0) == _monitor) return true;

        // O HWND precisa estar no monitor de destino antes de consultar o DPI efetivo.
        var moveu = SetWindowPos(_janela, 0, area.Direita - 1, area.Inferior - 1, 1, 1,
            SwpNoZOrder | SwpNoActivate | SwpHideWindow);
        if (moveu) _visivel = false;
        return moveu;
    }

    private bool CartoesIguais(IReadOnlyList<(string Nome, string Tempo)> cards, int quantidade)
    {
        for (int i = 0; i < quantidade; i++)
            if (_cartoesExibidos[i] != cards[i]) return false;
        return true;
    }

    private static int Escalar(int medida, int dpi, int percentual = 100) => (medida * dpi * percentual + 4800) / 9600;

    private readonly record struct Exibicao(Retangulo Area, int Dpi, int Percentual, int Opacidade, string Fonte,
        PosicaoPainelFlutuante Posicao, double? X, double? Y, Paleta Paleta, int Quantidade, long Restantes, bool Compacto);

    private readonly struct Medidas
    {
        internal int Margem { get; }
        internal int LarguraMaxima { get; }
        internal int AlturaDisponivel { get; }
        internal int AlturaCartao { get; }
        internal int AlturaResumo { get; }
        internal int Espaco { get; }
        internal int Quantidade { get; }
        internal long Restantes { get; }
        internal int TopoResumo => Quantidade * (AlturaCartao + Espaco);

        internal int Altura => Restantes > 0 ? TopoResumo + AlturaResumo : TopoResumo - Espaco;

        internal Medidas(Retangulo area, int dpi, int percentual, int quantidade, int restantes, bool compacto)
        {
            Margem = Escalar(12, dpi);
            LarguraMaxima = Math.Min(Escalar(272, dpi, percentual), area.Largura - 2 * Margem);
            AlturaDisponivel = area.Altura - 2 * Margem;
            AlturaCartao = Escalar(compacto ? 26 : 34, dpi, percentual);
            AlturaResumo = Escalar(28, dpi, percentual);
            Espaco = Escalar(4, dpi, percentual);
            Quantidade = Math.Min(MaxCartoes, quantidade);
            Restantes = Math.Max(0, restantes) + (long)quantidade - Quantidade;

            while (Quantidade > 0 && Altura > AlturaDisponivel)
            {
                Quantidade--;
                Restantes++;
            }
        }
    }

    private readonly record struct Cor(int R, int G, int B, int Alfa = 255)
    {
        internal static Cor DoWindows(int indice)
        {
            uint cor = GetSysColor(indice);
            return new Cor((int)(cor & 255), (int)(cor >> 8 & 255), (int)(cor >> 16 & 255));
        }
    }

    private readonly record struct Paleta(Cor Fundo, Cor Contorno, Cor Texto, Cor Atraso);

    private static Paleta ObterPaleta(bool temaClaro)
    {
        var contraste = new AltoContraste { Tamanho = (uint)Marshal.SizeOf<AltoContraste>() };
        if (SystemParametersInfo(SpiGetHighContrast, contraste.Tamanho, ref contraste, 0) &&
            (contraste.Flags & HcfHighContrastOn) != 0)
        {
            var texto = Cor.DoWindows(ColorWindowText);
            return new Paleta(Cor.DoWindows(ColorWindow), texto, texto, texto);
        }

        return temaClaro
            ? new Paleta(new(255, 255, 255, 235), new(160, 182, 207, 230), new(18, 37, 60), new(165, 34, 54))
            : new Paleta(new(30, 41, 59, 235), new(73, 99, 130, 230), new(255, 255, 255), new(255, 145, 155));
    }

    private bool Desenhar(IReadOnlyList<(string Nome, string Tempo)> cards, Exibicao exibicao, Medidas medidas)
    {
        var dc = CreateCompatibleDC(0);
        if (dc == 0) return false;

        nint bitmap = 0, fonteNome = 0, fonteTempo = 0;
        try
        {
            int Dimensao(int medida) => Escalar(medida, exibicao.Dpi, exibicao.Percentual);
            fonteNome = CriarFonte(Dimensao(15), 600, exibicao.Fonte);
            fonteTempo = CriarFonte(Dimensao(13), 400, exibicao.Fonte);
            if (fonteNome == 0 || fonteTempo == 0) return false;

            var margemInterna = Dimensao(10);
            var espacoTempo = Dimensao(10);
            var resumo = medidas.Restantes > 0 ? $"Mais {medidas.Restantes} em atraso" : "";
            if (!MedirTexto(dc, fonteTempo, resumo, out var larguraResumo)) return false;
            var largura = Math.Max(Dimensao(112), larguraResumo + 2 * margemInterna);
            Span<int> largurasTempo = stackalloc int[MaxCartoes];
            for (var i = 0; i < medidas.Quantidade; i++)
            {
                if (!MedirTexto(dc, fonteNome, cards[i].Nome, out var larguraNome)
                    || !MedirTexto(dc, fonteTempo, cards[i].Tempo, out largurasTempo[i])) return false;
                largura = Math.Max(largura, 2 * margemInterna + larguraNome + espacoTempo + largurasTempo[i]);
            }
            largura = Math.Min(largura, medidas.LarguraMaxima);

            var info = new InfoBitmap
            {
                Tamanho = 40, Largura = largura, Altura = -medidas.Altura, Planos = 1, BitsPorPixel = 32
            };
            bitmap = CreateDIBSection(dc, ref info, 0, out var bits, 0, 0);
            if (bitmap == 0 || bits == 0 || SelectObject(dc, bitmap) == 0 ||
                !PatBlt(dc, 0, 0, largura, medidas.Altura, Blackness)) return false;

            // O GDI rasteriza o texto branco em uma máscara preta. O nível de cinza é a cobertura do glifo.
            if (SetBkMode(dc, 1) == 0 || SetTextColor(dc, 0x00FFFFFF) == uint.MaxValue) return false;
            var direita = largura - margemInterna;
            Span<int> iniciosTempo = stackalloc int[MaxCartoes];
            for (int i = 0; i < medidas.Quantidade; i++)
            {
                var topo = i * (medidas.AlturaCartao + medidas.Espaco);
                var baseCartao = topo + medidas.AlturaCartao;
                var inicioTempo = Math.Max(margemInterna, direita - largurasTempo[i]);
                iniciosTempo[i] = inicioTempo;
                var limiteNome = inicioTempo - espacoTempo;
                if (limiteNome > margemInterna && !DesenharTexto(dc, fonteNome, cards[i].Nome,
                    new Retangulo(margemInterna, topo, limiteNome, baseCartao))) return false;
                if (!DesenharTexto(dc, fonteTempo, cards[i].Tempo, new Retangulo(inicioTempo, topo, direita, baseCartao),
                    AlinharDireita)) return false;
            }

            if (medidas.Restantes > 0)
            {
                var areaResumo = new Retangulo(margemInterna, medidas.TopoResumo, direita, medidas.Altura);
                if (!DesenharTexto(dc, fonteTempo, resumo, areaResumo)) return false;
            }

            if (!GdiFlush()) return false;
            var pixels = new int[largura * medidas.Altura];
            Marshal.Copy(bits, pixels, 0, pixels.Length);

            int raio = Dimensao(8);
            int contorno = Math.Max(1, Escalar(1, exibicao.Dpi));
            for (int i = 0; i < medidas.Quantidade; i++)
                ComporCartao(pixels, largura, i * (medidas.AlturaCartao + medidas.Espaco), medidas.AlturaCartao,
                    raio, contorno, iniciosTempo[i], exibicao.Paleta);
            if (medidas.Restantes > 0)
                ComporCartao(pixels, largura, medidas.TopoResumo, medidas.AlturaResumo,
                    raio, contorno, largura, exibicao.Paleta);

            Marshal.Copy(pixels, 0, bits, pixels.Length);
            var esquerda = exibicao.Posicao is PosicaoPainelFlutuante.InferiorEsquerda or PosicaoPainelFlutuante.SuperiorEsquerda;
            var cima = exibicao.Posicao is PosicaoPainelFlutuante.SuperiorDireita or PosicaoPainelFlutuante.SuperiorEsquerda;
            var destino = new Ponto(
                esquerda ? exibicao.Area.Esquerda + medidas.Margem : exibicao.Area.Direita - medidas.Margem - largura,
                cima ? exibicao.Area.Superior + medidas.Margem : exibicao.Area.Inferior - medidas.Margem - medidas.Altura);
            if (exibicao.X is double x && exibicao.Y is double y)
                destino = new Ponto(exibicao.Area.Esquerda + (int)Math.Round(Math.Clamp(x, 0, 1) * (exibicao.Area.Largura - largura)),
                    exibicao.Area.Superior + (int)Math.Round(Math.Clamp(y, 0, 1) * (exibicao.Area.Altura - medidas.Altura)));
            var origem = new Ponto(0, 0);
            var tamanho = new Tamanho(largura, medidas.Altura);
            var mistura = new Mistura { AlfaConstante = (byte)((exibicao.Opacidade * 255 + 50) / 100), FormatoAlfa = 1 }; // AC_SRC_OVER, AC_SRC_ALPHA
            return UpdateLayeredWindow(_janela, 0, ref destino, ref tamanho, dc, ref origem, 0, ref mistura, UlwAlpha);
        }
        finally
        {
            DeleteDC(dc);
            if (bitmap != 0) DeleteObject(bitmap);
            if (fonteNome != 0) DeleteObject(fonteNome);
            if (fonteTempo != 0) DeleteObject(fonteTempo);
        }
    }

    private static nint CriarFonte(int altura, int peso, string nome)
        => CreateFont(-altura, 0, 0, 0, peso, 0, 0, 0, 1, 0, 0, 4, 0, nome);

    private static bool MedirTexto(nint dc, nint fonte, string texto, out int largura)
    {
        largura = 0;
        if (texto.Length == 0) return true;
        if (SelectObject(dc, fonte) == 0 || !GetTextExtentPoint32(dc, texto, texto.Length, out var tamanho)) return false;
        largura = tamanho.Largura + 2;
        return true;
    }

    private static bool DesenharTexto(nint dc, nint fonte, string texto, Retangulo area, uint opcoes = 0)
        => texto.Length == 0 || (SelectObject(dc, fonte) != 0 && DrawText(dc, texto, texto.Length, ref area, FormatoTexto | opcoes) != 0);

    private static void ComporCartao(int[] pixels, int largura, int topo, int altura,
        int raio, int borda, int inicioTempo, Paleta paleta)
    {
        for (int y = 0; y < altura; y++)
        {
            for (int x = 0; x < largura; x++)
            {
                var corTexto = x < inicioTempo ? paleta.Texto : paleta.Atraso;
                int indice = (topo + y) * largura + x;
                int coberturaTexto = pixels[indice] & 255;
                int coberturaCartao = Cobertura(x, y, largura, altura, raio);
                int coberturaFundo = Cobertura(x - borda, y - borda, largura - 2 * borda, altura - 2 * borda, raio - borda);
                int alfaFundo = DividirPor255(paleta.Fundo.Alfa * coberturaFundo);
                int alfaBorda = DividirPor255(paleta.Contorno.Alfa * (coberturaCartao - coberturaFundo));
                int transparenciaTexto = 255 - coberturaTexto;

                // UpdateLayeredWindow recebe BGRA pré-multiplicado. A cobertura plena do texto tem alfa 255.
                int alfa = coberturaTexto + DividirPor255((alfaFundo + alfaBorda) * transparenciaTexto);
                int r = ComporCanal(corTexto.R, paleta.Fundo.R, paleta.Contorno.R);
                int g = ComporCanal(corTexto.G, paleta.Fundo.G, paleta.Contorno.G);
                int b = ComporCanal(corTexto.B, paleta.Fundo.B, paleta.Contorno.B);
                pixels[indice] = alfa << 24 | r << 16 | g << 8 | b;

                int ComporCanal(int texto, int fundo, int contorno) => DividirPor255(texto * coberturaTexto +
                    DividirPor255((fundo * alfaFundo + contorno * alfaBorda) * transparenciaTexto));
            }
        }
    }

    private static int DividirPor255(int valor) => (valor + 127) / 255;

    private static int Cobertura(int x, int y, int largura, int altura, int raio)
    {
        if (x < 0 || y < 0 || x >= largura || y >= altura) return 0;
        int dx = raio - Math.Min(x, largura - 1 - x);
        int dy = raio - Math.Min(y, altura - 1 - y);
        if (dx <= 0 || dy <= 0) return 255;
        double distancia = Math.Sqrt((dx - 0.5) * (dx - 0.5) + (dy - 0.5) * (dy - 0.5));
        return (int)(255 * Math.Clamp(raio + 0.5 - distancia, 0, 1));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Ponto(int x, int y)
    {
        internal int X = x;
        internal int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Tamanho(int largura, int altura)
    {
        internal int Largura = largura;
        internal int Altura = altura;
    }

    [StructLayout(LayoutKind.Sequential)]
    private record struct Retangulo
    {
        internal int Esquerda;
        internal int Superior;
        internal int Direita;
        internal int Inferior;

        internal Retangulo(int esquerda, int superior, int direita, int inferior)
            => (Esquerda, Superior, Direita, Inferior) = (esquerda, superior, direita, inferior);

        internal readonly int Largura => Direita - Esquerda;
        internal readonly int Altura => Inferior - Superior;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InformacoesMonitor
    {
        internal uint Tamanho;
        internal Retangulo Area;
        internal Retangulo Trabalho;
        internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Nome;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DispositivoTela
    {
        internal uint Tamanho;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Nome;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Descricao;
        internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Chave;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AltoContraste
    {
        internal uint Tamanho;
        internal uint Flags;
        internal nint Esquema;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LogFont
    {
        internal int Altura, Largura, Escape, Orientacao, Peso;
        internal byte Italico, Sublinhado, Riscado, ConjuntoCaracteres, Precisao, Recorte, Qualidade, Familia;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Nome;
    }

    private delegate int EnumerarFonte(nint fonte, nint metricas, uint tipo, nint dados);

    [StructLayout(LayoutKind.Sequential)]
    private struct InfoBitmap
    {
        internal uint Tamanho;
        internal int Largura;
        internal int Altura;
        internal ushort Planos;
        internal ushort BitsPorPixel;
        internal uint Compressao;
        internal uint TamanhoImagem;
        internal int PixelsPorMetroX;
        internal int PixelsPorMetroY;
        internal uint CoresUsadas;
        internal uint CoresImportantes;
        internal uint PrimeiraCor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Mistura
    {
        internal byte Operacao;
        internal byte Flags;
        internal byte AlfaConstante;
        internal byte FormatoAlfa;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool EnumerarMonitor(nint monitor, nint dc, nint area, nint dados);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(uint estilosExtras, string classe, string? nome, uint estilos,
        int x, int y, int largura, int altura, nint pai, nint menu, nint instancia, nint parametro);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint janela, nint ordem, int x, int y, int largura, int altura, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint janela);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint janela, uint flags);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(Ponto ponto, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint dc, nint area, EnumerarMonitor enumerar, nint dados);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref InformacoesMonitor informacoes);

    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string dispositivo, uint indice, ref DispositivoTela detalhes, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint janela);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint acao, uint tamanho, ref AltoContraste contraste, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetSysColor(int indice);

    [DllImport("user32.dll", EntryPoint = "DrawTextW", CharSet = CharSet.Unicode)]
    private static extern int DrawText(nint dc, string texto, int caracteres, ref Retangulo area, uint formato);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(nint janela, nint dcDestino, ref Ponto destino,
        ref Tamanho tamanho, nint dcOrigem, ref Ponto origem, uint chaveCor, ref Mistura mistura, uint flags);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint dc);

    [DllImport("gdi32.dll", EntryPoint = "EnumFontFamiliesExW", CharSet = CharSet.Unicode)]
    private static extern int EnumFontFamiliesEx(nint dc, ref LogFont filtro, EnumerarFonte enumerar, nint dados, uint flags);

    [DllImport("gdi32.dll", EntryPoint = "GetTextExtentPoint32W", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTextExtentPoint32(nint dc, string texto, int caracteres, out Tamanho tamanho);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint dc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateDIBSection(nint dc, ref InfoBitmap info, uint uso, out nint bits, nint secao, uint offset);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint dc, nint objeto);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint objeto);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PatBlt(nint dc, int x, int y, int largura, int altura, uint operacao);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(nint dc, int modo);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(nint dc, uint cor);

    [DllImport("gdi32.dll", EntryPoint = "CreateFontW", CharSet = CharSet.Unicode)]
    private static extern nint CreateFont(int altura, int largura, int escape, int orientacao, int peso, uint italico,
        uint sublinhado, uint riscado, uint charset, uint precisao, uint recorte, uint qualidade, uint familia, string nome);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GdiFlush();
}
