#Requires -Version 7.5
<#
.SYNOPSIS
    Resolves PSReadLine module conflicts and updates PowerShell modules.

.DESCRIPTION
    This script helps resolve module conflicts, especially PSReadLine, by properly
    unloading modules and updating them safely.

.PARAMETER Force
    Force close PowerShell sessions if needed.

.PARAMETER SkipPSReadLine
    Skip PSReadLine update to avoid conflicts.

.EXAMPLE
    .\Fix-PowerShell-Modules.ps1

.EXAMPLE
    .\Fix-PowerShell-Modules.ps1 -SkipPSReadLine

.NOTES
    Author: WileyWidget Development Team
    Version: 1.0.0
    Requires: PowerShell 7.5.2+
#>

[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$SkipPSReadLine
)

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Get-ModuleStatus {
    Write-ColorOutput "🔍 Checking module status..." -Color Yellow

    $modulesToCheck = @('PSReadLine', 'PSScriptAnalyzer', 'Pester')
    $status = @{}

    foreach ($module in $modulesToCheck) {
        $loaded = Get-Module -Name $module -ErrorAction SilentlyContinue
        $installed = Get-Module -Name $module -ListAvailable -ErrorAction SilentlyContinue

        $status[$module] = @{
            Loaded = $null -ne $loaded
            Installed = $null -ne $installed
            Version = if ($installed) { ($installed | Sort-Object Version -Descending | Select-Object -First 1).Version } else { $null }
        }
    }

    return $status
}

function Show-ModuleStatus {
    param([hashtable]$status)

    Write-ColorOutput "`n📊 Module Status:" -Color Cyan
    Write-ColorOutput ("{0,-15} {1,-10} {2,-10} {3,-15}" -f "Module", "Loaded", "Installed", "Version") -Color White
    Write-ColorOutput ("-" * 60) -Color White

    foreach ($module in $status.Keys) {
        $loaded = if ($status[$module].Loaded) { "✅" } else { "❌" }
        $installed = if ($status[$module].Installed) { "✅" } else { "❌" }
        $version = $status[$module].Version ?? "N/A"

        Write-ColorOutput ("{0,-15} {1,-10} {2,-10} {3,-15}" -f $module, $loaded, $installed, $version) -Color White
    }
}

function Unload-ConflictingModules {
    Write-ColorOutput "`n🔄 Unloading conflicting modules..." -Color Yellow

    $modulesToUnload = @('PSReadLine')

    foreach ($module in $modulesToUnload) {
        $loaded = Get-Module -Name $module -ErrorAction SilentlyContinue
        if ($loaded) {
            try {
                Remove-Module -Name $module -Force -ErrorAction Stop
                Write-ColorOutput "✅ Unloaded $module" -Color Green
            }
            catch {
                Write-ColorOutput "❌ Failed to unload $module`: $_" -Color Red
                return $false
            }
        } else {
            Write-ColorOutput "ℹ️  $module not currently loaded" -Color Blue
        }
    }

    return $true
}

function Update-PowerShellModules {
    param([switch]$SkipPSReadLine)

    Write-ColorOutput "`n📦 Updating PowerShell modules..." -Color Yellow

    $modules = @(
        'PSScriptAnalyzer',
        'Pester',
        'platyPS',
        'PSFramework',
        'ImportExcel'
    )

    if (-not $SkipPSReadLine) {
        $modules += 'PSReadLine'
    }

    foreach ($module in $modules) {
        Write-ColorOutput "Updating $module..." -Color White
        try {
            Install-Module -Name $module -Force -AllowClobber -Scope CurrentUser -ErrorAction Stop
            Write-ColorOutput "✅ $module updated successfully" -Color Green
        }
        catch {
            Write-ColorOutput "❌ Failed to update $module`: $_" -Color Red
        }
    }
}

function Show-ResolutionSteps {
    Write-ColorOutput "`n🛠️  PSReadLine Resolution Steps:" -Color Cyan
    Write-ColorOutput "1. Close ALL PowerShell terminals and VS Code windows" -Color White
    Write-ColorOutput "2. Open Task Manager (Ctrl+Shift+Esc)" -Color White
    Write-ColorOutput "3. End any remaining 'pwsh.exe' or 'powershell.exe' processes" -Color White
    Write-ColorOutput "4. Restart VS Code" -Color White
    Write-ColorOutput "5. Run this script again: .\Fix-PowerShell-Modules.ps1" -Color White

    Write-ColorOutput "`n💡 Alternative: Use -SkipPSReadLine to update other modules first" -Color Yellow
    Write-ColorOutput "   .\Fix-PowerShell-Modules.ps1 -SkipPSReadLine" -Color White
}

# Main execution
Write-ColorOutput "🔧 PowerShell Module Conflict Resolver" -Color Cyan
Write-ColorOutput "=" * 50 -Color White

# Check current status
$initialStatus = Get-ModuleStatus
Show-ModuleStatus -status $initialStatus

# Check for conflicts
$psReadLineLoaded = $initialStatus['PSReadLine'].Loaded
if ($psReadLineLoaded) {
    Write-ColorOutput "`n⚠️  PSReadLine module conflict detected!" -Color Yellow

    if ($Force) {
        Write-ColorOutput "🔄 Force mode enabled. Attempting to unload modules..." -Color Yellow
        $unloaded = Unload-ConflictingModules

        if ($unloaded) {
            Update-PowerShellModules -SkipPSReadLine:$SkipPSReadLine
        } else {
            Write-ColorOutput "❌ Could not unload conflicting modules. Manual intervention required." -Color Red
            Show-ResolutionSteps
            exit 1
        }
    } else {
        Write-ColorOutput "💡 To resolve this:" -Color Cyan
        Write-ColorOutput "   Option 1: Close all PowerShell sessions and run again" -Color White
        Write-ColorOutput "   Option 2: Run with -Force flag (may require restart)" -Color White
        Write-ColorOutput "   Option 3: Run with -SkipPSReadLine to update other modules" -Color White

        Show-ResolutionSteps
        exit 0
    }
} else {
    Write-ColorOutput "`n✅ No module conflicts detected. Updating modules..." -Color Green
    Update-PowerShellModules -SkipPSReadLine:$SkipPSReadLine
}

# Final status check
Write-ColorOutput "`n🔍 Final Status Check:" -Color Cyan
$finalStatus = Get-ModuleStatus
Show-ModuleStatus -status $finalStatus

Write-ColorOutput "`n✅ Module update process complete!" -Color Green

if ($psReadLineLoaded -and -not $Force) {
    Write-ColorOutput "`n💡 Note: PSReadLine was not updated due to conflict." -Color Yellow
    Write-ColorOutput "   Follow the resolution steps above to update it." -Color Yellow
}
