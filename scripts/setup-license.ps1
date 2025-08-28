# Syncfusion License Setup Script for WileyWidget
# This script helps set up Syncfusion license for development

param(
    [string]$LicenseKey,
    [switch]$CheckOnly,
    [switch]$Remove,
    [switch]$Watch
)

Write-Host "=== WileyWidget Syncfusion License Setup ===" -ForegroundColor Cyan

# Function to validate license key format
function Test-LicenseKeyFormat {
    param([string]$Key)

    if ([string]::IsNullOrWhiteSpace($Key)) {
        return $false
    }

    # Basic format validation (starts with letter, reasonable length)
    if ($Key.Length -lt 50 -or $Key.Length -gt 200) {
        return $false
    }

    if (-not ($Key -match "^[A-Za-z]")) {
        return $false
    }

    return $true
}

# Function to set environment variable
function Set-LicenseEnvironmentVariable {
    param([string]$Key)

    try {
        [System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', $Key, 'User')
        Write-Host "✅ License key set in User environment variable" -ForegroundColor Green

        # Refresh environment for current session
        $env:SYNCFUSION_LICENSE_KEY = $Key
        return $true
    } catch {
        Write-Host "❌ Failed to set environment variable: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Function to create license file
function New-LicenseFile {
    param([string]$Key)

    try {
        $exePath = Join-Path $PSScriptRoot ".." "WileyWidget" "bin" "Debug" "net9.0-windows" "WileyWidget.exe"
        if (-not (Test-Path $exePath)) {
            $exePath = Join-Path $PSScriptRoot ".." "WileyWidget" "bin" "Release" "net9.0-windows" "WileyWidget.exe"
        }

        if (Test-Path $exePath) {
            $licensePath = Join-Path (Split-Path $exePath) "license.key"
        } else {
            $licensePath = Join-Path $PSScriptRoot ".." "license.key"
        }

        $Key | Out-File -FilePath $licensePath -Encoding UTF8 -Force
        Write-Host "✅ License file created at: $licensePath" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "❌ Failed to create license file: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Function to check current license status
function Get-LicenseStatus {
    Write-Host ""
    Write-Host "Checking current license status..." -ForegroundColor Yellow

    # Check environment variable
    $envKey = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'User')
    if (-not [string]::IsNullOrWhiteSpace($envKey)) {
        Write-Host "✅ Environment variable set (User scope)" -ForegroundColor Green
        Write-Host "   Key starts with: $($envKey.Substring(0, [Math]::Min(10, $envKey.Length)))..." -ForegroundColor Gray
    } else {
        Write-Host "❌ No environment variable set" -ForegroundColor Red
    }

    # Check license file
    $licensePaths = @(
        (Join-Path $PSScriptRoot ".." "license.key"),
        (Join-Path $PSScriptRoot ".." "WileyWidget" "license.key")
    )

    $fileFound = $false
    foreach ($path in $licensePaths) {
        if (Test-Path $path) {
            $fileContent = Get-Content $path -Raw
            if (-not [string]::IsNullOrWhiteSpace($fileContent)) {
                Write-Host "✅ License file found: $path" -ForegroundColor Green
                Write-Host "   Key starts with: $($fileContent.Trim().Substring(0, [Math]::Min(10, $fileContent.Trim().Length)))..." -ForegroundColor Gray
                $fileFound = $true
                break
            }
        }
    }

    if (-not $fileFound) {
        Write-Host "❌ No valid license file found" -ForegroundColor Red
    }

    # Check embedded license file
    $embeddedPath = Join-Path $PSScriptRoot ".." "WileyWidget" "LicenseKey.Private.cs"
    if (Test-Path $embeddedPath) {
        Write-Host "✅ Embedded license file exists: $embeddedPath" -ForegroundColor Green
    } else {
        Write-Host "ℹ️  No embedded license file (optional)" -ForegroundColor Yellow
    }
}

# Function to remove license
function Remove-LicenseSetup {
    Write-Host ""
    Write-Host "Removing license setup..." -ForegroundColor Yellow

    # Remove environment variable
    try {
        [System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', $null, 'User')
        Write-Host "✅ Environment variable removed" -ForegroundColor Green
    } catch {
        Write-Host "❌ Failed to remove environment variable: $($_.Exception.Message)" -ForegroundColor Red
    }

    # Remove license files
    $licensePaths = @(
        (Join-Path $PSScriptRoot ".." "license.key"),
        (Join-Path $PSScriptRoot ".." "WileyWidget" "license.key")
    )

    foreach ($path in $licensePaths) {
        if (Test-Path $path) {
            try {
                Remove-Item $path -Force
                Write-Host "✅ License file removed: $path" -ForegroundColor Green
            } catch {
                Write-Host "❌ Failed to remove license file: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
}

# Watch mode for monitoring license registration
function Watch-LicenseRegistration {
    Write-Host ""
    Write-Host "Watching for Syncfusion license registration..." -ForegroundColor Yellow
    Write-Host "Press Ctrl+C to stop" -ForegroundColor Gray
    Write-Host ""

    try {
        while ($true) {
            # Check if any WileyWidget processes are running
            $processes = Get-Process -Name "WileyWidget" -ErrorAction SilentlyContinue
            if ($processes) {
                Write-Host "$(Get-Date -Format 'HH:mm:ss') - WileyWidget process(es) running (PID: $($processes.Id -join ', '))" -ForegroundColor Green
            } else {
                Write-Host "$(Get-Date -Format 'HH:mm:ss') - No WileyWidget processes running" -ForegroundColor Gray
            }

            Start-Sleep -Seconds 5
        }
    } catch {
        Write-Host ""
        Write-Host "Watch mode stopped" -ForegroundColor Yellow
    }
}

# Main execution
if ($Watch) {
    Watch-LicenseRegistration
    exit 0
}

if ($Remove) {
    Remove-LicenseSetup
    exit 0
}

if ($CheckOnly) {
    Get-LicenseStatus
    exit 0
}

# Interactive license setup
if ([string]::IsNullOrWhiteSpace($LicenseKey)) {
    Write-Host ""
    Write-Host "License Setup Options:" -ForegroundColor Cyan
    Write-Host "1. Environment Variable (Recommended)" -ForegroundColor White
    Write-Host "2. License File" -ForegroundColor White
    Write-Host "3. Check Current Status" -ForegroundColor White
    Write-Host ""

    $choice = Read-Host "Choose setup method (1-3)"

    switch ($choice) {
        "1" {
            $LicenseKey = Read-Host "Enter your Syncfusion license key"
        }
        "2" {
            $LicenseKey = Read-Host "Enter your Syncfusion license key"
        }
        "3" {
            Get-LicenseStatus
            exit 0
        }
        default {
            Write-Host "Invalid choice. Exiting." -ForegroundColor Red
            exit 1
        }
    }
}

# Validate license key
if (-not (Test-LicenseKeyFormat -LicenseKey $LicenseKey)) {
    Write-Host "❌ Invalid license key format" -ForegroundColor Red
    Write-Host "License keys should:" -ForegroundColor Yellow
    Write-Host "  - Start with a letter" -ForegroundColor White
    Write-Host "  - Be 50-200 characters long" -ForegroundColor White
    Write-Host "  - Contain alphanumeric characters" -ForegroundColor White
    exit 1
}

Write-Host ""
Write-Host "Setting up Syncfusion license..." -ForegroundColor Cyan

# Setup based on choice or default to environment variable
if ($choice -eq "2" -or $choice -eq $null) {
    # Try environment variable first (preferred)
    if (Set-LicenseEnvironmentVariable -Key $LicenseKey) {
        Write-Host ""
        Write-Host "🎉 License setup completed successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "The license will be automatically detected when you run the application." -ForegroundColor Cyan
    } else {
        # Fallback to file method
        Write-Host "Falling back to license file method..." -ForegroundColor Yellow
        if (New-LicenseFile -Key $LicenseKey) {
            Write-Host ""
            Write-Host "🎉 License setup completed successfully!" -ForegroundColor Green
            Write-Host ""
            Write-Host "The license file will be automatically detected when you run the application." -ForegroundColor Cyan
        } else {
            Write-Host "❌ All license setup methods failed" -ForegroundColor Red
            exit 1
        }
    }
} elseif ($choice -eq "1") {
    # Force environment variable method
    if (Set-LicenseEnvironmentVariable -Key $LicenseKey) {
        Write-Host ""
        Write-Host "🎉 License setup completed successfully!" -ForegroundColor Green
    } else {
        Write-Host "❌ Failed to set up license" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Run the application to verify license registration" -ForegroundColor White
Write-Host "  2. Check logs at: %APPDATA%\WileyWidget\logs" -ForegroundColor White
Write-Host "  3. Use 'pwsh ./scripts/setup-license.ps1 -CheckOnly' to verify status" -ForegroundColor White
