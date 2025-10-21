# Azure Key Vault Secret Rotation Script for WileyWidget
# This script helps manage secret rotation for production environments

param(
    [Parameter(Mandatory = $true)]
    [string]$NewLicenseKey,

    [switch]$CheckExpiration,
    [switch]$RotateSecret,
    [switch]$BackupCurrent
)

# Import Az module
Import-Module Az.KeyVault -ErrorAction Stop

$vaultName = "wiley-widget-secrets"
$secretName = "SyncfusionLicenseKey"

Write-Host "🔐 WileyWidget Azure Key Vault Secret Management" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyanif ($CheckExpiration) {
    Write-Host "`n📅 Checking secret expiration..." -ForegroundColor Yellow

    $secret = az keyvault secret show --vault-name $vaultName --name $secretName | ConvertFrom-Json

    if ($secret.attributes.expires) {
        $expires = [DateTime]::Parse($secret.attributes.expires)
        $daysUntilExpiration = ($expires - [DateTime]::UtcNow).Days

        Write-Host "Secret expires: $($expires.ToString('yyyy-MM-dd'))" -ForegroundColor White
        Write-Host "Days until expiration: $daysUntilExpiration" -ForegroundColor White

        if ($daysUntilExpiration -le 30) {
            Write-Host "⚠️  WARNING: Secret expires in $daysUntilExpiration days!" -ForegroundColor Red
        }
        elseif ($daysUntilExpiration -le 90) {
            Write-Host "⚠️  CAUTION: Secret expires in $daysUntilExpiration days." -ForegroundColor Yellow
        }
        else {
            Write-Host "✅ Secret expiration is healthy." -ForegroundColor Green
        }
    }
    else {
        Write-Host "ℹ️  No expiration date set on secret." -ForegroundColor Yellow
    }
    exit 0
}

if ($BackupCurrent) {
    Write-Host "`n💾 Backing up current secret..." -ForegroundColor Yellow

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupSecretName = "$secretName-backup-$timestamp"

    # Get current secret value
    $currentSecret = az keyvault secret show --vault-name $vaultName --name $secretName --query value -o tsv

    # Create backup
    az keyvault secret set --vault-name $vaultName --name $backupSecretName --value $currentSecret

    Write-Host "✅ Backup created: $backupSecretName" -ForegroundColor Green
    exit 0
}

if ($RotateSecret) {
    if ([string]::IsNullOrWhiteSpace($NewLicenseKey)) {
        Write-Host "❌ New license key is required for rotation." -ForegroundColor Red
        exit 1
    }

    Write-Host "`n🔄 Rotating Syncfusion license key..." -ForegroundColor Yellow

    # Backup current secret first
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupSecretName = "$secretName-backup-$timestamp"

    $currentSecret = az keyvault secret show --vault-name $vaultName --name $secretName --query value -o tsv
    az keyvault secret set --vault-name $vaultName --name $backupSecretName --value $currentSecret

    Write-Host "✅ Backup created: $backupSecretName" -ForegroundColor Green

    # Update the secret with new value
    az keyvault secret set --vault-name $vaultName --name $secretName --value $NewLicenseKey

    # Set new expiration date (1 year from now)
    $newExpiration = (Get-Date).AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ")
    az keyvault secret set-attributes --vault-name $vaultName --name $secretName --expires $newExpiration

    Write-Host "✅ Secret rotated successfully!" -ForegroundColor Green
    Write-Host "   New expiration: $newExpiration" -ForegroundColor White
    Write-Host "   Backup available: $backupSecretName" -ForegroundColor White

    Write-Host "`n📋 Next Steps:" -ForegroundColor Cyan
    Write-Host "   1. Test application with new license key" -ForegroundColor White
    Write-Host "   2. Update any local configurations if needed" -ForegroundColor White
    Write-Host "   3. Clean up old backups after verification" -ForegroundColor White
}

Write-Host "`nUsage Examples:" -ForegroundColor Cyan
Write-Host "  .\manage-secrets.ps1 -CheckExpiration" -ForegroundColor White
Write-Host "  .\manage-secrets.ps1 -BackupCurrent" -ForegroundColor White
Write-Host "  .\manage-secrets.ps1 -RotateSecret -NewLicenseKey 'your-new-key'" -ForegroundColor White
