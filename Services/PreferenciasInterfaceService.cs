using System.Text.Json;

namespace ServiceDesk.Services;

public sealed record PreferenciasInterface
{
    public string Tema { get; init; } = "Sistema";
    public string Cor { get; init; } = "Azul";
    public bool NotificacoesSistema { get; init; } = true;
    public bool SomNotificacoes { get; init; } = true;
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

    private static PreferenciasInterface Normalizar(PreferenciasInterface valor) => valor with
    {
        Tema = valor.Tema is "Sistema" or "Claro" or "Escuro" ? valor.Tema : "Sistema",
        Cor = valor.Cor is "Azul" or "Verde" or "Roxo" or "Laranja" or "Vermelho" ? valor.Cor : "Azul"
    };
}
