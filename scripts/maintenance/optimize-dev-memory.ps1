#!/usr/bin/env pwsh
# optimize-dev-memory.ps1
# Reduces memory footprint of development environment
# Run this when experiencing memory pressure during development

[CmdletBinding()]
param(
    [switch]$Aggressive,
    [switch]$ShowStats
)

$ErrorActionPreference = "Stop"

Write-Host "🧹 Development Environment Memory Optimization" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# Step 1: Show current memory usage
if ($ShowStats) {
    Write-Host "`n📊 Current Memory Usage:" -ForegroundColor Yellow

    $os = Get-CimInstance Win32_OperatingSystem
    $totalMemGB = [math]::Round($os.TotalVisibleMemorySize / 1MB, 2)
    $freeMemGB = [math]::Round($os.FreePhysicalMemory / 1MB, 2)
    $usedMemGB = $totalMemGB - $freeMemGB
    $usedPercent = [math]::Round(($usedMemGB / $totalMemGB) * 100, 1)

    Write-Host "  Total: $totalMemGB GB" -ForegroundColor Gray
    Write-Host "  Used: $usedMemGB GB ($usedPercent%)" -ForegroundColor $(if ($usedPercent -gt 80) { "Red" } else { "Yellow" })
    Write-Host "  Free: $freeMemGB GB" -ForegroundColor Green

    # Show top memory consumers
    Write-Host "`n  Top Memory Consumers:" -ForegroundColor Gray
    Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 10 | ForEach-Object {
        $memMB = [math]::Round($_.WorkingSet64 / 1MB, 0)
        Write-Host "    $($_.ProcessName): $memMB MB" -ForegroundColor Gray
    }
}

# Step 2: Clean up Docker resources
Write-Host "`n🐳 Cleaning Docker Resources..." -ForegroundColor Yellow

try {
    # Stop any running but unused containers
    $runningContainers = docker ps -q 2>$null
    if ($runningContainers) {
        Write-Host "  Found $($runningContainers.Count) running containers"

        # Stop containers that aren't actively being used
        docker ps --filter "status=exited" -q | ForEach-Object {
            docker rm $_ 2>$null | Out-Null
        }
        Write-Host "  ✓ Removed stopped containers" -ForegroundColor Green
    }

    # Remove dangling images
    $danglingImages = docker images -f "dangling=true" -q 2>$null
    if ($danglingImages) {
        Write-Host "  Removing $($danglingImages.Count) dangling images..."
        docker rmi $danglingImages 2>$null | Out-Null
        Write-Host "  ✓ Removed dangling images" -ForegroundColor Green
    }

    # Prune build cache (aggressive mode only)
    if ($Aggressive) {
        Write-Host "  Pruning Docker build cache..."
        docker builder prune -f 2>$null | Out-Null
        Write-Host "  ✓ Build cache pruned" -ForegroundColor Green
    }

    Write-Host "  ✓ Docker cleanup complete" -ForegroundColor Green
} catch {
    Write-Warning "  Docker cleanup failed: $_"
}

# Step 3: Clean up old .NET build artifacts
Write-Host "`n🔧 Cleaning .NET Build Artifacts..." -ForegroundColor Yellow

$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

try {
    # Clean bin/obj folders
    $binObjFolders = Get-ChildItem -Path $repoRoot -Include bin, obj -Recurse -Directory -Force -ErrorAction SilentlyContinue
    $cleanedMB = 0

    foreach ($folder in $binObjFolders) {
        try {
            $sizeMB = (Get-ChildItem -Path $folder.FullName -Recurse -File -ErrorAction SilentlyContinue |
                Measure-Object -Property Length -Sum).Sum / 1MB
            Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction SilentlyContinue
            $cleanedMB += $sizeMB
        } catch {
            # Skip locked folders
        }
    }

    Write-Host "  ✓ Cleaned $([math]::Round($cleanedMB, 0)) MB from bin/obj folders" -ForegroundColor Green
} catch {
    Write-Warning "  .NET cleanup failed: $_"
}

# Step 4: Clear NuGet HTTP cache (aggressive mode only)
if ($Aggressive) {
    Write-Host "`n📦 Clearing NuGet Caches..." -ForegroundColor Yellow

    try {
        dotnet nuget locals http-cache --clear 2>$null | Out-Null
        dotnet nuget locals temp --clear 2>$null | Out-Null
        Write-Host "  ✓ NuGet caches cleared" -ForegroundColor Green
    } catch {
        Write-Warning "  NuGet cache cleanup failed: $_"
    }
}

# Step 5: Close unused PowerShell sessions
Write-Host "`n💻 Checking PowerShell Sessions..." -ForegroundColor Yellow

try {
    $pwshProcesses = Get-Process -Name pwsh, powershell -ErrorAction SilentlyContinue
    $currentPID = $PID
    $unusedSessions = $pwshProcesses | Where-Object {
        $_.Id -ne $currentPID -and
        (Get-Date) - $_.StartTime -gt [TimeSpan]::FromHours(1)
    }

    if ($unusedSessions) {
        Write-Host "  Found $($unusedSessions.Count) PowerShell sessions older than 1 hour"
        Write-Host "  Consider closing these manually from Task Manager" -ForegroundColor Yellow
        $unusedSessions | Select-Object Id, ProcessName, @{N = 'MemoryMB'; E = { [math]::Round($_.WorkingSet64 / 1MB, 0) } } | Format-Table
    } else {
        Write-Host "  ✓ No stale PowerShell sessions found" -ForegroundColor Green
    }
} catch {
    Write-Warning "  PowerShell session check failed: $_"
}

# Step 6: Garbage collection for current PowerShell session
Write-Host "`n🧹 Running Garbage Collection..." -ForegroundColor Yellow

try {
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
    [System.GC]::Collect()
    Write-Host "  ✓ Garbage collection complete" -ForegroundColor Green
} catch {
    Write-Warning "  GC failed: $_"
}

# Step 7: Show final memory stats
if ($ShowStats) {
    Write-Host "`n📊 Memory Usage After Cleanup:" -ForegroundColor Yellow

    $os = Get-CimInstance Win32_OperatingSystem
    $totalMemGB = [math]::Round($os.TotalVisibleMemorySize / 1MB, 2)
    $freeMemGB = [math]::Round($os.FreePhysicalMemory / 1MB, 2)
    $usedMemGB = $totalMemGB - $freeMemGB
    $usedPercent = [math]::Round(($usedMemGB / $totalMemGB) * 100, 1)

    Write-Host "  Total: $totalMemGB GB" -ForegroundColor Gray
    Write-Host "  Used: $usedMemGB GB ($usedPercent%)" -ForegroundColor $(if ($usedPercent -gt 80) { "Red" } elseif ($usedPercent -gt 60) { "Yellow" } else { "Green" })
    Write-Host "  Free: $freeMemGB GB" -ForegroundColor Green
}

Write-Host "`n✅ Memory optimization complete!" -ForegroundColor Green
Write-Host "`nRecommendations:" -ForegroundColor Cyan
Write-Host "  • Close unused VS Code windows/tabs"
Write-Host "  • Use Agent Sessions tab to pause unused MCP servers"
Write-Host "  • Restart VS Code if memory usage remains high"
Write-Host "  • Run with -Aggressive flag for deeper cleanup"
Write-Host "  • Run with -ShowStats to see before/after memory usage"
