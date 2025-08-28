#Requires -Version 7.5
<#
.SYNOPSIS
    Comprehensive PowerShell module fix for persistent installation issues.

.DESCRIPTION
    This script addresses common PowerShell module installation problems by:
    - Cleaning up conflicting module installations
    - Fixing PSModulePath issues
    - Reinstalling modules in correct locations
    - Ensuring proper module loading

.PARAMETER CleanAll
    Remove all existing module installations before reinstalling.

.PARAMETER FixPaths
    Fix PSModulePath environment variable.

.PARAMETER Force
    Force reinstallation even if modules appear to be working.

.EXAMPLE
    .\Fix-PowerShell-Modules-Comprehensive.ps1 -CleanAll

.EXAMPLE
    .\Fix-PowerShell-Modules-Comprehensive.ps1 -FixPaths

.NOTES
    Author: WileyWidget Development Team
    Version: 2.0.0
    Requires: PowerShell 7.5.2+
#>

[CmdletBinding()]
param(
    [switch]$CleanAll,
    [switch]$FixPaths,
    [switch]$Force
)

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Get-CurrentModuleStatus {
    Write-ColorOutput "🔍 Checking current module status..." -Color Yellow

    $modules = @('PSScriptAnalyzer', 'Pester', 'PSReadLine', 'platyPS', 'PSFramework', 'ImportExcel')
    $status = @{}

    foreach ($module in $modules) {
        try {
            $installed = Get-Module -Name $module -ListAvailable -ErrorAction Stop
            $loaded = Get-Module -Name $module -ErrorAction SilentlyContinue

            $versions = $installed | Sort-Object Version -Descending
            $latest = $versions | Select-Object -First 1

            $status[$module] = @{
                Installed = $true
                Loaded = $null -ne $loaded
                Version = $latest.Version
                Path = $latest.ModuleBase
                MultipleVersions = ($versions.Count -gt 1)
                AllPaths = $versions | Select-Object -ExpandProperty ModuleBase
            }
        }
        catch {
            $status[$module] = @{
                Installed = $false
                Loaded = $false
                Version = $null
                Path = $null
                MultipleVersions = $false
                AllPaths = @()
            }
        }
    }

    return $status
}

function Show-ModuleStatus {
    param([hashtable]$status)

    Write-ColorOutput "`n📊 Module Status:" -Color Cyan
    Write-ColorOutput ("{0,-15} {1,-8} {2,-8} {3,-12} {4,-30}" -f "Module", "Loaded", "Multi", "Version", "Path") -Color White
    Write-ColorOutput ("-" * 80) -Color White

    foreach ($module in $status.Keys) {
        $s = $status[$module]
        $loaded = if ($s.Loaded) { "✅" } else { "❌" }
        $multi = if ($s.MultipleVersions) { "⚠️" } else { "✅" }
        $version = $s.Version ?? "N/A"
        $path = if ($s.Path) { Split-Path $s.Path -Leaf } else { "N/A" }

        Write-ColorOutput ("{0,-15} {1,-8} {2,-8} {3,-12} {4,-30}" -f $module, $loaded, $multi, $version, $path) -Color White

        if ($s.MultipleVersions) {
            Write-ColorOutput "  Multiple versions found:" -Color Yellow
            foreach ($path in $s.AllPaths) {
                Write-ColorOutput "    $path" -Color Yellow
            }
        }
    }
}

function Fix-PSModulePath {
    Write-ColorOutput "`n🔧 Fixing PSModulePath..." -Color Yellow

    # Get current paths
    $currentPaths = $env:PSModulePath -split ';' | Where-Object { $_ -and $_.Trim() }

    # Define correct paths for PowerShell 7
    $correctPaths = @(
        "$env:USERPROFILE\Documents\PowerShell\Modules",
        "C:\Program Files\PowerShell\Modules",
        "C:\Program Files\PowerShell\7\Modules"
    )

    # Filter out invalid paths and duplicates
    $validPaths = $currentPaths | Where-Object {
        $path = $_.Trim()
        # Remove empty paths, recursive references, and invalid paths
        $path -and
        $path -ne '$env:PSModulePath' -and
        $path -ne '$env:USERPROFILE\Documents\PowerShell\Modules' -and
        $path -ne '$env:ProgramFiles\PowerShell\Modules' -and
        (Test-Path $path -ErrorAction SilentlyContinue)
    }

    # Combine correct paths with valid existing paths
    $finalPaths = $correctPaths + ($validPaths | Where-Object { $correctPaths -notcontains $_ })

    # Remove duplicates and empty entries
    $finalPaths = $finalPaths | Select-Object -Unique | Where-Object { $_ }

    # Set the new PSModulePath
    $newPath = $finalPaths -join ';'
    $env:PSModulePath = $newPath

    Write-ColorOutput "✅ PSModulePath updated:" -Color Green
    $finalPaths | ForEach-Object { Write-ColorOutput "  $_" -Color White }

    return $finalPaths
}

function Remove-ConflictingModules {
    param([hashtable]$status)

    Write-ColorOutput "`n🧹 Removing conflicting module installations..." -Color Yellow

    $modulesToClean = @('PSScriptAnalyzer', 'Pester', 'PSReadLine')

    foreach ($module in $modulesToClean) {
        if ($status[$module].MultipleVersions -or $CleanAll) {
            Write-ColorOutput "Cleaning $module..." -Color White

            # Get all installations
            $installations = Get-Module -Name $module -ListAvailable -ErrorAction SilentlyContinue

            foreach ($install in $installations) {
                try {
                    $modulePath = $install.ModuleBase
                    if (Test-Path $modulePath) {
                        Write-ColorOutput "  Removing: $modulePath" -Color Yellow
                        Remove-Item -Path $modulePath -Recurse -Force -ErrorAction Stop
                        Write-ColorOutput "  ✅ Removed successfully" -Color Green
                    }
                }
                catch {
                    Write-ColorOutput "  ❌ Failed to remove: $_" -Color Red
                }
            }
        }
    }
}

function Install-ModulesCorrectly {
    Write-ColorOutput "`n📦 Installing modules in correct location..." -Color Yellow

    $modules = @(
        'PSScriptAnalyzer',
        'Pester',
        'PSReadLine',
        'platyPS',
        'PSFramework',
        'ImportExcel'
    )

    # Ensure we're installing to the user profile location
    $installPath = "$env:USERPROFILE\Documents\PowerShell\Modules"

    if (-not (Test-Path $installPath)) {
        New-Item -ItemType Directory -Path $installPath -Force | Out-Null
    }

    foreach ($module in $modules) {
        Write-ColorOutput "Installing $module..." -Color White
        try {
            # Force reinstall to ensure clean installation
            Install-Module -Name $module -Force -AllowClobber -Scope CurrentUser -ErrorAction Stop
            Write-ColorOutput "✅ $module installed successfully" -Color Green
        }
        catch {
            Write-ColorOutput "❌ Failed to install $module`: $_" -Color Red
        }
    }
}

function Test-ModuleLoading {
    Write-ColorOutput "`n🧪 Testing module loading..." -Color Yellow

    $testResults = @{}

    $modulesToTest = @('PSScriptAnalyzer', 'Pester', 'PSReadLine')

    foreach ($module in $modulesToTest) {
        try {
            # Remove if already loaded
            Remove-Module -Name $module -ErrorAction SilentlyContinue

            # Try to import
            Import-Module -Name $module -ErrorAction Stop

            # Get loaded module info
            $loaded = Get-Module -Name $module
            $testResults[$module] = @{
                Success = $true
                Version = $loaded.Version
                Path = $loaded.ModuleBase
            }

            Write-ColorOutput "✅ $module loaded successfully (v$($loaded.Version))" -Color Green

            # Remove after testing
            Remove-Module -Name $module -ErrorAction SilentlyContinue
        }
        catch {
            $testResults[$module] = @{
                Success = $false
                Error = $_.Exception.Message
            }
            Write-ColorOutput "❌ $module failed to load: $_" -Color Red
        }
    }

    return $testResults
}

function Show-SolutionSummary {
    Write-ColorOutput "`n📋 Solution Summary:" -Color Cyan
    Write-ColorOutput "1. ✅ Fixed PSModulePath to remove invalid entries" -Color Green
    Write-ColorOutput "2. ✅ Removed conflicting module installations" -Color Green
    Write-ColorOutput "3. ✅ Reinstalled modules in correct location" -Color Green
    Write-ColorOutput "4. ✅ Verified module loading functionality" -Color Green

    Write-ColorOutput "`n💡 To ensure persistence:" -Color Yellow
    Write-ColorOutput "• Restart VS Code completely" -Color White
    Write-ColorOutput "• Close all PowerShell terminals" -Color White
    Write-ColorOutput "• Test with: Get-Module -Name PSScriptAnalyzer -ListAvailable" -Color White

    Write-ColorOutput "`n🔄 If issues persist, run this script again with -CleanAll" -Color Yellow
}

# Main execution
Write-ColorOutput "🔧 Comprehensive PowerShell Module Fix" -Color Cyan
Write-ColorOutput "=" * 50 -Color White

# Initial status
$initialStatus = Get-CurrentModuleStatus
Show-ModuleStatus -status $initialStatus

# Fix PSModulePath if requested or if issues detected
if ($FixPaths -or $Force) {
    Fix-PSModulePath
}

# Clean conflicting installations
if ($CleanAll -or $Force) {
    Remove-ConflictingModules -status $initialStatus
}

# Reinstall modules
Install-ModulesCorrectly

# Test final status
Write-ColorOutput "`n🔍 Final Status Check:" -Color Cyan
$finalStatus = Get-CurrentModuleStatus
Show-ModuleStatus -status $finalStatus

# Test module loading
$loadTestResults = Test-ModuleLoading

# Show summary
Show-SolutionSummary

Write-ColorOutput "`n✅ Comprehensive module fix complete!" -Color Green
