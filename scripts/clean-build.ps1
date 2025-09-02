#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
 # 3. Remove WPF temp files from subdirectories
$objDirs = Get-ChildItem -Path "." -Recurse -Directory -Name "obj" -ErrorAction SilentlyContinue
foreach ($objDir in $objDirs) {
    Remove-SafelyWithLogging -Path "$objDir\*_wpftmp.*.cs" -Description "WPF temp files in $objDir\"
}

# 4. Remove specific WPF temp project files (legacy cleanup)
# Note: This is now handled in step 1, but kept for compatibilityns WPF temporary files and build artifacts to prevent accumulation.

.DESCRIPTION
    This script removes WPF temporary files (*_wpftmp.*.cs) that can accumulate
    during builds due to global usings or incremental build failures.
    
    Part of the fix for Issue #4: Massive Temp WPF File Accumulation.

.PARAMETER Deep
    Performs a deep clean including all obj/bin directories

.EXAMPLE
    .\scripts\clean-build.ps1
    Basic cleanup of WPF temp files

.EXAMPLE
    .\scripts\clean-build.ps1 -Deep
    Deep cleanup including all build artifacts
#>

param(
    [switch]$Deep = $false
)

# Set error action preference
$ErrorActionPreference = "Continue"

Write-Host "🧹 Starting WPF build cleanup..." -ForegroundColor Cyan

# Function to safely remove files
function Remove-SafelyWithLogging {
    param(
        [string]$Path,
        [string]$Description
    )
    
    try {
        $items = @(Get-ChildItem -Path $Path -ErrorAction SilentlyContinue)
        if ($items.Count -gt 0) {
            $count = $items.Count
            Remove-Item -Path $Path -Force -Recurse -ErrorAction SilentlyContinue
            Write-Host "✅ Removed $count $Description" -ForegroundColor Green
        } else {
            Write-Host "ℹ️  No $Description found" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "⚠️  Could not remove $Description`: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# 1. Remove WPF temp files from root directory
Write-Host "🎯 Cleaning WPF temporary files..." -ForegroundColor Yellow
Remove-SafelyWithLogging -Path "*_wpftmp.*" -Description "WPF temp files in root"

# 2. Remove WPF temp files from obj directory
Remove-SafelyWithLogging -Path "obj\*_wpftmp.*.cs" -Description "WPF temp files in obj\"

# 3. Remove WPF temp files from subdirectories
$objDirs = Get-ChildItem -Path "." -Recurse -Directory -Name "obj" -ErrorAction SilentlyContinue
foreach ($objDir in $objDirs) {
    Remove-SafelyWithLogging -Path "$objDir\*_wpftmp.*.cs" -Description "WPF temp files in $objDir\"
}

# 4. Remove specific WPF temp project files (now handled in step 1, but kept for compatibility)
# Remove-SafelyWithLogging -Path "*_wpftmp.csproj" -Description "WPF temp project files"

# 5. Deep clean if requested
if ($Deep) {
    Write-Host "🔥 Performing deep clean..." -ForegroundColor Red
    
    # Remove obj directories
    Remove-SafelyWithLogging -Path "obj" -Description "obj directories"
    Remove-SafelyWithLogging -Path "*/obj" -Description "nested obj directories"
    
    # Remove bin directories (preserve publish folder)
    Remove-SafelyWithLogging -Path "bin/Debug" -Description "Debug binaries"
    Remove-SafelyWithLogging -Path "bin/Release" -Description "Release binaries"
    Remove-SafelyWithLogging -Path "*/bin/Debug" -Description "nested Debug binaries"
    Remove-SafelyWithLogging -Path "*/bin/Release" -Description "nested Release binaries"
    
    # Remove MSBuild logs
    Remove-SafelyWithLogging -Path "*.binlog" -Description "MSBuild binary logs"
    Remove-SafelyWithLogging -Path "msbuild.log" -Description "MSBuild text logs"
}

# 5. Remove other temporary files
Remove-SafelyWithLogging -Path "*.tmp" -Description "temporary files"
Remove-SafelyWithLogging -Path "*.bak" -Description "backup files"

# 6. Clean NuGet package cache if requested
if ($Deep) {
    Write-Host "📦 Clearing local NuGet cache..." -ForegroundColor Magenta
    try {
        dotnet nuget locals all --clear
        Write-Host "✅ NuGet cache cleared" -ForegroundColor Green
    }
    catch {
        Write-Host "⚠️  Could not clear NuGet cache: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host "🎉 Build cleanup completed!" -ForegroundColor Green
Write-Host "💡 Run 'dotnet build' to start fresh" -ForegroundColor Cyan
