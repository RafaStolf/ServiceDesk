param(
    [long]$ConversaId = 19622,
    [long]$ContaId = 1,
    [string]$Configuracao = (Join-Path $env:USERPROFILE '.servicedesk/application.properties'),
    [string]$Destino = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'chatwoot-campos-19622.json')
)
$ErrorActionPreference = 'Stop'
if ($ConversaId -le 0 -or $ContaId -le 0) { throw 'Informe IDs positivos.' }
$propriedades = @{}
foreach ($linha in Get-Content -LiteralPath $Configuracao) {
    if ($linha.Trim() -eq '' -or $linha.Trim().StartsWith('#')) { continue }
    $separador = $linha.IndexOf('=')
    if ($separador -ge 0) {
        $chave = $linha.Substring(0, $separador).Trim()
        $valor = $linha.Substring($separador + 1).Trim()
        if ($chave.Length -gt 0) { $propriedades[$chave] = $valor }
    }
}
$base = $null
if (![Uri]::TryCreate($propriedades['chatwoot.base-url'], [UriKind]::Absolute, [ref]$base) -or $base.Scheme -ne 'https' -or $base.UserInfo) {
    throw 'A configuracao precisa de um endereco HTTPS sem credenciais na URL.'
}
if (!$propriedades['chatwoot.api-access-token']) { throw 'Chave nao encontrada na configuracao local.' }
Add-Type -AssemblyName System.Net.Http
$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$cliente = New-Object System.Net.Http.HttpClient -ArgumentList $handler
$cliente.Timeout = [TimeSpan]::FromSeconds(30)
$cliente.DefaultRequestHeaders.Add('api_access_token', $propriedades['chatwoot.api-access-token'])
$campos = [System.Collections.Generic.List[object]]::new()
$before = $null
$completa = $false
try {
    for ($pagina = 0; $pagina -lt 100; $pagina++) {
        $sufixo = if ($null -ne $before) { "?before=$before" } else { '' }
        $url = $base.AbsoluteUri.TrimEnd('/') + "/api/v1/accounts/$ContaId/conversations/$ConversaId/messages$sufixo"
        $resposta = $cliente.GetAsync($url).GetAwaiter().GetResult()
        try {
            if (!$resposta.IsSuccessStatusCode) { throw "Leitura recusada: HTTP $([int]$resposta.StatusCode). Nenhum campo foi exportado." }
            $dados = $resposta.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
            $mensagens = @($dados.payload)
            if ($mensagens.Count -eq 0) { $completa = $true; break }
            foreach ($m in $mensagens) {
                $campos.Add([ordered]@{
                    id = $m.id; created_at = $m.created_at; message_type = $m.message_type; private = $m.private
                    sender = [ordered]@{ type = $m.sender.type; id = $m.sender.id }
                    legacy_sender_type = $m.sender_type; legacy_sender_id = $m.sender_id
                    content_type = $m.content_type
                    has_automation_rule = ($null -ne $m.content_attributes.automation_rule_id)
                    has_campaign = ($null -ne $m.additional_attributes.campaign_id)
                    external_echo = $m.content_attributes.external_echo
                })
            }
            $menor = [long](($mensagens | Measure-Object -Property id -Minimum).Minimum)
            if ($null -ne $before -and $menor -ge $before) { throw 'A paginacao nao avancou. Nenhum campo foi exportado.' }
            $before = $menor
        } finally { $resposta.Dispose() }
    }
    if (!$completa) { throw 'Limite de paginas atingido. Nenhum arquivo parcial foi exportado.' }
    [ordered]@{ conversation_id=$ConversaId; account_id=$ContaId; complete=$true; messages=@($campos | Sort-Object created_at,id) } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Destino -Encoding utf8
    Write-Host "Campos salvos em $Destino. Sem texto, nomes, anexos ou chave da API."
} finally { $cliente.Dispose(); $handler.Dispose() }
