using System.Net;
using System.Text.Json;

namespace ServiceDesk.Services;

public sealed record AtualizacaoAplicativo(Version Versao, Uri Pagina);

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
        if (!release.GetProperty("assets").EnumerateArray().Any(item =>
            item.GetProperty("name").GetString() == "ServiceDesk-win-x64.zip")) return null;
        return new AtualizacaoAplicativo(normalizada, pagina);
    }
}
