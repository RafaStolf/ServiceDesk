param([Parameter(Mandatory=$true)][string]$InstallerPath)
$ErrorActionPreference = 'Stop'
$root = Join-Path $env:TEMP ('ServiceDesk-Installer-Test-' + [guid]::NewGuid().ToString('N'))
$target = Join-Path $root 'instalacao com espacos'
$config = Join-Path $env:USERPROFILE '.servicedesk'
$sentinela = Join-Path $config ('teste-instalador-' + [guid]::NewGuid().ToString('N') + '.txt')
New-Item -ItemType Directory -Path $root, $config -Force | Out-Null
'preservar' | Set-Content -LiteralPath $sentinela
function Executar([string]$Exe, [string]$Argumentos) {
    $process = Start-Process -FilePath $Exe -ArgumentList $Argumentos -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Processo falhou: $Exe (código $($process.ExitCode))." }
}
try {
    Executar $InstallerPath "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=`"$target`" /LOG=`"$root/install.log`""
    if (!(Test-Path "$target/ServiceDesk.exe") -or !(Test-Path "$target/unins000.exe")) { throw 'Instalação incompleta.' }
    $atalho = Join-Path ([Environment]::GetFolderPath('Programs')) 'ServiceDesk.lnk'
    if (!(Test-Path $atalho)) { throw 'Atalho do menu Iniciar não foi criado.' }
    $shell = New-Object -ComObject Shell.Application
    $itemAtalho = $shell.Namespace((Split-Path $atalho)).ParseName((Split-Path $atalho -Leaf))
    if ($itemAtalho.ExtendedProperty('System.AppUserModel.ID') -ne 'Centrium.ServiceDesk') { throw 'Identidade de notificacoes ausente no atalho.' }
    $env:SERVICEDESK_DIAGNOSTICO = '1'
    $app = Start-Process -FilePath "$target/ServiceDesk.exe" -WorkingDirectory $target -PassThru
    Start-Sleep -Seconds 10
    if ($app.HasExited) { throw 'Aplicativo instalado encerrou ao iniciar.' }
    Stop-Process -Id $app.Id -Force
    Executar $InstallerPath "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=`"$target`" /LOG=`"$root/reinstall.log`""
    Executar "$target/unins000.exe" "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$root/uninstall.log`""
    if (Test-Path "$target/ServiceDesk.exe") { throw 'Executável permaneceu após a desinstalação.' }
    if (Test-Path $atalho) { throw 'Atalho permaneceu após a desinstalação.' }
    if ((Get-Content -LiteralPath $sentinela).Trim() -ne 'preservar') { throw 'Configuração do usuário alterada.' }
    Write-Host 'OK: instalação em caminho com espaços, abertura, reinstalação e desinstalação; perfil preservado.'
} finally {
    Get-Process ServiceDesk -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq "$target\ServiceDesk.exe" } | Stop-Process -Force
    Remove-Item -LiteralPath $sentinela -Force -ErrorAction SilentlyContinue
    # Manter os logs no runner para diagnóstico se o teste falhar.
}
