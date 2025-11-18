#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Setup script for Syncfusion MCP Server
.DESCRIPTION
    Installs dependencies, builds the server, and configures VS Code integration
.PARAMETER BuildOnly
    Only build the server, skip configuration
.PARAMETER TestConnection
    Test the MCP server connection after setup
#>
param(
    [switch]$BuildOnly,
    [switch]$TestConnection
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ProjectPath = Join-Path $RepoRoot "src\SyncfusionMcpServer\SyncfusionMcpServer.csproj"
$McpConfigPath = Join-Path $env:APPDATA "Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json"

Write-Host "🔧 Syncfusion MCP Server Setup" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "❌ .NET SDK not found. Please install .NET 9.0 SDK" -ForegroundColor Red
    exit 1
}

$dotnetVersion = dotnet --version
Write-Host "✓ .NET SDK version: $dotnetVersion" -ForegroundColor Green

# Check Syncfusion license
if (-not $env:SYNCFUSION_LICENSE_KEY) {
    Write-Host "⚠ SYNCFUSION_LICENSE_KEY not set in environment" -ForegroundColor Yellow
    Write-Host "  License validation will be limited" -ForegroundColor Gray
}

# Build the server
Write-Host ""
Write-Host "Building Syncfusion MCP Server..." -ForegroundColor Yellow
Push-Location $RepoRoot
try {
    dotnet build $ProjectPath --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
    Write-Host "✓ Server built successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Build failed: $_" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}

if ($BuildOnly) {
    Write-Host ""
    Write-Host "✓ Build complete (skipping configuration)" -ForegroundColor Green
    exit 0
}

# Configure MCP settings
Write-Host ""
Write-Host "Configuring VS Code MCP settings..." -ForegroundColor Yellow

$mcpConfig = @{
    mcpServers = @{
        syncfusion = @{
            command = "dotnet"
            args = @(
                "run"
                "--project"
                $ProjectPath
            )
            env = @{
                SYNCFUSION_LICENSE_KEY = "`${env:SYNCFUSION_LICENSE_KEY}"
                WW_REPO_ROOT = $RepoRoot
            }
        }
    }
}

# Ensure directory exists
$configDir = Split-Path -Parent $McpConfigPath
if (-not (Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
}

# Merge with existing config
if (Test-Path $McpConfigPath) {
    $existing = Get-Content $McpConfigPath -Raw | ConvertFrom-Json
    if ($existing.mcpServers) {
        $existing.mcpServers.syncfusion = $mcpConfig.mcpServers.syncfusion
        $mcpConfig = $existing
    }
}

$mcpConfig | ConvertTo-Json -Depth 10 | Set-Content $McpConfigPath -Encoding UTF8
Write-Host "✓ MCP settings configured at:" -ForegroundColor Green
Write-Host "  $McpConfigPath" -ForegroundColor Gray

# Test connection
if ($TestConnection) {
    Write-Host ""
    Write-Host "Testing MCP server connection..." -ForegroundColor Yellow
    
    $testInput = @{
        jsonrpc = "2.0"
        id = 1
        method = "initialize"
        params = @{
            protocolVersion = "2024-11-05"
        }
    } | ConvertTo-Json -Compress
    
    try {
        $process = Start-Process -FilePath "dotnet" `
            -ArgumentList "run", "--project", $ProjectPath `
            -NoNewWindow -PassThru `
            -RedirectStandardInput -RedirectStandardOutput -RedirectStandardError
        
        Start-Sleep -Seconds 2
        
        if ($process.HasExited) {
            Write-Host "❌ Server failed to start" -ForegroundColor Red
            exit 1
        }
        
        $process.Kill()
        Write-Host "✓ Server started successfully" -ForegroundColor Green
    } catch {
        Write-Host "⚠ Connection test skipped: $_" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "✅ Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Restart VS Code to load the new MCP server" -ForegroundColor Gray
Write-Host "  2. In Copilot chat, you can now use:" -ForegroundColor Gray
Write-Host "     - syncfusion_validate_theme" -ForegroundColor Gray
Write-Host "     - syncfusion_analyze_datagrid" -ForegroundColor Gray
Write-Host "     - syncfusion_check_license" -ForegroundColor Gray
Write-Host "     - syncfusion_parse_xaml" -ForegroundColor Gray
Write-Host "     - syncfusion_generate_report" -ForegroundColor Gray
Write-Host ""
