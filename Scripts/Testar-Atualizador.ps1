param([Parameter(Mandatory=$true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
$root = Join-Path $env:TEMP ('ServiceDesk-Test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
try {
    foreach ($rollback in @($false, $true)) {
        $work = Join-Path $root ([guid]::NewGuid().ToString('N'))
        $target = Join-Path $work 'target'
        $stage = Join-Path $work 'stage'
        New-Item -ItemType Directory -Path $target, $stage | Out-Null
        Copy-Item "$PackagePath/*" $target -Recurse
        Copy-Item "$PackagePath/*" $stage -Recurse
        'antigo' | Set-Content (Join-Path $target 'update-marker.txt')
        'novo' | Set-Content (Join-Path $stage 'update-marker.txt')
        'preservar' | Set-Content (Join-Path $target 'arquivo-local.txt')
        if ($rollback) { 'executavel invalido' | Set-Content (Join-Path $stage 'ServiceDesk.exe') }
        $config = Join-Path $work 'config.json'
        @{Work=$work; Target=$target; Stage=$stage; ProcessId=2147483647} | ConvertTo-Json | Set-Content $config
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot/Atualizar.ps1" -ConfigPath $config
        $result = $LASTEXITCODE
        if (!$rollback -and $result -ne 0) { throw 'Instalação de teste falhou.' }
        if ($rollback -and $result -eq 0) { throw 'Deveria restaurar a versão anterior.' }
        $expected = if ($rollback) { 'antigo' } else { 'novo' }
        if ((Get-Content (Join-Path $target 'update-marker.txt')).Trim() -ne $expected) { throw 'Falha na substituição/restauração.' }
        if ((Get-Content (Join-Path $target 'arquivo-local.txt')).Trim() -ne 'preservar') { throw 'Arquivo local alterado.' }
        Start-Sleep -Seconds 3
        Get-Process ServiceDesk -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $target 'ServiceDesk.exe') } | Stop-Process -Force
        Write-Host "OK: atualizador (restauração=$rollback), arquivo local preservado."
    }
    $global:LASTEXITCODE = 0
} finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
