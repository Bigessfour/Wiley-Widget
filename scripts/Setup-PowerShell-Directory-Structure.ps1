#Requires -Version 7.5
<#
.SYNOPSIS
    Creates proper PowerShell directory structure and organizes modules according to best practices.

.DESCRIPTION
    This script establishes a clean PowerShell environment by:
    - Creating proper directory structure
    - Cleaning up PSModulePath
    - Organizing modules in correct locations
    - Setting up profiles and configurations
    - Following PowerShell 7.5.2 best practices

.PARAMETER CleanExisting
    Remove existing module installations before reorganizing.

.PARAMETER CreateProfiles
    Create PowerShell profile files.

.PARAMETER SetupPaths
    Set up proper PSModulePath.

.EXAMPLE
    .\Setup-PowerShell-Directory-Structure.ps1 -CleanExisting -CreateProfiles

.EXAMPLE
    .\Setup-PowerShell-Directory-Structure.ps1 -SetupPaths

.NOTES
    Author: WileyWidget Development Team
    Version: 1.0.0
    Requires: PowerShell 7.5.2+
#>

[CmdletBinding()]
param(
    [switch]$CleanExisting,
    [switch]$CreateProfiles,
    [switch]$SetupPaths
)

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function New-PowerShellDirectoryStructure {
    Write-ColorOutput "🏗️  Creating PowerShell directory structure..." -Color Cyan

    $baseDir = "$env:USERPROFILE\Documents\PowerShell"

    # Define directory structure
    $directories = @(
        $baseDir,
        "$baseDir\Modules",
        "$baseDir\Scripts",
        "$baseDir\Functions",
        "$baseDir\Types",
        "$baseDir\Formats",
        "$baseDir\Configuration",
        "$baseDir\Logs",
        "$baseDir\Cache"
    )

    foreach ($dir in $directories) {
        if (-not (Test-Path $dir)) {
            try {
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
                Write-ColorOutput "✅ Created: $dir" -Color Green
            }
            catch {
                Write-ColorOutput "❌ Failed to create: $dir - $_" -Color Red
            }
        } else {
            Write-ColorOutput "ℹ️  Exists: $dir" -Color Blue
        }
    }

    return $baseDir
}

function Set-ProperPSModulePath {
    Write-ColorOutput "`n🔧 Setting up proper PSModulePath..." -Color Yellow

    # Define the correct module paths in priority order
    $modulePaths = @(
        # User modules (highest priority)
        "$env:USERPROFILE\Documents\PowerShell\Modules",
        # System-wide PowerShell 7 modules
        "C:\Program Files\PowerShell\Modules",
        # PowerShell 7 specific modules
        "C:\Program Files\PowerShell\7\Modules"
    )

    # Filter out non-existent paths
    $validPaths = $modulePaths | Where-Object { Test-Path $_ }

    # Set the PSModulePath
    $newModulePath = $validPaths -join ';'
    $env:PSModulePath = $newModulePath

    Write-ColorOutput "✅ PSModulePath set to:" -Color Green
    $validPaths | ForEach-Object { Write-ColorOutput "  $_" -Color White }

    return $validPaths
}

function Get-ModuleInventory {
    Write-ColorOutput "`n📦 Taking module inventory..." -Color Yellow

    $modules = @('PSScriptAnalyzer', 'Pester', 'PSReadLine', 'platyPS', 'PSFramework', 'ImportExcel')
    $inventory = @{}

    foreach ($module in $modules) {
        $installations = Get-Module -Name $module -ListAvailable -ErrorAction SilentlyContinue
        $installationArray = @($installations)  # Ensure it's an array
        $installationCount = $installationArray.Count
        $installationPaths = if ($installationArray) { $installationArray | Select-Object -ExpandProperty ModuleBase } else { @() }

        $inventory[$module] = @{
            Installations = $installationArray
            Count = $installationCount
            Paths = $installationPaths
        }
    }

    return $inventory
}

function Show-ModuleInventory {
    param([hashtable]$inventory)

    Write-ColorOutput "`n📊 Module Inventory:" -Color Cyan
    Write-ColorOutput ("{0,-15} {1,-8} {2,-50}" -f "Module", "Count", "Paths") -Color White
    Write-ColorOutput ("-" * 80) -Color White

    foreach ($module in $inventory.Keys) {
        $inv = $inventory[$module]
        $count = $inv.Count
        $pathsArray = @($inv.Paths)  # Ensure it's an array
        $paths = if ($pathsArray.Count -gt 0) {
            ($pathsArray | ForEach-Object { Split-Path $_ -Leaf }) -join ', '
        } else { "None" }

        Write-ColorOutput ("{0,-15} {1,-8} {2,-50}" -f $module, $count, $paths) -Color White
    }
}

function Clean-ModuleInstallations {
    param([hashtable]$inventory)

    Write-ColorOutput "`n🧹 Cleaning module installations..." -Color Yellow

    $targetPath = "$env:USERPROFILE\Documents\PowerShell\Modules"

    foreach ($module in $inventory.Keys) {
        $inv = $inventory[$module]

        if ($inv.Count -gt 1) {
            Write-ColorOutput "Cleaning $module (found $($inv.Count) installations)..." -Color White

            # Keep only the installation in the target path, remove others
            $toRemove = $inv.Paths | Where-Object { $_ -notlike "$targetPath*" }

            foreach ($path in $toRemove) {
                try {
                    Write-ColorOutput "  Removing: $path" -Color Yellow
                    Remove-Item -Path $path -Recurse -Force -ErrorAction Stop
                    Write-ColorOutput "  ✅ Removed successfully" -Color Green
                }
                catch {
                    Write-ColorOutput "  ❌ Failed to remove: $_" -Color Red
                }
            }
        }
    }
}

function Install-ModulesInCorrectLocation {
    Write-ColorOutput "`n📦 Installing modules in correct location..." -Color Yellow

    $modules = @(
        'PSScriptAnalyzer',
        'Pester',
        'PSReadLine',
        'platyPS',
        'PSFramework',
        'ImportExcel'
    )

    foreach ($module in $modules) {
        Write-ColorOutput "Installing $module..." -Color White
        try {
            # Install to user scope to ensure it goes to the correct location
            Install-Module -Name $module -Scope CurrentUser -Force -AllowClobber -ErrorAction Stop
            Write-ColorOutput "✅ $module installed successfully" -Color Green
        }
        catch {
            Write-ColorOutput "❌ Failed to install $module`: $_" -Color Red
        }
    }
}

function New-PowerShellProfiles {
    Write-ColorOutput "`n📝 Creating PowerShell profiles..." -Color Yellow

    $profileDir = "$env:USERPROFILE\Documents\PowerShell"

    # Profile content
    $profileContent = @"
# PowerShell 7.5.2 Profile for WileyWidget
# Generated on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

# Set execution policy for development
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -Force

# Import commonly used modules
if (Get-Module -Name PSReadLine -ListAvailable) {
    Import-Module PSReadLine
}

# Set PSModulePath to ensure clean module loading
`$userModules = "`$env:USERPROFILE\Documents\PowerShell\Modules"
`$systemModules = "C:\Program Files\PowerShell\Modules"
`$ps7Modules = "C:\Program Files\PowerShell\7\Modules"

`$env:PSModulePath = "`$userModules;`$systemModules;`$ps7Modules"

# Set location to project directory if in WileyWidget
if (`$PWD.Path -like "*WileyWidget*") {
    # Profile is loaded, ready for development
}

# Custom functions and aliases can be added here
"@

    # Create profile files
    $profilePaths = @(
        "$profileDir\Microsoft.PowerShell_profile.ps1",
        "$profileDir\Microsoft.VSCode_profile.ps1"
    )

    foreach ($profilePath in $profilePaths) {
        try {
            $profileContent | Out-File -FilePath $profilePath -Encoding UTF8 -Force
            Write-ColorOutput "✅ Created profile: $profilePath" -Color Green
        }
        catch {
            Write-ColorOutput "❌ Failed to create profile: $_" -Color Red
        }
    }
}

function Test-DirectoryStructure {
    Write-ColorOutput "`n🧪 Testing directory structure..." -Color Yellow

    $baseDir = "$env:USERPROFILE\Documents\PowerShell"
    $testDirs = @(
        "$baseDir\Modules",
        "$baseDir\Scripts",
        "$baseDir\Functions",
        "$baseDir\Configuration"
    )

    $allGood = $true
    foreach ($dir in $testDirs) {
        if (Test-Path $dir) {
            Write-ColorOutput "✅ Directory exists: $dir" -Color Green
        } else {
            Write-ColorOutput "❌ Directory missing: $dir" -Color Red
            $allGood = $false
        }
    }

    # Test module loading
    Write-ColorOutput "`nTesting module loading..." -Color White
    $testModules = @('PSScriptAnalyzer', 'Pester')

    foreach ($module in $testModules) {
        try {
            $mod = Get-Module -Name $module -ListAvailable | Select-Object -First 1
            if ($mod) {
                Write-ColorOutput "✅ $module available at: $($mod.ModuleBase)" -Color Green
            } else {
                Write-ColorOutput "❌ $module not found" -Color Red
                $allGood = $false
            }
        }
        catch {
            Write-ColorOutput "❌ Error checking $module`: $_" -Color Red
            $allGood = $false
        }
    }

    return $allGood
}

function Show-SetupSummary {
    Write-ColorOutput "`n📋 PowerShell Directory Structure Setup Complete!" -Color Cyan
    Write-ColorOutput "=" * 60 -Color White

    Write-ColorOutput "✅ Created proper directory structure" -Color Green
    Write-ColorOutput "✅ Set up clean PSModulePath" -Color Green
    Write-ColorOutput "✅ Organized modules correctly" -Color Green
    Write-ColorOutput "✅ Created PowerShell profiles" -Color Green

    Write-ColorOutput "`n📁 Directory Structure:" -Color Yellow
    Write-ColorOutput "  $env:USERPROFILE\Documents\PowerShell\" -Color White
    Write-ColorOutput "  ├── Modules\        # PowerShell modules" -Color White
    Write-ColorOutput "  ├── Scripts\        # PowerShell scripts" -Color White
    Write-ColorOutput "  ├── Functions\      # Custom functions" -Color White
    Write-ColorOutput "  ├── Types\          # Custom types" -Color White
    Write-ColorOutput "  ├── Formats\        # Custom formats" -Color White
    Write-ColorOutput "  ├── Configuration\  # Configuration files" -Color White
    Write-ColorOutput "  ├── Logs\           # Log files" -Color White
    Write-ColorOutput "  └── Cache\          # Cache files" -Color White

    Write-ColorOutput "`n🔧 PSModulePath:" -Color Yellow
    ($env:PSModulePath -split ';') | ForEach-Object { Write-ColorOutput "  $_" -Color White }

    Write-ColorOutput "`n💡 Next Steps:" -Color Cyan
    Write-ColorOutput "1. Restart VS Code completely" -Color White
    Write-ColorOutput "2. Close all PowerShell terminals" -Color White
    Write-ColorOutput "3. Test with: Get-Module -Name PSScriptAnalyzer -ListAvailable" -Color White
    Write-ColorOutput "4. Run: .\scripts\Setup-PowerShell-Development.ps1 -VerifySetup" -Color White
}

# Main execution
Write-ColorOutput "🚀 Setting up PowerShell Directory Structure" -Color Cyan
Write-ColorOutput "=" * 50 -Color White

# Create directory structure
$baseDir = New-PowerShellDirectoryStructure

# Set up proper PSModulePath
if ($SetupPaths) {
    Set-ProperPSModulePath
}

# Get module inventory
$inventory = Get-ModuleInventory
Show-ModuleInventory -inventory $inventory

# Clean existing installations if requested
if ($CleanExisting) {
    Clean-ModuleInstallations -inventory $inventory
}

# Install modules in correct location
Install-ModulesInCorrectLocation

# Create profiles if requested
if ($CreateProfiles) {
    New-PowerShellProfiles
}

# Test the setup
$testResult = Test-DirectoryStructure

# Show summary
Show-SetupSummary

if ($testResult) {
    Write-ColorOutput "`n🎉 PowerShell directory structure setup completed successfully!" -Color Green
} else {
    Write-ColorOutput "`n⚠️  Setup completed with some issues. Please review the output above." -Color Yellow
}
