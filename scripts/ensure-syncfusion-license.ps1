# Syncfusion License Key Persistence Manager
# Ensures SYNCFUSION_LICENSE_KEY is ALWAYS available across all scopes

param(
    [switch]$Force,
    [switch]$Machine,
    [switch]$User,
    [switch]$Process,
    [switch]$Registry,
    [switch]$Verify
)

# Configuration
$LICENSE_KEY_NAME = "SYNCFUSION_LICENSE_KEY"
$PROJECT_ROOT = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ENV_FILE = Join-Path $PROJECT_ROOT ".env"

function Get-LicenseKeyFromEnvFile {
    if (-not (Test-Path $ENV_FILE)) {
        Write-Warning "ERROR: .env file not found: $ENV_FILE"
        return $null
    }

    $content = Get-Content $ENV_FILE
    foreach ($line in $content) {
        if ($line -match "^$LICENSE_KEY_NAME=(.+)$") {
            return $matches[1].Trim()
        }
    }

    Write-Warning "ERROR: $LICENSE_KEY_NAME not found in .env file"
    return $null
}

function Set-EnvironmentVariable {
    param(
        [string]$Scope,
        [string]$Value
    )

    try {
        [Environment]::SetEnvironmentVariable($LICENSE_KEY_NAME, $Value, $Scope)
        Write-Host "OK: Set $LICENSE_KEY_NAME in $Scope scope"
        return $true
    }
    catch {
        Write-Warning "ERROR: Failed to set $LICENSE_KEY_NAME in $Scope scope: $($_.Exception.Message)"
        return $false
    }
}

function Set-RegistryValue {
    param(
        [string]$Value
    )

    try {
        $regPath = "HKCU:\Environment"
        if (-not (Test-Path $regPath)) {
            New-Item -Path $regPath -Force | Out-Null
        }

        Set-ItemProperty -Path $regPath -Name $LICENSE_KEY_NAME -Value $Value -Type String
        Write-Host "OK: Set $LICENSE_KEY_NAME in registry (HKCU:\Environment)"
        return $true
    }
    catch {
        Write-Warning "ERROR: Failed to set registry value: $($_.Exception.Message)"
        return $false
    }
}

function Get-RegistryValue {
    try {
        $regPath = "HKCU:\Environment"
        if (Test-Path $regPath) {
            $value = Get-ItemProperty -Path $regPath -Name $LICENSE_KEY_NAME -ErrorAction SilentlyContinue
            if ($value) {
                return $value.$LICENSE_KEY_NAME
            }
        }
    }
    catch {
        # Silently continue if registry access fails
    }
    return $null
}

function Verify-LicenseKey {
    param(
        [string]$Key
    )

    if ([string]::IsNullOrWhiteSpace($Key)) {
        return $false
    }

    # Check for placeholder patterns
    $placeholders = @(
        "YOUR_SYNCFUSION_LICENSE_KEY_HERE",
        "PLACEHOLDER",
        "INSERT"
    )

    foreach ($placeholder in $placeholders) {
        if ($Key -like "*$placeholder*") {
            return $false
        }
    }

    # Basic format validation (base64-like)
    if ($Key.Length -lt 50) {
        return $false
    }

    return $true
}

function Get-LicenseKey {
    # Try different sources in order of preference

    # 1. Machine scope (most persistent)
    $key = [Environment]::GetEnvironmentVariable($LICENSE_KEY_NAME, "Machine")
    if ($key -and (Verify-LicenseKey $key)) {
        Write-Host "FOUND: Found valid key in Machine scope"
        return $key
    }

    # 2. User scope
    $key = [Environment]::GetEnvironmentVariable($LICENSE_KEY_NAME, "User")
    if ($key -and (Verify-LicenseKey $key)) {
        Write-Host "FOUND: Found valid key in User scope"
        return $key
    }

    # 3. Registry backup
    $key = Get-RegistryValue
    if ($key -and (Verify-LicenseKey $key)) {
        Write-Host "FOUND: Found valid key in registry"
        return $key
    }

    # 4. .env file
    $key = Get-LicenseKeyFromEnvFile
    if ($key -and (Verify-LicenseKey $key)) {
        Write-Host "FOUND: Found valid key in .env file"
        return $key
    }

    Write-Warning "WARNING: No valid $LICENSE_KEY_NAME found in any location"
    return $null
}

function Show-Status {
    Write-Host "STATUS: $LICENSE_KEY_NAME Status Report" -ForegroundColor Cyan
    Write-Host "=" * 50

    # Check all scopes
    $scopes = @("Machine", "User", "Process")
    foreach ($scope in $scopes) {
        $key = [Environment]::GetEnvironmentVariable($LICENSE_KEY_NAME, $scope)
        $status = if ($key -and (Verify-LicenseKey $key)) { "[VALID]" } elseif ($key) { "[INVALID]" } else { "[NOT SET]" }
        Write-Host "  $scope`: $status"
    }

    # Check registry
    $regKey = Get-RegistryValue
    $regStatus = if ($regKey -and (Verify-LicenseKey $regKey)) { "[VALID]" } elseif ($regKey) { "[INVALID]" } else { "[NOT SET]" }
    Write-Host "  Registry`: $regStatus"

    # Check .env file
    $envKey = Get-LicenseKeyFromEnvFile
    $envStatus = if ($envKey -and (Verify-LicenseKey $envKey)) { "[VALID]" } elseif ($envKey) { "[INVALID]" } else { "[NOT SET]" }
    Write-Host "  .env file`: $envStatus"
}

# Main execution
if ($Verify) {
    Show-Status
    exit 0
}

# Get the license key
$licenseKey = Get-LicenseKey

if (-not $licenseKey) {
    Write-Error "ERROR: Cannot proceed: No valid $LICENSE_KEY_NAME found"
    exit 1
}

# Set in requested scopes (or all if none specified)
$successCount = 0

if ($Machine -or (-not ($Machine -or $User -or $Process -or $Registry))) {
    if (Set-EnvironmentVariable -Scope "Machine" -Value $licenseKey) { $successCount++ }
}

if ($User -or (-not ($Machine -or $User -or $Process -or $Registry))) {
    if (Set-EnvironmentVariable -Scope "User" -Value $licenseKey) { $successCount++ }
}

if ($Process -or (-not ($Machine -or $User -or $Process -or $Registry))) {
    if (Set-EnvironmentVariable -Scope "Process" -Value $licenseKey) { $successCount++ }
}

if ($Registry -or (-not ($Machine -or $User -or $Process -or $Registry))) {
    if (Set-RegistryValue -Value $licenseKey) { $successCount++ }
}

if ($successCount -gt 0) {
    Write-Host "SUCCESS: Successfully set $LICENSE_KEY_NAME in $successCount location(s)"
    Write-Host "INFO: The license key will now be available in all future PowerShell sessions"
} else {
    Write-Error "ERROR: Failed to set $LICENSE_KEY_NAME in any location"
    exit 1
}

# Final verification
Write-Host ""
Show-Status
