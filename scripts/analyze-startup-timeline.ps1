#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analyzes WileyWidget startup timing from the latest startup log session.

.DESCRIPTION
    This script starts the WinForms app, waits for startup activity, stops the process,
    extracts the newest startup session from the daily log, and writes a report with:
      - OnShown duration
      - Deferred ribbon initialization duration
      - Maximum UI probe callback latency
      - UI probe timeout count
      - Slow UI operations recorded during startup

.EXAMPLE
    .\scripts\analyze-startup-timeline.ps1

.EXAMPLE
    .\scripts\analyze-startup-timeline.ps1 -TimeoutSeconds 20 -OutputDir .\diagnostics
#>

param(
    [int]$TimeoutSeconds = 20,
    [string]$OutputDir = "./diagnostics"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-LatestStartupLog {
    param([string]$BaseDir)

    $logDir = Join-Path $BaseDir 'logs'
    if (-not (Test-Path $logDir)) {
        throw "Log directory not found: $logDir"
    }

    $latest = Get-ChildItem -Path $logDir -Filter 'wiley-widget-*.log' -File |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

    if (-not $latest) {
        throw "No startup log files found under: $logDir"
    }

    return $latest.FullName
}

function Get-LatestStartupSessionLines {
    param([string[]]$Lines)

    $startIndexes = @()
    for ($i = 0; $i -lt $Lines.Length; $i++) {
        if ($Lines[$i] -match 'Program\.Main: Starting WileyWidget application') {
            $startIndexes += $i
        }
    }

    if ($startIndexes.Count -eq 0) {
        throw 'No startup session markers found in log.'
    }

    $start = $startIndexes[-1]
    return $Lines[$start..($Lines.Length - 1)]
}

function Get-LastIntMatch {
    param(
        [string[]]$Lines,
        [string]$Pattern
    )

    $value = $null
    foreach ($line in $Lines) {
        $match = [regex]::Match($line, $Pattern)
        if ($match.Success) {
            $value = [int]$match.Groups[1].Value
        }
    }

    return $value
}

function Get-MaxLatency {
    param([string[]]$Lines)

    $maxLatency = 0
    foreach ($line in $Lines) {
        $matches = [regex]::Matches($line, '(?:Elevated callback latency|Warm-up high UI callback latency)\s+(\d+)ms')
        foreach ($match in $matches) {
            $latency = [int]$match.Groups[1].Value
            if ($latency -gt $maxLatency) {
                $maxLatency = $latency
            }
        }
    }

    return $maxLatency
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$workspaceRoot = (Get-Location).Path
$reportFile = Join-Path $OutputDir ("startup-timeline-{0}.txt" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Startup Timeline Analysis" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Timeout: ${TimeoutSeconds}s"
Write-Host "Output: $reportFile"

$process = $null
$appStartTime = Get-Date

try {
    $process = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--no-build", "--project", "src/WileyWidget.WinForms/WileyWidget.WinForms.csproj") `
        -PassThru

    Write-Host "Started WileyWidget (PID: $($process.Id))" -ForegroundColor Green

    $deadline = $appStartTime.AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 250
        if ($process.HasExited) {
            break
        }
    } while ((Get-Date) -lt $deadline)
} finally {
    if ($process -ne $null -and -not $process.HasExited) {
        Write-Host "Stopping WileyWidget..." -ForegroundColor Yellow
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }
}

$latestLog = Get-LatestStartupLog -BaseDir $workspaceRoot
$allLines = Get-Content -Path $latestLog
$sessionLines = Get-LatestStartupSessionLines -Lines $allLines

$onShownMs = Get-LastIntMatch -Lines $sessionLines -Pattern 'OnShown completed in\s+(\d+)ms'
$deferredRibbonMs = Get-LastIntMatch -Lines $sessionLines -Pattern 'Deferred ribbon init in\s+(\d+)ms'
$maxUiProbeLatencyMs = Get-MaxLatency -Lines $sessionLines
$uiProbeTimeoutCount = @($sessionLines | Where-Object { $_ -match '\[UI-PROBE\].*(did not service probe within|probe timeout after)' }).Count
$slowUiOperations = @($sessionLines | Where-Object { $_ -match '\[UI-PROBE\] Slow UI operation' })
$coalescedLayoutEvents = @($sessionLines | Where-Object { $_ -match '\[LAYOUT\] Coalesced' })
$startupSessionId = $null
foreach ($line in $sessionLines) {
    $sessionMatch = [regex]::Match($line, 'SessionId=([a-f0-9]+)')
    if ($sessionMatch.Success) {
        $startupSessionId = $sessionMatch.Groups[1].Value
        break
    }
}

$reportLines = @(
    '═══════════════════════════════════════════════════════════════',
    '  WileyWidget Startup Timeline Report',
    '═══════════════════════════════════════════════════════════════',
    '',
    ('Generated: {0}' -f (Get-Date)),
    ('Log: {0}' -f $latestLog),
    ('SessionId: {0}' -f ($(if ($startupSessionId) { $startupSessionId } else { 'unknown' }))),
    '',
    'Startup Metrics:',
    '───────────────────────────────────────────────────────────────',
    ('OnShownMs: {0}' -f ($(if ($null -ne $onShownMs) { $onShownMs } else { 'n/a' }))),
    ('DeferredRibbonMs: {0}' -f ($(if ($null -ne $deferredRibbonMs) { $deferredRibbonMs } else { 'n/a' }))),
    ('MaxUiProbeLatencyMs: {0}' -f $maxUiProbeLatencyMs),
    ('UiProbeTimeoutCount: {0}' -f $uiProbeTimeoutCount),
    ('SlowUiOperationCount: {0}' -f $slowUiOperations.Count),
    ('CoalescedLayoutEventCount: {0}' -f $coalescedLayoutEvents.Count),
    '',
    'Slow UI Operations:',
    '───────────────────────────────────────────────────────────────'
)

if ($slowUiOperations.Count -gt 0) {
    $reportLines += $slowUiOperations
} else {
    $reportLines += 'None recorded in latest session.'
}

$reportLines += @(
    '',
    'Coalesced Layout Events:',
    '───────────────────────────────────────────────────────────────'
)

if ($coalescedLayoutEvents.Count -gt 0) {
    $reportLines += ($coalescedLayoutEvents | Select-Object -First 20)
} else {
    $reportLines += 'None recorded in latest session.'
}

$reportLines += @(
    '',
    'Key Startup Lines:',
    '───────────────────────────────────────────────────────────────'
)

$keyPatterns = @(
    'OnShown completed in',
    'Deferred ribbon init in',
    'Deferred ribbon tab population completed',
    'Right dock panel initialized',
    'Warm-up high UI callback latency'
)

foreach ($pattern in $keyPatterns) {
    $matches = @($sessionLines | Where-Object { $_ -match [regex]::Escape($pattern) })
    if ($matches.Count -gt 0) {
        $reportLines += $matches
    }
}

$reportLines | Out-File -FilePath $reportFile -Encoding UTF8

Write-Host ""
Write-Host "Report generated: $reportFile" -ForegroundColor Cyan
Write-Host ('OnShownMs: {0}' -f ($(if ($null -ne $onShownMs) { $onShownMs } else { 'n/a' }))) -ForegroundColor Yellow
Write-Host ('DeferredRibbonMs: {0}' -f ($(if ($null -ne $deferredRibbonMs) { $deferredRibbonMs } else { 'n/a' }))) -ForegroundColor Yellow
Write-Host ('MaxUiProbeLatencyMs: {0}' -f $maxUiProbeLatencyMs) -ForegroundColor Yellow
Write-Host ('SlowUiOperationCount: {0}' -f $slowUiOperations.Count) -ForegroundColor Yellow
