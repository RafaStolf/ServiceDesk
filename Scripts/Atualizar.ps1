param([Parameter(Mandatory=$true)][string]$ConfigPath)
$ErrorActionPreference = 'Stop'
$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
$backup = Join-Path $config.Work 'backup'
$changed = [System.Collections.Generic.List[string]]::new()
try {
    $old = Get-Process -Id $config.ProcessId -ErrorAction SilentlyContinue
    if ($old -and !$old.WaitForExit(60000)) { throw 'O ServiceDesk ainda está aberto. Feche-o e tente novamente.' }
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    foreach ($source in Get-ChildItem -LiteralPath $config.Stage -File -Recurse) {
        $relative = $source.FullName.Substring($config.Stage.Length).TrimStart('\')
        $target = Join-Path $config.Target $relative
        $saved = Join-Path $backup $relative
        if (Test-Path -LiteralPath $target) {
            New-Item -ItemType Directory -Path (Split-Path $saved) -Force | Out-Null
            Copy-Item -LiteralPath $target -Destination $saved -Force
        }
        $changed.Add($relative)
        New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
        Copy-Item -LiteralPath $source.FullName -Destination $target -Force
    }
    $process = Start-Process -FilePath (Join-Path $config.Target 'ServiceDesk.exe') -WorkingDirectory $config.Target -PassThru
    Start-Sleep -Seconds 10
    if ($process.HasExited) { throw 'A nova versão não iniciou. Restaurando a versão anterior.' }
    'Atualização concluída.' | Set-Content -LiteralPath (Join-Path $config.Work 'resultado.txt')
} catch {
    $reason = $_.Exception.Message
    foreach ($relative in $changed) {
        $target = Join-Path $config.Target $relative
        $saved = Join-Path $backup $relative
        if (Test-Path -LiteralPath $saved) { Copy-Item -LiteralPath $saved -Destination $target -Force }
        elseif (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
    }
    $reason | Set-Content -LiteralPath (Join-Path $config.Work 'resultado.txt')
    if ($changed.Count -gt 0) { Start-Process -FilePath (Join-Path $config.Target 'ServiceDesk.exe') -WorkingDirectory $config.Target }
    if ($env:SERVICEDESK_DIAGNOSTICO -ne '1') { (New-Object -ComObject WScript.Shell).Popup("Não foi possível atualizar: $reason", 0, 'ServiceDesk', 16) | Out-Null }
    exit 1
}
