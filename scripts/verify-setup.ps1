<#
.SYNOPSIS
    Comprehensive setup verification for WileyWidget

.DESCRIPTION
    Verifies all prerequisites and configuration for WileyWidget application

.EXAMPLE
    .\scripts\verify-setup.ps1
    .\scripts\verify-setup.ps1 -Verbose
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  WileyWidget Setup Verification" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$checks = @{
    Passed = 0
    Failed = 0
    Warnings = 0
}

function Test-Requirement {
    param(
        [string]$Name,
        [scriptblock]$Test,
        [string]$FailMessage,
        [string]$SuccessMessage,
        [switch]$Critical
    )
    
    Write-Host "[$Name] " -NoNewline
    
    try {
        $result = & $Test
        
        if ($result) {
            Write-Host "✓" -ForegroundColor Green
            if ($SuccessMessage) {
                Write-Host "  → $SuccessMessage" -ForegroundColor Gray
            }
            $script:checks.Passed++
            return $true
        } else {
            if ($Critical) {
                Write-Host "✗ CRITICAL" -ForegroundColor Red
                $script:checks.Failed++
            } else {
                Write-Host "⚠ WARNING" -ForegroundColor Yellow
                $script:checks.Warnings++
            }
            if ($FailMessage) {
                Write-Host "  → $FailMessage" -ForegroundColor Gray
            }
            return $false
        }
    } catch {
        Write-Host "✗ ERROR" -ForegroundColor Red
        Write-Host "  → $_" -ForegroundColor Gray
        $script:checks.Failed++
        return $false
    }
}

# Check 1: .NET SDK
Test-Requirement -Name ".NET SDK" -Critical -Test {
    $version = dotnet --version 2>$null
    if ($version -and [version]$version -ge [version]"9.0.0") {
        $script:dotnetVersion = $version
        return $true
    }
    return $false
} -SuccessMessage ".NET $dotnetVersion installed" `
  -FailMessage ".NET 9.0 SDK required (download from https://dot.net)"

# Check 2: PowerShell Version
Test-Requirement -Name "PowerShell" -Test {
    return $PSVersionTable.PSVersion.Major -ge 7
} -SuccessMessage "PowerShell $($PSVersionTable.PSVersion)" `
  -FailMessage "PowerShell 7+ recommended (https://github.com/PowerShell/PowerShell/releases)"

# Check 3: SQL Server Express
$sqlServerRunning = Test-Requirement -Name "SQL Server Express" -Critical -Test {
    $service = Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
    if ($service) {
        $script:sqlStatus = $service.Status
        return $service.Status -eq 'Running'
    }
    return $false
} -SuccessMessage "SQL Server Express running" `
  -FailMessage "SQL Server Express not running (run: Start-Service 'MSSQL`$SQLEXPRESS')"

# Check 4: Database exists
if ($sqlServerRunning) {
    Test-Requirement -Name "Database WileyWidgetDev" -Critical -Test {
        $query = "SELECT COUNT(*) FROM sys.databases WHERE name='WileyWidgetDev'"
        $result = sqlcmd -S .\SQLEXPRESS -Q $query -h -1 2>$null
        return [int]$result -gt 0
    } -SuccessMessage "Database exists" `
      -FailMessage "Database missing (run: dotnet ef database update)"
    
    # Check 5: Database tables
    Test-Requirement -Name "Database Tables" -Test {
        $query = "SELECT COUNT(*) FROM WileyWidgetDev.INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'"
        $result = sqlcmd -S .\SQLEXPRESS -Q $query -h -1 2>$null
        $script:tableCount = [int]$result
        return $tableCount -ge 15
    } -SuccessMessage "$tableCount tables found" `
      -FailMessage "Expected 15+ tables (run: dotnet ef database update)"
}

# Check 6: Available Memory
Test-Requirement -Name "Available Memory" -Test {
    $freeMemory = [Math]::Round((Get-CimInstance -ClassName Win32_OperatingSystem).FreePhysicalMemory / 1MB, 2)
    $script:freeMemoryGB = $freeMemory
    return $freeMemory -gt 2
} -SuccessMessage "$freeMemoryGB GB free" `
  -FailMessage "Low memory (< 2GB free)"

# Check 7: Secrets Vault
Test-Requirement -Name "Secrets Vault" -Test {
    $secretsPath = Join-Path $env:APPDATA "WileyWidget\Secrets"
    if (Test-Path $secretsPath) {
        $script:secretCount = (Get-ChildItem $secretsPath -File | Measure-Object).Count
        return $secretCount -gt 0
    }
    return $false
} -SuccessMessage "$secretCount secret files found" `
  -FailMessage "Secrets vault not initialized (will auto-create on first run)"

# Check 8: QuickBooks Tokens
Test-Requirement -Name "QuickBooks Tokens" -Test {
    $settingsPath = Join-Path $env:APPDATA "WileyWidget\settings.json"
    if (Test-Path $settingsPath) {
        $settings = Get-Content $settingsPath | ConvertFrom-Json
        return $null -ne $settings.QboAccessToken -and $settings.QboAccessToken -ne ""
    }
    return $false
} -SuccessMessage "Tokens configured" `
  -FailMessage "QuickBooks not configured (optional - run: .\scripts\quickbooks\setup-oauth.ps1)"

# Check 9: XAI API Key
Test-Requirement -Name "XAI API Key" -Test {
    $key = [System.Environment]::GetEnvironmentVariable('XAI_API_KEY', 'User')
    return $null -ne $key -and $key -ne ""
} -SuccessMessage "XAI configured" `
  -FailMessage "XAI not configured (optional - AI features will use NullAIService)"

# Check 10: Project Build Status
Test-Requirement -Name "Project Build" -Critical -Test {
    $buildLog = dotnet build "$PSScriptRoot\..\WileyWidget.sln" --no-restore --verbosity quiet 2>&1
    return $LASTEXITCODE -eq 0
} -SuccessMessage "Build successful" `
  -FailMessage "Build failed (run: dotnet build WileyWidget.sln)"

# Check 11: Disk Space
Test-Requirement -Name "Disk Space" -Test {
    $drive = Get-PSDrive C
    $freeSpaceGB = [Math]::Round($drive.Free / 1GB, 2)
    $script:diskSpace = $freeSpaceGB
    return $freeSpaceGB -gt 5
} -SuccessMessage "$diskSpace GB free on C:" `
  -FailMessage "Low disk space (< 5GB free)"

# Summary
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Verification Summary" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Host "Passed:   " -NoNewline
Write-Host $checks.Passed -ForegroundColor Green

Write-Host "Warnings: " -NoNewline
Write-Host $checks.Warnings -ForegroundColor Yellow

Write-Host "Failed:   " -NoNewline
Write-Host $checks.Failed -ForegroundColor Red

Write-Host ""

if ($checks.Failed -eq 0 -and $checks.Warnings -le 2) {
    Write-Host "Setup Status: " -NoNewline
    Write-Host "COMPLETE ✓" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now run:" -ForegroundColor Cyan
    Write-Host "  dotnet run --project src/WileyWidget.WinUI --configuration Release" -ForegroundColor White
    Write-Host ""
    exit 0
} elseif ($checks.Failed -eq 0) {
    Write-Host "Setup Status: " -NoNewline
    Write-Host "READY (with warnings)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Application will run, but some features may be limited." -ForegroundColor Gray
    Write-Host "Review warnings above and configure optional components." -ForegroundColor Gray
    Write-Host ""
    exit 0
} else {
    Write-Host "Setup Status: " -NoNewline
    Write-Host "INCOMPLETE ✗" -ForegroundColor Red
    Write-Host ""
    Write-Host "Critical checks failed. Please resolve issues above before running application." -ForegroundColor Gray
    Write-Host "See SETUP_GUIDE.md for detailed instructions." -ForegroundColor Gray
    Write-Host ""
    exit 1
}
