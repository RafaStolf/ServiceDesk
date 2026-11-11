param(
    [string]$Pasta = 'D:\Documentos\Arquivos\Centrium\Projetos\Certificados\ServiceDesk-Assinatura',
    [string]$Repositorio = 'RafaStolf/ServiceDesk'
)
$ErrorActionPreference = 'Stop'
if (!(Get-Command gh -ErrorAction SilentlyContinue)) { throw 'Instale o GitHub CLI: winget install --id GitHub.cli ; depois execute gh auth login.' }
gh auth status
if ($LASTEXITCODE -ne 0) { throw 'Execute gh auth login com acesso de administrador ao repositorio.' }
$pfx = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $Pasta 'ServiceDesk.pfx')))
$senha = [IO.File]::ReadAllText((Join-Path $Pasta 'password.txt')).Trim()
# Entrada padrao: nenhum segredo aparece em argumento, arquivo temporario ou saida.
$pfx | gh secret set SERVICEDESK_CERTIFICATE_PFX_BASE64 --repo $Repositorio
if ($LASTEXITCODE -ne 0) { throw 'Falha ao cadastrar o PFX.' }
$senha | gh secret set SERVICEDESK_CERTIFICATE_PASSWORD --repo $Repositorio
if ($LASTEXITCODE -ne 0) { throw 'Falha ao cadastrar a senha. Cadastre ambos antes de publicar.' }
Remove-Variable pfx, senha
Write-Output 'Secrets cadastrados. Apenas seus nomes podem ser listados no GitHub.'
