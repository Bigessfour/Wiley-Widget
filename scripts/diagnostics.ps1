# WileyWidget Diagnostic Tools
# Collection of PowerShell scripts for debugging and diagnostics

#Requires -Version 7.0

<#
.SYNOPSIS
    Comprehensive diagnostic script for WileyWidget debugging
.DESCRIPTION
    Runs multiple diagnostic checks and captures system state for debugging
.PARAMETER ProcessName
    Name of the process to diagnose (default: WileyWidget*)
.PARAMETER OutputPath
    Path to save diagnostic output (default: tmp/diagnostics)
.EXAMPLE
    .\diagnostics.ps1 -ProcessName "WileyWidget.WinForms"
#>

[CmdletBinding()]
param(
    [string]$ProcessName = "WileyWidget*",
    [string]$OutputPath = "tmp/diagnostics"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Create output directory
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportFile = Join-Path $OutputPath "diagnostic-report-$timestamp.txt"

function Write-Diagnostic {
    param([string]$Message, [string]$Level = "INFO")
    $logMessage = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path $reportFile -Value $logMessage
}

Write-Diagnostic "Starting WileyWidget Diagnostic Report" "HEADER"
Write-Diagnostic "Timestamp: $timestamp"
Write-Diagnostic "Process Pattern: $ProcessName"
Write-Diagnostic "Output Path: $OutputPath"
Write-Diagnostic ""

# System Information
Write-Diagnostic "=== SYSTEM INFORMATION ==="
Write-Diagnostic "OS: $([System.Environment]::OSVersion.VersionString)"
Write-Diagnostic "PowerShell: $($PSVersionTable.PSVersion)"
Write-Diagnostic ".NET Versions: $(dotnet --list-sdks)"
Write-Diagnostic ""

# Process Information
Write-Diagnostic "=== PROCESS INFORMATION ==="
$processes = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue
if ($processes) {
    foreach ($process in $processes) {
        Write-Diagnostic "Process: $($process.Name) (ID: $($process.Id))"
        Write-Diagnostic "  CPU: $($process.CPU.ToString('F2'))%"
        Write-Diagnostic "  Memory: $([math]::Round($process.WorkingSet64 / 1MB, 2)) MB"
        Write-Diagnostic "  Threads: $($process.Threads.Count)"
        Write-Diagnostic "  Start Time: $($process.StartTime)"
        Write-Diagnostic ""
    }
} else {
    Write-Diagnostic "No processes found matching pattern: $ProcessName" "WARNING"
}

# Recent Error Logs
Write-Diagnostic "=== RECENT ERROR LOGS ==="
$logFiles = Get-ChildItem "logs/*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 3
if ($logFiles) {
    foreach ($logFile in $logFiles) {
        Write-Diagnostic "Analyzing: $($logFile.Name)"
        $errors = Select-String -Path $logFile.FullName -Pattern "ERROR|EXCEPTION|FATAL" -CaseSensitive:$false
        if ($errors) {
            Write-Diagnostic "  Found $($errors.Count) error(s) in last 24 hours"
            $errors | Select-Object -Last 5 | ForEach-Object {
                Write-Diagnostic "    $($_.LineNumber): $($_.Line.Trim())"
            }
        } else {
            Write-Diagnostic "  No errors found"
        }
        Write-Diagnostic ""
    }
} else {
    Write-Diagnostic "No log files found" "WARNING"
}

# Performance Counters
Write-Diagnostic "=== PERFORMANCE METRICS ==="
try {
    $cpu = Get-Counter '\Processor(_Total)\% Processor Time' -ErrorAction Stop
    Write-Diagnostic "CPU Usage: $($cpu.CounterSamples.CookedValue.ToString('F1'))%"

    $memory = Get-Counter '\Memory\% Committed Bytes In Use' -ErrorAction Stop
    Write-Diagnostic "Memory Usage: $($memory.CounterSamples.CookedValue.ToString('F1'))%"
} catch {
    Write-Diagnostic "Could not retrieve performance counters: $($_.Exception.Message)" "WARNING"
}

Write-Diagnostic ""
Write-Diagnostic "=== DIAGNOSTIC COMPLETE ==="
Write-Diagnostic "Report saved to: $reportFile"

# Auto-capture dump if processes are hanging
$hungProcesses = $processes | Where-Object { $_.Responding -eq $false }
if ($hungProcesses) {
    Write-Diagnostic "Detected non-responsive processes. Capturing memory dumps..." "WARNING"
    foreach ($hungProcess in $hungProcesses) {
        $dumpPath = Join-Path $OutputPath "dump-$($hungProcess.Id)-$timestamp.dmp"
        Write-Diagnostic "Capturing dump for process $($hungProcess.Id) to $dumpPath"
        try {
            dotnet-dump collect -p $hungProcess.Id -o $dumpPath
            Write-Diagnostic "Dump captured successfully"
        } catch {
            Write-Diagnostic "Failed to capture dump: $($_.Exception.Message)" "ERROR"
        }
    }
}

Write-Diagnostic "Diagnostic script completed"