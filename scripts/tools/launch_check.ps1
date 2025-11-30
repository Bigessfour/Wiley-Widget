$proc = Start-Process -FilePath 'c:\Users\biges\Desktop\Wiley-Widget\WileyWidget.WinForms\bin\Debug\net9.0-windows\WileyWidget.WinForms.exe' -PassThru
Start-Sleep -Seconds 3
try {
    $p = Get-Process -Id $proc.Id -ErrorAction Stop
    Write-Host "PID:$($proc.Id) Name:$($p.ProcessName) Title:$($p.MainWindowTitle)"
} catch {
    Write-Host "Process not running"
}
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
