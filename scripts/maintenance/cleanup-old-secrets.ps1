#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Cleans up old hyphenated secret names from the vault and forces fresh migration.

.DESCRIPTION
    Removes old secret files using hyphenated naming (QuickBooks-ClientId, Syncfusion-LicenseKey, etc.)
    to allow fresh migration using standardized UPPERCASE_UNDERSCORE naming.

.EXAMPLE
    .\cleanup-old-secrets.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$secretsPath = "$env:APPDATA\WileyWidget\Secrets"

Write-Host "🧹 Cleaning up old secret vault entries..." -ForegroundColor Cyan

if (-not (Test-Path $secretsPath)) {
    Write-Host "✓ Secrets directory doesn't exist yet - nothing to clean" -ForegroundColor Green
    exit 0
}

# Old hyphenated secret names to remove
$oldSecrets = @(
    "syncfusion-license-key.secret"
    "QuickBooks-ClientId.secret"
    "QuickBooks-ClientSecret.secret"
    "QuickBooks-RedirectUri.secret"
    "QuickBooks-Environment.secret"
    "XAI-ApiKey.secret"
    "XAI-BaseUrl.secret"
    "Syncfusion-LicenseKey.secret"
)

$removedCount = 0

foreach ($secretFile in $oldSecrets) {
    $fullPath = Join-Path $secretsPath $secretFile
    if (Test-Path $fullPath) {
        Remove-Item $fullPath -Force
        Write-Host "  ✓ Removed: $secretFile" -ForegroundColor Yellow
        $removedCount++
    }
}

if ($removedCount -eq 0) {
    Write-Host "✓ No old secrets found to clean up" -ForegroundColor Green
} else {
    Write-Host "✓ Removed $removedCount old secret files" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next startup will migrate secrets using standardized naming:" -ForegroundColor Cyan
    Write-Host "  - SYNCFUSION_LICENSE_KEY" -ForegroundColor White
    Write-Host "  - QBO_CLIENT_ID" -ForegroundColor White
    Write-Host "  - QBO_CLIENT_SECRET" -ForegroundColor White
    Write-Host "  - XAI_API_KEY" -ForegroundColor White
}
