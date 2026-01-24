# xAI Grok API Key Management & User Secrets Configuration

## Overview

This document describes production-ready configuration for the xAI Grok API key used by the JARVIS Chat feature in Wiley Widget. The system follows **Microsoft's recommended configuration hierarchy** for secure secret management in .NET applications.

**Last Updated**: January 2026  
**Status**: Production-Ready  
**Scope**: JARVIS Chat (Blazor + WinForms), Semantic Kernel Integration

---

## Configuration Hierarchy (Priority Order)

The xAI API key is resolved in the following order (first match wins):

### ✅ **Tier 1: User Secrets (HIGHEST PRIORITY - RECOMMENDED FOR DEVELOPMENT)**

**Location**: `%APPDATA%\Microsoft\UserSecrets\WileyWidget.WinForms-12345678-1234-1234-1234-123456789012\secrets.json`

**Setup Command**:
```powershell
cd src/WileyWidget.WinForms
dotnet user-secrets set XAI:ApiKey "xai-your-api-key-here"
```

**Why User Secrets?**
- ✅ Stored outside source control (safe)
- ✅ Encrypted at rest on Windows
- ✅ Per-user, per-machine isolation
- ✅ Survives app restarts
- ✅ Microsoft recommended practice
- ✅ Overrides environment variables and appsettings.json

**Verify Secrets Are Configured**:
```powershell
dotnet user-secrets list
```

**Example secrets.json** (manual location if needed):
```json
{
  "XAI:ApiKey": "xai-your-actual-api-key"
}
```

---

### ⚠️ **Tier 2: Environment Variables (PROCESS SCOPE - CI/CD FRIENDLY)**

**Setup Command** (PowerShell):
```powershell
# Set for current process only (temporary, session-based)
$env:XAI_API_KEY = "xai-your-api-key"
```

**Permanent Setup** (Windows):
```powershell
# Set in Windows environment variables (persistent, machine/user scope)
[Environment]::SetEnvironmentVariable("XAI_API_KEY", "xai-your-api-key", [EnvironmentVariableTarget]::User)
```

**Why Environment Variables?**
- ✅ CI/CD friendly (GitHub Actions, Azure DevOps)
- ✅ Container-friendly (Docker, Kubernetes)
- ⚠️ Visible in process lists (potential exposure)
- ⚠️ Not encrypted at rest

**Note**: Migration from machine-scoped environment variable is **REQUIRED** for production.

---

### ❌ **Tier 3: appsettings.json (LOWEST PRIORITY - NOT RECOMMENDED)**

**Location**: `src/WileyWidget.WinForms/appsettings.json`

**Configuration Key**: `XAI:ApiKey`

**Example** (DO NOT COMMIT ACTUAL KEY):
```json
{
  "XAI": {
    "ApiKey": "xai-placeholder-key-here"
  }
}
```

**Why NOT Recommended**:
- ❌ Risk of accidental commit to source control
- ❌ Visible to anyone with repository access
- ❌ Not encrypted at rest

**Use Only For**:
- Local development (with placeholder/test key)
- Configuration defaults (never actual keys)

---

## Production Deployment Checklist

### Before Deploying to Production:

- [ ] **Create xAI Account & API Key**
  - Visit: https://console.x.ai/
  - Generate API key with appropriate permissions
  - Store in secure location (password manager, vault)

- [ ] **Migrate from Machine Env Var to User Secrets**
  ```powershell
  # 1. Verify current machine-scoped env var
  Get-ItemProperty -Path 'HKCU:\Environment' -Name 'XAI_API_KEY' -ErrorAction SilentlyContinue
  
  # 2. Copy value to user.secrets
  dotnet user-secrets set XAI:ApiKey "xai-<your-key>"
  
  # 3. Verify user.secrets
  dotnet user-secrets list
  
  # 4. Remove machine env var (optional, but recommended)
  Remove-Item -Path 'HKCU:\Environment\XAI_API_KEY' -Force
  ```

- [ ] **Validate Configuration at Startup**
  - Application runs `GrokHealthCheck` on startup
  - Checks: API key configured, endpoint reachable, authentication succeeds
  - Health check result shown in logs

- [ ] **Enable Logging & Monitoring**
  - Grok service logs all API calls (with masked keys)
  - Set `Logging:LogLevel:WileyWidget.WinForms.Services.AI: Information`
  - Monitor for 401/403 (auth failures) and 429 (rate limits)

- [ ] **Document Rotation Policy**
  - API keys should rotate every 90 days
  - Update: `dotnet user-secrets set XAI:ApiKey "xai-<new-key>"`
  - Restart application for changes to take effect

---

## Runtime Configuration

### appsettings.json Default Configuration

```json
{
  "Grok": {
    "Model": "grok-4",
    "Endpoint": "https://api.x.ai/v1",
    "DefaultPresencePenalty": 0.0,
    "DefaultFrequencyPenalty": 0.0,
    "AutoSelectModelOnStartup": false,
    "ValidateOnStartup": true
  },
  "XAI": {
    "DefaultPresencePenalty": 0.0,
    "DefaultFrequencyPenalty": 0.0
  }
}
```

### Configuration Keys

| Key | Default | Description |
|-----|---------|-------------|
| `Grok:ApiKey` | `null` | ⚠️ Do not set here; use user.secrets instead |
| `Grok:Model` | `grok-4` | xAI model to use (grok-4, grok-4-1-fast, etc.) |
| `Grok:Endpoint` | `https://api.x.ai/v1` | xAI API endpoint (use new `/v1/responses` format) |
| `Grok:ValidateOnStartup` | `true` | Run health check on app start |
| `Grok:AutoSelectModelOnStartup` | `false` | Auto-detect best available model |

---

## Code Integration

### IGrokApiKeyProvider (Production-Ready API Key Management)

The `IGrokApiKeyProvider` service encapsulates all API key resolution logic:

```csharp
// Injected automatically into services that need it
public class MyService
{
    private readonly IGrokApiKeyProvider _keyProvider;

    public MyService(IGrokApiKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
        
        // Safe to log masked key
        var maskedKey = _keyProvider.MaskedApiKey;  // "xai-...abc123"
        
        // Check validation status
        if (!_keyProvider.IsValidated)
        {
            logger.LogWarning("Grok API key not validated at startup");
        }
        
        // Get configuration source for diagnostics
        var source = _keyProvider.GetConfigurationSource();
        // Example: "API Key: configuration (XAI:ApiKey) | Validated: true | Source: User Secrets (Secure)"
    }
}
```

### Startup Health Check

The application automatically runs `GrokHealthCheck` during startup:

```csharp
public sealed class GrokHealthCheck : IHealthCheck
{
    // Verifies:
    // 1. API key is configured (not null)
    // 2. API endpoint is reachable
    // 3. Authentication succeeds (401/403 detection)
    // 4. Returns Healthy/Degraded/Unhealthy status
}
```

**Health Check Results**:
- `HEALTHY` ✅: API key valid, service ready for chat
- `DEGRADED` ⚠️: API key configured but unreachable or invalid
- `UNHEALTHY` ❌: API key missing or authentication failed

---

## Chat History Persistence

The JARVIS Chat feature stores conversation history in the database for context preservation:

### Database Tables

**ConversationHistory**:
```sql
CREATE TABLE ConversationHistories (
    ConversationId NVARCHAR(MAX) PRIMARY KEY,
    Title NVARCHAR(MAX),
    Content NVARCHAR(MAX),
    MessagesJson NVARCHAR(MAX),  -- JSON array of { Role, Content } objects
    MessageCount INT,
    CreatedAt DATETIME2,
    UpdatedAt DATETIME2
);
```

### Integration Points

1. **JARVISAssist.razor** (Blazor Chat Component)
   - Generates conversation ID (Guid)
   - Passes to `ChatBridge.SubmitPromptAsync(prompt, conversationId)`

2. **GrokAgentService** (Backend)
   - Loads history: `LoadChatHistoryAsync(conversationId, systemPrompt)`
   - Builds Semantic Kernel `ChatHistory` from stored messages
   - Sends to Grok with context
   - Saves response: `SaveChatHistoryAsync(conversationId, history)`

3. **EfConversationRepository** (Data Layer)
   - Implements `IConversationRepository`
   - Uses `IDbContextFactory<AppDbContext>` for scoped access
   - Supports: Save, Get, GetAll, Delete operations

### ChatHistoryHealthCheck

Verifies database connectivity for persistence at startup:

```csharp
public sealed class ChatHistoryHealthCheck : IHealthCheck
{
    // Checks:
    // 1. IConversationRepository is registered
    // 2. Database is reachable
    // 3. ConversationHistory table exists
}
```

---

## Diagnostic & Logging

### Enabled Logging

Set in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "WileyWidget.WinForms.Services.AI.GrokAgentService": "Debug",
      "WileyWidget.WinForms.Services.AI.GrokApiKeyProvider": "Information",
      "WileyWidget.WinForms.Services.AI.GrokHealthCheck": "Information",
      "WileyWidget.Services.EfConversationRepository": "Debug"
    }
  }
}
```

### Log Examples

**Startup Logs**:
```
[INF] Initializing API key from configuration hierarchy...
[INF] API key loaded from user.secrets (XAI:ApiKey) (length: 48, fromUserSecrets: True)
[INF] Starting Grok API health check...
[INF] API key configured ✅ (from user.secrets - secure)
[INF] ✅ Grok API health check PASSED - API key validated successfully (xai-...c123)
[INF] Grok service async initialization complete
```

**Chat Execution**:
```
[INF] RunAgentToChatBridgeAsync invoked - User request length: 42, ConversationId: 550e8400-e29b-41d4-a716-446655440000
[DBG] Loaded chat history from L1 memory cache for 550e8400-e29b-41d4-a716-446655440000
[DBG] Invoking streaming chat with ToolCallBehavior.AutoInvokeKernelFunctions - Plugins: 3
[INF] RunAgentToChatBridgeAsync completed via Semantic Kernel streaming - Response length: 245
[DBG] Saved conversation 550e8400-e29b-41d4-a716-446655440000 with 4 messages
```

### Masking & Security

- API keys are **never logged in full**
- Logged as `xai-...abc123` (first 4 + last 4 characters)
- Configuration source always logged (user.secrets vs env vs config)
- Health checks provide masked status without exposing actual key

---

## Troubleshooting

### Issue: "No API key configured"

**Symptom**: JARVIS Chat shows "No API key configured for Grok"

**Solution**:
```powershell
# 1. Verify user.secrets
dotnet user-secrets list

# 2. If not set, configure now
dotnet user-secrets set XAI:ApiKey "xai-your-key"

# 3. Restart application

# 4. Check startup logs for health check result
```

### Issue: "API key validation timed out"

**Symptom**: Startup hangs or health check times out

**Causes**:
- Network connectivity issue
- xAI endpoint unreachable
- Firewall blocking api.x.ai

**Solution**:
```powershell
# Test connectivity
Test-NetConnection -ComputerName api.x.ai -Port 443

# Check firewall
Get-NetFirewallRule | Where-Object { $_.DisplayName -like "*api*" }
```

### Issue: "API key is invalid or expired"

**Symptom**: 401 Unauthorized from xAI

**Solution**:
```powershell
# 1. Verify key in console.x.ai
# 2. Update secrets
dotnet user-secrets set XAI:ApiKey "xai-new-key"

# 3. Restart application

# 4. Check logs for successful validation
```

### Issue: Chat history not persisting

**Symptom**: Closing/reopening app loses conversation

**Causes**:
- Database not configured
- `IConversationRepository` not registered
- Database connection failure

**Solution**:
```powershell
# 1. Verify database health check
# Look for: "Chat history persistence is ready"

# 2. Check EfConversationRepository logs
Get-Content app.log | Select-String "EfConversationRepository"

# 3. Verify database connectivity
# (See database setup documentation)
```

---

## Migration Guide: Machine Env Var → User Secrets

### For Current Installations

**Step 1: Backup Current Configuration**
```powershell
# Get current machine env var
$key = [Environment]::GetEnvironmentVariable("XAI_API_KEY", [EnvironmentVariableTarget]::Machine)
Write-Host "Current key length: $($key.Length) chars"
```

**Step 2: Migrate to User Secrets**
```powershell
cd src/WileyWidget.WinForms

# Set in user.secrets
dotnet user-secrets set XAI:ApiKey "xai-paste-your-key-here"

# Verify
dotnet user-secrets list
# Output should show: XAI:ApiKey = xai-****...
```

**Step 3: Test**
```powershell
# Start application
dotnet run

# Check startup logs for:
# "[INF] API key loaded from user.secrets"
# "[INF] ✅ Grok API health check PASSED"
```

**Step 4: Cleanup (Optional)**
```powershell
# Remove machine env var
Remove-Item -Path 'HKCU:\Environment\XAI_API_KEY' -Force

# Restart terminal/app to clear process env var
```

---

## References

- **Microsoft .NET Configuration Documentation**: https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration
- **Safe Storage of App Secrets (User Secrets)**: https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets
- **xAI Grok API Documentation**: https://docs.x.ai/docs/api-reference
- **xAI Console**: https://console.x.ai/

---

## Support & Questions

For issues with JARVIS Chat API key configuration:

1. **Check logs**: Look for `GrokHealthCheck`, `GrokApiKeyProvider` entries
2. **Review this document**: Most common issues covered above
3. **Validate configuration**: `dotnet user-secrets list`
4. **Test connectivity**: `Test-NetConnection -ComputerName api.x.ai -Port 443`

---

**Last Updated**: January 2026  
**Version**: 1.0 Production-Ready  
**Maintainer**: Wiley Widget Development Team
