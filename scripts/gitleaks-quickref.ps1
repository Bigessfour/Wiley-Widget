# Gitleaks Quick Reference and Status Checker

<#
.SYNOPSIS
    Quick reference script for Gitleaks configuration and troubleshooting
.DESCRIPTION
    This script provides quick commands and status checks for Gitleaks setup
.PARAMETER Action
    Action to perform: Status, Test, Fix, SwitchToTrufflehog
.EXAMPLE
    .\gitleaks-quickref.ps1 -Action Status
.EXAMPLE
    .\gitleaks-quickref.ps1 -Action Test
#>

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("Status", "Test", "Fix", "SwitchToTrufflehog")]
    [string]$Action = "Status"
)

function Write-Status {
    Write-Information "🔍 Gitleaks Status Check" -InformationAction Continue
    Write-Information "─" * 50 -InformationAction Continue

    # Check Go installation
    try {
        $goVersion = go version 2>$null
        Write-Information "✅ Go installed: $goVersion" -InformationAction Continue
    } catch {
        Write-Information "❌ Go not found. Install from: https://golang.org/dl/" -InformationAction Continue
    }

    # Check GOPATH
    try {
        $goPath = go env GOPATH
        Write-Information "✅ GOPATH: $goPath" -InformationAction Continue
    } catch {
        Write-Information "❌ GOPATH not set" -InformationAction Continue
    }

    # Check gitleaks installation
    try {
        $gitleaksVersion = gitleaks version 2>$null
        Write-Information "✅ Gitleaks installed: $gitleaksVersion" -InformationAction Continue
    } catch {
        Write-Information "❌ Gitleaks not found in PATH" -InformationAction Continue
    }

    # Check configuration file
    $configPath = ".trunk/configs/.gitleaks.toml"
    if (Test-Path $configPath) {
        Write-Information "✅ Config file exists: $configPath" -InformationAction Continue
    } else {
        Write-Information "❌ Config file missing: $configPath" -InformationAction Continue
    }

    # Check trunk configuration
    $trunkConfig = ".trunk/trunk.yaml"
    if (Test-Path $trunkConfig) {
        $content = Get-Content $trunkConfig -Raw
        if ($content -match "gitleaks") {
            Write-Information "✅ Trunk configured for gitleaks" -InformationAction Continue
        } else {
            Write-Information "❌ Gitleaks not enabled in trunk.yaml" -InformationAction Continue
        }
    } else {
        Write-Information "❌ Trunk config missing: $trunkConfig" -InformationAction Continue
    }
}

function Test-Gitleaks {
    Write-Information "🧪 Testing Gitleaks Configuration" -InformationAction Continue
    Write-Information "─" * 50 -InformationAction Continue

    # Test basic functionality
    try {
        $result = gitleaks detect --config .trunk/configs/.gitleaks.toml --path . --verbose 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ Gitleaks test passed" -InformationAction Continue
        } else {
            Write-Information "❌ Gitleaks test failed" -InformationAction Continue
            Write-Information "Output: $result" -InformationAction Continue
        }
    } catch {
        Write-Information "❌ Gitleaks test error: $_" -InformationAction Continue
    }

    # Test trunk integration
    try {
        $trunkResult = trunk check --filter=gitleaks --quiet 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ Trunk gitleaks integration working" -InformationAction Continue
        } else {
            Write-Information "❌ Trunk gitleaks integration failed" -InformationAction Continue
            Write-Information "Output: $trunkResult" -InformationAction Continue
        }
    } catch {
        Write-Information "❌ Trunk test error: $_" -InformationAction Continue
    }
}

function Fix-Gitleaks {
    Write-Information "🔧 Attempting Gitleaks Fix" -InformationAction Continue
    Write-Information "─" * 50 -InformationAction Continue

    # Fix 1: Install/Update Go
    Write-Information "1. Checking Go installation..." -InformationAction Continue
    try {
        go version | Out-Null
        Write-Information "   ✅ Go is available" -InformationAction Continue
    } catch {
        Write-Information "   ❌ Go not found. Please install Go first." -InformationAction Continue
        Write-Information "   Download: https://golang.org/dl/" -InformationAction Continue
        return
    }

    # Fix 2: Set GOPROXY for faster downloads
    Write-Information "2. Setting GOPROXY..." -InformationAction Continue
    go env -w GOPROXY=https://proxy.golang.org,direct

    # Fix 3: Install gitleaks
    Write-Information "3. Installing gitleaks..." -InformationAction Continue
    try {
        go install github.com/gitleaks/gitleaks/v8@latest
        Write-Information "   ✅ Gitleaks installed" -InformationAction Continue
    } catch {
        Write-Information "   ❌ Installation failed: $_" -InformationAction Continue
    }

    # Fix 4: Add to PATH
    Write-Information "4. Adding to PATH..." -InformationAction Continue
    $goBinPath = "$(go env GOPATH)\bin"
    if ($env:PATH -notlike "*$goBinPath*") {
        $env:PATH += ";$goBinPath"
        Write-Information "   ✅ Added to PATH for this session" -InformationAction Continue
        Write-Information "   Note: Add permanently to system PATH" -InformationAction Continue
    } else {
        Write-Information "   ✅ Already in PATH" -InformationAction Continue
    }

    # Fix 5: Test installation
    Write-Information "5. Testing installation..." -InformationAction Continue
    try {
        gitleaks version | Out-Null
        Write-Information "   ✅ Gitleaks working" -InformationAction Continue
    } catch {
        Write-Information "   ❌ Still not working. Try restarting PowerShell." -InformationAction Continue
    }
}

function Switch-ToTrufflehog {
    Write-Information "🔄 Switching to Trufflehog" -InformationAction Continue
    Write-Information "──────────────────────────────────────────────────" -InformationAction Continue

    $trunkConfig = ".trunk/trunk.yaml"

    if (-not (Test-Path $trunkConfig)) {
        Write-Information "❌ Trunk config not found" -InformationAction Continue
        return
    }

    Write-Information "Updating trunk.yaml..." -InformationAction Continue

    # Read current config
    $content = Get-Content $trunkConfig -Raw

    # Comment out gitleaks
    $content = $content -replace "- gitleaks@", "# - gitleaks@"

    # Uncomment trufflehog
    $content = $content -replace "# - trufflehog@", "- trufflehog@"

    # Write back
    $content | Set-Content $trunkConfig

    Write-Information "✅ Switched to Trufflehog" -InformationAction Continue
    Write-Information "Run 'trunk check --filter=trufflehog' to test" -InformationAction Continue
}

# Main execution
switch ($Action) {
    "Status" { Write-Status }
    "Test" { Test-Gitleaks }
    "Fix" { Fix-Gitleaks }
    "SwitchToTrufflehog" { Switch-ToTrufflehog }
    default { Write-Status }
}

Write-Information "" -InformationAction Continue
Write-Information "📚 For detailed help, see: docs/gitleaks-configuration-guide.md" -InformationAction Continue
Write-Information "🐛 For issues, run: .\scripts\setup-gitleaks.ps1 -Diagnose" -InformationAction Continue
