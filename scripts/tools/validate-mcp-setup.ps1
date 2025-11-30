#Requires -Version 7.0
<#
.SYNOPSIS
    Validates the Wiley Widget MCP (Model Context Protocol) server setup.

.DESCRIPTION
    Comprehensive validation script for all MCP servers in the Wiley Widget workspace:
    - Python GitHub MCP server (port 6723)
    - Docker-based C# MCP server
    - Docker-based GitHub MCP server
    - npx-based filesystem/everything servers

    Checks for:
    - Docker availability and container health
    - Port availability and binding
    - Python venv and dependencies
    - Server endpoint responsiveness
    - Continue.dev configuration
    - Environment variables

.PARAMETER HealthCheck
    Perform comprehensive health checks on all running MCP servers.

.PARAMETER UpdateImages
    Pull latest Docker images for MCP servers before validating.

.PARAMETER StartServers
    Attempt to start any stopped MCP servers during validation.

.PARAMETER Port
    Specific port to check for Python MCP server (default: 6723).

.EXAMPLE
    .\validate-mcp-setup.ps1
    # Basic validation of all MCP components

.EXAMPLE
    .\validate-mcp-setup.ps1 -HealthCheck
    # Full health check with endpoint testing

.EXAMPLE
    .\validate-mcp-setup.ps1 -UpdateImages -StartServers
    # Update Docker images and auto-start servers

.NOTES
    Author: Wiley Widget Development Team
    Requires: Docker Desktop, PowerShell 7+, Python 3.14+
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [switch]$HealthCheck,

    [Parameter(Mandatory = $false)]
    [switch]$UpdateImages,

    [Parameter(Mandatory = $false)]
    [switch]$StartServers,

    [Parameter(Mandatory = $false)]
    [int]$Port = 6723
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ──────────────────────────────────────────────────────────────
# Configuration
# ──────────────────────────────────────────────────────────────

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$PythonExe = Join-Path $RepoRoot ".continue\venv\Scripts\python.exe"
$ServerScript = Join-Path $RepoRoot ".continue\mcpServers\wiley-widget-gh-mcp.py"
$YamlConfig = Join-Path $RepoRoot ".continue\mcpServers\wiley-widget-gh.yaml"

$script:Issues = @()
$script:Warnings = @()
$script:Passed = @()

function Add-Issue {
    param([string]$Message)
    $script:Issues += $Message
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Add-Warning {
    param([string]$Message)
    $script:Warnings += $Message
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
}

function Add-Passed {
    param([string]$Message)
    $script:Passed += $Message
    Write-Host "✅ $Message" -ForegroundColor Green
}

# ──────────────────────────────────────────────────────────────
# Header
# ──────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Wiley Widget MCP Server Validation" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ──────────────────────────────────────────────────────────────
# 1. Python Environment Validation
# ──────────────────────────────────────────────────────────────

Write-Host "🐍 Validating Python Environment..." -ForegroundColor Cyan

if (Test-Path $PythonExe) {
    Add-Passed "Python venv found: $PythonExe"

    # Check Python version
    try {
        $pythonVersion = & $PythonExe --version 2>&1
        if ($pythonVersion -match 'Python (\d+\.\d+\.\d+)') {
            $version = [version]$matches[1]
            # Use a robust version comparison so future minors/majors compare correctly
            $requiredVersion = [version]"3.14.0"
            if ([version]$matches[1] -ge $requiredVersion) {
                Add-Passed "Python version: $($matches[1]) (>= 3.14 required)"
            } else {
                Add-Warning "Python version $($matches[1]) is older than recommended 3.14+"
            }
        }
    } catch {
        Add-Warning "Could not determine Python version: $_"
    }

    # Check installed packages
    $pipList = & $PythonExe -m pip list 2>&1 | Out-String

    $requiredPackages = @('fastapi', 'uvicorn', 'pydantic')
    foreach ($pkg in $requiredPackages) {
        if ($pipList -match $pkg) {
            Add-Passed "Package installed: $pkg"
        } else {
            Add-Issue "Missing Python package: $pkg"
            if ($StartServers) {
                Write-Host "   Installing $pkg..." -ForegroundColor Yellow
                & $PythonExe -m pip install $pkg --quiet
            }
        }
    }

} else {
    Add-Issue "Python venv not found at: $PythonExe"
    Add-Warning "Create with: python -m venv .continue\venv; .continue\venv\Scripts\pip install fastapi uvicorn pydantic"
}

Write-Host ""

# ──────────────────────────────────────────────────────────────
# 2. Server Script Validation
# ──────────────────────────────────────────────────────────────

Write-Host "📄 Validating MCP Server Files..." -ForegroundColor Cyan

if (Test-Path $ServerScript) {
    Add-Passed "Server script found: wiley-widget-gh-mcp.py"

    # Check for required imports
    $scriptContent = Get-Content $ServerScript -Raw
    if ($scriptContent -match 'from fastapi import FastAPI') {
        Add-Passed "FastAPI import detected"
    } else {
        Add-Warning "FastAPI import not found in server script"
    }

    if ($scriptContent -match "uvicorn\.run") {
        Add-Passed "Uvicorn server launch detected"
    } else {
        Add-Warning "Uvicorn launch not found in server script"
    }

} else {
    Add-Issue "Server script not found at: $ServerScript"
}

if (Test-Path $YamlConfig) {
    Add-Passed "YAML config found: wiley-widget-gh.yaml"

    $yamlContent = Get-Content $YamlConfig -Raw
    # Check for Python path in comments (GitHub Copilot Chat uses mcp-settings.json)
    if ($yamlContent -match 'C:/Users/biges/Desktop/Wiley-Widget/\.continue/venv/Scripts/python\.exe') {
        Add-Passed "YAML contains Python path reference"
    } elseif ($yamlContent -match 'GitHub Copilot Chat uses \.vscode/mcp-settings\.json') {
        Add-Passed "YAML configured for Continue.dev (Copilot uses mcp-settings.json)"
    } elseif ($yamlContent -match '\.continue/venv') {
        Add-Warning "YAML uses relative Python path (may cause issues with Continue.dev)"
    } else {
        Add-Warning "YAML is minimal config (actual config in .vscode/mcp-settings.json)"
    }

} else {
    Add-Warning "YAML config not found at: $YamlConfig"
}Write-Host ""

# ──────────────────────────────────────────────────────────────
# 3. Port Availability Check
# ──────────────────────────────────────────────────────────────

Write-Host "🌐 Checking Port $Port Availability..." -ForegroundColor Cyan

$portCheck = netstat -ano | Select-String ":$Port\s" | Select-String "LISTENING"

if ($portCheck) {
    # Port is in use - check if it's our Python server
    $processIds = $portCheck | ForEach-Object {
        if ($_ -match '\s+(\d+)\s*$') {
            [int]$matches[1]
        }
    } | Select-Object -Unique

    $isPythonServer = $false
    foreach ($procId in $processIds) {
        $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
        if ($proc -and $proc.ProcessName -eq 'python') {
            $isPythonServer = $true
            Add-Passed "Port $Port is bound by Python process (PID: $procId)"
        }
    }

    if (-not $isPythonServer) {
        Add-Warning "Port $Port is in use by non-Python process (PIDs: $($processIds -join ', '))"
    }

} else {
    Add-Warning "Port $Port is not in use (server not running)"

    if ($StartServers) {
        Write-Host "   Attempting to start server..." -ForegroundColor Yellow
        $startScript = Join-Path $RepoRoot "scripts\tools\start-wiley-gh-mcp.ps1"
        if (Test-Path $startScript) {
            & $startScript -Background
            Start-Sleep -Seconds 2
        } else {
            Add-Warning "Startup script not found: $startScript"
        }
    }
}

Write-Host ""

# ──────────────────────────────────────────────────────────────
# 4. Server Health Check (if running)
# ──────────────────────────────────────────────────────────────

if ($HealthCheck) {
    Write-Host "🏥 Performing Health Checks..." -ForegroundColor Cyan

    $baseUrl = "http://127.0.0.1:$Port"
    $endpoints = @(
        @{ Path = "/.well-known/ai-plugin.json"; Name = "AI Plugin Manifest" }
        @{ Path = "/list-cs-files"; Name = "List C# Files" }
        @{ Path = "/project-tree"; Name = "Project Tree" }
    )

    foreach ($endpoint in $endpoints) {
        try {
            $response = Invoke-WebRequest -Uri "$baseUrl$($endpoint.Path)" -Method Get -TimeoutSec 5 -UseBasicParsing
            if ($response.StatusCode -eq 200) {
                Add-Passed "$($endpoint.Name) endpoint responding ($($endpoint.Path))"
            } else {
                Add-Warning "$($endpoint.Name) returned status $($response.StatusCode)"
            }
        } catch {
            Add-Issue "$($endpoint.Name) endpoint failed: $_"
        }
    }

    Write-Host ""
}

# ──────────────────────────────────────────────────────────────
# 5. Docker MCP Servers Check
# ──────────────────────────────────────────────────────────────

Write-Host "🐳 Checking Docker MCP Servers..." -ForegroundColor Cyan

try {
    $dockerVersion = docker --version 2>&1
    if ($dockerVersion -match 'Docker version') {
        Add-Passed "Docker available: $dockerVersion"

        # Check for MCP containers
        $containers = docker ps --filter "name=mcp" --format "{{.Names}}" 2>&1
        if ($containers -and $containers -is [string] -and $containers.Length -gt 0) {
            $containerList = $containers -split "`n" | Where-Object { $_ }
            Add-Passed "MCP containers running: $($containerList -join ', ')"
        } else {
            Add-Warning "No MCP Docker containers found running"
        }

    } else {
        Add-Warning "Docker not available or not responding"
    }
} catch {
    Add-Warning "Docker check failed: $_"
}

Write-Host ""

# ──────────────────────────────────────────────────────────────
# 6. Environment Variables Check
# ──────────────────────────────────────────────────────────────

Write-Host "🔐 Checking Environment Variables..." -ForegroundColor Cyan

$requiredEnvVars = @(
    @{ Name = 'WW_REPO_ROOT'; Expected = $RepoRoot }
    @{ Name = 'GITHUB_TOKEN'; Expected = $null; Optional = $true }
)

foreach ($envVar in $requiredEnvVars) {
    $value = [Environment]::GetEnvironmentVariable($envVar.Name)

    if ($value) {
        if ($envVar.Expected -and $value -ne $envVar.Expected) {
            Add-Warning "$($envVar.Name) is set but differs from expected ($value vs $($envVar.Expected))"
        } else {
            $displayValue = if ($envVar.Name -eq 'GITHUB_TOKEN') { '***' } else { $value }
            Add-Passed "$($envVar.Name) = $displayValue"
        }
    } else {
        if ($envVar.Optional) {
            Add-Warning "$($envVar.Name) not set (optional but recommended)"
        } else {
            Add-Issue "$($envVar.Name) not set (required)"
        }
    }
}

Write-Host ""

# ──────────────────────────────────────────────────────────────
# Summary Report
# ──────────────────────────────────────────────────────────────

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Validation Summary" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ Passed:   $($script:Passed.Count)" -ForegroundColor Green
Write-Host "⚠️  Warnings: $($script:Warnings.Count)" -ForegroundColor Yellow
Write-Host "❌ Issues:   $($script:Issues.Count)" -ForegroundColor Red
Write-Host ""

if ($script:Issues.Count -eq 0) {
    Write-Host "🎉 MCP setup is healthy!" -ForegroundColor Green
    exit 0
} elseif ($script:Issues.Count -le 2) {
    Write-Host "⚠️  MCP setup has minor issues (see above)" -ForegroundColor Yellow
    exit 0
} else {
    Write-Host "❌ MCP setup has critical issues - please fix before continuing" -ForegroundColor Red
    exit 1
}
