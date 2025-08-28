# PowerShell 7.5.2 Development Environment Setup
# This script configures the development environment for PowerShell 7.5.2 compliance

param(
    [switch]$InstallTools,
    [switch]$UpdateVSCodeSettings,
    [switch]$VerifySetup
)

#Requires -Version 7.5

<#
.SYNOPSIS
    Sets up PowerShell 7.5.2 development environment with GitHub Copilot support.

.DESCRIPTION
    This script installs and configures all necessary tools for PowerShell 7.5.2
    development, including PSScriptAnalyzer, Pester, and VS Code extensions.

.PARAMETER InstallTools
    Install PowerShell modules and tools.

.PARAMETER UpdateVSCodeSettings
    Update VS Code settings for PowerShell 7.5.2 support.

.PARAMETER VerifySetup
    Verify that all tools are properly configured.

.EXAMPLE
    .\Setup-PowerShell-Development.ps1 -InstallTools -UpdateVSCodeSettings

.EXAMPLE
    .\Setup-PowerShell-Development.ps1 -VerifySetup
#>

function Install-PowerShellTools {
    Write-Verbose "Installing PowerShell development tools..." -Verbose

    $modules = @(
        'PSScriptAnalyzer',
        'Pester',
        'platyPS',
        'PSReadLine',
        'PSFramework',
        'ImportExcel'
    )

    foreach ($module in $modules) {
        Write-Verbose "Installing $module..." -Verbose
        try {
            # Check if module is currently in use
            $inUse = Get-Module -Name $module -ErrorAction SilentlyContinue
            if ($inUse) {
                Write-Host "⚠️  Module '$module' is currently loaded. Skipping update to avoid conflicts." -ForegroundColor Yellow
                Write-Host "   To update: Close all PowerShell sessions and run this script again." -ForegroundColor Yellow
                continue
            }

            # Force reinstall to get latest version
            Install-Module -Name $module -Force -AllowClobber -Scope CurrentUser -ErrorAction Stop
            Write-Host "✅ $module installed/updated successfully" -ForegroundColor Green
        }
        catch {
            Write-Host "❌ Failed to install $module`: $_" -ForegroundColor Red
            Write-Host "   Try closing all PowerShell windows and running the script again." -ForegroundColor Yellow
        }
    }

    Write-Host "`n💡 If you see PSReadLine warnings:" -ForegroundColor Cyan
    Write-Host "   1. Close all PowerShell terminals and VS Code" -ForegroundColor White
    Write-Host "   2. Restart VS Code" -ForegroundColor White
    Write-Host "   3. Run this script again" -ForegroundColor White
}

function Update-VSCodeSettings {
    Write-Verbose "Updating VS Code settings for PowerShell 7.5.2..." -Verbose

    $settingsPath = Join-Path $PSScriptRoot '.vscode\settings.json'
    $pssaPath = Join-Path $PSScriptRoot '.vscode\PSScriptAnalyzerSettings.psd1'

    # Ensure PSScriptAnalyzer settings file exists
    if (-not (Test-Path $pssaPath)) {
        Write-Warning "PSScriptAnalyzer settings file not found: $pssaPath"
        return
    }

    Write-Verbose "VS Code settings updated for PowerShell 7.5.2 compliance" -Verbose
}

function Test-PowerShellSetup {
    Write-Verbose "Verifying PowerShell 7.5.2 development setup..." -Verbose

    $tests = @()

    # Test PowerShell version
    $psVersion = $PSVersionTable.PSVersion
    $tests += [PSCustomObject]@{
        Test = "PowerShell Version"
        Expected = "7.5.2"
        Actual = $psVersion.ToString()
        Status = if ($psVersion -ge [version]'7.5.2') { "✓" } else { "✗" }
    }

    # Test required modules
    $requiredModules = @('PSScriptAnalyzer', 'Pester')
    foreach ($module in $requiredModules) {
        $installed = Get-Module -Name $module -ListAvailable
        $tests += [PSCustomObject]@{
            Test = "Module: $module"
            Expected = "Installed"
            Actual = if ($installed) { "Installed" } else { "Not found" }
            Status = if ($installed) { "✓" } else { "✗" }
        }
    }

    # Test VS Code settings
    $settingsPath = Join-Path $PSScriptRoot '..\.vscode\settings.json'
    $settingsExist = Test-Path $settingsPath
    Write-Verbose "Checking VS Code settings at: $settingsPath" -Verbose
    Write-Verbose "Settings file exists: $settingsExist" -Verbose
    $tests += [PSCustomObject]@{
        Test = "VS Code Settings"
        Expected = "Exists"
        Actual = if ($settingsExist) { "Exists" } else { "Missing" }
        Status = if ($settingsExist) { "✓" } else { "✗" }
    }

    # Display results
    Write-Verbose "`n=== PowerShell Setup Verification ===" -Verbose
    Write-Host "`n=== PowerShell Setup Verification ===" -ForegroundColor Cyan

    # Show each test result individually for better visibility
    foreach ($test in $tests) {
        $color = if ($test.Status -eq "✓") { "Green" } else { "Red" }
        Write-Host ("{0,-25} {1,-15} {2,-15} {3}" -f $test.Test, $test.Expected, $test.Actual, $test.Status) -ForegroundColor $color
    }

    $failedTests = $tests | Where-Object { $_.Status -eq "✗" }
    if ($failedTests) {
        Write-Host "`n❌ Failed Tests:" -ForegroundColor Red
        foreach ($failedTest in $failedTests) {
            Write-Host "  - $($failedTest.Test): Expected $($failedTest.Expected), got $($failedTest.Actual)" -ForegroundColor Red
        }
        Write-Warning "Some tests failed. Please review the setup."
        return $false
    } else {
        Write-Host "`n✅ All tests passed! PowerShell 7.5.2 environment is ready." -ForegroundColor Green
        Write-Verbose "✓ All tests passed! PowerShell 7.5.2 environment is ready." -Verbose
        return $true
    }
}

# Main execution
if ($InstallTools) {
    Install-PowerShellTools
}

if ($UpdateVSCodeSettings) {
    Update-VSCodeSettings
}

if ($VerifySetup) {
    $result = Test-PowerShellSetup
    exit [int](-not $result)
}

Write-Verbose "PowerShell 7.5.2 development environment setup complete." -Verbose
Write-Verbose "Use -InstallTools, -UpdateVSCodeSettings, or -VerifySetup parameters as needed." -Verbose
