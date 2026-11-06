namespace ServiceDesk.Services;

public sealed record ConfiguracaoChatwoot(string BaseUrl, string Token, long ContaId, long? UsuarioId, long? InboxId);

public static class CarregadorConfiguracaoChatwoot
{
    public static ConfiguracaoChatwoot Carregar()
    {
        var propriedades = ConfiguracaoLocal.CarregarPropriedades();

        propriedades.TryGetValue("chatwoot.base-url", out var baseUrl);
        propriedades.TryGetValue("chatwoot.api-access-token", out var token);
        propriedades.TryGetValue("chatwoot.account-id", out var conta);
        propriedades.TryGetValue("chatwoot.user-id", out var usuario);
        propriedades.TryGetValue("chatwoot.inbox-id", out var inbox);

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token) || !long.TryParse(conta, out var contaId))
            throw new InvalidOperationException("Preencha chatwoot.base-url, chatwoot.api-access-token e chatwoot.account-id em application.properties.");

        return new ConfiguracaoChatwoot(baseUrl.TrimEnd('/'), token, contaId,
            long.TryParse(usuario, out var usuarioId) ? usuarioId : null,
            long.TryParse(inbox, out var inboxId) ? inboxId : null);
    }
}
