# User Secrets Configuration Guide

## Overview

This application uses .NET User Secrets for storing sensitive API keys and credentials locally. User secrets are stored outside the project directory and are never committed to source control.

> Machine scope is the canonical environment source for runtime and MCP operations.
> User secrets and user-scope environment values are supported as compatibility fallbacks during migration.
> See `docs/ENVIRONMENT_SCOPE_POLICY.md` for canonical names and alias/deprecation policy.

## Configured Services

### xAI Grok API (Recommendation Service)

The `GrokRecommendationService` uses the xAI Grok API for AI-powered budget recommendations.

**Configuration Requirements:**

- `XAI:Enabled` - Already set to `true` in appsettings.json
- `XAI:ApiKey` - Must be set in user-secrets (currently placeholder)
- `XAI:Endpoint` - Already set to `https://api.x.ai/v1`
- `XAI:Model` - Already set to `grok-4.1`

### QuickBooks API (Department Expense Service)

The `DepartmentExpenseService` integrates with QuickBooks for real financial data via OAuth 2.0.

**Configuration Requirements:**

- `QuickBooks:Enabled` - Already set to `true` in appsettings.json
- `Services:QuickBooks:OAuth:ClientId` - Must be set in user-secrets
- `Services:QuickBooks:OAuth:ClientSecret` - Must be set in user-secrets
- `Services:QuickBooks:OAuth:RealmId` - Optional (set in user-secrets if using multiple companies)

**What is QuickBooks OAuth:**

- Industry-standard OAuth 2.0 authentication
- Does NOT require storing user passwords
- Company-specific Realm ID identifies which QuickBooks company to query
- Tokens are cached locally (tokenization prevents re-authentication)

## Setting Up User Secrets

### 1. Install .NET User Secrets Tool (if not already installed)

```powershell
dotnet tool install --global dotnet-user-secrets
```

### 2. Initialize User Secrets for the Project

The project already has a UserSecretsId configured:

```xml
<UserSecretsId>WileyWidget.WinForms-12345678-1234-1234-1234-123456789012</UserSecretsId>
```

### 3. Set xAI API Key

#### Option A: Using dotnet CLI (Recommended)

```powershell
# Navigate to the WinForms project directory
cd src\WileyWidget.WinForms

# Set the xAI API key
dotnet user-secrets set "XAI:ApiKey" "YOUR_XAI_API_KEY_HERE"
```

#### Option B: Using PowerShell Script

```powershell
# Run the setup script from repository root
.\scripts\setup\setup-integration-services.ps1
```

#### Option C: Manually Edit secrets.json

1. Locate your secrets file:
   - Windows: `%APPDATA%\Microsoft\UserSecrets\WileyWidget.WinForms-12345678-1234-1234-1234-123456789012\secrets.json`
   - Linux/macOS: `~/.microsoft/usersecrets/WileyWidget.WinForms-12345678-1234-1234-1234-123456789012/secrets.json`

2. Edit or create the file with:

```json
{
  "XAI": {
    "ApiKey": "YOUR_XAI_API_KEY_HERE"
  }
}
```

### 4. Set QuickBooks OAuth Credentials (Optional)

Getting QuickBooks credentials requires a developer account in the Intuit Developer Program.

#### Prerequisites

1. Intuit Developer Account: https://developer.intuit.com
2. Create or select an app in the Developer Portal
3. Generate OAuth 2.0 credentials

#### Option A: Using dotnet CLI (Recommended)

```powershell
# Navigate to the WinForms project directory
cd src\WileyWidget.WinForms

# Set QuickBooks OAuth credentials
dotnet user-secrets set "Services:QuickBooks:OAuth:ClientId" "YOUR_CLIENT_ID_HERE"
dotnet user-secrets set "Services:QuickBooks:OAuth:ClientSecret" "YOUR_CLIENT_SECRET_HERE"

# Optional: Set the Realm ID (QuickBooks Company ID)
dotnet user-secrets set "Services:QuickBooks:OAuth:RealmId" "YOUR_REALM_ID_HERE"
```

#### Option B: Using PowerShell Script

```powershell
# Run the setup script from repository root
.\scripts\setup\setup-integration-services.ps1
```

#### Option C: Manually Edit secrets.json

1. Locate your secrets file (same as above)

2. Add/update the QuickBooks section:

```json
{
  "XAI": {
    "ApiKey": "YOUR_XAI_API_KEY_HERE"
  },
  "Services": {
    "QuickBooks": {
      "OAuth": {
        "ClientId": "YOUR_CLIENT_ID_HERE",
        "ClientSecret": "YOUR_CLIENT_SECRET_HERE",
        "RealmId": "YOUR_REALM_ID_HERE"
      }
    }
  }
}
```

### 5. Verify Configuration

After setting the API keys, rebuild and run the application:

```powershell
dotnet build src\WileyWidget.WinForms\WileyWidget.WinForms.csproj
dotnet run --project src\WileyWidget.WinForms\WileyWidget.WinForms.csproj
```

#### Check xAI Configuration

Check the startup logs (in `logs/` directory) for:

```
[INF] [GrokRecommendation] Using API key from UserSecrets (Enabled: True, Validated: True)
```

Or when disabled:

```
[WRN] xAI Grok API disabled - using rule-based recommendations (set XAI:Enabled=true and provide XAI:ApiKey)
```

#### Check QuickBooks Configuration

Check the startup logs for:

```
[INF] QuickBooks integration enabled.
[INF] Querying expenses for {Department} (QuickBooksEnabled=True)
```

Or when disabled:

```
[WRN] QuickBooks integration disabled - using sample data (set QuickBooks:Enabled=true in config)
```

#### Verify User Secrets Are Set

```powershell
# List all configured user-secrets
cd src\WileyWidget.WinForms
dotnet user-secrets list

# Output should include:
# XAI:ApiKey = ****...
# Services:QuickBooks:OAuth:ClientId = ****...
# Services:QuickBooks:OAuth:ClientSecret = ****...
```

The API key is not set correctly or is still the placeholder value.

## Getting API Keys and Credentials

### xAI API Key

1. Visit: https://x.ai/
2. Sign up for an xAI account
3. Navigate to the API section (https://console.x.ai/)
4. Generate a new API key
5. Copy the key and set it using one of the methods above
6. Test with cURL or Postman to verify key validity

### QuickBooks OAuth Credentials

1. Create an Intuit Developer account: https://developer.intuit.com
2. Create a new app in the Developer Portal
3. Enable QuickBooks Online accounting API
4. Configure OAuth 2.0 settings:
   - **Redirect URIs:** Add `http://localhost:5000/callback` for local development
   - **Scopes:** Select "com.intuit.quickbooks.accounting"
5. In the "Keys & OAuth" section, copy:
   - **Client ID** (also called Consumer Key)
   - **Client Secret** (also called Consumer Secret)
6. Set these credentials using user-secrets (see step 4 above)
7. When connecting for the first time:
   - User logs in with QuickBooks credentials
   - Authorizes the app to access their data
   - System automatically stores Realm ID and access token
   - Tokens are refreshed automatically as needed

**Note:** The app uses OAuth 2.0's authorization code flow, which means users authorize once and the app stores encrypted credentials for API calls. No user passwords are stored locally.

## Security Best Practices

✅ **DO:**

- Use DPAPI-encrypted secrets for local development
- Use DPAPI for production (desktop app)
- Use environment variables in CI/CD pipelines
- Add `secrets.json` to `.gitignore` (already configured)

❌ **DON'T:**

- Commit API keys to source control
- Share secrets.json files
- Use production keys in development
- Store secrets in appsettings.json (use placeholders only)

## Troubleshooting

### Issue: "xAI Grok API disabled" warning in logs

**Causes:**

1. API key not set in user-secrets
2. API key is still the placeholder value
3. `XAI:Enabled` is false in appsettings.json (already true)

**Solution:**
Follow the "Set xAI API Key" steps above.

### Issue: User secrets not loading

**Causes:**

1. Not running from WinForms project directory
2. UserSecretsId mismatch in .csproj
3. Secrets file has invalid JSON syntax

**Solution:**

1. Verify UserSecretsId matches in .csproj and secrets file location
2. Validate JSON syntax in secrets.json
3. Check that appsettings.json has `XAI:Enabled: true`

### Issue: QuickBooks showing as disabled

**Causes:**

1. OAuth credentials not set in user-secrets
2. `QuickBooks:Enabled` is false in appsettings.json
3. Internal configuration not loading properly

**Solution:**

Follow the "Set QuickBooks OAuth Credentials" steps above and ensure:

- `Services:QuickBooks:OAuth:ClientId` is set in user-secrets
- `Services:QuickBooks:OAuth:ClientSecret` is set in user-secrets
- Restart the application for changes to take effect

### Issue: QuickBooks authorization fails

**Causes:**

1. Redirect URI not configured correctly in Intuit Developer Portal
2. OAuth scopes mismatched between app and Intuit config
3. Credentials expired or revoked

**Solution:**

1. Verify redirect URI in Intuit Developer Portal matches app configuration
2. Ensure `com.intuit.quickbooks.accounting` scope is enabled
3. Re-authenticate by navigating to Budget/Expense panel
4. Check application logs for OAuth error details

## Configuration Hierarchy

.NET configuration sources are loaded in this order (later sources override earlier):

1. appsettings.json (defaults and structure)
2. appsettings.{Environment}.json (environment-specific)
3. User Secrets (development only)
4. Environment Variables
5. Command-line arguments

This means user-secrets will override the placeholder values in appsettings.json.

## Related Files

- Configuration Schema: `src\WileyWidget.WinForms\appsettings.json`
- Project File: `src\WileyWidget.WinForms\WileyWidget.WinForms.csproj`
- Grok Service: `src\WileyWidget.Business\Services\GrokRecommendationService.cs`
- QuickBooks Service: `src\WileyWidget.Business\Services\DepartmentExpenseService.cs`

## References

- [Safe storage of app secrets in development in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [Configuration in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [xAI API Documentation](https://docs.x.ai/)
