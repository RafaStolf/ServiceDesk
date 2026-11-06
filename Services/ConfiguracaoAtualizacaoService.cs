using Windows.Storage;

namespace ServiceDesk.Services;

public sealed record ConfiguracaoAtualizacao(bool Ativa, int IntervaloMinutos, bool NotificacoesAtivas, int LimiteNotificacaoMinutos)
{
    public static readonly ConfiguracaoAtualizacao Padrao = new(true, 5, true, 10);
}

/// <summary>Armazena preferências de atualização por usuário, dentro do pacote Windows.</summary>
public sealed class ConfiguracaoAtualizacaoService
{
    private const string AtualizacaoAtivaKey = "atualizacao_automatica_ativa";
    private const string IntervaloKey = "intervalo_atualizacao_minutos";
    private const string NotificacoesAtivasKey = "notificacoes_ativas";
    private const string LimiteNotificacaoKey = "limite_notificacao_minutos";

    public ConfiguracaoAtualizacao Carregar()
    {
        var valores = ApplicationData.Current.LocalSettings.Values;
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
        return new ConfiguracaoAtualizacao(ativa, Math.Clamp(minutos, 1, 120), notificacoesAtivas, Math.Clamp(limite, 1, 1440));
    }

    public void Salvar(ConfiguracaoAtualizacao configuracao)
    {
        var valores = ApplicationData.Current.LocalSettings.Values;
        valores[AtualizacaoAtivaKey] = configuracao.Ativa;
        valores[IntervaloKey] = Math.Clamp(configuracao.IntervaloMinutos, 1, 120);
        valores[NotificacoesAtivasKey] = configuracao.NotificacoesAtivas;
        valores[LimiteNotificacaoKey] = Math.Clamp(configuracao.LimiteNotificacaoMinutos, 1, 1440);
    }
}
