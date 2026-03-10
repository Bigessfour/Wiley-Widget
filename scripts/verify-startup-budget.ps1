#!/usr/bin/env pwsh
<#!
.SYNOPSIS
    Verifies startup performance budgets from the latest Wiley Widget startup log.

.DESCRIPTION
    Fails with exit code 1 when startup regressions are detected, including:
      - OnShown duration above threshold
      - Elevated UI probe latency above threshold
      - UI probe timeout entries
      - MDI rapid-request skip storm signatures
      - Right-dock factory fallback path usage (optional)
      - Error/Fatal log levels

.EXAMPLE
    pwsh -File scripts/verify-startup-budget.ps1

.EXAMPLE
    pwsh -File scripts/verify-startup-budget.ps1 -MaxOnShownMs 300 -FailOnRightDockFallback $false
#>

param(
    [string]$LogPath = "",
    [int]$MaxOnShownMs = 250,
    [int]$MaxDeferredRibbonInitMs = 2500,
    [int]$MaxUiProbeLatencyMs = 750,
    [int]$MaxUiProbeTimeouts = 0,
    [int]$MaxMdiRapidSkips = 0,
    [bool]$FailOnRightDockFallback = $false,
    [bool]$RequireOnShownMetric = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-LatestStartupLog {
    param([string]$baseDir)

    $logDir = Join-Path $baseDir 'logs'
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

function Get-MaxLatency {
    param([System.Collections.Generic.List[string]]$lines)

    $latencies = [System.Collections.Generic.List[int]]::new()

    foreach ($line in $lines) {
        $elevated = [regex]::Match($line, 'Elevated callback latency\s+(\d+)ms')
        if ($elevated.Success) {
            [void]$latencies.Add([int]$elevated.Groups[1].Value)
        }

        $warmup = [regex]::Match($line, 'Warm-up high UI callback latency\s+(\d+)ms')
        if ($warmup.Success) {
            [void]$latencies.Add([int]$warmup.Groups[1].Value)
        }
    }

    if ($latencies.Count -eq 0) {
        return 0
    }

    return ($latencies | Measure-Object -Maximum).Maximum
}

$workspaceRoot = (Get-Location).Path
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Get-LatestStartupLog -baseDir $workspaceRoot
}

if (-not (Test-Path $LogPath)) {
    throw "Log file not found: $LogPath"
}

$lines = [System.Collections.Generic.List[string]](Get-Content -Path $LogPath)
if ($lines.Count -eq 0) {
    throw "Log file is empty: $LogPath"
}

$onShownMatches = Select-String -InputObject $lines -Pattern 'OnShown completed in\s+(\d+)ms' -AllMatches
$onShownMs = $null
if ($onShownMatches) {
    $onShownMs = [int]$onShownMatches[-1].Matches[0].Groups[1].Value
}

$deferredRibbonMatches = Select-String -InputObject $lines -Pattern 'Deferred ribbon init in\s+(\d+)ms' -AllMatches
$deferredRibbonMs = 0
if ($deferredRibbonMatches) {
    $deferredRibbonMs = [int]$deferredRibbonMatches[-1].Matches[0].Groups[1].Value
}

$observedMaxUiProbeLatencyMs = Get-MaxLatency -lines $lines
$uiProbeTimeoutCount = @(Select-String -InputObject $lines -Pattern '\[UI-PROBE\].*(did not service probe within|probe timeout after)' -AllMatches).Count
$mdiRapidSkipCount = @(Select-String -InputObject $lines -Pattern '\[MDI-CONSTRAIN\] Skipped\s+[0-9]+\s+rapid requests' -AllMatches).Count
$rightDockFallbackCount = @(Select-String -InputObject $lines -Pattern 'temporary right dock panel added to .*factory (initialization was unavailable|unavailable or failed)' -AllMatches).Count
$errorFatalCount = @(Select-String -InputObject $lines -Pattern '\[(ERR|FTL)\]' -AllMatches).Count

$violations = [System.Collections.Generic.List[string]]::new()

if ($RequireOnShownMetric -and $null -eq $onShownMs) {
    [void]$violations.Add('Missing OnShown completion metric in startup log.')
}

if ($null -ne $onShownMs -and $onShownMs -gt $MaxOnShownMs) {
    [void]$violations.Add("OnShown duration $onShownMs ms exceeds budget $MaxOnShownMs ms.")
}

if ($deferredRibbonMs -gt $MaxDeferredRibbonInitMs) {
    [void]$violations.Add("Deferred ribbon init $deferredRibbonMs ms exceeds budget $MaxDeferredRibbonInitMs ms.")
}

if ($observedMaxUiProbeLatencyMs -gt $MaxUiProbeLatencyMs) {
    [void]$violations.Add("Max UI probe callback latency $observedMaxUiProbeLatencyMs ms exceeds budget $MaxUiProbeLatencyMs ms.")
}

if ($uiProbeTimeoutCount -gt $MaxUiProbeTimeouts) {
    [void]$violations.Add("UI probe timeout count $uiProbeTimeoutCount exceeds budget $MaxUiProbeTimeouts.")
}

if ($mdiRapidSkipCount -gt $MaxMdiRapidSkips) {
    [void]$violations.Add("MDI rapid-request skip count $mdiRapidSkipCount exceeds budget $MaxMdiRapidSkips.")
}

if ($FailOnRightDockFallback -and $rightDockFallbackCount -gt 0) {
    [void]$violations.Add("Right dock fallback path triggered $rightDockFallbackCount time(s).")
}

if ($errorFatalCount -gt 0) {
    [void]$violations.Add("Found $errorFatalCount error/fatal startup log entries.")
}

Write-Host '═══════════════════════════════════════════════════════════════' -ForegroundColor Cyan
Write-Host '  Startup Budget Verification' -ForegroundColor Cyan
Write-Host '═══════════════════════════════════════════════════════════════' -ForegroundColor Cyan
Write-Host "Log: $LogPath"
Write-Host "OnShownMs: $($onShownMs ?? 'n/a') (budget <= $MaxOnShownMs)"
Write-Host "DeferredRibbonMs: $deferredRibbonMs (budget <= $MaxDeferredRibbonInitMs)"
Write-Host "MaxUiProbeLatencyMs: $observedMaxUiProbeLatencyMs (budget <= $MaxUiProbeLatencyMs)"
Write-Host "UiProbeTimeoutCount: $uiProbeTimeoutCount (budget <= $MaxUiProbeTimeouts)"
Write-Host "MdiRapidSkipCount: $mdiRapidSkipCount (budget <= $MaxMdiRapidSkips)"
Write-Host "RightDockFallbackCount: $rightDockFallbackCount (failOnFallback=$FailOnRightDockFallback)"
Write-Host "ErrorFatalCount: $errorFatalCount"

if ($violations.Count -gt 0) {
    Write-Host ''
    Write-Host '❌ Startup budget gate FAILED:' -ForegroundColor Red
    foreach ($violation in $violations) {
        Write-Host "  - $violation" -ForegroundColor Red
    }
    exit 1
}

Write-Host ''
Write-Host '✅ Startup budget gate PASSED.' -ForegroundColor Green
exit 0
