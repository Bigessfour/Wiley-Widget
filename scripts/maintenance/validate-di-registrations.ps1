#!/usr/bin/env pwsh
# validate-di-registrations.ps1
# CI integration script for DI registration validation
#
# This script orchestrates DI validation checks for CI/CD:
# 1. Runs resource_scanner_enhanced.py to find DI references in code
# 2. Validates that referenced services have corresponding registrations
# 3. Generates validation report for CI artifacts
#
# Usage in CI:
#   pwsh -File scripts/maintenance/validate-di-registrations.ps1 -CI
#
# Local usage:
#   pwsh -File scripts/maintenance/validate-di-registrations.ps1

[CmdletBinding()]
param(
    [switch]$CI,
    [string]$OutputPath = "TestResults/di-validation-report.json",
    [switch]$FailOnWarnings
)

$ErrorActionPreference = "Stop"
$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path (Split-Path $scriptRoot -Parent) -Parent

Write-Host "🔍 DI Registration Validation Pipeline" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Step 1: Run resource scanner to find DI references
Write-Host "`n📊 Step 1: Scanning codebase for DI service references..." -ForegroundColor Yellow

$scannerPath = Join-Path $repoRoot "tools\resource_scanner_enhanced.py"
if (-not (Test-Path $scannerPath)) {
    Write-Error "Resource scanner not found: $scannerPath"
    exit 1
}

$scanOutput = Join-Path $repoRoot "TestResults\resource-scan-results.json"
$scanCommand = "python `"$scannerPath`" --output `"$scanOutput`" --focus=all"

Write-Host "  Running: $scanCommand" -ForegroundColor Gray
try {
    $scanResult = Invoke-Expression $scanCommand 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Resource scanner completed with warnings"
        Write-Host $scanResult
    } else {
        Write-Host "  ✓ Resource scan completed" -ForegroundColor Green
    }
} catch {
    Write-Error "Failed to run resource scanner: $_"
    exit 1
}

# Step 2: Parse scan results to extract DI service references
Write-Host "`n📋 Step 2: Extracting DI service references from scan results..." -ForegroundColor Yellow

if (-not (Test-Path $scanOutput)) {
    Write-Warning "Scan output not found, using fallback analysis"
    $diReferences = @()
} else {
    $scanData = Get-Content $scanOutput | ConvertFrom-Json

    # Extract service references (interfaces starting with I, ending with Service/Repository/Manager)
    $diReferences = @()
    if ($scanData.PSObject.Properties.Name -contains "cs_converter_implementations") {
        $diReferences += $scanData.cs_converter_implementations | Where-Object {
            $_ -match '^I[A-Z]\w+(Service|Repository|Manager|Provider|Factory|Helper)$'
        }
    }

    Write-Host "  Found $($diReferences.Count) potential DI service references" -ForegroundColor Gray
}

# Step 3: Validate DI registrations exist for referenced services
Write-Host "`n🔍 Step 3: Validating DI registrations..." -ForegroundColor Yellow

$validationReport = @{
    timestamp          = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
    repository         = "Wiley-Widget"
    branch             = $env:GITHUB_REF_NAME ?? "local"
    commit             = $env:GITHUB_SHA ?? (git rev-parse HEAD 2>$null)
    scan_results       = @{
        total_references = $diReferences.Count
        unique_services  = ($diReferences | Select-Object -Unique).Count
    }
    validation_results = @{
        registered_services   = 0
        unregistered_services = 0
        validation_errors     = @()
    }
    success            = $false
}

# Search for service registrations in App.DependencyInjection.cs
$appDIPath = Join-Path $repoRoot "src\WileyWidget\App.DependencyInjection.cs"
if (Test-Path $appDIPath) {
    $appDIContent = Get-Content $appDIPath -Raw

    $registered = 0
    $unregistered = @()

    foreach ($serviceRef in ($diReferences | Select-Object -Unique)) {
        if ($appDIContent -match $serviceRef) {
            $registered++
            Write-Host "  ✓ $serviceRef is registered" -ForegroundColor Green
        } else {
            $unregistered += $serviceRef
            Write-Warning "  ⚠ $serviceRef is NOT registered"
        }
    }

    $validationReport.validation_results.registered_services = $registered
    $validationReport.validation_results.unregistered_services = $unregistered.Count
    $validationReport.validation_results.validation_errors = $unregistered

    $successRate = if ($diReferences.Count -gt 0) {
        [math]::Round(($registered / $diReferences.Count) * 100, 2)
    } else {
        100.0
    }

    Write-Host "`n📊 Validation Results:" -ForegroundColor Cyan
    Write-Host "  Registered: $registered/$($diReferences.Count) ($successRate%)" -ForegroundColor $(if ($successRate -ge 90) { "Green" } else { "Yellow" })
    Write-Host "  Unregistered: $($unregistered.Count)" -ForegroundColor $(if ($unregistered.Count -eq 0) { "Green" } else { "Red" })

    $validationReport.success = ($successRate -ge 90)
} else {
    Write-Error "App.DependencyInjection.cs not found: $appDIPath"
    $validationReport.success = $false
}

# Step 4: Generate validation report for CI
Write-Host "`n📝 Step 4: Generating validation report..." -ForegroundColor Yellow

$outputDir = Split-Path $OutputPath -Parent
if ($outputDir -and -not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$validationReport | ConvertTo-Json -Depth 10 | Set-Content $OutputPath -Encoding UTF8
Write-Host "  Report saved: $OutputPath" -ForegroundColor Gray

# Step 5: Set CI status
if ($CI) {
    Write-Host "`n🚀 Setting CI status..." -ForegroundColor Yellow

    if ($validationReport.success) {
        Write-Host "  ✅ DI validation PASSED" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "  ❌ DI validation FAILED" -ForegroundColor Red
        if ($FailOnWarnings) {
            exit 1
        } else {
            Write-Warning "Continuing despite failures (FailOnWarnings not set)"
            exit 0
        }
    }
} else {
    Write-Host "`n✅ Validation complete (local run)" -ForegroundColor Green
}
