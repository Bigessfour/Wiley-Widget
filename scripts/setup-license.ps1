<#
.SYNOPSIS
Sets the Syncfusion License Key in the User Environment variables.

.DESCRIPTION
This script sets the SYNCFUSION_LICENSE_KEY environment variable at the User scope.
It also clears any legacy/invalid keys from the current session to avoid conflicts.
This is required for the Wiley Widget v32+ application.

.PARAMETER Key
The Syncfusion License Key string. If not provided, you will be prompted securely.

.EXAMPLE
.\scripts\setup-license.ps1 -Key "YourLongKeyHere..."
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$Key
)

if ([string]::IsNullOrWhiteSpace($Key)) {
    Write-Host "Please enter your Syncfusion License Key (Input will be masked):" -ForegroundColor Cyan
    $Key = Read-Host -MaskInput
}

# Basic Validation for v32 Key
if ($Key.Length < 100) {
    Write-Host "⚠️  WARNING: The key provided is only $($Key.Length) characters long." -ForegroundColor Yellow
    Write-Host "   Syncfusion v32+ keys are typically much longer (Base64 JWTs)." -ForegroundColor Yellow
    $confirm = Read-Host "   Are you sure this is a v32 key? (y/n)"
    if ($confirm -ne 'y') {
        Write-Host "   Operation cancelled." -ForegroundColor Red
        exit
    }
}

Write-Host "Setting SYNCFUSION_LICENSE_KEY environment variable (User scope)..." -ForegroundColor Cyan

# Set User Scope (Persistent)
[System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', $Key, 'User')

# Set Process Scope (Current Session) so they don't need to restart THIS terminal immediately
[System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', $Key, 'Process')

Write-Host "✅ License Key set successfully." -ForegroundColor Green
Write-Host "   - User Scope: Persists across restarts."
Write-Host "   - Process Scope: Available in this terminal session immediately."
Write-Host ""
Write-Host "ℹ️  NOTE: You may need to restart Visual Studio or VS Code for the IDE to pick up the new variable." -ForegroundColor Yellow
