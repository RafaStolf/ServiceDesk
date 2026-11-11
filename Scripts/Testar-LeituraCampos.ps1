$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
Add-Type -TypeDefinition @'
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
public class LeituraCamposHandler : HttpMessageHandler {
    public static int Calls;
    private readonly string token;
    private readonly string payload;
    public LeituraCamposHandler(string token, string payload) { this.token = token; this.payload = payload; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        if (request.Method != HttpMethod.Get) throw new Exception("Somente GET permitido.");
        if (request.Headers.GetValues("api_access_token").Single() != token) throw new Exception("Token com sinal de igual foi lido incorretamente.");
        if (request.RequestUri.Host != "chatwoot.invalid") throw new Exception("Nenhum servidor real pode ser consultado.");
        Calls++;
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = new StringContent(Calls == 1 ? payload : "{\"payload\":[]}");
        return Task.FromResult(response);
    }
}
'@ -ReferencedAssemblies @([System.Net.Http.HttpClient].Assembly.Location, [System.Linq.Enumerable].Assembly.Location, [System.Net.HttpStatusCode].Assembly.Location)
$root = Join-Path $env:TEMP ('ServiceDesk-Fields-Test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$fixtureToken = 'TOKEN-FICTICIO=='
$payload = @{ payload = @(@{
    id=123; created_at=1794340000; message_type=1; private=$false; content='TEXTO-NAO-EXPORTAR'
    sender=@{ id=42; type='user'; name='NOME-NAO-EXPORTAR' }
    attachments=@(@{ data_url='https://exemplo.invalid/ANEXO-NAO-EXPORTAR' })
}) } | ConvertTo-Json -Depth 6 -Compress
$global:ServiceDeskFixtureHandler = New-Object LeituraCamposHandler -ArgumentList $fixtureToken,$payload
# Injetar somente o transporte simulado. O script real e seu parser são executados integralmente.
function New-Object {
    param([string]$TypeName, [object[]]$ArgumentList)
    if ($TypeName -eq 'System.Net.Http.HttpClient') {
        return Microsoft.PowerShell.Utility\New-Object System.Net.Http.HttpClient -ArgumentList $global:ServiceDeskFixtureHandler
    }
    if ($null -eq $ArgumentList -or $ArgumentList.Count -eq 0) { return Microsoft.PowerShell.Utility\New-Object -TypeName $TypeName }
    return Microsoft.PowerShell.Utility\New-Object -TypeName $TypeName -ArgumentList $ArgumentList
}
try {
    $config = Join-Path $root 'application.properties'
    @('# comentario', '', 'linha sem separador', 'chatwoot.base-url=https://chatwoot.invalid', "chatwoot.api-access-token=$fixtureToken", 'teste=valor=com=iguais') |
        Set-Content -LiteralPath $config -Encoding utf8
    $destino = Join-Path $root 'campos.json'
    & "$PSScriptRoot/Exportar-CamposConversa.ps1" -Configuracao $config -Destino $destino
    $texto = Get-Content -LiteralPath $destino -Raw
    $dados = $texto | ConvertFrom-Json
    if (!$dados.complete -or @($dados.messages).Count -ne 1 -or $dados.messages[0].sender.id -ne 42) { throw 'Metadados incompletos.' }
    if ($texto -match 'TOKEN-FICTICIO|TEXTO-NAO-EXPORTAR|NOME-NAO-EXPORTAR|ANEXO-NAO-EXPORTAR') { throw 'Dados desnecessarios foram exportados.' }
    if ([LeituraCamposHandler]::Calls -ne 2) { throw 'Paginacao nao foi executada.' }
    Write-Host "OK: PowerShell $($PSVersionTable.PSVersion), leitura real do script, token com =, paginacao GET e metadados sem conteudo."
} finally {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Variable ServiceDeskFixtureHandler -Scope Global -ErrorAction SilentlyContinue
}
