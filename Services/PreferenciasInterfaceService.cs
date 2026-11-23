using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceDesk.Services;

public enum SomNotificacao { Padrao, Mensagem, Email, Lembrete, Alarme }
public enum PosicaoPainelFlutuante { InferiorDireita, InferiorEsquerda, SuperiorDireita, SuperiorEsquerda }

public sealed record PosicaoJanela(int X, int Y, int Width, int Height);

public sealed record PreferenciasInterface
{
    internal const int EscalaFlutuanteMinima = 75;
    internal const int EscalaFlutuanteMaxima = 150;
    internal const string FonteFlutuantePadrao = "Segoe UI";
    public string[] CamposFila { get; init; } = ["Empresa", "E-mail"];
    public string[] CamposPainel { get; init; } = [];
    public bool FilaCompacta { get; init; }
    public bool PainelCompacto { get; init; }
    public bool SempreVisivel { get; init; }
    public PosicaoJanela? JanelaInicial { get; init; }
    public string Tema { get; init; } = "Sistema";
    public string Cor { get; init; } = "Azul";
    public bool NotificacoesSistema { get; init; } = true;
    public bool SomNotificacoes { get; init; } = true;
    public SomNotificacao ToqueNotificacoes { get; init; } = SomNotificacao.Padrao;
    public int AvisoEtapaN1Minutos { get; init; } = 2;
    public bool MostrarCardsFlutuantes { get; init; } = true;
    public PosicaoPainelFlutuante PosicaoFlutuante { get; init; }
    public string? MonitorFlutuante { get; init; }
    internal const int OpacidadeMinima = 10;
    public double? PosicaoRelativaX { get; init; }
    public double? PosicaoRelativaY { get; init; }
    public int OpacidadeFlutuante { get; init; } = 100;
    public int EscalaFlutuante { get; init; } = 100;
    public string FonteFlutuante { get; init; } = FonteFlutuantePadrao;
    public int IntervaloLembreteMinutos { get; init; } = 5;
    [JsonIgnore]
    public SomNotificacao? SomSelecionado => SomNotificacoes ? ToqueNotificacoes : null;
}

public sealed class PreferenciasInterfaceService
{
    private readonly string _arquivo;
    public PreferenciasInterfaceService(string? diretorio = null) => _arquivo = Path.Combine(diretorio
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".servicedesk"), "interface.json");

    public PreferenciasInterface Carregar()
    {
        try { return Normalizar(JsonSerializer.Deserialize<PreferenciasInterface>(File.ReadAllText(_arquivo)) ?? new()); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    public void Salvar(PreferenciasInterface preferencias)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
        var temporario = _arquivo + ".tmp";
        File.WriteAllText(temporario, JsonSerializer.Serialize(Normalizar(preferencias)));
        File.Move(temporario, _arquivo, overwrite: true);
    }

    private static bool CampoValido(string campo) => campo is "Empresa" or "E-mail" or "Telefone";

    private static PreferenciasInterface Normalizar(PreferenciasInterface valor) => valor with
    {
        CamposFila = (valor.CamposFila ?? ["Empresa", "E-mail"]).Where(CampoValido).Distinct().ToArray(),
        CamposPainel = (valor.CamposPainel ?? []).Where(CampoValido).Distinct().ToArray(),
        Tema = valor.Tema is "Sistema" or "Claro" or "Escuro" ? valor.Tema : "Sistema",
        Cor = valor.Cor is "Azul" or "Verde" or "Roxo" or "Laranja" or "Vermelho" ? valor.Cor : "Azul",
        ToqueNotificacoes = Enum.IsDefined(valor.ToqueNotificacoes) ? valor.ToqueNotificacoes : SomNotificacao.Padrao,
        AvisoEtapaN1Minutos = Math.Clamp(valor.AvisoEtapaN1Minutos, 0, 15),
        PosicaoFlutuante = Enum.IsDefined(valor.PosicaoFlutuante) ? valor.PosicaoFlutuante : PosicaoPainelFlutuante.InferiorDireita,
        MonitorFlutuante = string.IsNullOrWhiteSpace(valor.MonitorFlutuante) ? null : valor.MonitorFlutuante.Trim(),
        OpacidadeFlutuante = Math.Clamp(valor.OpacidadeFlutuante, PreferenciasInterface.OpacidadeMinima, 100),
        EscalaFlutuante = Math.Clamp(valor.EscalaFlutuante, PreferenciasInterface.EscalaFlutuanteMinima, PreferenciasInterface.EscalaFlutuanteMaxima),
        FonteFlutuante = valor.FonteFlutuante?.Trim() is { Length: > 0 and < 32 } fonte && !fonte.Any(char.IsControl)
            ? fonte : PreferenciasInterface.FonteFlutuantePadrao,
        IntervaloLembreteMinutos = Math.Clamp(valor.IntervaloLembreteMinutos, 0, 60)
    };
}
