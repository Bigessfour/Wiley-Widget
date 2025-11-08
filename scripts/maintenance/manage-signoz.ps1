# SigNoz Management Script for Wiley Widget
# Self-hosted SigNoz observability platform setup and management
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("start", "stop", "restart", "status", "logs", "setup")]
    [string]$Action = "start",

    [Parameter(Mandatory = $false)]
    [switch]$WaitForReady = $false,

    [Parameter(Mandatory = $false)]
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$WorkspaceRoot = Split-Path -Parent $ScriptDir
$DockerComposeFile = Join-Path $ScriptDir "docker\docker-compose.signoz.yml"

Write-Host "🔍 SigNoz Management Script" -ForegroundColor Cyan
Write-Host "Action: $Action" -ForegroundColor Yellow

function Test-DockerInstallation {
    try {
        $dockerVersion = docker --version 2>$null
        if ($LASTEXITCODE -ne 0) {
            throw "Docker not found"
        }
        Write-Host "✅ Docker installed: $dockerVersion" -ForegroundColor Green
        return $true
    } catch {
        Write-Error "❌ Docker is not installed or not in PATH. Please install Docker Desktop first."
        return $false
    }
}

function Test-DockerComposeFile {
    if (-not (Test-Path $DockerComposeFile)) {
        Write-Error "❌ Docker Compose file not found: $DockerComposeFile"
        return $false
    }
    Write-Host "✅ Docker Compose file found: $DockerComposeFile" -ForegroundColor Green
    return $true
}

function Start-SigNoz {
    Write-Host "🚀 Starting SigNoz containers..." -ForegroundColor Cyan

    # Change to the docker directory for relative paths to work
    Push-Location (Split-Path $DockerComposeFile)

    try {
        # Start containers in detached mode
        docker compose -f docker-compose.signoz.yml up -d

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ SigNoz containers started successfully!" -ForegroundColor Green

            if ($WaitForReady) {
                Wait-ForSigNozReady -TimeoutSeconds $TimeoutSeconds
            }

            Show-SigNozInfo
        } else {
            Write-Error "❌ Failed to start SigNoz containers"
        }
    } finally {
        Pop-Location
    }
}

function Stop-SigNoz {
    Write-Host "🛑 Stopping SigNoz containers..." -ForegroundColor Yellow

    Push-Location (Split-Path $DockerComposeFile)

    try {
        docker compose -f docker-compose.signoz.yml down

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ SigNoz containers stopped successfully!" -ForegroundColor Green
        } else {
            Write-Error "❌ Failed to stop SigNoz containers"
        }
    } finally {
        Pop-Location
    }
}

function Restart-SigNoz {
    Stop-SigNoz
    Start-Sleep -Seconds 5
    Start-SigNoz
}

function Get-SigNozStatus {
    Write-Host "📊 SigNoz Container Status:" -ForegroundColor Cyan

    Push-Location (Split-Path $DockerComposeFile)

    try {
        docker compose -f docker-compose.signoz.yml ps

        Write-Host "`n🔗 SigNoz Service URLs:" -ForegroundColor Cyan
        Write-Host "Frontend (Web UI): http://localhost:3301" -ForegroundColor Yellow
        Write-Host "Query Service API: http://localhost:8080" -ForegroundColor Yellow
        Write-Host "OTLP gRPC Endpoint: http://localhost:4317" -ForegroundColor Yellow
        Write-Host "OTLP HTTP Endpoint: http://localhost:4318" -ForegroundColor Yellow
        Write-Host "ClickHouse HTTP: http://localhost:8123" -ForegroundColor Yellow
        Write-Host "Alertmanager: http://localhost:9093" -ForegroundColor Yellow
    } finally {
        Pop-Location
    }
}

function Show-SigNozLogs {
    Write-Host "📝 SigNoz Container Logs:" -ForegroundColor Cyan

    Push-Location (Split-Path $DockerComposeFile)

    try {
        docker compose -f docker-compose.signoz.yml logs --follow --tail=50
    } finally {
        Pop-Location
    }
}

function Wait-ForSigNozReady {
    param([int]$TimeoutSeconds = 120)

    Write-Host "⏳ Waiting for SigNoz to be ready..." -ForegroundColor Yellow

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $frontendReady = $false
    $queryServiceReady = $false
    $otlpReady = $false

    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        try {
            # Check frontend
            if (-not $frontendReady) {
                $response = Invoke-WebRequest -Uri "http://localhost:3301" -TimeoutSec 5 -UseBasicParsing -ErrorAction SilentlyContinue
                if ($response.StatusCode -eq 200) {
                    $frontendReady = $true
                    Write-Host "✅ Frontend ready" -ForegroundColor Green
                }
            }

            # Check query service
            if (-not $queryServiceReady) {
                $response = Invoke-WebRequest -Uri "http://localhost:8080/api/v1/version" -TimeoutSec 5 -UseBasicParsing -ErrorAction SilentlyContinue
                if ($response.StatusCode -eq 200) {
                    $queryServiceReady = $true
                    Write-Host "✅ Query Service ready" -ForegroundColor Green
                }
            }

            # Check OTLP endpoint
            if (-not $otlpReady) {
                $tcpClient = New-Object System.Net.Sockets.TcpClient
                try {
                    $tcpClient.ConnectAsync("localhost", 4317).Wait(1000)
                    if ($tcpClient.Connected) {
                        $otlpReady = $true
                        Write-Host "✅ OTLP gRPC endpoint ready" -ForegroundColor Green
                    }
                } catch { }
                finally {
                    $tcpClient.Dispose()
                }
            }

            # All services ready?
            if ($frontendReady -and $queryServiceReady -and $otlpReady) {
                Write-Host "✅ SigNoz is fully ready!" -ForegroundColor Green
                return $true
            }
        } catch {
            # Continue checking
        }

        Start-Sleep -Seconds 2
        Write-Host "." -NoNewline -ForegroundColor Gray
    }

    $stopwatch.Stop()
    Write-Host ""
    Write-Warning "⚠️  SigNoz did not become fully ready within $TimeoutSeconds seconds"
    return $false
}

function Show-SigNozInfo {
    Write-Host ""
    Write-Host "🎉 SigNoz Self-Hosted Setup Complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📊 Access SigNoz Dashboard:" -ForegroundColor Cyan
    Write-Host "   Web UI: http://localhost:3301" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "🔗 Wiley Widget Integration:" -ForegroundColor Cyan
    Write-Host "   OTLP gRPC: http://localhost:4317" -ForegroundColor Yellow
    Write-Host "   OTLP HTTP: http://localhost:4318" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "💡 Next Steps:" -ForegroundColor Cyan
    Write-Host "   1. Open http://localhost:3301 in your browser" -ForegroundColor White
    Write-Host "   2. Run Wiley Widget to send telemetry data" -ForegroundColor White
    Write-Host "   3. Check traces, metrics, and logs in the SigNoz dashboard" -ForegroundColor White
    Write-Host ""
}

function Initialize-SigNozSetup {
    Write-Host "🏗️  Setting up SigNoz self-hosted environment..." -ForegroundColor Cyan

    # Create logs directory if it doesn't exist
    $logsDir = Join-Path $WorkspaceRoot "logs"
    if (-not (Test-Path $logsDir)) {
        New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
        Write-Host "✅ Created logs directory: $logsDir" -ForegroundColor Green
    }

    # Pull Docker images
    Write-Host "📦 Pulling SigNoz Docker images..." -ForegroundColor Yellow
    Push-Location (Split-Path $DockerComposeFile)

    try {
        docker compose -f docker-compose.signoz.yml pull

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Docker images pulled successfully!" -ForegroundColor Green
        } else {
            Write-Warning "⚠️  Some images may not have been pulled. Continuing..."
        }
    } finally {
        Pop-Location
    }

    # Start SigNoz
    Start-SigNoz
}

# Main execution
switch ($Action) {
    "setup" {
        if (-not (Test-DockerInstallation) -or -not (Test-DockerComposeFile)) {
            exit 1
        }
        Initialize-SigNozSetup
    }
    "start" {
        if (-not (Test-DockerInstallation) -or -not (Test-DockerComposeFile)) {
            exit 1
        }
        Start-SigNoz
    }
    "stop" {
        Stop-SigNoz
    }
    "restart" {
        if (-not (Test-DockerInstallation) -or -not (Test-DockerComposeFile)) {
            exit 1
        }
        Restart-SigNoz
    }
    "status" {
        Get-SigNozStatus
    }
    "logs" {
        Show-SigNozLogs
    }
    default {
        Write-Error "Invalid action: $Action"
        exit 1
    }
}

Write-Host ""
Write-Host "✅ SigNoz management completed!" -ForegroundColor Green
