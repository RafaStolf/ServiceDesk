param([Parameter(Mandatory=$true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ServiceDeskNativeTest {
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint="PostMessageW")] public static extern bool PostMessage(IntPtr hwnd, uint msg, UIntPtr wp, IntPtr lp);
}
'@
$exe = Join-Path $PackagePath 'ServiceDesk.exe'
$process = Start-Process -FilePath $exe -ArgumentList '--background' -WorkingDirectory $PackagePath -PassThru
try {
    Start-Sleep -Seconds 5
    if ($process.HasExited) { throw 'Aplicativo encerrou no início em segundo plano.' }
    $hwnd = [IntPtr][long](Get-Content (Join-Path $env:TEMP "ServiceDesk-window-$($process.Id).txt"))
    if ([ServiceDeskNativeTest]::IsWindowVisible($hwnd)) { throw 'Início automático abriu a janela.' }
    $second = Start-Process -FilePath $exe -WorkingDirectory $PackagePath -PassThru
    if (!$second.WaitForExit(10000)) { Stop-Process -Id $second.Id -Force; throw 'Uma segunda instância permaneceu aberta.' }
    Start-Sleep -Seconds 2
    if (![ServiceDeskNativeTest]::IsWindowVisible($hwnd)) { throw 'O atalho não reabriu a instância existente.' }
    [ServiceDeskNativeTest]::PostMessage($hwnd, 0x10, [UIntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Seconds 2
    $process.Refresh()
    if ($process.HasExited -or [ServiceDeskNativeTest]::IsWindowVisible($hwnd)) { throw 'Fechar a janela não manteve o aplicativo em segundo plano.' }
    Write-Host 'OK: início oculto, instância única, reabertura e fechar para segundo plano.'
} finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }
    Remove-Item (Join-Path $env:TEMP "ServiceDesk-window-$($process.Id).txt") -ErrorAction SilentlyContinue
}
