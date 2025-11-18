<#
.SYNOPSIS
Design Phase Workflow - MCP-Integrated Test Scaffolding & Feedback Loop

.DESCRIPTION
Orchestrates the design phase using C# MCP:
1. Run .csx test scaffolds via MCP for immediate feedback
2. Capture stack traces, coverage, and test results
3. Generate context for Copilot prompts
4. Iterate on test designs based on MCP output

.PARAMETER ScriptName
The .csx script to run (from scripts/examples/csharp/)

.PARAMETER GenerateContext
Generate Copilot-ready context from MCP output

.PARAMETER CoverageMode
Run with coverage analysis

.EXAMPLE
.\scripts\design-phase-workflow.ps1 -ScriptName "02-viewmodel-test.csx"

.EXAMPLE
.\scripts\design-phase-workflow.ps1 -ScriptName "04-repository-tests.csx" -GenerateContext -CoverageMode
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ScriptName,

    [Parameter(Mandatory = $false)]
    [switch]$GenerateContext,

    [Parameter(Mandatory = $false)]
    [switch]$CoverageMode,

    [Parameter(Mandatory = $false)]
    [ValidateSet("Repository", "ViewModel", "Prism", "All")]
    [string]$TestCategory = "All",

    [Parameter(Mandatory = $false)]
    [switch]$Interactive
)

$ErrorActionPreference = "Stop"
$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path $scriptRoot -Parent
$logsDir = Join-Path $repoRoot "logs"
$examplesDir = Join-Path $scriptRoot "examples\csharp"
$resultsDir = Join-Path $repoRoot "TestResults\MCP"

# Ensure directories exist
@($logsDir, $resultsDir) | ForEach-Object {
    if (-not (Test-Path $_)) {
        New-Item -ItemType Directory -Path $_ -Force | Out-Null
    }
}

Write-Host "=== MCP Design Phase Workflow ===" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot"
Write-Host "Logs: $logsDir"
Write-Host "Results: $resultsDir`n"

# Test categories mapped to .csx files
$testCategories = @{
    Repository = @(
        "04-repository-tests.csx",
        "05-repository-tests-simplified.csx"
    )
    ViewModel = @(
        "02-viewmodel-test.csx",
        "03-async-test.csx"
    )
    Prism = @(
        "20-prism-container-e2e-test.csx",
        "21-prism-modules-e2e-test.csx",
        "22-prism-di-registration-e2e-test.csx",
        "23-prism-module-lifecycle-e2e-test.csx",
        "24-prism-container-resolution-e2e-test.csx",
        "25-prism-region-adapters-e2e-test.csx"
    )
}

function Invoke-McpScript {
    param(
        [string]$CsxFile,
        [bool]$Capture = $true
    )

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $outputLog = Join-Path $resultsDir "$([IO.Path]::GetFileNameWithoutExtension($CsxFile))-$timestamp.log"
    $errorLog = Join-Path $resultsDir "$([IO.Path]::GetFileNameWithoutExtension($CsxFile))-$timestamp-error.log"

    Write-Host "`n▶ Running: $CsxFile" -ForegroundColor Yellow

    try {
        # Run via Docker MCP container
        $mcpResult = docker run -i --rm `
            --memory=2g `
            -v "${examplesDir}:/app:ro" `
            -v "${logsDir}:/logs:rw" `
            -v "${repoRoot}:/workspace:ro" `
            -e CSX_ALLOWED_PATH="/app" `
            -e WW_REPO_ROOT="/workspace" `
            -e WW_LOGS_DIR="/logs" `
            -e WW_WAIT_FOR_LOGS_SECONDS="10" `
            ghcr.io/infinityflowapp/csharp-mcp:latest `
            "/app/$CsxFile" 2>&1

        if ($Capture) {
            $mcpResult | Out-File -FilePath $outputLog -Encoding UTF8
            Write-Host "  ✓ Output saved to: $outputLog" -ForegroundColor Green
        }

        # Parse results
        $passed = $mcpResult -match "✓|PASSED|SUCCESS"
        $failed = $mcpResult -match "✗|FAILED|ERROR"

        $result = @{
            ScriptName = $CsxFile
            Passed = $passed -and -not $failed
            Output = $mcpResult -join "`n"
            OutputLog = $outputLog
            Timestamp = $timestamp
            StackTraces = @()
            Assertions = @()
        }

        # Extract stack traces
        $result.StackTraces = $mcpResult | Where-Object { $_ -match "at .+\..+\(" }

        # Extract assertions
        $result.Assertions = $mcpResult | Where-Object { $_ -match "Assert|✓|✗" }

        return $result

    } catch {
        Write-Host "  ✗ Error running script: $_" -ForegroundColor Red
        $_.ToString() | Out-File -FilePath $errorLog -Encoding UTF8

        return @{
            ScriptName = $CsxFile
            Passed = $false
            Output = $_.ToString()
            ErrorLog = $errorLog
            Timestamp = $timestamp
        }
    }
}

function Get-CopilotContext {
    param(
        [object[]]$Results
    )

    Write-Host "`n📝 Generating Copilot Context..." -ForegroundColor Cyan

    $contextFile = Join-Path $resultsDir "copilot-context-$(Get-Date -Format 'yyyyMMdd-HHmmss').md"

    $context = @"
# MCP Test Execution Context
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Summary
- Total Scripts: $($Results.Count)
- Passed: $($Results.Where({$_.Passed}).Count)
- Failed: $($Results.Where({-not $_.Passed}).Count)

## Test Results

"@

    foreach ($result in $Results) {
        $status = if ($result.Passed) { "✅ PASSED" } else { "❌ FAILED" }
        $context += @"

### $status - $($result.ScriptName)

**Timestamp:** $($result.Timestamp)

#### Output Summary
``````
$($result.Output -split "`n" | Select-Object -First 50 | Out-String)
``````

"@

        if ($result.StackTraces) {
            $context += @"

#### Stack Traces
``````
$($result.StackTraces | Out-String)
``````

"@
        }

        if ($result.Assertions) {
            $context += @"

#### Assertions
``````
$($result.Assertions | Out-String)
``````

"@
        }

        if ($result.ErrorLog) {
            $context += @"

#### Error Log
See: ``$($result.ErrorLog)``

"@
        }

        $context += @"

#### Full Output Log
``$($result.OutputLog)``

---

"@
    }

    $context += @"

## Copilot Prompt Suggestions

### For Failed Tests
Use this context to refine tests:
1. Review stack traces above
2. Identify missing mocks or setup
3. Check assertion expectations
4. Verify container configuration

### Example Copilot Prompt
``````
@workspace Based on the MCP test results in $contextFile, please:
1. Analyze the failed assertions in [script-name]
2. Review the stack traces for root cause
3. Suggest fixes for the test setup or implementation
4. Update the .csx script with proper mocks and assertions
``````

## Next Steps
1. Review failed tests in detail
2. Use Copilot to analyze stack traces
3. Iterate on test scaffolds
4. Re-run workflow to validate fixes

---
*Generated by design-phase-workflow.ps1*
"@

    $context | Out-File -FilePath $contextFile -Encoding UTF8
    Write-Host "  ✓ Context saved to: $contextFile" -ForegroundColor Green

    return $contextFile
}

function Show-InteractiveMenu {
    Write-Host "`n=== Interactive Test Selection ===" -ForegroundColor Cyan
    Write-Host "1. Repository Tests"
    Write-Host "2. ViewModel Tests"
    Write-Host "3. Prism E2E Tests"
    Write-Host "4. Run Single Script"
    Write-Host "5. Run All Tests"
    Write-Host "Q. Quit`n"

    $choice = Read-Host "Select option"

    switch ($choice) {
        "1" { return "Repository" }
        "2" { return "ViewModel" }
        "3" { return "Prism" }
        "4" {
            $script = Read-Host "Enter .csx filename"
            return $script
        }
        "5" { return "All" }
        "Q" { exit 0 }
        default {
            Write-Host "Invalid choice" -ForegroundColor Red
            return Show-InteractiveMenu
        }
    }
}

# Main workflow
try {
    if ($Interactive) {
        $selection = Show-InteractiveMenu
        if ($selection -in @("Repository", "ViewModel", "Prism", "All")) {
            $TestCategory = $selection
        } else {
            $ScriptName = $selection
        }
    }

    $results = @()

    if ($ScriptName) {
        # Run single script
        $results += Invoke-McpScript -CsxFile $ScriptName
    } else {
        # Run category or all
        $scriptsToRun = @()

        if ($TestCategory -eq "All") {
            $scriptsToRun = $testCategories.Values | ForEach-Object { $_ }
        } else {
            $scriptsToRun = $testCategories[$TestCategory]
        }

        foreach ($script in $scriptsToRun) {
            $results += Invoke-McpScript -CsxFile $script
        }
    }

    # Display summary
    Write-Host "`n=== Execution Summary ===" -ForegroundColor Cyan
    Write-Host "Total: $($results.Count)" -ForegroundColor White
    Write-Host "Passed: $($results.Where({$_.Passed}).Count)" -ForegroundColor Green
    Write-Host "Failed: $($results.Where({-not $_.Passed}).Count)" -ForegroundColor Red

    # Generate Copilot context if requested
    if ($GenerateContext -or $results.Where({-not $_.Passed}).Count -gt 0) {
        $contextFile = Get-CopilotContext -Results $results

        Write-Host "`n💡 Use this Copilot prompt:" -ForegroundColor Yellow
        Write-Host "@workspace Analyze the MCP test results in ``$contextFile``. Focus on failed tests and suggest fixes." -ForegroundColor White
    }

    # Coverage mode (placeholder for future integration)
    if ($CoverageMode) {
        Write-Host "`n📊 Coverage mode enabled (placeholder for future coverage analysis)" -ForegroundColor Magenta
    }

    # Exit code based on results
    $exitCode = if ($results.Where({-not $_.Passed}).Count -eq 0) { 0 } else { 1 }
    exit $exitCode

} catch {
    Write-Host "`n❌ Workflow Error: $_" -ForegroundColor Red
    exit 1
}
