param(
    [Parameter(Mandatory=$true)]
    [string]$Path
)

# Collect process list and, if Sysinternals handle.exe is available, try to find owners for $Path
Write-Host "Collecting file lock information for: $Path"
Get-Process | Where-Object { $_.ProcessName -match 'Code|pwsh|powershell|python|dotnet|explorer|msbuild' } | Select-Object ProcessName,Id,Path | Format-Table -AutoSize

$handlesExe = "C:\Program Files\Sysinternals\handle.exe"
if (-not (Test-Path $handlesExe)) {
    $handlesExe = "C:\Windows\System32\handle.exe"
}

if (Test-Path $handlesExe) {
    Write-Host "Running handle.exe for: $Path"
    & "$handlesExe" -nobanner $Path | Select-Object -First 200
} else {
    Write-Host "handle.exe not found on disk — install Sysinternals (https://docs.microsoft.com/sysinternals/) to enable deeper diagnostics."
}
