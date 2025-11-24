<#
.SYNOPSIS
  Capture a native crash dump for the running WileyWidget.WinUI process using ProcDump.

.DESCRIPTION
  This helper will look for `procdump.exe` on PATH or in a local `tools\procdump` folder. If found,
  it will attach and write a full dump (-ma) on an unhandled exception (-e) into the logs/native-dumps folder.

  If ProcDump is not installed, the script prints instructions for downloading Sysinternals ProcDump.
#>
[CmdletBinding()]
param(
    [string]$ProcessName = 'WileyWidget.WinUI',
    [string]$DumpDir = "$PSScriptRoot\..\..\logs\native-dumps"
)

# Ensure dump directory exists
$DumpDir = Resolve-Path -Path $DumpDir -ErrorAction SilentlyContinue | ForEach-Object { $_.Path } -ErrorAction SilentlyContinue
if (-not $DumpDir) {
    $DumpDir = Join-Path $PSScriptRoot "..\..\logs\native-dumps"
}
if (-not (Test-Path -Path $DumpDir)) { New-Item -ItemType Directory -Path $DumpDir | Out-Null }

function Find-ProcDump {
    # Check PATH
    $pd = Get-Command procdump -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source -ErrorAction SilentlyContinue
    if ($pd) { return $pd }
    # Check local tools folder
    $local = Join-Path $PSScriptRoot 'procdump.exe'
    if (Test-Path $local) { return $local }
    $localFolder = Join-Path $PSScriptRoot '..\..\tools\procdump\procdump.exe'
    if (Test-Path $localFolder) { return (Resolve-Path $localFolder).Path }
    # Check repository root (user placed procdump.exe at repo root)
    $repoRoot = Resolve-Path -Path (Join-Path $PSScriptRoot '..\..\..') -ErrorAction SilentlyContinue
    if ($repoRoot) {
        $candidate = Join-Path $repoRoot.Path 'procdump.exe'
        if (Test-Path $candidate) { return (Resolve-Path $candidate).Path }
    }
    return $null
}

$procDump = Find-ProcDump
if (-not $procDump) {
    Write-Host 'ProcDump (procdump.exe) not found on PATH or in scripts/tools.' -ForegroundColor Yellow
    Write-Host 'Download from Sysinternals: https://learn.microsoft.com/sysinternals/downloads/procdump' -ForegroundColor Yellow
    Write-Host "After placing procdump.exe in %USERPROFILE%\\tools or adding it to PATH re-run this script."
    exit 2
}

Write-Host "Found ProcDump: $procDump" -ForegroundColor Green
Write-Host "Dump directory: $DumpDir" -ForegroundColor Green

# Start ProcDump: -e capture unhandled exceptions, -ma full dump, -x write dumped files to folder
# Use -w to wait for the process to start and then attach to it. If the process name
# doesn't include the .exe suffix, append it so ProcDump doesn't try to "launch" a missing file.
try {
    $target = $ProcessName
    if (-not $target.EndsWith('.exe', [System.StringComparison]::InvariantCultureIgnoreCase)) {
        $target = "$target.exe"
    }

    # If a fully-qualified exe exists in the project's output, prefer that so ProcDump can launch it directly
    $exePath = $null
    $repoRoot = Resolve-Path -Path (Join-Path $PSScriptRoot '..\..\') -ErrorAction SilentlyContinue
    if ($repoRoot) {
        $candidate = Join-Path $repoRoot.Path "src\WileyWidget.WinUI\bin\Debug\net9.0-windows10.0.26100.0\win-x64\$target"
        if (Test-Path $candidate) { $exePath = (Resolve-Path $candidate).Path }
    }

    if ($exePath) {
        Write-Host "Launching and monitoring: $exePath" -ForegroundColor Green
        & $procDump -e -ma -x $DumpDir $exePath
    }
    else {
        Write-Host "Waiting for process '$target' to start and then attaching (using -w)" -ForegroundColor Green
        & $procDump -e -ma -x $DumpDir -w $target
    }
}
catch {
    Write-Host "Failed to run ProcDump: $_" -ForegroundColor Red
    exit 3
}

Write-Host "ProcDump exited. Check $DumpDir for .dmp files." -ForegroundColor Green

# Suggest next step
Write-Host "Next: run 'scripts/tools/analyze-native-dump.ps1 <path-to-dump>' or attach the dump to a debugger (WinDbg/Visual Studio)." -ForegroundColor Cyan
