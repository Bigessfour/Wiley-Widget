param(
    [string]$ExePath = "./publish/WileyWidget.WinForms.exe",
    [int]$TimeoutSeconds = 20
)

Write-Host "Verifying startup for: $ExePath"
if (-not (Test-Path $ExePath)) {
    Write-Error "Executable not found: $ExePath"
    exit 2
}

$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $ExePath
$startInfo.Arguments = "--verify-startup"
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true

$proc = [System.Diagnostics.Process]::Start($startInfo)
$proc.WaitForExit($TimeoutSeconds * 1000) | Out-Null

if (-not $proc.HasExited) {
    Write-Error "Process did not exit within $TimeoutSeconds seconds"
    $proc.Kill()
    exit 3
}

Write-Host "Exit code: $($proc.ExitCode)"
$stdout = $proc.StandardOutput.ReadToEnd()
$stderr = $proc.StandardError.ReadToEnd()
if ($stdout) { Write-Host "STDOUT:`n$stdout" }
if ($stderr) { Write-Host "STDERR:`n$stderr" }

if ($proc.ExitCode -ne 0) { exit $proc.ExitCode }

Write-Host "Startup verification succeeded."