#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Professional .NET process management script for development environments
.DESCRIPTION
    Manages orphaned .NET processes during development with intelligent detection,
    cleanup, and monitoring capabilities.
#>

param(
    [switch]$Force,
    [switch]$Monitor,
    [switch]$Clean,
    [int]$TimeoutSeconds = 30,
    [string]$ProcessName = "dotnet"
)

#Requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

class DotNetProcessManager {
    [string]$ProcessName
    [int]$TimeoutSeconds
    [hashtable]$ProcessCache

    DotNetProcessManager([string]$processName, [int]$timeout) {
        $this.ProcessName = $processName
        $this.TimeoutSeconds = $timeout
        $this.ProcessCache = @{}
    }

    [array] GetOrphanedProcesses() {
        $processes = Get-Process -Name $this.ProcessName -ErrorAction SilentlyContinue

        $orphaned = @()
        foreach ($proc in $processes) {
            try {
                # Check if process has a parent (Windows API)
                $parentId = $this.GetParentProcessId($proc.Id)

                # Consider orphaned if parent doesn't exist or parent id is 0
                if (($null -eq $parentId) -or ($parentId -eq 0)) {
                    $orphaned += $proc
                    continue
                }

                $parentProcess = Get-Process -Id $parentId -ErrorAction SilentlyContinue
                # Ensure null comparisons have $null on the left
                if ($null -eq $parentProcess) {
                    $orphaned += $proc
                    continue
                }

                # Check if parent is a development tool.
                # Guard Name access in case parentProcess unexpectedly lacks it.
                $devTools = @('devenv', 'code', 'rider', 'vs', 'dotnet', 'testhost', 'vstest.console')
                $isDevParent = $devTools | Where-Object { ($null -ne $parentProcess) -and ($parentProcess.Name -like "*$_*") }

                if (-not $isDevParent) {
                    $orphaned += $proc
                }
            }
            catch {
                # If we can't determine parent, consider it potentially orphaned
                $orphaned += $proc
            }
        }

        return $orphaned
    }

    [int] GetParentProcessId([int]$processId) {
        try {
            $result = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = $processId"
            return $result.ParentProcessId
        }
        catch {
            return 0
        }
    }

    [void] KillProcesses([array]$processes, [bool]$force = $false) {
        foreach ($proc in $processes) {
            try {
                Write-Information "🛑 Killing process: $($proc.Name) (PID: $($proc.Id), CPU: $([math]::Round($proc.CPU, 2))s, Memory: $([math]::Round($proc.WorkingSet64 / 1MB, 2))MB)"

                if ($force) {
                    $proc | Stop-Process -Force
                }
                else {
                    $proc | Stop-Process
                }

                # Wait for process to actually terminate
                $startTime = Get-Date
                while (-not $proc.HasExited -and ((Get-Date) - $startTime).TotalSeconds -lt 5) {
                    Start-Sleep -Milliseconds 100
                }

                Write-Information "✅ Process $($proc.Id) terminated"
            }
            catch {
                Write-Warning "Failed to kill process $($proc.Id): $($_.Exception.Message)"
            }
        }
    }

    [void] MonitorProcesses() {
        Write-Information "🔍 Starting .NET process monitor (Ctrl+C to stop)..."

        $initialProcesses = $this.GetOrphanedProcesses()
        Write-Information "📊 Initial orphaned processes: $($initialProcesses.Count)"

        try {
            while ($true) {
                $currentProcesses = $this.GetOrphanedProcesses()
                $newProcesses = $currentProcesses | Where-Object { $_.Id -notin $initialProcesses.Id }

                if ($newProcesses.Count -gt 0) {
                    Write-Warning "⚠️  New orphaned processes detected:"
                    foreach ($proc in $newProcesses) {
                        Write-Warning "   - $($proc.Name) (PID: $($proc.Id))"
                    }
                }

                Start-Sleep -Seconds 5
            }
        }
        catch {
            Write-Information "`n👋 Monitor stopped"
        }
    }

    [void] CleanBuildArtifacts() {
        Write-Information "🧹 Cleaning build artifacts..."

        $paths = @(
            "bin",
            "obj",
            ".vs",
            "TestResults",
            "*.log",
            "*.tmp"
        )

        foreach ($path in $paths) {
            if (Test-Path $path) {
                Write-Verbose "   Removing: $path"
                Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        Write-Information "✅ Build artifacts cleaned"
    }
}

function Invoke-ProcessCleanup {
    param(
        [string]$ProcessName = "dotnet",
        [switch]$Force,
        [int]$TimeoutSeconds = 30
    )

    $manager = [DotNetProcessManager]::new($ProcessName, $TimeoutSeconds)

    Write-Information "🔍 Scanning for orphaned $ProcessName processes..."

    $orphaned = $manager.GetOrphanedProcesses()

    if ($orphaned.Count -eq 0) {
        Write-Information "✅ No orphaned $ProcessName processes found"
        return
    }

    Write-Warning "🚨 Found $($orphaned.Count) orphaned $ProcessName process(es):"
    foreach ($proc in $orphaned) {
        Write-Warning "   - $($proc.Name) (PID: $($proc.Id), Started: $($proc.StartTime))"
    }

    if (-not $Force) {
        $response = Read-Host "Kill these processes? (y/N)"
        if ($response -ne 'y' -and $response -ne 'Y') {
            Write-Information "❌ Operation cancelled"
            return
        }
    }

    $manager.KillProcesses($orphaned, $Force.IsPresent)

    # Verify cleanup
    Start-Sleep -Seconds 2
    $remaining = $manager.GetOrphanedProcesses()
    if ($remaining.Count -eq 0) {
        Write-Information "✅ All orphaned processes cleaned up"
    }
    else {
        Write-Warning "⚠️  $($remaining.Count) processes still remain"
    }
}

# Main execution
try {
    if ($Monitor) {
        $manager = [DotNetProcessManager]::new($ProcessName, $TimeoutSeconds)
        $manager.MonitorProcesses()
    }
    elseif ($Clean) {
        $manager = [DotNetProcessManager]::new($ProcessName, $TimeoutSeconds)
        $manager.CleanBuildArtifacts()
        Invoke-ProcessCleanup -ProcessName $ProcessName -Force:$Force -TimeoutSeconds $TimeoutSeconds
    }
    else {
        Invoke-ProcessCleanup -ProcessName $ProcessName -Force:$Force -TimeoutSeconds $TimeoutSeconds
    }
}
catch {
    Write-Error "Process management failed: $($_.Exception.Message)"
    exit 1
}
