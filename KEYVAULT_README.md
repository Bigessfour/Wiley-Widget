# Azure Key Vault Setup for Wiley Widget

## Overview
This project uses Azure Key Vault to securely store API keys and secrets instead of environment variables or hardcoded values.

## Key Vault Details
- **Name**: `wiley-widget-secrets`
- **Resource Group**: `WileyWidgetRG`
- **Location**: `eastus`
- **URL**: `https://wiley-widget-secrets.vault.azure.net/`

## Stored Secrets
- `SYNCFUSION-LICENSE-KEY`: Syncfusion license key
- `XAI-API-KEY`: XAI API key

## Setup Process
1. ✅ Azure Key Vault created with RBAC authorization
2. ✅ Current user assigned "Key Vault Secrets Officer" role
3. ✅ API keys migrated from machine environment variables
4. ✅ Machine environment variables cleared for security
5. ✅ PowerShell scripts created for secret loading

## Usage

### Load Secrets for Current Session
```powershell
.\scripts\load-mcp-secrets.ps1
```

### Start Application with Secrets
```powershell
.\scripts\start-with-secrets.ps1
```

### Manual Secret Retrieval
```powershell
# Get Syncfusion License Key
az keyvault secret show --vault-name "wiley-widget-secrets" --name "SYNCFUSION-LICENSE-KEY"

# Get XAI API Key
az keyvault secret show --vault-name "wiley-widget-secrets" --name "XAI-API-KEY"
```

## Security Benefits
- ✅ Secrets not stored in environment variables
- ✅ Secrets not committed to Git
- ✅ Centralized secret management
- ✅ RBAC-based access control
- ✅ Audit logging for all secret access
- ✅ Automatic secret rotation support

## MCP Configuration
The `mcp-config.json` uses environment variables that are populated by the Key Vault loading script.

## Maintenance
- Secrets are loaded into session environment variables only
- Run the loading script at the start of each development session
- Monitor Key Vault access logs in Azure Portal
- Rotate secrets regularly using Azure Key Vault features

## Troubleshooting
- Ensure Azure CLI is authenticated: `az account show`
- Check Key Vault access: `az keyvault secret list --vault-name "wiley-widget-secrets"`
- Verify RBAC permissions if access denied
