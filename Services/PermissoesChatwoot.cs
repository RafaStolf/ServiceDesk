using System.Text.Json;

namespace ServiceDesk.Services;

internal readonly record struct PermissoesChatwoot(bool PodeConsultarRelatorios, bool PodeVerNaoAtribuidos)
{
    public static PermissoesChatwoot Ler(JsonElement perfil, long contaId)
    {
        if (perfil.ValueKind != JsonValueKind.Object || !perfil.TryGetProperty("accounts", out var contas)
            || contas.ValueKind != JsonValueKind.Array)
            return default;

        foreach (var conta in contas.EnumerateArray())
        {
            if (conta.ValueKind != JsonValueKind.Object || !conta.TryGetProperty("id", out var id)
                || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var valor) || valor != contaId)
                continue;

            var papel = MensagemChatwoot.Texto(conta, "role");
            if (papel == "administrator") return new(true, true);
            if (!conta.TryGetProperty("permissions", out var permissoes))
                return new(false, papel == "agent");
            if (permissoes.ValueKind != JsonValueKind.Array) return default;

            var codigos = permissoes.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()).ToHashSet(StringComparer.Ordinal);
            return new(codigos.Contains("report_manage"),
                codigos.Overlaps(["administrator", "agent", "conversation_manage", "conversation_unassigned_manage"]));
        }
        return default;
    }
}
