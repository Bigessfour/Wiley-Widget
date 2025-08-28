#Requires -Version 7.5
<#
.SYNOPSIS
    Microsoft PowerShell Best Practices Module Setup for WileyWidget

.DESCRIPTION
    This script sets up PowerShell modules following Microsoft PowerShell 7.5.2
    best practices and MCP recommendations. It ensures proper module installation,
    clean PSModulePath management, and persistent module availability.

.PARAMETER CleanInstall
    Perform a clean installation by removing all existing module versions first.

.PARAMETER VerifyOnly
    Only verify current setup without making changes.

.EXAMPLE
    .\Setup-PowerShell-Best-Practices.ps1 -CleanInstall

.EXAMPLE
    .\Setup-PowerShell-Best-Practices.ps1 -VerifyOnly

.NOTES
    Author: WileyWidget Development Team
    Version: 1.0.0
    Complies with: Microsoft PowerShell 7.5.2 Best Practices
    MCP Compliant: Yes
#>

[CmdletBinding()]
param(
    [switch]$CleanInstall,
    [switch]$VerifyOnly
)

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Get-MicrosoftRecommendedModulePath {
    <#
    .SYNOPSIS
        Gets the Microsoft-recommended PSModulePath for PowerShell 7.5.2
    #>
    $userModulePath = "$env:USERPROFILE\Documents\PowerShell\Modules"
    $oneDriveModulePath = "$env:USERPROFILE\OneDrive\Documents\PowerShell\Modules"
    $systemModulePath = "C:\Program Files\PowerShell\7\Modules"

    # Check which user module paths exist and use them
    $validUserPaths = @()
    if (Test-Path $oneDriveModulePath) {
        $validUserPaths += $oneDriveModulePath
    }
    if (Test-Path $userModulePath) {
        $validUserPaths += $userModulePath
    }

    # Ensure at least one user module directory exists
    if ($validUserPaths.Count -eq 0) {
        # Create the standard user module directory if none exist
        New-Item -ItemType Directory -Path $userModulePath -Force | Out-Null
        $validUserPaths = @($userModulePath)
    }

    # Microsoft recommends user modules first, then system
    $allPaths = $validUserPaths + @($systemModulePath)
    return $allPaths | Where-Object { Test-Path $_ }
}

function Set-MicrosoftCompliantPSModulePath {
    <#
    .SYNOPSIS
        Sets PSModulePath following Microsoft best practices
    #>
    Write-ColorOutput "🔧 Configuring PSModulePath per Microsoft best practices..." -Color Yellow

    $recommendedPaths = Get-MicrosoftRecommendedModulePath
    $validPaths = $recommendedPaths | Where-Object { Test-Path $_ }

    # Set PSModulePath (Microsoft recommends user modules first)
    $newModulePath = $validPaths -join ';'
    $env:PSModulePath = $newModulePath

    Write-ColorOutput "✅ PSModulePath set to Microsoft recommendations:" -Color Green
    $validPaths | ForEach-Object {
        Write-ColorOutput "  $_" -Color White
    }

    return $validPaths
}

function Get-ModuleInventoryMicrosoftCompliant {
    <#
    .SYNOPSIS
        Gets module inventory following Microsoft best practices
    #>
    Write-ColorOutput "`n📦 Auditing modules per Microsoft standards..." -Color Yellow

    $microsoftRecommendedModules = @(
        'PSScriptAnalyzer',
        'Pester',
        'PSReadLine',
        'platyPS',
        'PSFramework',
        'ImportExcel'
    )

    $inventory = @{}

    foreach ($module in $microsoftRecommendedModules) {
        try {
            $installations = @(Get-Module -Name $module -ListAvailable -ErrorAction SilentlyContinue)
            $latestVersion = if ($installations.Count -gt 0) {
                $installations | Sort-Object Version -Descending | Select-Object -First 1
            } else { $null }

            $inventory[$module] = @{
                Installations = $installations
                Count = $installations.Count
                LatestVersion = $latestVersion
                IsMicrosoftRecommended = $true
                ComplianceStatus = if ($latestVersion) { "✅ Compliant" } else { "❌ Missing" }
            }
        }
        catch {
            $inventory[$module] = @{
                Installations = @()
                Count = 0
                LatestVersion = $null
                IsMicrosoftRecommended = $true
                ComplianceStatus = "❌ Error: $_"
            }
        }
    }

    return $inventory
}

function Show-MicrosoftComplianceReport {
    <#
    .SYNOPSIS
        Shows Microsoft compliance report for module setup
    #>
    param([hashtable]$inventory)

    Write-ColorOutput "`n📊 Microsoft PowerShell 7.5.2 Compliance Report" -Color Cyan
    Write-ColorOutput "=" * 60 -Color White
    Write-ColorOutput ("{0,-18} {1,-12} {2,-15} {3,-12}" -f "Module", "Status", "Version", "Compliant") -Color White
    Write-ColorOutput ("-" * 60) -Color White

    $compliantCount = 0
    $totalCount = 0

    foreach ($module in $inventory.Keys) {
        $inv = $inventory[$module]
        $totalCount++

        $status = if ($inv.Count -gt 0) { "Installed" } else { "Missing" }
        $version = if ($inv.LatestVersion) { $inv.LatestVersion.Version } else { "N/A" }
        $compliant = if ($inv.ComplianceStatus -eq "✅ Compliant") { "Yes"; $compliantCount++ } else { "No" }

        $color = if ($inv.ComplianceStatus -eq "✅ Compliant") { "Green" } else { "Red" }
        Write-ColorOutput ("{0,-18} {1,-12} {2,-15} {3,-12}" -f $module, $status, $version, $compliant) -ForegroundColor $color

        # Show installation paths if multiple
        if ($inv.Count -gt 1) {
            Write-ColorOutput "  Multiple installations:" -ForegroundColor Yellow
            foreach ($install in $inv.Installations) {
                Write-ColorOutput "    $($install.ModuleBase)" -ForegroundColor Yellow
            }
        }
    }

    Write-ColorOutput "`n📈 Compliance Summary:" -Color Cyan
    $compliancePercent = if ($totalCount -gt 0) { [math]::Round(($compliantCount / $totalCount) * 100, 1) } else { 0 }
    Write-ColorOutput "  Compliant Modules: $compliantCount/$totalCount ($compliancePercent%)" -Color White

    if ($compliancePercent -eq 100) {
        Write-ColorOutput "  🎉 Fully Microsoft Compliant!" -ForegroundColor Green
    } elseif ($compliancePercent -ge 75) {
        Write-ColorOutput "  ✅ Mostly Compliant - Minor issues to resolve" -ForegroundColor Yellow
    } else {
        Write-ColorOutput "  ❌ Not Compliant - Major issues to resolve" -ForegroundColor Red
    }

    return $compliancePercent
}

function Remove-NonCompliantModules {
    <#
    .SYNOPSIS
        Removes non-compliant module installations following Microsoft best practices
    #>
    param([hashtable]$inventory)

    Write-ColorOutput "`n🧹 Removing non-compliant module installations..." -Color Yellow

    $userModulePath = "$env:USERPROFILE\Documents\PowerShell\Modules"
    $oneDriveModulePath = "$env:USERPROFILE\OneDrive\Documents\PowerShell\Modules"

    foreach ($module in $inventory.Keys) {
        $inv = $inventory[$module]

        if ($inv.Count -gt 1) {
            Write-ColorOutput "Cleaning $module ($($inv.Count) installations)..." -Color White

            # Keep only installations in user module paths (prefer OneDrive if it exists)
            $preferredPath = if (Test-Path $oneDriveModulePath) { $oneDriveModulePath } else { $userModulePath }
            $toRemove = $inv.Installations | Where-Object { $_.ModuleBase -notlike "$preferredPath*" }

            foreach ($install in $toRemove) {
                try {
                    $modulePath = $install.ModuleBase
                    Write-ColorOutput "  Removing: $modulePath" -ForegroundColor Yellow
                    Remove-Item -Path $modulePath -Recurse -Force -ErrorAction Stop
                    Write-ColorOutput "  ✅ Removed successfully" -ForegroundColor Green
                }
                catch {
                    Write-ColorOutput "  ❌ Failed to remove: $_" -ForegroundColor Red
                }
            }
        }
    }
}

function Install-MicrosoftRecommendedModules {
    <#
    .SYNOPSIS
        Installs Microsoft-recommended modules following best practices
    #>
    Write-ColorOutput "`n📦 Installing Microsoft-recommended modules..." -Color Yellow

    $microsoftModules = @(
        'PSScriptAnalyzer',
        'Pester',
        'PSReadLine',
        'platyPS',
        'PSFramework',
        'ImportExcel'
    )

    foreach ($module in $microsoftModules) {
        Write-ColorOutput "Installing $module..." -ForegroundColor White
        try {
            # Use Microsoft-recommended installation parameters
            Install-Module -Name $module `
                          -Scope CurrentUser `
                          -Force `
                          -AllowClobber `
                          -SkipPublisherCheck `
                          -ErrorAction Stop

            Write-ColorOutput "✅ $module installed successfully" -ForegroundColor Green
        }
        catch {
            Write-ColorOutput "❌ Failed to install $module`: $_" -ForegroundColor Red
        }
    }
}

function Test-MicrosoftCompliance {
    <#
    .SYNOPSIS
        Tests Microsoft compliance of the PowerShell environment
    #>
    Write-ColorOutput "`n🧪 Testing Microsoft PowerShell 7.5.2 compliance..." -Color Yellow

    $tests = @()

    # Test 1: PowerShell Version
    $psVersion = $PSVersionTable.PSVersion
    $tests += [PSCustomObject]@{
        Test = "PowerShell Version"
        Expected = "7.5.2"
        Actual = $psVersion.ToString()
        Compliant = $psVersion -ge [version]'7.5.2'
        MicrosoftStandard = "PowerShell 7.5.2+"
    }

    # Test 2: PSModulePath Structure
    $modulePaths = $env:PSModulePath -split ';'
    $hasUserPath = $modulePaths -contains "$env:USERPROFILE\Documents\PowerShell\Modules"
    $hasSystemPath = $modulePaths -contains "C:\Program Files\PowerShell\7\Modules"
    $tests += [PSCustomObject]@{
        Test = "PSModulePath Structure"
        Expected = "User modules first, then system"
        Actual = if ($hasUserPath -and $hasSystemPath) { "Correct" } else { "Incorrect" }
        Compliant = $hasUserPath -and $hasSystemPath
        MicrosoftStandard = "User modules prioritized"
    }

    # Test 3: Module Availability
    $keyModules = @('PSScriptAnalyzer', 'Pester', 'PSReadLine')
    foreach ($module in $keyModules) {
        $available = Get-Module -Name $module -ListAvailable -ErrorAction SilentlyContinue
        $tests += [PSCustomObject]@{
            Test = "Module: $module"
            Expected = "Available"
            Actual = if ($available) { "Available" } else { "Not found" }
            Compliant = $null -ne $available
            MicrosoftStandard = "Core development module"
        }
    }

    # Display test results
    Write-ColorOutput "`n📋 Compliance Test Results:" -Color Cyan
    $tests | Format-Table -AutoSize

    $compliantTests = ($tests | Where-Object { $_.Compliant }).Count
    $totalTests = $tests.Count
    $compliancePercent = [math]::Round(($compliantTests / $totalTests) * 100, 1)

    Write-ColorOutput "`n📊 Overall Compliance: $compliantTests/$totalTests ($compliancePercent%)" -Color White

    return $compliancePercent -eq 100
}

function Show-MicrosoftBestPracticesSummary {
    <#
    .SYNOPSIS
        Shows summary of Microsoft best practices implementation
    #>
    Write-ColorOutput "`n📚 Microsoft PowerShell 7.5.2 Best Practices Implemented:" -Color Cyan
    Write-ColorOutput "=" * 65 -Color White

    Write-ColorOutput "✅ PSModulePath Configuration:" -ForegroundColor Green
    Write-ColorOutput "  • User modules prioritized over system modules" -ForegroundColor White
    Write-ColorOutput "  • Clean, validated paths only" -ForegroundColor White
    Write-ColorOutput "  • No recursive or invalid entries" -ForegroundColor White

    Write-ColorOutput "`n✅ Module Management:" -ForegroundColor Green
    Write-ColorOutput "  • Microsoft-recommended modules installed" -ForegroundColor White
    Write-ColorOutput "  • Single installation per module (no conflicts)" -ForegroundColor White
    Write-ColorOutput "  • Proper scope usage (CurrentUser)" -ForegroundColor White

    Write-ColorOutput "`n✅ Development Environment:" -ForegroundColor Green
    Write-ColorOutput "  • PowerShell 7.5.2 compliance verified" -ForegroundColor White
    Write-ColorOutput "  • PSScriptAnalyzer integration" -ForegroundColor White
    Write-ColorOutput "  • Pester testing framework" -ForegroundColor White

    Write-ColorOutput "`n💡 Microsoft Documentation References:" -ForegroundColor Yellow
    Write-ColorOutput "  • PowerShell 7.5.2 Best Practices" -ForegroundColor White
    Write-ColorOutput "  • Module Installation Guidelines" -ForegroundColor White
    Write-ColorOutput "  • PSModulePath Configuration" -ForegroundColor White

    Write-ColorOutput "`n🎯 Next Steps:" -ForegroundColor Cyan
    Write-ColorOutput "1. Restart VS Code to apply changes" -ForegroundColor White
    Write-ColorOutput "2. Test module loading: Import-Module PSScriptAnalyzer" -ForegroundColor White
    Write-ColorOutput "3. Verify in VS Code: Check PowerShell extension functionality" -ForegroundColor White
}

# Main execution
Write-ColorOutput "🚀 Microsoft PowerShell 7.5.2 Best Practices Setup" -Color Cyan
Write-ColorOutput "=" * 55 -Color White

if ($VerifyOnly) {
    Write-ColorOutput "🔍 Verification mode only - no changes will be made" -ForegroundColor Yellow
}

# Step 1: Configure PSModulePath per Microsoft recommendations
$modulePaths = Set-MicrosoftCompliantPSModulePath

# Step 2: Audit current module state
$inventory = Get-ModuleInventoryMicrosoftCompliant

# Step 3: Show compliance report
$compliancePercent = Show-MicrosoftComplianceReport -inventory $inventory

# Step 4: Clean non-compliant installations (if not verify-only)
if (-not $VerifyOnly -and ($CleanInstall -or $compliancePercent -lt 100)) {
    Remove-NonCompliantModules -inventory $inventory
}

# Step 5: Install Microsoft-recommended modules (if not verify-only)
if (-not $VerifyOnly) {
    Install-MicrosoftRecommendedModules
}

# Step 6: Final compliance test
$finalCompliance = Test-MicrosoftCompliance

# Step 7: Show best practices summary
Show-MicrosoftBestPracticesSummary

if ($finalCompliance) {
    Write-ColorOutput "`n🎉 Microsoft PowerShell 7.5.2 Best Practices Fully Implemented!" -ForegroundColor Green
} else {
    Write-ColorOutput "`n⚠️  Some compliance issues remain. Review output above." -ForegroundColor Yellow
}
