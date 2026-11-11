param([Parameter(Mandatory)][string[]]$Arquivos, [switch]$SomenteVerificar)
$ErrorActionPreference = 'Stop'
if (!$env:SIGNING_THUMBPRINT -or !$env:SIGNTOOL_PATH) { throw 'Assinatura CI nao preparada.' }
foreach ($arquivo in $Arquivos) {
    $caminho = (Resolve-Path -LiteralPath $arquivo).Path
    if (!$SomenteVerificar) {
        & $env:SIGNTOOL_PATH sign /s My /sha1 $env:SIGNING_THUMBPRINT /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $caminho
        if ($LASTEXITCODE -ne 0) { throw "Falha ao assinar $caminho." }
    }
    $assinatura = Get-AuthenticodeSignature -LiteralPath $caminho
    if ($assinatura.Status -ne 'Valid' -or $assinatura.SignerCertificate.Thumbprint -ne $env:SIGNING_THUMBPRINT -or !$assinatura.TimeStamperCertificate) {
        throw "Assinatura ou carimbo de tempo invalido: $caminho ($($assinatura.Status))."
    }
}
