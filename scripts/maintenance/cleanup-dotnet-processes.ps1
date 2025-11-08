#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Comprehensive cleanup of .NET processes and build artifacts
.DESCRIPTION
    Cleans up orphaned dotnet, testhost, and build server processes.
    Ensures no processes are left running after task completion.
    Implements Microsoft's recommendations for .NET process management.
.NOTES
    Author: Wiley Widget Development Team
    Date: November 4, 2025
    Version: 1.0.0
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [switch]$Force
)

$ErrorActionPreference = "Continue"

Write-Host "🧹 Starting comprehensive .NET process cleanup..." -ForegroundColor Cyan
Write-Host "   Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray

# Track cleanup statistics
$script:cleanupStats = @{
    BuildServerShutdown = $false
    TesthostKilled      = 0
    DotnetKilled        = 0
    WileyWidgetKilled   = 0
    MSBuildKilled       = 0
    VBCSCompilerKilled  = 0
    TotalProcesses      = 0
}

#region Helper Functions

function Write-CleanupStatus {
    param(
        [string]$Message,
        [string]$Type = "Info"
    )

    $color = switch ($Type) {
        "Success" { "Green" }
        "Warning" { "Yellow" }
        "Error" { "Red" }
        default { "White" }
    }

    Write-Host "   $Message" -ForegroundColor $color
}

function Get-OrphanedDotnetProcesses {
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
        # Filter criteria for orphaned processes:
        # 1. Has a valid StartTime
        # 2. Running for more than 1 minute
        # 3. No main window (background process)
        # 4. Not the current PowerShell's parent dotnet process

        $_.StartTime -and
        ((Get-Date) - $_.StartTime).TotalMinutes -gt 1 -and
        $_.MainWindowTitle -eq "" -and
        $_.Id -ne $PID
    }
}

#endregion

#region 1. Shutdown dotnet build-server

Write-Host ""
Write-Host "📦 Step 1: Shutting down dotnet build-server..." -ForegroundColor Yellow

try {
    $buildServerOutput = dotnet build-server shutdown 2>&1

    if ($LASTEXITCODE -eq 0) {
        $script:cleanupStats.BuildServerShutdown = $true
        Write-CleanupStatus "✅ Build server shutdown successful" "Success"

        if ($PSCmdlet.MyInvocation.BoundParameters.ContainsKey('Verbose')) {
            Write-CleanupStatus "Output: $buildServerOutput" "Info"
        }
    } else {
        Write-CleanupStatus "⚠️  Build server shutdown returned non-zero exit code: $LASTEXITCODE" "Warning"
    }
} catch {
    Write-CleanupStatus "⚠️  Build server shutdown failed: $($_.Exception.Message)" "Warning"
    Write-CleanupStatus "   (This is normal if no build server was running)" "Info"
}

#endregion

#region 2. Kill testhost processes

Write-Host ""
Write-Host "🧪 Step 2: Terminating testhost processes..." -ForegroundColor Yellow

$testhostProcesses = Get-Process -Name "testhost" -ErrorAction SilentlyContinue

if ($testhostProcesses) {
    $count = ($testhostProcesses | Measure-Object).Count
    Write-CleanupStatus "Found $count testhost process(es)" "Warning"

    foreach ($process in $testhostProcesses) {
        try {
            $processId = $process.Id
            $startTime = if ($process.StartTime) { $process.StartTime.ToString("HH:mm:ss") } else { "Unknown" }

            Write-CleanupStatus "Killing testhost PID: $processId (started: $startTime)" "Info"
            Stop-Process -Id $processId -Force -ErrorAction Stop

            $script:cleanupStats.TesthostKilled++
        } catch {
            Write-CleanupStatus "Failed to kill testhost PID ${processId}: $($_.Exception.Message)" "Error"
        }
    }

    Write-CleanupStatus "✅ Terminated $($script:cleanupStats.TesthostKilled) testhost process(es)" "Success"
} else {
    Write-CleanupStatus "✅ No testhost processes found" "Success"
}

#endregion

#region 3. Kill orphaned dotnet processes

Write-Host ""
Write-Host "🔧 Step 3: Terminating orphaned dotnet processes..." -ForegroundColor Yellow

$dotnetProcesses = Get-OrphanedDotnetProcesses

if ($dotnetProcesses) {
    $count = ($dotnetProcesses | Measure-Object).Count
    Write-CleanupStatus "Found $count orphaned dotnet process(es)" "Warning"

    foreach ($process in $dotnetProcesses) {
        try {
            $processId = $process.Id
            $runtime = [math]::Round(((Get-Date) - $process.StartTime).TotalMinutes, 1)
            $commandLine = (Get-CimInstance Win32_Process -Filter "ProcessId = $processId" -ErrorAction SilentlyContinue).CommandLine

            if ($PSCmdlet.MyInvocation.BoundParameters.ContainsKey('Verbose') -and $commandLine) {
                Write-CleanupStatus "Command: $commandLine" "Info"
            }

            Write-CleanupStatus "Killing dotnet PID: $processId (runtime: $runtime min)" "Info"
            Stop-Process -Id $processId -Force -ErrorAction Stop

            $script:cleanupStats.DotnetKilled++
        } catch {
            Write-CleanupStatus "Failed to kill dotnet PID ${processId}: $($_.Exception.Message)" "Error"
        }
    }

    Write-CleanupStatus "✅ Terminated $($script:cleanupStats.DotnetKilled) orphaned dotnet process(es)" "Success"
} else {
    Write-CleanupStatus "✅ No orphaned dotnet processes found" "Success"
}

#endregion

#region 3.5 Kill WileyWidget processes

Write-Host ""
Write-Host "🏢 Step 3.5: Terminating WileyWidget processes..." -ForegroundColor Yellow

$wileyWidgetProcesses = Get-Process -Name "WileyWidget" -ErrorAction SilentlyContinue

if ($wileyWidgetProcesses) {
    $count = ($wileyWidgetProcesses | Measure-Object).Count
    Write-CleanupStatus "Found $count WileyWidget process(es)" "Warning"

    foreach ($process in $wileyWidgetProcesses) {
        try {
            $processId = $process.Id
            $runtime = [math]::Round(((Get-Date) - $process.StartTime).TotalMinutes, 1)
            $commandLine = (Get-CimInstance Win32_Process -Filter "ProcessId = $processId" -ErrorAction SilentlyContinue).CommandLine

            if ($PSCmdlet.MyInvocation.BoundParameters.ContainsKey('Verbose') -and $commandLine) {
                Write-CleanupStatus "Command: $commandLine" "Info"
            }

            Write-CleanupStatus "Killing WileyWidget PID: $processId (runtime: $runtime min)" "Info"
            Stop-Process -Id $processId -Force -ErrorAction Stop

            $script:cleanupStats.WileyWidgetKilled++
        } catch {
            Write-CleanupStatus "Failed to kill WileyWidget PID ${processId}: $($_.Exception.Message)" "Error"
        }
    }

    Write-CleanupStatus "✅ Terminated WileyWidget processes" "Success"
} else {
    Write-CleanupStatus "✅ No WileyWidget processes found" "Success"
}

#endregion

#region 4. Kill MSBuild processes

Write-Host ""
Write-Host "🏗️  Step 4: Terminating MSBuild processes..." -ForegroundColor Yellow

$msbuildProcesses = Get-Process -Name "MSBuild" -ErrorAction SilentlyContinue

if ($msbuildProcesses) {
    $count = ($msbuildProcesses | Measure-Object).Count
    Write-CleanupStatus "Found $count MSBuild process(es)" "Warning"

    foreach ($process in $msbuildProcesses) {
        try {
            $processId = $process.Id
            Write-CleanupStatus "Killing MSBuild PID: $processId" "Info"
            Stop-Process -Id $processId -Force -ErrorAction Stop

            $script:cleanupStats.MSBuildKilled++
        } catch {
            Write-CleanupStatus "Failed to kill MSBuild PID ${processId}: $($_.Exception.Message)" "Error"
        }
    }

    Write-CleanupStatus "✅ Terminated $($script:cleanupStats.MSBuildKilled) MSBuild process(es)" "Success"
} else {
    Write-CleanupStatus "✅ No MSBuild processes found" "Success"
}

#endregion

#region 5. Kill VBCSCompiler processes (Roslyn compiler server)

Write-Host ""
Write-Host "⚙️  Step 5: Terminating VBCSCompiler (Roslyn) processes..." -ForegroundColor Yellow

$vbcsProcesses = Get-Process -Name "VBCSCompiler" -ErrorAction SilentlyContinue

if ($vbcsProcesses) {
    $count = ($vbcsProcesses | Measure-Object).Count
    Write-CleanupStatus "Found $count VBCSCompiler process(es)" "Warning"

    foreach ($process in $vbcsProcesses) {
        try {
            $processId = $process.Id
            Write-CleanupStatus "Killing VBCSCompiler PID: $processId" "Info"
            Stop-Process -Id $processId -Force -ErrorAction Stop

            $script:cleanupStats.VBCSCompilerKilled++
        } catch {
            Write-CleanupStatus "Failed to kill VBCSCompiler PID ${processId}: $($_.Exception.Message)" "Error"
        }
    }

    Write-CleanupStatus "✅ Terminated $($script:cleanupStats.VBCSCompilerKilled) VBCSCompiler process(es)" "Success"
} else {
    Write-CleanupStatus "✅ No VBCSCompiler processes found" "Success"
}

#endregion

#region Cleanup Summary

Write-Host ""
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "📊 Cleanup Summary" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan

$script:cleanupStats.TotalProcesses = $script:cleanupStats.TesthostKilled +
$script:cleanupStats.DotnetKilled +
$script:cleanupStats.MSBuildKilled +
$script:cleanupStats.VBCSCompilerKilled

Write-Host "   Build Server Shutdown: $($script:cleanupStats.BuildServerShutdown)" -ForegroundColor $(if ($script:cleanupStats.BuildServerShutdown) { "Green" } else { "Yellow" })
Write-Host "   Testhost Processes:    $($script:cleanupStats.TesthostKilled)" -ForegroundColor $(if ($script:cleanupStats.TesthostKilled -gt 0) { "Yellow" } else { "Green" })
Write-Host "   Dotnet Processes:      $($script:cleanupStats.DotnetKilled)" -ForegroundColor $(if ($script:cleanupStats.DotnetKilled -gt 0) { "Yellow" } else { "Green" })
Write-Host "   MSBuild Processes:     $($script:cleanupStats.MSBuildKilled)" -ForegroundColor $(if ($script:cleanupStats.MSBuildKilled -gt 0) { "Yellow" } else { "Green" })
Write-Host "   VBCSCompiler:          $($script:cleanupStats.VBCSCompilerKilled)" -ForegroundColor $(if ($script:cleanupStats.VBCSCompilerKilled -gt 0) { "Yellow" } else { "Green" })
Write-Host "   " + ("-" * 68) -ForegroundColor Gray
Write-Host "   Total Processes:       $($script:cleanupStats.TotalProcesses)" -ForegroundColor $(if ($script:cleanupStats.TotalProcesses -gt 0) { "Yellow" } else { "Green" })

Write-Host ""
if ($script:cleanupStats.TotalProcesses -eq 0) {
    Write-Host "✨ System is clean - no orphaned processes found" -ForegroundColor Green
} else {
    Write-Host "✨ Cleanup complete - $($script:cleanupStats.TotalProcesses) orphaned process(es) terminated" -ForegroundColor Green
}
Write-Host ""

#endregion

# Return exit code based on cleanup success
if ($script:cleanupStats.TotalProcesses -gt 10) {
    Write-Host "⚠️  WARNING: Unusually high number of orphaned processes detected!" -ForegroundColor Red
    Write-Host "   Consider investigating build/test process management." -ForegroundColor Yellow
    exit 1
}

exit 0
