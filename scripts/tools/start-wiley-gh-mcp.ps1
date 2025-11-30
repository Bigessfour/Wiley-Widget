#Requires -Version 7.0
<#
.SYNOPSIS
    Starts the Wiley Widget GitHub MCP Server (Python FastAPI on port 6723).

.DESCRIPTION
    Launches the Python-based GitHub MCP server that provides custom tools for
    Continue.dev and GitHub Copilot Chat integration. The server exposes endpoints
    for listing C# files, searching code, running dotnet builds/tests, and more.

.PARAMETER Background
    Run the server as a background process (detached). Default is foreground with logs.

.PARAMETER Port
    Port to bind the server to. Default is 6723.

.PARAMETER KillExisting
    Kill any existing Python MCP server processes on the target port before starting.

.EXAMPLE
    .\start-wiley-gh-mcp.ps1
    # Starts server in foreground (Ctrl+C to stop)

.EXAMPLE
    .\start-wiley-gh-mcp.ps1 -Background
    # Starts server in background (detached process)

.EXAMPLE
    .\start-wiley-gh-mcp.ps1 -KillExisting
    # Kills existing server on port 6723, then starts new instance

.NOTES
    Author: Wiley Widget Development Team
    Dependencies: Python 3.14+, FastAPI, Uvicorn (installed in .continue/venv)
    Server endpoints:
        - http://127.0.0.1:6723/.well-known/ai-plugin.json (manifest)
        - http://127.0.0.1:6723/list-cs-files
        - http://127.0.0.1:6723/search-code
        - http://127.0.0.1:6723/dotnet-build
        - http://127.0.0.1:6723/dotnet-test
        - http://127.0.0.1:6723/project-tree
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [switch]$Background,

    [Parameter(Mandatory = $false)]
    [int]$Port = 6723,

    [Parameter(Mandatory = $false)]
    [switch]$KillExisting
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ──────────────────────────────────────────────────────────────
# Configuration
# ──────────────────────────────────────────────────────────────

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$PythonExe = Join-Path $RepoRoot ".continue\venv\Scripts\python.exe"
$ServerScript = Join-Path $RepoRoot ".continue\mcpServers\wiley-widget-gh-mcp.py"
$LogFile = Join-Path $RepoRoot "logs\mcp-github-server.log"

# ──────────────────────────────────────────────────────────────
# Pre-flight checks
# ──────────────────────────────────────────────────────────────

Write-Host "🔍 Wiley Widget GitHub MCP Server Startup" -ForegroundColor Cyan
Write-Host "   Repository: $RepoRoot" -ForegroundColor DarkGray
Write-Host "   Python:     $PythonExe" -ForegroundColor DarkGray
Write-Host "   Server:     $ServerScript" -ForegroundColor DarkGray
Write-Host "   Port:       $Port" -ForegroundColor DarkGray
Write-Host ""

if (-not (Test-Path $PythonExe)) {
    Write-Error "Python venv not found at: $PythonExe`nRun: python -m venv .continue\venv; .continue\venv\Scripts\pip install fastapi uvicorn pydantic"
}

if (-not (Test-Path $ServerScript)) {
    Write-Error "MCP server script not found at: $ServerScript"
}

# Ensure logs directory exists
$LogsDir = Split-Path -Parent $LogFile
if (-not (Test-Path $LogsDir)) {
    New-Item -Path $LogsDir -ItemType Directory -Force | Out-Null
}

# ──────────────────────────────────────────────────────────────
# Kill existing server if requested
# ──────────────────────────────────────────────────────────────

if ($KillExisting) {
    Write-Host "🔪 Checking for existing MCP server processes..." -ForegroundColor Yellow

    # Find processes listening on the target port
    $netstat = netstat -ano | Select-String ":$Port\s" | Select-String "LISTENING"

    if ($netstat) {
        $processIds = $netstat | ForEach-Object {
            if ($_ -match '\s+(\d+)\s*$') {
                [int]$matches[1]
            }
        } | Select-Object -Unique

        foreach ($pythonProcess in $processIds) {
            try {
                $proc = Get-Process -Id $pythonProcess -ErrorAction SilentlyContinue
                if ($proc -and $proc.ProcessName -eq 'python') {
                    Write-Host "   Killing Python process $pythonProcess on port $Port" -ForegroundColor DarkYellow
                    Stop-Process -Id $pythonProcess -Force
                    Start-Sleep -Milliseconds 500
                }
            } catch {
                Write-Warning "Failed to kill process $pythonProcess - $($_.Exception.Message)"
            }
        }
    } else {
        Write-Host "   No existing server found on port $Port" -ForegroundColor DarkGray
    }
}# ──────────────────────────────────────────────────────────────
# Check if port is already in use
# ──────────────────────────────────────────────────────────────

$portCheck = netstat -ano | Select-String ":$Port\s" | Select-String "LISTENING"
if ($portCheck) {
    Write-Warning "Port $Port is already in use!"
    Write-Host "Run with -KillExisting to terminate existing processes, or choose a different port." -ForegroundColor Yellow
    Write-Host "Existing processes:" -ForegroundColor DarkGray
    Write-Host $portCheck -ForegroundColor DarkGray
    exit 1
}

# ──────────────────────────────────────────────────────────────
# Set environment variables
# ──────────────────────────────────────────────────────────────

$env:WW_REPO_ROOT = $RepoRoot
$env:PYTHONUNBUFFERED = "1"  # Force Python to flush output immediately

# Check for GitHub token (optional but recommended)
if (-not $env:GITHUB_TOKEN) {
    Write-Warning "GITHUB_TOKEN environment variable not set. Some GitHub features may be limited."
    Write-Host "   Set with: `$env:GITHUB_TOKEN = 'ghp_...' (or add to .vscode\profile.ps1)" -ForegroundColor DarkGray
}

# ──────────────────────────────────────────────────────────────
# Start the server
# ──────────────────────────────────────────────────────────────

Write-Host "🚀 Starting GitHub MCP Server on http://127.0.0.1:$Port" -ForegroundColor Green

if ($Background) {
    Write-Host "   Mode: Background (detached)" -ForegroundColor DarkGray
    Write-Host "   Logs: $LogFile" -ForegroundColor DarkGray
    Write-Host ""

    # Start detached process with output redirected to log file
    $ErrorLogFile = $LogFile -replace '\.log$', '-error.log'
    $startArgs = @{
        FilePath = $PythonExe
        ArgumentList = @($ServerScript)
        NoNewWindow = $true
        RedirectStandardOutput = $LogFile
        RedirectStandardError = $ErrorLogFile
        PassThru = $true
    }

    $process = Start-Process @startArgs

    # Wait a moment for startup
    Start-Sleep -Milliseconds 1500

    # Verify it's running
    if ($process.HasExited) {
        Write-Error "Server failed to start. Check logs at: $LogFile and $ErrorLogFile"
    }

    # Check if port is now listening
    $portCheck = netstat -ano | Select-String ":$Port\s" | Select-String "LISTENING"
    if (-not $portCheck) {
        Write-Error "Server process started but port $Port is not listening. Check logs at: $LogFile"
    }

    Write-Host "✅ Server started successfully (PID: $($process.Id))" -ForegroundColor Green
    Write-Host "   Test with: curl http://127.0.0.1:$Port/.well-known/ai-plugin.json" -ForegroundColor DarkCyan
    Write-Host "   Stop with: Stop-Process -Id $($process.Id)" -ForegroundColor DarkGray

} else {
    Write-Host "   Mode: Foreground (Ctrl+C to stop)" -ForegroundColor DarkGray
    Write-Host ""

    # Run in foreground with live output
    & $PythonExe $ServerScript
}
