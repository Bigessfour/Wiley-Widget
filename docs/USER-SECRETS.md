# User Secrets Configuration Guide

## Overview

This application uses .NET User Secrets for storing sensitive API keys and credentials locally. User secrets are stored outside the project directory and are never committed to source control.

## Configured Services

### xAI Grok API (Recommendation Service)

The `GrokRecommendationService` uses the xAI Grok API for AI-powered budget recommendations.

**Configuration Requirements:**

- `XAI:Enabled` - Already set to `true` in appsettings.json
- `XAI:ApiKey` - Must be set in user-secrets (currently placeholder)
- `XAI:Endpoint` - Already set to `https://api.x.ai/v1`
- `XAI:Model` - Already set to `grok-4.1`

### QuickBooks API (Department Expense Service)

The `DepartmentExpenseService` can integrate with QuickBooks for real financial data.

**Configuration Requirements:**

- `QuickBooks:Enabled` - Already set to `true` in appsettings.json
- No API key required for current sample data mode

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
.\scripts\setup-xai-secrets.ps1
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

### 4. Verify Configuration

After setting the API key, rebuild and run the application:

```powershell
dotnet build src\WileyWidget.WinForms\WileyWidget.WinForms.csproj
dotnet run --project src\WileyWidget.WinForms\WileyWidget.WinForms.csproj
```

Check the startup logs (in `logs/` directory) for:

```
[INF] xAI Grok API enabled with model grok-4.1
```

If you see:

```
[WRN] xAI Grok API disabled - using rule-based recommendations (set XAI:Enabled=true and provide XAI:ApiKey)
```

The API key is not set correctly or is still the placeholder value.

## Getting an xAI API Key

1. Visit: https://x.ai/
2. Sign up for an xAI account
3. Navigate to the API section
4. Generate a new API key
5. Copy the key and set it using one of the methods above

## Security Best Practices

✅ **DO:**

- Use user-secrets for local development
- Use Azure Key Vault or similar for production
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

**Status:** This is expected - QuickBooks API integration requires OAuth setup (future enhancement)

**Current Behavior:** Service uses realistic sample data when `QuickBooks:Enabled: true`

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
