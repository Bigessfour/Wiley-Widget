# Gitleaks Setup and Troubleshooting Script
# This script helps diagnose and fix gitleaks installation issues

param(
    [switch]$Diagnose,
    [switch]$Fix,
    [switch]$Test,
    [switch]$Alternative,
    [switch]$Help
)

$ErrorActionPreference = "Stop"

function Show-Help {
    Write-Output "Gitleaks Setup and Troubleshooting Script"
    Write-Output "========================================"
    Write-Output ""
    Write-Output "Usage: .\setup-gitleaks.ps1 [options]"
    Write-Output ""
    Write-Output "Options:"
    Write-Output "  -Diagnose    Run comprehensive diagnostics"
    Write-Output "  -Fix         Attempt to fix installation issues"
    Write-Output "  -Test        Test gitleaks functionality"
    Write-Output "  -Alternative Configure alternative security scanning"
    Write-Output "  -Help        Show this help message"
    Write-Output ""
    Write-Output "Examples:"
    Write-Output "  .\setup-gitleaks.ps1 -Diagnose"
    Write-Output "  .\setup-gitleaks.ps1 -Fix"
    Write-Output "  .\setup-gitleaks.ps1 -Alternative"
}

function Test-GoInstallation {
    Write-Output "🔍 Checking Go installation..."

    try {
        $goVersion = go version
        Write-Output "✅ Go is installed: $goVersion"

        # Check GOPATH and GOROOT
        $goPath = go env GOPATH
        $goRoot = go env GOROOT

        Write-Output "📁 GOPATH: $goPath"
        Write-Output "📁 GOROOT: $goRoot"

        # Check if paths exist
        if (Test-Path $goPath) {
            Write-Output "✅ GOPATH directory exists"
        } else {
            Write-Output "⚠️ GOPATH directory does not exist"
        }

        if (Test-Path $goRoot) {
            Write-Output "✅ GOROOT directory exists"
        } else {
            Write-Output "⚠️ GOROOT directory exists"
        }

        return $true
    }
    catch {
        Write-Output "❌ Go is not installed or not in PATH"
        Write-Output "💡 Install Go from: https://golang.org/dl/"
        return $false
    }
}

function Test-TrunkInstallation {
    Write-Output "🔍 Checking Trunk installation..."

    try {
        $trunkVersion = trunk --version
        Write-Output "✅ Trunk is installed: $trunkVersion"
        return $true
    }
    catch {
        Write-Output "❌ Trunk is not installed or not in PATH"
        Write-Output "💡 Install Trunk from: https://docs.trunk.io/cli"
        return $false
    }
}

function Test-GitleaksInstallation {
    Write-Output "🔍 Checking Gitleaks installation..."

    try {
        # Try to run gitleaks directly
        $gitleaksVersion = gitleaks version
        Write-Output "✅ Gitleaks is installed: $gitleaksVersion"
        return $true
    }
    catch {
        Write-Output "❌ Gitleaks is not installed or not in PATH"
        return $false
    }
}

function Test-TrunkGitleaks {
    Write-Output "🔍 Testing Trunk Gitleaks integration..."

    try {
        # Try to run trunk with gitleaks
        $result = trunk check --filter=gitleaks --verbose 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Output "✅ Trunk Gitleaks integration is working"
            return $true
        } else {
            Write-Output "❌ Trunk Gitleaks integration failed"
            Write-Output "Error output: $result"
            return $false
        }
    }
    catch {
        Write-Output "❌ Failed to test Trunk Gitleaks integration: $($_.Exception.Message)"
        return $false
    }
}

function Install-GitleaksManually {
    Write-Output "🔧 Attempting manual Gitleaks installation..."

    # Check if Go is available
    if (-not (Test-GoInstallation)) {
        Write-Output "❌ Cannot install Gitleaks without Go"
        return $false
    }

    try {
        # Install gitleaks using Go
        Write-Output "📦 Installing gitleaks via Go..."
        go install github.com/gitleaks/gitleaks/v8@latest

        # Check if installation was successful
        if (Test-GitleaksInstallation) {
            Write-Output "✅ Gitleaks installed successfully"
            return $true
        } else {
            Write-Output "❌ Gitleaks installation failed"
            return $false
        }
    }
    catch {
        Write-Output "❌ Failed to install Gitleaks: $($_.Exception.Message)"
        return $false
    }
}

function Configure-TrufflehogAlternative {
    Write-Output "🔄 Configuring Trufflehog as alternative to Gitleaks..."

    $trunkYamlPath = ".trunk\trunk.yaml"

    if (-not (Test-Path $trunkYamlPath)) {
        Write-Output "❌ Trunk configuration file not found"
        return $false
    }

    try {
        $content = Get-Content $trunkYamlPath -Raw

        # Comment out gitleaks and uncomment trufflehog
        $content = $content -replace '(?m)^(\s*)- gitleaks@.*$', '#$1- gitleaks@8.28.0:' -replace '(?m)^(\s*)#\s*- trufflehog@.*$', '$1- trufflehog@3.90.5'

        # Write back to file
        $content | Out-File $trunkYamlPath -Encoding UTF8 -Force

        Write-Output "✅ Configured Trufflehog as alternative"
        Write-Output "💡 Run 'trunk check --filter=trufflehog' to test"
        return $true
    }
    catch {
        Write-Output "❌ Failed to configure Trufflehog: $($_.Exception.Message)"
        return $false
    }
}

function Create-GitleaksConfig {
    Write-Output "📝 Creating Gitleaks configuration..."

    $configPath = ".trunk\configs\.gitleaks.toml"

    if (Test-Path $configPath) {
        Write-Output "✅ Gitleaks configuration already exists"
        return $true
    }

    try {
        # Create basic configuration
        $config = @"
# Basic Gitleaks Configuration
title = "Wiley Widget Security Scan"

[allowlist]
paths = [
    "^\\\\.trunk/",
    "^bin/",
    "^obj/",
    "^TestResults/"
]

# Use default rules
extend.useDefaultRules = true
"@

        $config | Out-File $configPath -Encoding UTF8 -Force
        Write-Output "✅ Created basic Gitleaks configuration"
        return $true
    }
    catch {
        Write-Output "❌ Failed to create Gitleaks configuration: $($_.Exception.Message)"
        return $false
    }
}

function Run-Diagnostics {
    Write-Output "🔍 Running comprehensive diagnostics..."
    Write-Output "====================================="

    $results = @{}

    # Test each component
    $results["Go"] = Test-GoInstallation
    $results["Trunk"] = Test-TrunkInstallation
    $results["Gitleaks"] = Test-GitleaksInstallation
    $results["TrunkGitleaks"] = Test-TrunkGitleaks

    # Summary
    Write-Output ""
    Write-Output "📊 Diagnostics Summary:"
    foreach ($component in $results.Keys) {
        $status = if ($results[$component]) { "✅ PASS" } else { "❌ FAIL" }
        Write-Output "  $component`: $status"
    }

    # Recommendations
    Write-Output ""
    Write-Output "💡 Recommendations:"

    if (-not $results["Go"]) {
        Write-Output "  - Install Go: https://golang.org/dl/"
    }

    if (-not $results["Gitleaks"]) {
        Write-Output "  - Install Gitleaks: go install github.com/gitleaks/gitleaks/v8@latest"
    }

    if (-not $results["TrunkGitleaks"]) {
        Write-Output "  - Try alternative: .\setup-gitleaks.ps1 -Alternative"
        Write-Output "  - Or fix manually: .\setup-gitleaks.ps1 -Fix"
    }

    return $results
}

function Test-GitleaksFunctionality {
    Write-Output "🧪 Testing Gitleaks functionality..."

    # Create a test file with a fake secret
    $testFile = "test-secret.txt"
    $testContent = @"
# Test file with fake secret (should be detected)
API_KEY=sk-test12345678901234567890123456789012
DATABASE_PASSWORD=mySecretPassword123!
"@

    try {
        $testContent | Out-File $testFile -Encoding UTF8 -Force

        # Test gitleaks on the test file
        if (Test-GitleaksInstallation) {
            Write-Output "🔍 Running gitleaks on test file..."
            $result = gitleaks detect --verbose --redact --config .trunk/configs/.gitleaks.toml --path $testFile 2>&1

            if ($LASTEXITCODE -eq 0) {
                Write-Output "✅ Gitleaks detected secrets successfully"
            } else {
                Write-Output "⚠️ Gitleaks completed with warnings"
                Write-Output "Output: $result"
            }
        } else {
            Write-Output "❌ Cannot test Gitleaks functionality - not installed"
        }

        # Clean up
        if (Test-Path $testFile) {
            Remove-Item $testFile -Force
        }

        return $true
    }
    catch {
        Write-Output "❌ Failed to test Gitleaks functionality: $($_.Exception.Message)"
        return $false
    }
}

# Main execution logic
if ($Help) {
    Show-Help
    exit 0
}

$actions = @()

if ($Diagnose) { $actions += "Diagnose" }
if ($Fix) { $actions += "Fix" }
if ($Test) { $actions += "Test" }
if ($Alternative) { $actions += "Alternative" }

if ($actions.Count -eq 0) {
    Write-Output "No actions specified. Use -Help for usage information."
    exit 1
}

Write-Output "🔧 Gitleaks Setup and Troubleshooting"
Write-Output "===================================="

foreach ($action in $actions) {
    try {
        switch ($action) {
            "Diagnose" {
                Write-Output ""
                Write-Output "📋 Running Diagnostics..."
                Run-Diagnostics | Out-Null
            }
            "Fix" {
                Write-Output ""
                Write-Output "🔧 Attempting Fixes..."

                # Create configuration if it doesn't exist
                Create-GitleaksConfig

                # Try manual installation
                if (-not (Test-GitleaksInstallation)) {
                    Install-GitleaksManually
                } else {
                    Write-Output "✅ Gitleaks is already installed"
                }

                # Test the integration
                Test-TrunkGitleaks
            }
            "Test" {
                Write-Output ""
                Write-Output "🧪 Testing Gitleaks..."
                Test-GitleaksFunctionality
            }
            "Alternative" {
                Write-Output ""
                Write-Output "🔄 Configuring Alternative..."
                Configure-TrufflehogAlternative
            }
        }
    }
    catch {
        Write-Output "❌ Failed to execute $action`: $($_.Exception.Message)"
    }
}

Write-Output ""
Write-Output "🎉 Gitleaks setup completed!"
