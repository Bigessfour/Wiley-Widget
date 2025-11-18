<#
.SYNOPSIS
    Monitor Wiley Widget Docker containers health and performance

.DESCRIPTION
    Monitors container status, resource usage, and logs.
    Provides alerts for unhealthy containers and resource issues.

.PARAMETER AlertThresholdCpu
    CPU usage threshold for alerts (default: 80%)

.PARAMETER AlertThresholdMemory
    Memory usage threshold for alerts (default: 85%)

.PARAMETER CheckInterval
    Interval between checks in seconds (default: 30)

.PARAMETER Continuous
    Run monitoring continuously (default: false, single check)

.EXAMPLE
    .\monitor-containers.ps1
    .\monitor-containers.ps1 -Continuous -CheckInterval 60

.NOTES
    Author: Wiley Widget Team
    Date: November 14, 2025
    Requires: Docker
#>

[CmdletBinding()]
param(
    [int]$AlertThresholdCpu = 80,
    [int]$AlertThresholdMemory = 85,
    [int]$CheckInterval = 30,
    [switch]$Continuous,
    [switch]$ShowLogs
)

$ErrorActionPreference = "Continue"

# Function to check container health
function Get-ContainerHealth {
    param([string]$ContainerName)
    
    $healthStatus = docker inspect --format='{{.State.Health.Status}}' $ContainerName 2>$null
    
    if ($LASTEXITCODE -eq 0) {
        return $healthStatus
    } else {
        return "unknown"
    }
}

# Function to get container stats
function Get-ContainerStats {
    param([string]$ContainerName)
    
    $stats = docker stats $ContainerName --no-stream --format "{{.CPUPerc}}|{{.MemPerc}}|{{.MemUsage}}|{{.NetIO}}|{{.BlockIO}}" 2>$null
    
    if ($LASTEXITCODE -eq 0 -and $stats) {
        $parts = $stats -split '\|'
        
        return [PSCustomObject]@{
            Name = $ContainerName
            CPU = [double]($parts[0] -replace '%', '').Trim()
            Memory = [double]($parts[1] -replace '%', '').Trim()
            MemoryUsage = $parts[2]
            NetworkIO = $parts[3]
            BlockIO = $parts[4]
        }
    }
    
    return $null
}

# Function to check container logs for errors
function Get-ContainerErrors {
    param([string]$ContainerName)
    
    $errorLogs = docker logs $ContainerName --tail 50 2>&1 | Select-String -Pattern "error|exception|failed|fatal" -CaseSensitive:$false
    
    return $errorLogs
}

# Function to display status
function Show-ContainerStatus {
    param([object[]]$Containers, [hashtable]$Stats, [hashtable]$Health)
    
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Container Status - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    
    foreach ($container in $Containers) {
        $name = $container.Names -replace '^/', ''
        $status = $container.Status
        $state = $container.State
        $health = $Health[$name]
        $stat = $Stats[$name]
        
        # Status indicator
        $statusColor = switch ($state) {
            "running" { "Green" }
            "exited" { "Red" }
            "paused" { "Yellow" }
            default { "Gray" }
        }
        
        Write-Host "  ┌─ $name" -ForegroundColor White
        Write-Host "  │  Status: " -NoNewline
        Write-Host $status -ForegroundColor $statusColor
        
        if ($health) {
            $healthColor = switch ($health) {
                "healthy" { "Green" }
                "unhealthy" { "Red" }
                "starting" { "Yellow" }
                default { "Gray" }
            }
            Write-Host "  │  Health: " -NoNewline
            Write-Host $health -ForegroundColor $healthColor
        }
        
        if ($stat) {
            $cpuColor = if ($stat.CPU -gt $AlertThresholdCpu) { "Red" } else { "Green" }
            $memColor = if ($stat.Memory -gt $AlertThresholdMemory) { "Red" } else { "Green" }
            
            Write-Host "  │  CPU: " -NoNewline
            Write-Host ("{0:F2}%" -f $stat.CPU) -ForegroundColor $cpuColor -NoNewline
            Write-Host " | Memory: " -NoNewline
            Write-Host ("{0:F2}% ({1})" -f $stat.Memory, $stat.MemoryUsage) -ForegroundColor $memColor
            Write-Host "  │  Network: $($stat.NetworkIO) | Block I/O: $($stat.BlockIO)" -ForegroundColor Gray
            
            # Alert if thresholds exceeded
            if ($stat.CPU -gt $AlertThresholdCpu) {
                Write-Host "  │  ⚠ ALERT: CPU usage exceeds threshold ($AlertThresholdCpu%)" -ForegroundColor Red
            }
            if ($stat.Memory -gt $AlertThresholdMemory) {
                Write-Host "  │  ⚠ ALERT: Memory usage exceeds threshold ($AlertThresholdMemory%)" -ForegroundColor Red
            }
        }
        
        # Check for recent errors in logs
        if ($ShowLogs) {
            $errors = Get-ContainerErrors -ContainerName $name
            if ($errors) {
                Write-Host "  │  ⚠ Recent errors in logs:" -ForegroundColor Yellow
                $errors | Select-Object -First 3 | ForEach-Object {
                    $line = $_.Line.Trim()
                    if ($line.Length -gt 80) { $line = $line.Substring(0, 77) + "..." }
                    Write-Host "  │    - $line" -ForegroundColor Gray
                }
            }
        }
        
        Write-Host "  └─────────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
        Write-Host ""
    }
}

# Main monitoring loop
try {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "  Wiley Widget Container Monitor" -ForegroundColor Yellow
    Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "  CPU Alert Threshold: $AlertThresholdCpu%" -ForegroundColor Cyan
    Write-Host "  Memory Alert Threshold: $AlertThresholdMemory%" -ForegroundColor Cyan
    if ($Continuous) {
        Write-Host "  Check Interval: $CheckInterval seconds" -ForegroundColor Cyan
        Write-Host "  Mode: Continuous (Press Ctrl+C to exit)" -ForegroundColor Cyan
    } else {
        Write-Host "  Mode: Single check" -ForegroundColor Cyan
    }
    Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Yellow
    
    do {
        # Get running containers
        $containers = docker ps --format "{{json .}}" | ConvertFrom-Json
        
        if (-not $containers) {
            Write-Host ""
            Write-Host "⚠ No running containers found" -ForegroundColor Yellow
            Write-Host ""
        } else {
            # Collect stats and health
            $stats = @{}
            $health = @{}
            
            foreach ($container in $containers) {
                $name = $container.Names -replace '^/', ''
                $stats[$name] = Get-ContainerStats -ContainerName $name
                $health[$name] = Get-ContainerHealth -ContainerName $name
            }
            
            # Display status
            Show-ContainerStatus -Containers $containers -Stats $stats -Health $health
        }
        
        if ($Continuous) {
            Start-Sleep -Seconds $CheckInterval
        }
        
    } while ($Continuous)
    
} catch {
    Write-Host ""
    Write-Host "✗ Monitoring error: $_" -ForegroundColor Red
    Write-Host ""
    exit 1
}
