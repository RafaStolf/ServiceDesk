using System.Net;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ServiceDesk.Services;

public sealed record AtualizacaoAplicativo(Version Versao, Uri Pagina, Uri Pacote, Uri Hash);

public sealed class AtualizacaoAplicativoService
{
    public static Version VersaoAtual => typeof(AtualizacaoAplicativoService).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);

    public async Task<AtualizacaoAplicativo?> VerificarAsync(CancellationToken cancellationToken)
    {
        using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        cliente.DefaultRequestHeaders.UserAgent.ParseAdd("ServiceDesk/" + VersaoAtual);
        using var resposta = await cliente.GetAsync("https://api.github.com/repos/RafaStolf/ServiceDesk/releases/latest", cancellationToken);
        if (resposta.StatusCode == HttpStatusCode.NotFound) return null;
        resposta.EnsureSuccessStatusCode();
        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(cancellationToken));
        return LerRelease(documento.RootElement, VersaoAtual);
    }

    internal static AtualizacaoAplicativo? LerRelease(JsonElement release, Version atual)
    {
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
        var tag = release.GetProperty("tag_name").GetString()?.TrimStart('v');
        if (!Version.TryParse(tag, out var versao)) return null;
        var normalizada = new Version(versao.Major, versao.Minor, Math.Max(0, versao.Build), Math.Max(0, versao.Revision));
        var atualNormalizada = new Version(atual.Major, atual.Minor, Math.Max(0, atual.Build), Math.Max(0, atual.Revision));
        if (normalizada <= atualNormalizada) return null;
        if (!Uri.TryCreate(release.GetProperty("html_url").GetString(), UriKind.Absolute, out var pagina)
            || pagina.Scheme != "https" || pagina.Host != "github.com"
            || !pagina.AbsolutePath.StartsWith("/RafaStolf/ServiceDesk/releases/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O GitHub retornou um endereço de atualização inválido.");
        var assets = release.GetProperty("assets").EnumerateArray().ToList();
        Uri? Asset(string nome)
        {
            var item = assets.FirstOrDefault(x => x.GetProperty("name").GetString() == nome);
            if (item.ValueKind == JsonValueKind.Undefined) return null;
            if (!Uri.TryCreate(item.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var uri)
                || uri.Scheme != "https" || uri.Host != "github.com"
                || !uri.AbsolutePath.StartsWith("/RafaStolf/ServiceDesk/releases/download/", StringComparison.Ordinal))
                throw new InvalidOperationException("Endereço de download inválido.");
            return uri;
        }
        var pacote = Asset("ServiceDesk-win-x64.zip");
        var hash = Asset("SHA256SUMS.txt");
        return pacote is null || hash is null ? null : new AtualizacaoAplicativo(normalizada, pagina, pacote, hash);
    }

    public async Task PrepararAsync(AtualizacaoAplicativo atualizacao, CancellationToken cancellationToken)
    {
        if (atualizacao.Versao <= VersaoAtual) throw new InvalidOperationException("A versão já está atualizada.");
        if (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
            throw new InvalidOperationException("A atualização automática está disponível para Windows x64.");
        var target = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var probe = Path.Combine(target, Guid.NewGuid().ToString("N") + ".tmp");
        try { await File.WriteAllTextAsync(probe, "", cancellationToken); File.Delete(probe); }
        catch (UnauthorizedAccessException) { throw new InvalidOperationException("Sem permissão para atualizar esta pasta. Use a versão portátil em uma pasta do seu usuário."); }
        var work = Path.Combine(Path.GetTempPath(), "ServiceDesk-Update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            using var cliente = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            cliente.DefaultRequestHeaders.UserAgent.ParseAdd("ServiceDesk/" + VersaoAtual);
            var checksum = await cliente.GetStringAsync(atualizacao.Hash, cancellationToken);
            var linha = checksum.Split('\n').SingleOrDefault(x => x.Trim().EndsWith("  ServiceDesk-win-x64.zip", StringComparison.Ordinal))
                ?? throw new InvalidOperationException("Verificação SHA-256 indisponível.");
            var esperado = linha.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            if (esperado.Length != 64 || !esperado.All(Uri.IsHexDigit)) throw new InvalidOperationException("SHA-256 inválido.");
            var zip = Path.Combine(work, "pacote.zip");
            using (var resposta = await cliente.GetAsync(atualizacao.Pacote, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                resposta.EnsureSuccessStatusCode();
                await using var entrada = await resposta.Content.ReadAsStreamAsync(cancellationToken);
                await using var arquivo = File.Create(zip);
                await entrada.CopyToAsync(arquivo, cancellationToken);
            }
            await using (var arquivo = File.OpenRead(zip))
            {
                var obtido = Convert.ToHexString(await SHA256.HashDataAsync(arquivo, cancellationToken));
                if (!obtido.Equals(esperado, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("O download não passou na verificação SHA-256. Tente novamente.");
            }
            var stage = Path.Combine(work, "stage");
            ExtrairPacote(zip, stage);
            var versao = FileVersionInfo.GetVersionInfo(Path.Combine(stage, "ServiceDesk.exe")).FileVersion;
            if (!Version.TryParse(versao, out var arquivoVersao) || arquivoVersao != atualizacao.Versao)
                throw new InvalidOperationException("A versão do executável não corresponde à atualização.");
            var script = Path.Combine(work, "Atualizar.ps1");
            using (var recurso = typeof(AtualizacaoAplicativoService).Assembly.GetManifestResourceStream("ServiceDesk.Scripts.Atualizar.ps1")
                ?? throw new InvalidOperationException("Atualizador não encontrado."))
            await using (var saida = File.Create(script)) { await recurso.CopyToAsync(saida, cancellationToken); }
            var config = Path.Combine(work, "config.json");
            await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new { Work = work, Stage = stage, Target = target, ProcessId = Environment.ProcessId }), cancellationToken);
            var inicio = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
            { UseShellExecute = false, CreateNoWindow = true };
            foreach (var argumento in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-ConfigPath", config }) inicio.ArgumentList.Add(argumento);
            using var processo = Process.Start(inicio) ?? throw new InvalidOperationException("Não foi possível iniciar o atualizador.");
        }
        catch { Directory.Delete(work, recursive: true); throw; }
    }

    internal static void ExtrairPacote(string zip, string destino)
    {
        var raiz = Path.GetFullPath(destino) + Path.DirectorySeparatorChar;
        using var arquivo = ZipFile.OpenRead(zip);
        if (arquivo.Entries.Sum(x => x.Length) > 2L * 1024 * 1024 * 1024) throw new InvalidOperationException("Pacote excede o tamanho permitido.");
        foreach (var item in arquivo.Entries)
        {
            var caminho = Path.GetFullPath(Path.Combine(destino, item.FullName.Replace('\\', '/')));
            if (!caminho.StartsWith(raiz, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Caminho inválido no pacote.");
        }
        ZipFile.ExtractToDirectory(zip, destino);
        if (!File.Exists(Path.Combine(destino, "ServiceDesk.exe"))) throw new InvalidOperationException("Pacote sem ServiceDesk.exe.");
    }
}
