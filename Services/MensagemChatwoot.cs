using System.Text.Json;

namespace ServiceDesk.Services;

/// <summary>Campos do endpoint de mensagens, não os campos internos do modelo Rails.</summary>
internal static class MensagemChatwoot
{
    public static string? Texto(JsonElement elemento, string campo) =>
        elemento.ValueKind == JsonValueKind.Object && elemento.TryGetProperty(campo, out var valor)
        && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    public static int? Tipo(JsonElement mensagem)
    {
        if (mensagem.ValueKind != JsonValueKind.Object || !mensagem.TryGetProperty("message_type", out var valor)) return null;
        return valor.ValueKind == JsonValueKind.Number && valor.TryGetInt32(out var numero) ? numero : null;
    }

    public static bool Publica(JsonElement mensagem) =>
        Tipo(mensagem) is 0 or 1 or 3
        && !Privada(mensagem);

    public static bool Privada(JsonElement mensagem)
    {
        if (mensagem.ValueKind != JsonValueKind.Object) return false;
        static bool Verdadeira(JsonElement objeto, string campo) =>
            objeto.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.True;
        // API oficial: private. Compatibilidade CCChatwoot: is.private, literal ou aninhado.
        // Qualquer marcação privada prevalece quando os campos divergem.
        return Verdadeira(mensagem, "private") || Verdadeira(mensagem, "is.private")
            || mensagem.TryGetProperty("is", out var flags) && flags.ValueKind == JsonValueKind.Object
                && Verdadeira(flags, "private");
    }

    public static string? TipoRemetente(JsonElement mensagem)
    {
        if (mensagem.ValueKind != JsonValueKind.Object) return null;
        if (mensagem.TryGetProperty("sender", out var sender) && Texto(sender, "type") is { } tipo) return tipo;
        // Compatibilidade com payloads de eventos que também expõem atributos do modelo.
        return Texto(mensagem, "sender_type");
    }

    public static long? IdRemetente(JsonElement mensagem)
    {
        if (mensagem.ValueKind != JsonValueKind.Object) return null;
        if (mensagem.TryGetProperty("sender", out var sender) && sender.ValueKind == JsonValueKind.Object
            && sender.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var numero)) return numero;
        return mensagem.TryGetProperty("sender_id", out var legado) && legado.ValueKind == JsonValueKind.Number && legado.TryGetInt64(out var valor)
            ? valor : null;
    }

    public static bool DoAgente(JsonElement mensagem, long usuarioId) => Publica(mensagem)
        && Tipo(mensagem) is 1 or 3
        && string.Equals(TipoRemetente(mensagem), "user", StringComparison.OrdinalIgnoreCase)
        && IdRemetente(mensagem) == usuarioId;

    public static bool DoContato(JsonElement mensagem) => Publica(mensagem) && Tipo(mensagem) == 0
        && string.Equals(TipoRemetente(mensagem), "contact", StringComparison.OrdinalIgnoreCase);
}
