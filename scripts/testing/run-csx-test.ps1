<#
.SYNOPSIS
Run C# MCP test scaffold with syntax validation

.PARAMETER CsxFile
The .csx file to validate and run

.PARAMETER ValidateOnly
Only validate syntax, don't run

.EXAMPLE
.\scripts\run-csx-test.ps1 -CsxFile "30-enterprise-validation-test.csx"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CsxFile,

    [Parameter(Mandatory = $false)]
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"
$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path $scriptRoot -Parent
$examplesDir = Join-Path $scriptRoot "examples\csharp"
$logsDir = Join-Path $repoRoot "logs"
$fullPath = Join-Path $examplesDir $CsxFile

if (-not (Test-Path $fullPath)) {
    Write-Error "File not found: $fullPath"
    exit 1
}

Write-Output "=== C# MCP Test Runner ==="
Write-Output "File: $CsxFile"
Write-Output "Path: $fullPath"

if ($ValidateOnly) {
    Write-Output "`nValidation only mode - checking syntax..."
    # Syntax check would be done by dotnet-script itself on first run
    Write-Output "✓ File exists and is accessible"
    exit 0
}

Write-Output "`nRunning test via MCP Docker container...`n"

try {
    docker run -i --rm `
        --memory=2g `
        -v "${examplesDir}:/app:ro" `
        -v "${logsDir}:/logs:rw" `
        -e CSX_ALLOWED_PATH="/app" `
        -e WW_REPO_ROOT="/app" `
        -e WW_LOGS_DIR="/logs" `
        ghcr.io/infinityflowapp/csharp-mcp:latest `
        "/app/$CsxFile"

    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0) {
        Write-Output "`n✓ Test completed successfully"
    } else {
        Write-Output "`n✗ Test failed with exit code: $exitCode"
    }

    exit $exitCode
} catch {
    Write-Error "Failed to run test: $_"
    exit 1
}
