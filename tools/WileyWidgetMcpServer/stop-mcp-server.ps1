#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Stops the WileyWidget MCP Server to allow clean rebuilds.

.DESCRIPTION
    Finds and stops all running WileyWidgetMcpServer.exe and related dotnet processes
    that have WileyWidget DLLs loaded. This allows dotnet build to succeed without
    file lock errors.

.EXAMPLE
    .\stop-mcp-server.ps1
    Stops all MCP server processes
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Write-Host "Stopping WileyWidget MCP Server processes..." -ForegroundColor Cyan

# Find WileyWidgetMcpServer processes
$mcpProcesses = Get-Process -Name "WileyWidgetMcpServer" -ErrorAction SilentlyContinue

if ($mcpProcesses) {
    Write-Host "Found $($mcpProcesses.Count) WileyWidgetMcpServer process(es)" -ForegroundColor Yellow
    $mcpProcesses | ForEach-Object {
        Write-Host "  Stopping PID $($_.Id)..." -ForegroundColor Gray
        Stop-Process -Id $_.Id -Force
    }
    Write-Host "✓ Stopped WileyWidgetMcpServer processes" -ForegroundColor Green
} else {
    Write-Host "No WileyWidgetMcpServer processes found" -ForegroundColor Gray
}

# Find dotnet processes that might have WileyWidget DLLs loaded
$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
    try {
        $_.Modules.FileName -like "*WileyWidget*"
    } catch {
        $false
    }
}

if ($dotnetProcesses) {
    Write-Host "Found $($dotnetProcesses.Count) dotnet process(es) with WileyWidget DLLs loaded" -ForegroundColor Yellow
    $dotnetProcesses | ForEach-Object {
        Write-Host "  Stopping PID $($_.Id)..." -ForegroundColor Gray
        Stop-Process -Id $_.Id -Force
    }
    Write-Host "✓ Stopped dotnet processes with WileyWidget DLLs" -ForegroundColor Green
} else {
    Write-Host "No dotnet processes with WileyWidget DLLs found" -ForegroundColor Gray
}

Write-Host ""
Write-Host "✓ MCP Server stopped. You can now rebuild." -ForegroundColor Green
Write-Host "  Note: VS Code will automatically restart the MCP server when needed." -ForegroundColor Gray
