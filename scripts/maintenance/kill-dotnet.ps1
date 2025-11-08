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
    [switch]$DryRun,
    [int]$TimeoutSeconds = 30,
    [string]$ProcessName = "dotnet",
    [string]$Project,
    [string]$Pattern,
    [string]$LogFile = "${PSScriptRoot}\\logs\\kill-dotnet.log"
)

#Requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"  # Make Write-Information visible

class DotNetProcessManager {
    [string]$ProcessName
    [int]$TimeoutSeconds

    DotNetProcessManager([string]$processName, [int]$timeout) {
        $this.ProcessName = $processName
        $this.TimeoutSeconds = $timeout
    }

    [array] GetCandidateProcesses([string]$project, [string]$pattern) {
        # Use Win32_Process to get CommandLine details which Get-Process doesn't provide
        $procs = Get-CimInstance -ClassName Win32_Process -Filter "Name = 'dotnet.exe' OR Name = 'dotnet'" -ErrorAction SilentlyContinue

        if (-not $procs) { return @() }

        $candidates = @()
        foreach ($p in $procs) {
            $cmd = $p.CommandLine -as [string]

            # Filter by project string if provided
            if ($project) {
                if (-not ($cmd -and $cmd.ToLower() -like "*" + $project.ToLower() + "*")) {
                    continue
                }
            }

            if ($pattern) {
                try {
                    if (-not ($cmd -and ($cmd -match $pattern))) { continue }
                }
                catch {
                    Write-Verbose "Invalid regex pattern '$pattern' - skipping pattern filter"
                    continue
                }
            }

            $obj = [PSCustomObject]@{
                ProcessId = [int]$p.ProcessId
                ParentProcessId = [int]$p.ParentProcessId
                CommandLine = $cmd
                Name = $p.Name
                CreationDate = $p.CreationDate
            }
            $candidates += $obj
        }

        return $candidates
    }

    [array] GetOrphanedProcesses([string]$project, [string]$pattern) {
        $procs = $this.GetCandidateProcesses($project, $pattern)

        $orphaned = @()
        foreach ($p in $procs) {
            $parentId = $p.ParentProcessId
            if (-not $parentId -or $parentId -eq 0) {
                $orphaned += $p; continue
            }

            $parent = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = $parentId" -ErrorAction SilentlyContinue
            if (-not $parent) { $orphaned += $p; continue }

            $parentName = $parent.Name -as [string]
            $devTools = @('devenv', 'code', 'rider', 'vs', 'vstest', 'testhost')
            $isDevParent = $false
            foreach ($d in $devTools) {
                if ($parentName -and $parentName.ToLower() -like "*$d*") { $isDevParent = $true; break }
            }

            if (-not $isDevParent) { $orphaned += $p }
        }

        return $orphaned
    }

    [void] KillProcessesById([int[]]$pids, [bool]$force) {
        foreach ($targetPid in $pids) {
            try {
                Write-Information "🛑 Terminating PID $targetPid (tree) ..."
                # Use taskkill to ensure child processes are terminated on Windows
                $taskkillArgs = @('/PID', $targetPid.ToString(), '/T')
                if ($force) { $taskkillArgs += '/F' }
                & taskkill @taskkillArgs | Out-Null
                Write-Information "✅ Requested termination for PID $targetPid"
            }
            catch {
                Write-Warning ("Failed to terminate PID {0}: {1}" -f $targetPid, $_.Exception.Message)
            }
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
        [switch]$DryRun,
        [string]$Project,
        [string]$Pattern,
        [int]$TimeoutSeconds = 30,
        [string]$LogFile
    )

    $manager = [DotNetProcessManager]::new($ProcessName, $TimeoutSeconds)

    Write-Information "🔍 Scanning for candidate $ProcessName processes (Project='$Project', Pattern='$Pattern')..."
    $candidates = $manager.GetOrphanedProcesses($Project, $Pattern)

    if ($candidates.Count -eq 0) {
        Write-Information "✅ No matching candidate processes found"
        return
    }

    Write-Warning "🚨 Found $($candidates.Count) candidate $ProcessName process(es):"
    $candidates | ForEach-Object { Write-Information "   - PID: $($_.ProcessId)  Cmd: $($_.CommandLine)" }

    if ($DryRun -or -not $Force) {
        Write-Information "`nDry-run / interactive mode - no processes will be killed. To actually kill, run with -Force or remove DryRun."
        if ($LogFile) {
            $entry = "$(Get-Date -Format o) - DryRun listing: $($candidates.Count) processes matching Project='$Project' Pattern='$Pattern'`n"
            $candidates | ForEach-Object { $entry += "PID:$($_.ProcessId) CMD:$($_.CommandLine)`n" }
            New-Item -ItemType Directory -Path (Split-Path $LogFile) -ErrorAction SilentlyContinue | Out-Null
            Add-Content -Path $LogFile -Value $entry
            Write-Information "Log written to $LogFile"
        }
        return
    }

    $pids = $candidates | ForEach-Object { $_.ProcessId }
    $manager.KillProcessesById($pids, $true)

    Start-Sleep -Seconds 2
    $remaining = $manager.GetOrphanedProcesses($Project, $Pattern)
    if ($remaining.Count -eq 0) {
        Write-Information "✅ All matching processes cleaned up"
        if ($LogFile) { Add-Content -Path $LogFile -Value "$(Get-Date -Format o) - Killed PIDs: $($pids -join ',')`n" }
    }
    else {
        Write-Warning "⚠️  $($remaining.Count) matching processes still remain"
        if ($LogFile) { Add-Content -Path $LogFile -Value "$(Get-Date -Format o) - Remaining PIDs after kill attempt: $($remaining | ForEach-Object { $_.ProcessId } -join ',')`n" }
    }
}

# Main execution
try {
    if ($Monitor) {
        Write-Warning "Monitor mode not implemented for CIM-based listing; please run the script repeatedly or use -DryRun for inspection."
        exit 0
    }
    elseif ($Clean) {
        $manager = [DotNetProcessManager]::new($ProcessName, $TimeoutSeconds)
        $manager.CleanBuildArtifacts()
        Invoke-ProcessCleanup -ProcessName $ProcessName -Force:$Force -DryRun:$DryRun -Project:$Project -Pattern:$Pattern -TimeoutSeconds $TimeoutSeconds -LogFile:$LogFile
    }
    else {
        Invoke-ProcessCleanup -ProcessName $ProcessName -Force:$Force -DryRun:$DryRun -Project:$Project -Pattern:$Pattern -TimeoutSeconds $TimeoutSeconds -LogFile:$LogFile
    }
}
catch {
    Write-Error "Process management failed: $($_.Exception.Message)"
    exit 1
}
