# Application Secrets & Key Vault Integration

This document lists the logical secrets the application expects and how they map to Azure Key Vault secret names and configuration keys.

## Key Vault Configuration

Add the vault name to configuration (e.g., `appsettings.Production.json`):

```json
{
  "Azure": {
    "KeyVaultName": "wileywidget-kv"
  }
}
```

The startup code (`ConfigureApplicationConfiguration`) will add the Azure Key Vault configuration provider automatically when this value is present, making secrets accessible through `IConfiguration` without code changes elsewhere.

## Secret Naming Convention

| Purpose                                | Key Vault Secret Name          | Consumed Configuration Keys (fallback order)                                     |
| -------------------------------------- | ------------------------------ | -------------------------------------------------------------------------------- |
| Syncfusion license                     | `Syncfusion-LicenseKey`        | (Resolved via custom method `GetSyncfusionLicenseKey`)                           |
| OpenAI API key                         | `OpenAI-ApiKey`                | `Secrets:OpenAI:ApiKey`, `OpenAI:ApiKey`, `OPENAI_API_KEY` env var               |
| Azure OpenAI Endpoint                  | `AzureAI-Endpoint`             | `Secrets:AzureAI:Endpoint`, `Azure:AI:Endpoint`                                  |
| Application Insights Connection String | `AppInsights-ConnectionString` | `ApplicationInsights:ConnectionString` (standard), env variable, then legacy key |
| QuickBooks Client Id                   | `QuickBooks-ClientId`          | (Future) `Secrets:QuickBooks:ClientId`, `QuickBooks:ClientId`                    |
| QuickBooks Client Secret               | `QuickBooks-ClientSecret`      | (Future) `Secrets:QuickBooks:ClientSecret`                                       |
| QuickBooks Redirect URI                | `QuickBooks-RedirectUri`       | (Future) `Secrets:QuickBooks:RedirectUri`                                        |
| QuickBooks Environment                 | `QuickBooks-Environment`       | (Future) `Secrets:QuickBooks:Environment`                                        |
| SQL Connection String                  | `Sql-ConnectionString`         | `ConnectionStrings:Sql` or overridden by EF options                              |

## Adding Secrets via Azure CLI

```powershell
az keyvault secret set --vault-name wileywidget-kv --name OpenAI-ApiKey --value <key>
az keyvault secret set --vault-name wileywidget-kv --name AzureAI-Endpoint --value https://your-azure-openai-endpoint
az keyvault secret set --vault-name wileywidget-kv --name AppInsights-ConnectionString --value "InstrumentationKey=...;IngestionEndpoint=..."
az keyvault secret set --vault-name wileywidget-kv --name Syncfusion-LicenseKey --value <license>
```

## Resolution Order

For each secret the application attempts resolution in this order:

1. Azure Key Vault (through configuration provider → `Secrets:*` path or direct conventional key)
2. JSON configuration (`appsettings*.json` or user secrets)
3. Environment variables
4. Fallback logic (where implemented, e.g., Key Vault direct retrieval for Syncfusion)

## Extending Secret Usage

When introducing a new secret:

1. Add it to Key Vault with a consistent hyphenated name.
2. Reference it in code via `IConfiguration` using a hierarchical key under `Secrets:` or existing domain section.
3. Update this document.

## Security Notes

- Prefer Managed Identity in production (DefaultAzureCredential already supports it).
- Remove plaintext secrets from environment variables once Key Vault is authoritative.
- Use separate vaults or RBAC segmentation for highly sensitive credentials if necessary.

---

Last updated: ${DATE}
