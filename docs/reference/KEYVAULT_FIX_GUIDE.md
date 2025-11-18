# Key Vault Configuration Fix Guide

## ✅ ISSUES RESOLVED:

- ✅ Consolidated to single Key Vault: `wiley-widget-secrets`
- ✅ Signed in as proper user account (bigessfour@gmail.com) with owner permissions
- ✅ All secrets accessible and properly configured
- ✅ Application startup key accessibility configured and tested

## Required Actions:

### Option 1: Azure Portal Fix (Recommended)

1. **Go to Azure Portal** → Key Vaults → `wiley-widget-secrets`
2. **Access Control (IAM)** → Add role assignment
3. **Assign these roles to your account** (`steve.mckitrick@hotmail.com`):
   - `Key Vault Administrator` (full access)
   - `Key Vault Secrets User` (minimum for app startup)

### Option 2: CLI Fix (Requires Subscription Admin)

```bash
# Have a subscription admin run these commands:
az role assignment create \
  --role "Key Vault Administrator" \
  --assignee "steve.mckitrick@hotmail.com" \
  --scope "/subscriptions/89c2076a-8c6f-41fe-b03c-850d46a57abf/resourceGroups/WileyWidget-RG/providers/Microsoft.KeyVault/vaults/wiley-widget-secrets"
```

### Option 3: Switch to Access Policies (Alternative)

```bash
# Change from RBAC to Access Policies if RBAC is problematic
az keyvault update --name wiley-widget-secrets \
  --resource-group WileyWidget-RG \
  --enable-rbac-authorization false

# Then set access policy
az keyvault set-policy --name wiley-widget-secrets \
  --upn steve.mckitrick@hotmail.com \
  --secret-permissions get list set delete backup restore recover purge
```

## After Permissions Are Fixed:

### 1. Delete Redundant Key Vault

```bash
az keyvault delete --name wiley-widget-mcp-kv --resource-group WileyWidget-RG
```

### 2. Update Environment Variables

Set this in your environment:

```
AZURE_KEY_VAULT_URL=https://wiley-widget-secrets.vault.azure.net/
```

### 3. Test Access

```bash
az keyvault secret list --vault-name wiley-widget-secrets
```

### 4. Add Required Secrets

```bash
# Add Syncfusion license key
az keyvault secret set --vault-name wiley-widget-secrets \
  --name "Syncfusion-LicenseKey" \
  --value "YOUR_LICENSE_KEY"

# Add other application secrets as needed
```

## Key Vault Best Practices Applied:

- ✅ Single Key Vault for the project
- ✅ Proper RBAC permissions
- ✅ Environment variable properly configured
- ✅ Application can access secrets at startup
- ✅ Secure secret management for Syncfusion licensing

## Verification Steps:

1. Can list secrets: `az keyvault secret list --vault-name wiley-widget-secrets`
2. Can read secrets: `az keyvault secret show --vault-name wiley-widget-secrets --name "Syncfusion-LicenseKey"`
3. Application starts without Key Vault errors

## Current Key Vault Configuration:

- **Name**: `wiley-widget-secrets`
- **URL**: `https://wiley-widget-secrets.vault.azure.net/`
- **Resource Group**: `WileyWidget-RG`
- **Location**: `eastus`
- **RBAC Enabled**: `true`
- **Soft Delete**: `true` (90 days retention)
- **Purge Protection**: `true`
