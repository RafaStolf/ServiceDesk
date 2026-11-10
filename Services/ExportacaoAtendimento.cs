using System.IO.Compression;
using System.Text;

namespace ServiceDesk.Services;

public sealed record AnexoAtendimento(string NomeArquivo, string? Url, string Tipo);
public sealed record ExportacaoAtendimento(string Texto, IReadOnlyList<AnexoAtendimento> Anexos);

public static class PacoteAtendimento
{
    // Downloads usam um cliente separado: a chave do Chatwoot nunca é enviada ao servidor de mídia.
    public static async Task SalvarAsync(Stream destino, ExportacaoAtendimento exportacao, CancellationToken cancellationToken)
    {
        using var cliente = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var pacote = new ZipArchive(destino, ZipArchiveMode.Create, leaveOpen: true);
        await EscreverTextoAsync(pacote, "historico.txt", exportacao.Texto, cancellationToken);
        await EscreverTextoAsync(pacote, "COMO-USAR.txt",
            "Extraia este ZIP. No projeto Atendimentos do ChatGPT, anexe historico.txt e os arquivos da pasta anexos no mesmo chat. " +
            "Copiar apenas o texto não envia as mídias. Os nomes no histórico relacionam cada arquivo à mensagem. " +
            "A aceitação e análise de cada formato dependem dos recursos e limites do ChatGPT. " +
            "Se áudio ou vídeo não for aceito, será necessário transcrever o áudio e extrair imagens relevantes do vídeo antes da análise. " +
            "Este pacote preserva os originais; não faz transcrição nem interpretação automática.", cancellationToken);
        foreach (var anexo in exportacao.Anexos)
        {
            if (!Uri.TryCreate(anexo.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException($"O anexo {anexo.NomeArquivo} não possui endereço HTTPS disponível. O pacote não foi concluído.");
            using var resposta = await cliente.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            resposta.EnsureSuccessStatusCode();
            await using var origem = await resposta.Content.ReadAsStreamAsync(cancellationToken);
            await using var arquivo = pacote.CreateEntry(anexo.NomeArquivo, CompressionLevel.NoCompression).Open();
            await origem.CopyToAsync(arquivo, cancellationToken);
        }
    }

    private static async Task EscreverTextoAsync(ZipArchive pacote, string nome, string texto, CancellationToken cancellationToken)
    {
        await using var arquivo = pacote.CreateEntry(nome).Open();
        await arquivo.WriteAsync(Encoding.UTF8.GetBytes(texto), cancellationToken);
    }
}
