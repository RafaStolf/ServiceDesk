param([switch]$Teste)
$ErrorActionPreference = 'Stop'
$pasta = Join-Path $env:RUNNER_TEMP 'servicedesk-signing'
New-Item -ItemType Directory -Path $pasta -Force | Out-Null
if ($Teste) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=ServiceDesk CI Teste - nao distribuir' -CertStoreLocation Cert:\CurrentUser\My
} else {
    if (!$env:SERVICEDESK_CERTIFICATE_PFX_BASE64 -or !$env:SERVICEDESK_CERTIFICATE_PASSWORD) {
        throw 'Configure os secrets SERVICEDESK_CERTIFICATE_PFX_BASE64 e SERVICEDESK_CERTIFICATE_PASSWORD antes de publicar.'
    }
    $arquivo = Join-Path $pasta 'ServiceDesk.pfx'
    try {
        [IO.File]::WriteAllBytes($arquivo, [Convert]::FromBase64String($env:SERVICEDESK_CERTIFICATE_PFX_BASE64))
        $senha = ConvertTo-SecureString $env:SERVICEDESK_CERTIFICATE_PASSWORD -AsPlainText -Force
        $cert = Import-PfxCertificate -FilePath $arquivo -CertStoreLocation Cert:\CurrentUser\My -Password $senha
    } finally {
        Remove-Item -LiteralPath $arquivo -Force -ErrorAction SilentlyContinue
        if ($senha) { $senha.Dispose() }
    }
}
if (@($cert).Count -ne 1 -or !$cert.HasPrivateKey -or $cert.NotAfter -le (Get-Date) -or $cert.NotBefore -gt (Get-Date)) {
    throw 'Certificado de assinatura ausente, ambiguo ou fora da validade.'
}
$usos = @($cert.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' } | ForEach-Object {
    $_.EnhancedKeyUsages | ForEach-Object { $_.Value }
})
if ('1.3.6.1.5.5.7.3.3' -notin $usos) {
    throw 'O certificado nao autoriza assinatura de codigo.'
}
$publico = Join-Path $env:GITHUB_WORKSPACE 'ServiceDesk-Publicador.cer'
Export-Certificate -Cert $cert -FilePath $publico | Out-Null
# Confianca somente no runner descartavel, para validar o certificado proprio.
Import-Certificate -FilePath $publico -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
"SIGNING_THUMBPRINT=$($cert.Thumbprint)" >> $env:GITHUB_ENV
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" | Sort-Object FullName -Descending | Select-Object -First 1
if (!$signtool) { throw 'SignTool do Windows SDK nao encontrado.' }
"SIGNTOOL_PATH=$($signtool.FullName)" >> $env:GITHUB_ENV
