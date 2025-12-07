<#
.SYNOPSIS
    Kills safe development resource hog processes to free CPU/RAM during intensive builds.

.DESCRIPTION
    Aggressively terminates background processes that consume resources but are safe to kill:
    - Browsers (Chrome, Edge) without visible windows
    - Chat/sync apps (Slack, Discord, OneDrive, Zoom, Spotify)
    - Antimalware (MsMpEng) when CPU > 20%
    - Idle node.exe/python.exe instances (no visible windows, CPU > 5%)
    
    Safety filters:
    - Excludes processes with visible windows (MainWindowTitle)
    - Excludes VSCode-related paths
    - Excludes current PowerShell session
    - Uses SilentlyContinue to avoid errors on already-terminated processes

.PARAMETER DryRun
    Preview processes that would be killed without actually terminating them.

.PARAMETER MinCpuPercent
    Minimum CPU percentage for node/python processes to be killed (default: 5).

.PARAMETER MinMalwareCpu
    Minimum CPU percentage for antimalware processes to be killed (default: 20).

.EXAMPLE
    .\kill-safe-dev-processes.ps1
    Kills all safe dev bloat processes.

.EXAMPLE
    .\kill-safe-dev-processes.ps1 -DryRun
    Shows what would be killed without actually terminating.

.EXAMPLE
    .\kill-safe-dev-processes.ps1 -MinCpuPercent 10 -MinMalwareCpu 30
    More conservative: only kill node/python using >10% CPU and malware using >30% CPU.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [int]$MinCpuPercent = 5,

    [Parameter(Mandatory = $false)]
    [int]$MinMalwareCpu = 20
)

$ErrorActionPreference = 'SilentlyContinue'

# Define safe-to-kill process categories
$alwaysKillBrowsers = @('chrome', 'msedge', 'msedgewebview2')
$alwaysKillApps = @('Slack', 'Discord', 'OneDrive', 'Zoom', 'Spotify', 'Steam', 'Teams')
$conditionalKillScripting = @('node', 'python', 'pythonw')
$conditionalKillAntimalware = @('MsMpEng', 'MpCmdRun')

$killed = 0
$skipped = 0

Write-Host "`n🧹 Safe Dev Bloat Killer" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "[DRY RUN MODE - No processes will be killed]`n" -ForegroundColor Yellow
}

# Function to safely kill processes
function Kill-SafeProcess {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        
        [Parameter(Mandatory)]
        [string]$Reason
    )

    $memoryMB = [math]::Round($Process.WorkingSet / 1MB, 2)
    $cpu = if ($Process.CPU) { [math]::Round($Process.CPU, 2) } else { 0 }
    
    $message = "  [$Reason] $($Process.ProcessName) (PID: $($Process.Id)) - CPU: $cpu, RAM: ${memoryMB}MB"
    
    if ($DryRun) {
        Write-Host $message -ForegroundColor Yellow
        $script:skipped++
    } else {
        Write-Host $message -ForegroundColor Green
        try {
            $Process | Stop-Process -Force -ErrorAction Stop
            $script:killed++
        } catch {
            Write-Host "    └─ Failed to kill: $_" -ForegroundColor Red
        }
    }
}

# Get all processes once for efficiency
$allProcesses = Get-Process | Where-Object { $_.Id -ne $PID }

Write-Host "`n1️⃣  Killing background browsers..." -ForegroundColor Cyan
$browserProcesses = $allProcesses | Where-Object {
    $_.ProcessName -in $alwaysKillBrowsers -and
    $_.MainWindowTitle -eq '' -and
    $_.Path -notlike '*\Visual Studio Code\*'
}
foreach ($proc in $browserProcesses) {
    Kill-SafeProcess -Process $proc -Reason "Browser (no window)"
}

Write-Host "`n2️⃣  Killing chat/sync apps (no visible windows)..." -ForegroundColor Cyan
$appProcesses = $allProcesses | Where-Object {
    $_.ProcessName -in $alwaysKillApps -and
    $_.MainWindowTitle -eq '' -and
    $_.Path -notlike '*\Visual Studio Code\*'
}
foreach ($proc in $appProcesses) {
    Kill-SafeProcess -Process $proc -Reason "App (background)"
}

Write-Host "`n3️⃣  Killing idle scripting processes (CPU > $MinCpuPercent%)..." -ForegroundColor Cyan
$scriptProcesses = $allProcesses | Where-Object {
    $_.ProcessName -in $conditionalKillScripting -and
    $_.MainWindowTitle -eq '' -and
    $_.CPU -gt $MinCpuPercent -and
    $_.Path -notlike '*\Visual Studio Code\*' -and
    $_.Path -notlike '*\python.exe*' # Exclude if it's the active Python interpreter
}
foreach ($proc in $scriptProcesses) {
    Kill-SafeProcess -Process $proc -Reason "Script (idle CPU>$MinCpuPercent%)"
}

Write-Host "`n4️⃣  Killing high-CPU antimalware (CPU > $MinMalwareCpu%)..." -ForegroundColor Cyan
$malwareProcesses = $allProcesses | Where-Object {
    $_.ProcessName -in $conditionalKillAntimalware -and
    $_.CPU -gt $MinMalwareCpu
}
foreach ($proc in $malwareProcesses) {
    Kill-SafeProcess -Process $proc -Reason "Antimalware (CPU>$MinMalwareCpu%)"
}

# Summary
Write-Host "`n" + ("=" * 50) -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "✅ Dry run complete: $skipped processes would be killed" -ForegroundColor Yellow
} else {
    Write-Host "✅ Killed $killed processes successfully" -ForegroundColor Green
    if ($skipped -gt 0) {
        Write-Host "⚠️  Failed to kill $skipped processes" -ForegroundColor Yellow
    }
}
Write-Host ""
