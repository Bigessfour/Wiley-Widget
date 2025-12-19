#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Safely rebuild the WileyWidget MCP Server by stopping it first.

.DESCRIPTION
    Stops the running MCP server, rebuilds the project, and reports success.
    VS Code will automatically restart the MCP server when a tool is next invoked.

.EXAMPLE
    .\rebuild-mcp-server.ps1
    Stops MCP server, rebuilds, reports status
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host " WileyWidget MCP Server Rebuild" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop MCP Server
Write-Host "[1/3] Stopping MCP Server..." -ForegroundColor Yellow
& "$scriptDir\stop-mcp-server.ps1"

# Wait a moment for processes to fully stop
Start-Sleep -Seconds 2

# Step 2: Build
Write-Host ""
Write-Host "[2/3] Building MCP Server..." -ForegroundColor Yellow
Push-Location $scriptDir
try {
    $output = dotnet build --no-restore 2>&1
    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0) {
        Write-Host "✓ Build succeeded" -ForegroundColor Green

        # Show last few lines for context
        $output | Select-Object -Last 3 | ForEach-Object {
            Write-Host "  $_" -ForegroundColor Gray
        }
    } else {
        Write-Host "✗ Build failed (exit code: $exitCode)" -ForegroundColor Red
        Write-Host ""
        Write-Host "Build output:" -ForegroundColor Yellow
        $output | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
        throw "Build failed"
    }
} finally {
    Pop-Location
}

# Step 3: Report
Write-Host ""
Write-Host "[3/3] Status" -ForegroundColor Yellow
$dllPath = Join-Path $scriptDir "bin\Debug\net9.0-windows10.0.26100.0\WileyWidgetMcpServer.dll"
if (Test-Path $dllPath) {
    $dll = Get-Item $dllPath
    Write-Host "  DLL updated: $($dll.LastWriteTime)" -ForegroundColor Gray
    Write-Host "  Size: $([math]::Round($dll.Length / 1KB, 2)) KB" -ForegroundColor Gray
}

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✓ MCP Server rebuild complete!" -ForegroundColor Green
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "VS Code will automatically restart the MCP server" -ForegroundColor Gray
Write-Host "when you next invoke an MCP tool." -ForegroundColor Gray
Write-Host ""
