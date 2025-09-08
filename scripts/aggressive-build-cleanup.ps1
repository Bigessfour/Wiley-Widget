#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Aggressive build cleanup script that kills all .NET processes and cleans build artifacts
.DESCRIPTION
    This script performs an aggressive cleanup before building by:
    1. Killing all .NET related processes (dotnet, MSBuild, VBCSCompiler, WileyWidget)
    2. Cleaning build artifacts (bin, obj directories)
    3. Optionally performing a clean build
.PARAMETER ProjectPath
    Path to the .csproj file to build (optional)
.PARAMETER SkipBuild
    Skip the actual build step, only perform cleanup
.PARAMETER Verbose
    Enable verbose output
.EXAMPLE
    .\aggressive-build-cleanup.ps1 -ProjectPath "WileyWidget.csproj"
.EXAMPLE
    .\aggressive-build-cleanup.ps1 -SkipBuild
#>

param(
    [Parameter(HelpMessage = "Path to the .csproj file to build")]
    [string]$ProjectPath = "WileyWidget.csproj",
    
    [Parameter(HelpMessage = "Skip the actual build step, only perform cleanup")]
    [switch]$SkipBuild,
    
    [Parameter(HelpMessage = "Enable detailed output")]
    [switch]$Detail
)

# Set error action preference
$ErrorActionPreference = "Continue"

# Enable verbose output if requested
if ($Detail) {
    $VerbosePreference = "Continue"
}

Write-Host "🛑 AGGRESSIVE BUILD CLEANUP STARTED" -ForegroundColor Red -BackgroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow

# Step 1: Kill all .NET related processes
Write-Host "🔪 Step 1: Killing all .NET related processes..." -ForegroundColor Cyan

$processNames = @('dotnet', 'MSBuild', 'VBCSCompiler', 'WileyWidget', 'vshost', 'vshost32', 'ServiceHub.*', 'Microsoft.CodeAnalysis.*')
$killedCount = 0

foreach ($processPattern in $processNames) {
    try {
        $processes = Get-Process | Where-Object { $_.ProcessName -match "^$processPattern" }
        foreach ($process in $processes) {
            Write-Host "  🎯 Killing $($process.ProcessName) (PID: $($process.Id))" -ForegroundColor Red
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $killedCount++
        }
    }
    catch {
        Write-Verbose "No processes found matching pattern: $processPattern"
    }
}

if ($killedCount -eq 0) {
    Write-Host "  ✅ No .NET processes found to kill" -ForegroundColor Green
} else {
    Write-Host "  💀 Killed $killedCount processes" -ForegroundColor Yellow
    Write-Host "  ⏱️  Waiting 3 seconds for processes to terminate..." -ForegroundColor Yellow
    Start-Sleep -Seconds 3
}

# Step 2: Clean build artifacts
Write-Host "🧹 Step 2: Cleaning build artifacts..." -ForegroundColor Cyan

$cleanTargets = @(
    @{ Path = "bin"; Description = "Binary output" },
    @{ Path = "obj"; Description = "Object files" },
    @{ Path = "TestResults"; Description = "Test results" },
    @{ Path = "publish"; Description = "Publish output" },
    @{ Path = "packages"; Description = "NuGet packages" },
    @{ Path = ".vs"; Description = "Visual Studio cache" }
)

$cleanedCount = 0
foreach ($target in $cleanTargets) {
    if (Test-Path $target.Path) {
        try {
            Write-Host "  🗑️  Removing $($target.Description): $($target.Path)" -ForegroundColor Red
            Remove-Item -Path $target.Path -Recurse -Force -ErrorAction Stop
            $cleanedCount++
        }
        catch {
            Write-Warning "Failed to remove $($target.Path): $($_.Exception.Message)"
        }
    } else {
        Write-Verbose "Target not found: $($target.Path)"
    }
}

# Clean temporary files
$tempPatterns = @("*.tmp", "*.temp", "*.log", "*.cache", "*.user", "*.suo", "*.sdf")
foreach ($pattern in $tempPatterns) {
    $tempFiles = Get-ChildItem -Path . -Filter $pattern -Recurse -ErrorAction SilentlyContinue
    foreach ($file in $tempFiles) {
        try {
            Write-Host "  🗑️  Removing temp file: $($file.Name)" -ForegroundColor Red
            Remove-Item -Path $file.FullName -Force -ErrorAction Stop
            $cleanedCount++
        }
        catch {
            Write-Verbose "Failed to remove temp file: $($file.FullName)"
        }
    }
}

if ($cleanedCount -eq 0) {
    Write-Host "  ✅ No build artifacts found to clean" -ForegroundColor Green
} else {
    Write-Host "  🧹 Cleaned $cleanedCount items" -ForegroundColor Yellow
}

# Step 3: Optional build step
if (-not $SkipBuild) {
    Write-Host "🔨 Step 3: Starting clean build..." -ForegroundColor Cyan
    
    if (-not (Test-Path $ProjectPath)) {
        Write-Error "Project file not found: $ProjectPath"
        exit 1
    }
    
    Write-Host "  📦 Building project: $ProjectPath" -ForegroundColor Green
    
    # Set environment variables for clean build
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
    
    try {
        $buildArgs = @(
            "build",
            $ProjectPath,
            "/property:GenerateFullPaths=true",
            "/consoleloggerparameters:NoSummary",
            "--verbosity", "minimal",
            "--nologo"
        )
        
        Write-Host "  🏗️  Executing: dotnet $($buildArgs -join ' ')" -ForegroundColor Cyan
        
        $buildResult = & dotnet @buildArgs
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ Build completed successfully!" -ForegroundColor Green
        } else {
            Write-Error "Build failed with exit code: $LASTEXITCODE"
            exit $LASTEXITCODE
        }
    }
    catch {
        Write-Error "Build execution failed: $($_.Exception.Message)"
        exit 1
    }
} else {
    Write-Host "🚫 Step 3: Skipping build (SkipBuild flag set)" -ForegroundColor Yellow
}

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
Write-Host "✅ AGGRESSIVE BUILD CLEANUP COMPLETED" -ForegroundColor Green -BackgroundColor Black
Write-Host ""
