param(
    [Parameter(Mandatory)][string]$Certificado,
    [Parameter(Mandatory)][string]$ArquivoSenha,
    [Parameter(Mandatory)][string[]]$Arquivos
)
$ErrorActionPreference = 'Stop'
$senha = ConvertTo-SecureString ([IO.File]::ReadAllText((Resolve-Path $ArquivoSenha)).Trim()) -AsPlainText -Force
$cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    (Resolve-Path $Certificado).Path, $senha,
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
try {
    if (!$cert.HasPrivateKey) { throw 'Certificado sem chave privada.' }
    if ($cert.NotAfter -le (Get-Date)) { throw 'Certificado expirado.' }
    foreach ($arquivo in $Arquivos) {
        $caminho = (Resolve-Path $arquivo).Path
        $resultado = Set-AuthenticodeSignature -FilePath $caminho -Certificate $cert -HashAlgorithm SHA256 -TimestampServer 'http://timestamp.digicert.com'
        # Certificado próprio ainda não confiado pode retornar UnknownError/NotTrusted.
        $assinatura = Get-AuthenticodeSignature -FilePath $caminho
        if (!$assinatura.SignerCertificate -or $assinatura.SignerCertificate.Thumbprint -ne $cert.Thumbprint -or
            $assinatura.Status -eq 'HashMismatch' -or !$assinatura.TimeStamperCertificate) {
            throw "Falha ao assinar ${caminho}: $($resultado.StatusMessage)"
        }
        Write-Output "Assinado: $caminho ($($assinatura.Status))"
    }
} finally { $cert.Dispose(); $senha.Dispose() }
