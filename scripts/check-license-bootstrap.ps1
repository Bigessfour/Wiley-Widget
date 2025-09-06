#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analyzes Syncfusion license bootstrap status and provides diagnostic information.

.DESCRIPTION
    This script checks all potential sources of Syncfusion license configuration,
    analyzes bootstrap.log for license registration attempts, and provides
    actionable recommendations for resolving license issues.

.PARAMETER Watch
    Continuously monitor the logs for license-related events.

.PARAMETER Detailed
    Show detailed analysis including log excerpts and file contents.

.EXAMPLE
    ./scripts/check-license-bootstrap.ps1
    
.EXAMPLE
    ./scripts/check-license-bootstrap.ps1 -Watch
    
.EXAMPLE
    ./scripts/check-license-bootstrap.ps1 -Detailed
#>

param(
    [switch]$Watch,
    [switch]$Detailed
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "🔍 === SYNCFUSION LICENSE BOOTSTRAP ANALYSIS ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check Environment Variables
Write-Host "📋 Environment Variable Analysis:" -ForegroundColor Yellow
$envUser = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'User')
$envMachine = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'Machine') 
$envProcess = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY')

Write-Host "   User Level:    $(if ($envUser) { "✅ SET ($($envUser.Length) chars)" } else { "❌ NOT SET" })"
Write-Host "   Machine Level: $(if ($envMachine) { "✅ SET ($($envMachine.Length) chars)" } else { "❌ NOT SET" })"
Write-Host "   Process Level: $(if ($envProcess) { "✅ SET ($($envProcess.Length) chars)" } else { "❌ NOT SET" })"

# 2. Check License File
Write-Host ""
Write-Host "📄 License File Analysis:" -ForegroundColor Yellow
$licenseFile = "license.key"
if (Test-Path $licenseFile) {
    $content = Get-Content $licenseFile -Raw
    $isValid = $content.Length -gt 50 -and -not $content.Contains("YOUR_SYNCFUSION_LICENSE_KEY_HERE")
    Write-Host "   File Exists: ✅ YES ($($content.Length) chars, Valid: $isValid)"
    
    if ($Detailed -and $isValid) {
        $preview = $content.Substring(0, [Math]::Min(20, $content.Length)) + "..."
        Write-Host "   Preview: $preview" -ForegroundColor Gray
    }
} else {
    Write-Host "   File Exists: ❌ NO"
}

# 3. Analyze Bootstrap Logs
Write-Host ""
Write-Host "📊 Bootstrap Log Analysis:" -ForegroundColor Yellow
$bootstrapLog = "logs/bootstrap.log"

if (Test-Path $bootstrapLog) {
    Write-Host "   Bootstrap Log: ✅ FOUND"
    
    # Search for license-related entries
    $licenseEntries = Select-String -Path $bootstrapLog -Pattern "license|License|LICENSE" -AllMatches
    Write-Host "   License Entries: $($licenseEntries.Count)"
    
    if ($Detailed -and $licenseEntries.Count -gt 0) {
        Write-Host ""
        Write-Host "   📝 License Log Entries:" -ForegroundColor Gray
        $licenseEntries | ForEach-Object {
            Write-Host "      $($_.Line)" -ForegroundColor DarkGray
        }
    }
    
    # Check for registration success indicators
    $successPatterns = @(
        "license registered successfully",
        "license registration completed",
        "✅.*license"
    )
    
    $hasSuccess = $false
    foreach ($pattern in $successPatterns) {
        if (Select-String -Path $bootstrapLog -Pattern $pattern -Quiet) {
            $hasSuccess = $true
            break
        }
    }
    
    Write-Host "   Success Indicators: $(if ($hasSuccess) { "✅ FOUND" } else { "❌ NOT FOUND" })"
    
    # Check for error indicators
    $errorPatterns = @(
        "license.*error",
        "❌.*license",
        "trial mode"
    )
    
    $hasErrors = $false
    foreach ($pattern in $errorPatterns) {
        if (Select-String -Path $bootstrapLog -Pattern $pattern -Quiet) {
            $hasErrors = $true
            break
        }
    }
    
    Write-Host "   Error Indicators: $(if ($hasErrors) { "🚨 FOUND" } else { "✅ NONE" })"
    
} else {
    Write-Host "   Bootstrap Log: ❌ NOT FOUND"
}

# 4. Check Application Logs
Write-Host ""
Write-Host "📋 Application Log Analysis:" -ForegroundColor Yellow
$appLogs = Get-ChildItem "logs/app-*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($appLogs) {
    Write-Host "   Latest App Log: ✅ $($appLogs.Name)"
    
    # Check for license diagnostic entries
    $diagnosticEntries = Select-String -Path $appLogs.FullName -Pattern "LICENSE BOOTSTRAP DIAGNOSTIC|License.*Analysis" -AllMatches
    Write-Host "   Diagnostic Entries: $($diagnosticEntries.Count)"
    
    if ($Detailed -and $diagnosticEntries.Count -gt 0) {
        Write-Host ""
        Write-Host "   📊 Diagnostic Entries:" -ForegroundColor Gray
        $diagnosticEntries | Select-Object -First 3 | ForEach-Object {
            Write-Host "      $($_.Line)" -ForegroundColor DarkGray
        }
    }
} else {
    Write-Host "   App Logs: ❌ NOT FOUND"
}

# 5. Overall Assessment
Write-Host ""
Write-Host "🎯 Assessment & Recommendations:" -ForegroundColor Green

$hasAnyLicense = $envUser -or $envMachine -or $envProcess -or (Test-Path $licenseFile -and (Get-Content $licenseFile -Raw).Length -gt 50)

if (-not $hasAnyLicense) {
    Write-Host "   Status: 🚨 NO LICENSE DETECTED" -ForegroundColor Red
    Write-Host "   Impact: Application will run in trial mode with watermarks"
    Write-Host ""
    Write-Host "   🔧 Fix Options:" -ForegroundColor Yellow
    Write-Host "      1. Set environment variable: [Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'your-key-here', 'User')"
    Write-Host "      2. Create license.key file with your license key"
    Write-Host "      3. Run: ./scripts/load-mcp-secrets.ps1 (if using Azure Key Vault)"
} else {
    Write-Host "   Status: ✅ LICENSE SOURCE DETECTED" -ForegroundColor Green
    Write-Host "   Next: Check bootstrap logs for registration confirmation"
    
    if ($hasErrors) {
        Write-Host ""
        Write-Host "   ⚠️ Issues Found:" -ForegroundColor Yellow
        Write-Host "      - Check bootstrap.log for specific error messages"
        Write-Host "      - Verify license key format and validity"
        Write-Host "      - Ensure license hasn't expired"
    }
}

# 6. Watch Mode
if ($Watch) {
    Write-Host ""
    Write-Host "👀 Watching for license-related log entries (Press Ctrl+C to exit)..." -ForegroundColor Cyan
    
    $logFiles = @("logs/bootstrap.log", "logs/app-*.log")
    
    try {
        while ($true) {
            foreach ($pattern in $logFiles) {
                Get-ChildItem $pattern -ErrorAction SilentlyContinue | ForEach-Object {
                    Get-Content $_.FullName -Tail 10 | Where-Object { $_ -match "license|License|LICENSE" } | ForEach-Object {
                        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $_" -ForegroundColor Yellow
                    }
                }
            }
            Start-Sleep -Seconds 2
        }
    }
    catch {
        Write-Host "Watch mode stopped." -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "🔍 === END ANALYSIS ===" -ForegroundColor Cyan
