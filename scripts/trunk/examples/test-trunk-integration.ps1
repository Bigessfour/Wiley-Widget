#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test Trunk merge queue CLI integration.

.DESCRIPTION
    Tests the TrunkMergeQueue PowerShell module and Python wrapper
    with various operations.

.PARAMETER SkipLogin
    Skip the trunk login check (assumes already authenticated).

.EXAMPLE
    .\test-trunk-integration.ps1
    # Run full test suite including login check

.EXAMPLE
    .\test-trunk-integration.ps1 -SkipLogin
    # Run tests assuming already logged in
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [switch]$SkipLogin
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))

Write-Host "=== Trunk Merge Queue Integration Tests ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check if trunk CLI is installed
Write-Host "[Test 1] Checking trunk CLI installation..." -ForegroundColor Yellow

try {
    $version = trunk --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Trunk CLI installed: $version" -ForegroundColor Green
    }
    else {
        Write-Host "  ✗ Trunk CLI not found or error" -ForegroundColor Red
        Write-Host "  Run: pwsh scripts/trunk/setup-trunk.ps1 -InstallVSCodeExtension (Windows) or bash scripts/trunk/setup-trunk.sh --install-vscode (macOS/Linux)" -ForegroundColor Yellow
        exit 1
    }
}
catch {
    Write-Host "  ✗ Error checking trunk CLI: $_" -ForegroundColor Red
    exit 1
}

# Test 2: Check authentication (unless skipped)
if (-not $SkipLogin) {
    Write-Host "`n[Test 2] Checking trunk authentication..." -ForegroundColor Yellow
    Write-Host "  Note: You may need to run 'trunk login' first" -ForegroundColor Cyan
    Write-Host "  Press Ctrl+C to skip or wait for timeout..." -ForegroundColor Gray

    try {
        $loginCheck = trunk merge status 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ Authenticated and can access merge queue" -ForegroundColor Green
        }
        else {
            Write-Host "  ⚠ May need authentication: $loginCheck" -ForegroundColor Yellow
            Write-Host "  Run: trunk login" -ForegroundColor Cyan
        }
    }
    catch {
        Write-Host "  ⚠ Could not verify authentication" -ForegroundColor Yellow
    }
}
else {
    Write-Host "`n[Test 2] Skipping authentication check" -ForegroundColor Gray
}

# Test 3: Import and test PowerShell module
Write-Host "`n[Test 3] Testing PowerShell module..." -ForegroundColor Yellow

$modulePath = Join-Path $PSScriptRoot '..' 'TrunkMergeQueue.psm1'
if (-not (Test-Path $modulePath)) {
    Write-Host "  ✗ Module not found: $modulePath" -ForegroundColor Red
    exit 1
}

Import-Module $modulePath -Force
Write-Host "  ✓ Module imported successfully" -ForegroundColor Green

# Test module functions exist
$expectedFunctions = @(
    'Test-TrunkCli',
    'Get-TrunkMergeQueueStatus',
    'Submit-TrunkMergeQueuePr',
    'Remove-TrunkMergeQueuePr',
    'Suspend-TrunkMergeQueue',
    'Resume-TrunkMergeQueue'
)

foreach ($func in $expectedFunctions) {
    if (Get-Command $func -ErrorAction SilentlyContinue) {
        Write-Host "  ✓ Function exported: $func" -ForegroundColor Green
    }
    else {
        Write-Host "  ✗ Function missing: $func" -ForegroundColor Red
        exit 1
    }
}

# Test 4: Test PowerShell module CLI check
Write-Host "`n[Test 4] Testing Test-TrunkCli..." -ForegroundColor Yellow

$cliCheck = Test-TrunkCli
if ($cliCheck.Installed) {
    Write-Host "  ✓ CLI check returned installed=true, version=$($cliCheck.Version)" -ForegroundColor Green
}
else {
    Write-Host "  ✗ CLI check failed: $($cliCheck.Error)" -ForegroundColor Red
}

# Test 5: Test Python wrapper
Write-Host "`n[Test 5] Testing Python wrapper..." -ForegroundColor Yellow

$pythonWrapper = Join-Path $workspaceRoot 'tools' 'trunk_merge_queue.py'
if (-not (Test-Path $pythonWrapper)) {
    # Try alternate location
    $pythonWrapper = Join-Path $workspaceRoot 'scripts' 'tools' 'trunk_merge_queue.py'
}
if (-not (Test-Path $pythonWrapper)) {
    Write-Host "  ✗ Python wrapper not found: $pythonWrapper" -ForegroundColor Red
    exit 1
}

try {
    $pythonCheck = python $pythonWrapper check --json 2>&1 | ConvertFrom-Json
    if ($pythonCheck.installed -eq $true) {
        Write-Host "  ✓ Python wrapper detected CLI: $($pythonCheck.version)" -ForegroundColor Green
    }
    else {
        Write-Host "  ⚠ Python wrapper did not detect CLI" -ForegroundColor Yellow
        Write-Host "    This may be due to PATH issues in subprocess" -ForegroundColor Gray
    }
}
catch {
    Write-Host "  ✗ Error testing Python wrapper: $_" -ForegroundColor Red
}

# Test 6: Test xai_tool_executor integration
Write-Host "`n[Test 6] Checking xai_tool_executor integration..." -ForegroundColor Yellow

$executorPath = Join-Path $workspaceRoot 'scripts' 'tools' 'xai_tool_executor.py'
if (Test-Path $executorPath) {
    $hasHandlers = Select-String -Path $executorPath -Pattern "trunk_merge_(status|submit|cancel|pause|resume)" -Quiet
    if ($hasHandlers) {
        Write-Host "  ✓ Trunk handlers found in xai_tool_executor" -ForegroundColor Green
    }
    else {
        Write-Host "  ✗ Trunk handlers not found in xai_tool_executor" -ForegroundColor Red
    }
}
else {
    Write-Host "  ⚠ xai_tool_executor not found" -ForegroundColor Yellow
}

# Test 7: Quick status check (real test)
Write-Host "`n[Test 7] Testing live status query..." -ForegroundColor Yellow
Write-Host "  Running: Get-TrunkMergeQueueStatus" -ForegroundColor Gray

try {
    $statusResult = Get-TrunkMergeQueueStatus -ErrorAction Stop
    if ($statusResult.Success) {
        Write-Host "  ✓ Status query succeeded" -ForegroundColor Green
        Write-Host "`n--- Queue Status ---" -ForegroundColor Cyan
        Write-Host $statusResult.Output
        Write-Host "-------------------" -ForegroundColor Cyan
    }
    else {
        Write-Host "  ⚠ Status query returned non-zero exit code: $($statusResult.ExitCode)" -ForegroundColor Yellow
        Write-Host "  Output: $($statusResult.Output)" -ForegroundColor Gray
    }
}
catch {
    Write-Host "  ✗ Error running status query: $_" -ForegroundColor Red
}

Write-Host "`n=== Test Summary ===" -ForegroundColor Cyan
Write-Host "All integration tests completed." -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Run 'trunk login' if not authenticated" -ForegroundColor White
Write-Host "  2. Test with a specific PR: Get-TrunkMergeQueueStatus -PrNumber 123" -ForegroundColor White
Write-Host "  3. Submit a PR: Submit-TrunkMergeQueuePr -PrNumber 123" -ForegroundColor White
Write-Host ""
