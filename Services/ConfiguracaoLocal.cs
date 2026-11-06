namespace ServiceDesk.Services;

/// <summary>Lê a configuração privada da instalação sem incorporá-la ao pacote nem ao código-fonte.</summary>
public static class ConfiguracaoLocal
{
    public static IReadOnlyDictionary<string, string> CarregarPropriedades()
    {
        var arquivo = LocalizarArquivo()
            ?? throw new InvalidOperationException("Não localizei application.properties. Use %USERPROFILE%\\.servicedesk\\application.properties ou mantenha o arquivo de configuração do projeto.");
        return File.ReadLines(arquivo)
            .Select(linha => linha.Trim())
            .Where(linha => linha.Length > 0 && !linha.StartsWith('#'))
            .Select(linha => linha.Split('=', 2))
            .Where(partes => partes.Length == 2)
            .ToDictionary(partes => partes[0].Trim(), partes => partes[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static void SalvarPropriedades(IReadOnlyDictionary<string, string> alteracoes)
    {
        var propriedades = LocalizarArquivo() is { } arquivoExistente
            ? CarregarPropriedades().ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (chave, valor) in alteracoes) propriedades[chave] = valor.Trim();

        var diretorio = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".servicedesk");
        Directory.CreateDirectory(diretorio);
        var destino = Path.Combine(diretorio, "application.properties");
        File.WriteAllLines(destino, propriedades.OrderBy(item => item.Key).Select(item => $"{item.Key}={item.Value}"));
    }

    private static string? LocalizarArquivo()
    {
        var perfil = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".servicedesk", "application.properties");
        if (File.Exists(perfil)) return perfil;
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            var candidato = Path.Combine(diretorio.FullName, "config", "application.properties");
            if (File.Exists(candidato)) return candidato;
            diretorio = diretorio.Parent;
        }
        return null;
    }
}
