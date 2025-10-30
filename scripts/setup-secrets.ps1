#!/usr/bin/env pwsh
# Script to help set up environment variables for local development
# Run this script to create your .env file with proper placeholders

param(
    [switch]$Create,
    [switch]$Validate,
    [switch]$Help
)

if ($Help) {
    Write-Output @"
🔒 Wiley Widget Secret Management Helper

Usage:
  ./setup-secrets.ps1 -Create     # Create .env template
  ./setup-secrets.ps1 -Validate   # Check if secrets are properly configured
  ./setup-secrets.ps1 -Help       # Show this help

Examples:
  # First time setup
  ./setup-secrets.ps1 -Create
  # Edit the .env file with your actual values
  # Then validate
  ./setup-secrets.ps1 -Validate
"@
    exit 0
}

$envFile = ".env"

if ($Create) {
    Write-Information "🔧 Creating .env template..." -InformationAction Continue

    $envTemplate = @"
# Wiley Widget Environment Variables
# Copy this file and replace placeholder values with your actual secrets
# This file is already in .gitignore and will not be committed

# Database Configuration
DATABASE_CONNECTION_STRING=Server=localhost\\SQLEXPRESS01;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;

# Email Configuration (for error notifications)
EMAIL_FROM_ADDRESS=your-email@example.com
EMAIL_PASSWORD=your-app-password-here
EMAIL_SMTP_SERVER=smtp.gmail.com
EMAIL_SMTP_PORT=587
EMAIL_USERNAME=your-email@example.com
EMAIL_TO_ADDRESS=admin@wileywidget.local

# QuickBooks API Configuration
QBO_CLIENT_ID=your-quickbooks-client-id-here
QBO_CLIENT_SECRET=your-quickbooks-client-secret-here
QBO_REDIRECT_URI=https://localhost:5001/callback
QBO_ENVIRONMENT=Sandbox

# Syncfusion License (get from https://www.syncfusion.com/account/manage-license)
SYNCFUSION_LICENSE_KEY=your-syncfusion-license-key-here

# Azure configuration removed - project no longer uses Azure services.
# If you later re-enable Azure, add the necessary AZURE_* variables here or set them in your system environment.
"@

    $envTemplate | Out-File -FilePath $envFile -Encoding UTF8
    Write-Information "✅ Created $envFile template" -InformationAction Continue
    Write-Information "📝 Edit $envFile with your actual secret values" -InformationAction Continue
    Write-Information "🔐 Your secrets will be loaded automatically when you run the application" -InformationAction Continue
}

if ($Validate) {
    Write-Information "🔍 Validating secret configuration..." -InformationAction Continue

    if (-not (Test-Path $envFile)) {
        Write-Error "❌ .env file not found. Run with -Create first."
        exit 1
    }

    # Check for placeholder values that weren't replaced
    $content = Get-Content $envFile -Raw
    $placeholders = @(
        "your-email@example.com",
        "your-app-password-here",
        "your-quickbooks-client-id-here",
        "your-quickbooks-client-secret-here",
        "your-syncfusion-license-key-here",
        "your-subscription-id-here",
        "your-tenant-id-here",
        "your-client-id-here",
        "your-client-secret-here",
        "your-server.database.windows.net",
        "your-database-name",
        "your-keyvault.vault.azure.net"
    )

    $foundPlaceholders = @()
    foreach ($placeholder in $placeholders) {
        if ($content -match [regex]::Escape($placeholder)) {
            $foundPlaceholders += $placeholder
        }
    }

    if ($foundPlaceholders.Count -gt 0) {
        Write-Warning "⚠️  Found placeholder values that need to be replaced:"
        foreach ($placeholder in $foundPlaceholders) {
            Write-Warning "   - $placeholder"
        }
        Write-Information "📝 Edit $envFile and replace these with your actual values" -InformationAction Continue
    }
    else {
        Write-Information "✅ No placeholder values found" -InformationAction Continue
    }

    # Check if any secrets might be in config files
    $configFiles = @(
        "appsettings.json",
        "config/appsettings.json",
        "config/appsettings.Development.json",
        "config/appsettings.Production.json"
    )

    $foundSecrets = $false
    foreach ($configFile in $configFiles) {
        if (Test-Path $configFile) {
            $configContent = Get-Content $configFile -Raw
            # Look for values that don't look like environment variable placeholders
            if ($configContent -match '"[A-Za-z0-9+/]{20,}"' -and $configContent -notmatch '\$\{[A-Z_]+\}') {
                Write-Warning "⚠️  Potential hardcoded secret found in $configFile"
                $foundSecrets = $true
            }
        }
    }

    if (-not $foundSecrets) {
        Write-Information "✅ No hardcoded secrets found in configuration files" -InformationAction Continue
    }

    Write-Information "🔐 Validation complete" -InformationAction Continue
}

if (-not $Create -and -not $Validate) {
    Write-Information "Run with -Help to see usage instructions" -InformationAction Continue
}
