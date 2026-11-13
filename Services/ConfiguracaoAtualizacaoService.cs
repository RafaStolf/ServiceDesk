using System.Runtime.InteropServices;
using Windows.Storage;

namespace ServiceDesk.Services;

public sealed record ConfiguracaoAtualizacao(bool Ativa, int IntervaloMinutos, bool NotificacoesAtivas, int LimiteNotificacaoMinutos, bool TempoReal = true)
{
    public static readonly ConfiguracaoAtualizacao Padrao = new(true, 5, true, 10);
}

/// <summary>Armazena preferências de atualização por usuário, no pacote Windows ou no perfil local da versão portátil.</summary>
public sealed class ConfiguracaoAtualizacaoService
{
    private const string AtualizacaoAtivaKey = "atualizacao_automatica_ativa";
    private const string TempoRealKey = "atualizacao_tempo_real";
    private const string IntervaloKey = "intervalo_atualizacao_minutos";
    private const string NotificacoesAtivasKey = "notificacoes_ativas";
    private const string LimiteNotificacaoKey = "limite_notificacao_minutos";

    public ConfiguracaoAtualizacao Carregar()
    {
        var valores = TentarValoresDoPacote();
        if (valores is null)
        {
            try
            {
                return Normalizar(System.Text.Json.JsonSerializer.Deserialize<ConfiguracaoAtualizacao>(File.ReadAllText(ArquivoPortatil))
                    ?? ConfiguracaoAtualizacao.Padrao);
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
            {
                return ConfiguracaoAtualizacao.Padrao;
            }
        }
        var ativa = valores.TryGetValue(AtualizacaoAtivaKey, out var valorAtiva)
            ? valorAtiva as bool? ?? ConfiguracaoAtualizacao.Padrao.Ativa
            : ConfiguracaoAtualizacao.Padrao.Ativa;
        var minutos = valores.TryGetValue(IntervaloKey, out var valorIntervalo) && valorIntervalo is int intervalo
            ? intervalo : ConfiguracaoAtualizacao.Padrao.IntervaloMinutos;
        var notificacoesAtivas = valores.TryGetValue(NotificacoesAtivasKey, out var valorNotificacoes)
            ? valorNotificacoes as bool? ?? ConfiguracaoAtualizacao.Padrao.NotificacoesAtivas
            : ConfiguracaoAtualizacao.Padrao.NotificacoesAtivas;
        var limite = valores.TryGetValue(LimiteNotificacaoKey, out var valorLimite) && valorLimite is int limiteConfigurado
            ? limiteConfigurado : ConfiguracaoAtualizacao.Padrao.LimiteNotificacaoMinutos;
        var tempoReal = !valores.TryGetValue(TempoRealKey, out var valorTempoReal)
            || valorTempoReal is not bool modoTempoReal || modoTempoReal;
        return new ConfiguracaoAtualizacao(ativa, Math.Clamp(minutos, 1, 120), notificacoesAtivas, Math.Clamp(limite, 1, 1440), tempoReal);
    }

    public void Salvar(ConfiguracaoAtualizacao configuracao)
    {
        var valores = TentarValoresDoPacote();
        if (valores is null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ArquivoPortatil)!);
            var temporario = ArquivoPortatil + ".tmp";
            File.WriteAllText(temporario, System.Text.Json.JsonSerializer.Serialize(Normalizar(configuracao)));
            File.Move(temporario, ArquivoPortatil, overwrite: true);
            return;
        }
        valores[AtualizacaoAtivaKey] = configuracao.Ativa;
        valores[TempoRealKey] = configuracao.TempoReal;
        valores[IntervaloKey] = Math.Clamp(configuracao.IntervaloMinutos, 1, 120);
        valores[NotificacoesAtivasKey] = configuracao.NotificacoesAtivas;
        valores[LimiteNotificacaoKey] = Math.Clamp(configuracao.LimiteNotificacaoMinutos, 1, 1440);
    }

    private static string ArquivoPortatil => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".servicedesk", "preferencias.json");

    private static ConfiguracaoAtualizacao Normalizar(ConfiguracaoAtualizacao valor) => valor with
    {
        IntervaloMinutos = Math.Clamp(valor.IntervaloMinutos, 1, 120),
        LimiteNotificacaoMinutos = Math.Clamp(valor.LimiteNotificacaoMinutos, 1, 1440)
    };

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(ref uint comprimento, IntPtr nome);

    private static IDictionary<string, object>? TentarValoresDoPacote()
    {
        const int bufferInsuficiente = 122;
        uint comprimento = 0;
        // Sem identidade MSIX, as preferências ficam no perfil local.
        if (GetCurrentPackageFullName(ref comprimento, IntPtr.Zero) != bufferInsuficiente) return null;

        try { return ApplicationData.Current.LocalSettings.Values; }
        catch (InvalidOperationException) { return null; }
        catch (System.Runtime.InteropServices.COMException) { return null; }
    }
}
