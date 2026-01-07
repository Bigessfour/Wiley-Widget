# WileyWidget AI Configuration Guide

**Version**: 1.0
**Last Updated**: 2026-01-03

## 📋 Table of Contents

1. [Configuration Overview](#configuration-overview)
2. [Development Setup](#development-setup)
3. [Production Setup](#production-setup)
4. [Model Selection](#model-selection)
5. [Performance Tuning](#performance-tuning)
6. [Security Configuration](#security-configuration)
7. [Caching Configuration](#caching-configuration)
8. [Monitoring Configuration](#monitoring-configuration)

---

## Configuration Overview

WileyWidget's AI services are configured through:

- **appsettings.json** - Base configuration
- **User Secrets** - Development API keys (dotnet user-secrets)
- **EncryptedLocalSecretVaultService** - Production secrets (Windows DPAPI encrypted)
- **Environment Variables** - Runtime overrides

### Configuration Priority

```text
1. Environment Variables (highest priority)
2. EncryptedLocalSecretVaultService (production: %APPDATA%\WileyWidget\Secrets)
3. User Secrets (development: dotnet user-secrets)
4. appsettings.{Environment}.json
5. appsettings.json (lowest priority)
```

---

## Development Setup

### Step 1: Initialize User Secrets

```bash
cd src/WileyWidget.WinForms
dotnet user-secrets init
dotnet user-secrets set "XAI:ApiKey" "xai-YOUR_KEY_HERE"
```

### Step 2: Create appsettings.Development.json

```json
{
  "XAI": {
    "Enabled": true,
    "Endpoint": "https://api.x.ai/v1/chat/completions",
    "Model": "grok-4-1-fast",
    "Temperature": 0.3,
    "MaxTokens": 800,
    "TimeoutSeconds": 30,
    "MaxConcurrentRequests": 3,
    "CircuitBreakerBreakSeconds": 30
  },
  "GrokRecommendation": {
    "CacheDuration": "00:30:00"
  },
  "AppOptions": {
    "BudgetVarianceHighThresholdPercent": 10.0,
    "BudgetVarianceLowThresholdPercent": -5.0,
    "AIHighConfidence": 90,
    "AILowConfidence": 60,
    "EnableDataCaching": true,
    "EnterpriseDataCacheSeconds": 5
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "WileyWidget.Services.XAIService": "Debug",
      "WileyWidget.Business.Services.GrokRecommendationService": "Debug",
      "WileyWidget.Services.GrokSupercomputer": "Information"
    }
  }
}
```

### Step 3: Verify Configuration

```bash
# List all secrets
dotnet user-secrets list

# Test configuration
dotnet run --project src/WileyWidget.WinForms
```

### Plugin Auto-Registration

Grok agent (`GrokAgentService`) automatically scans the executing assembly for plugin classes whose methods are annotated with `[KernelFunction]` and imports them into the Semantic Kernel during construction. To add a plugin:

- Create a public class (e.g., put under `WileyWidget.WinForms.Plugins`) and annotate its methods with `[KernelFunction("name")]`.

- The service will import these plugin types at runtime; failures are logged as warnings.

Example:

```csharp
public sealed class EchoPlugin
{
    [KernelFunction("echo")]
    public string Echo(string message) => message;
}

```

Now any new plugin method will be available to kernel flows without additional registration.

---

## Production Setup

### Step 1: Configure EncryptedLocalSecretVaultService

Wiley Widget uses **EncryptedLocalSecretVaultService** for production secrets, which stores encrypted secrets using Windows DPAPI (Data Protection API) with `DataProtectionScope.LocalMachine` at:

```text
%APPDATA%\WileyWidget\Secrets
```

Secrets are encrypted at machine scope (usable by any account on the same host, subject to filesystem ACLs) and cannot be decrypted off the machine. Legacy user-scoped blobs are auto-migrated to machine scope on first read.

### Step 2: Store Secrets via Secret Vault

```csharp
// Via ISecretVaultService (injected)
public async Task ConfigureProductionSecretsAsync(ISecretVaultService secretVault)
{
    // Store XAI API key
    await secretVault.SetSecretAsync("XAI-API-KEY", "xai-YOUR_PRODUCTION_KEY");

    // Store other critical secrets
    await secretVault.SetSecretAsync("QBO-CLIENT-ID", "your-qb-client-id");
    await secretVault.SetSecretAsync("QBO-CLIENT-SECRET", "your-qb-client-secret");
    await secretVault.SetSecretAsync("SYNCFUSION_LICENSE_KEY", "your-syncfusion-key");
}
```

**Storage Location**: Each secret is stored as an encrypted file:

```text
C:\Users\{Username}\AppData\Roaming\WileyWidget\Secrets\XAI-API-KEY.enc
C:\Users\{Username}\AppData\Roaming\WileyWidget\Secrets\QBO-CLIENT-ID.enc
```

### Step 3: Production Configuration

**appsettings.Production.json**:

```json
{
  "XAI": {
    "Enabled": true,
    "Endpoint": "https://api.x.ai/v1/chat/completions",
    "Model": "grok-4",
    "Temperature": 0.3,
    "MaxTokens": 800,
    "TimeoutSeconds": 15,
    "MaxConcurrentRequests": 5,
    "CircuitBreakerBreakSeconds": 60
  },
  "GrokRecommendation": {
    "CacheDuration": "02:00:00"
  },
  "AppOptions": {
    "EnableDataCaching": true,
    "EnterpriseDataCacheSeconds": 60
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "WileyWidget.Services": "Information",
      "WileyWidget.Business": "Information"
    }
  }
}
```

### Step 4: Environment Variables

```bash
# Windows
setx ASPNETCORE_ENVIRONMENT "Production"
setx DOTNET_ENVIRONMENT "Production"

# Optional: Override secrets via environment variables
setx XAI_API_KEY "xai-YOUR_KEY"
setx SYNCFUSION_LICENSE_KEY "your-syncfusion-key"
```

**Note**: Environment variables have **highest priority** and will override secrets from EncryptedLocalSecretVaultService if set.

---

## Model Selection

### Available Models (January 2026)

| Model             | Context Window | Speed     | Quality | Features                         | Use Case                      |
| ----------------- | -------------- | --------- | ------- | -------------------------------- | ----------------------------- |
| **grok-4-1-fast** | 2,000,000      | Lightning | High    | Agentic tool calling, structured | Real-time, high-throughput    |
| **grok-4**        | 131,072        | Medium    | Best    | Reasoning, multimodal            | Complex analytics, compliance |
| **grok-4-latest** | 131,072        | Medium    | Best    | Auto-updates to latest           | Production (stable)           |
| **grok-3**        | 131,072        | Fast      | Good    | Previous generation              | Legacy compatibility          |

**Knowledge Cut-off**: November 2024 for Grok 3 and Grok 4

**Pricing** (as of Jan 2026):

- **grok-4-1-fast**: Input $0.50/1M tokens, Output $1.50/1M tokens
- **grok-4**: Input $2/1M tokens, Output $8/1M tokens
- **grok-3**: Input $1/1M tokens, Output $4/1M tokens

**Important**: Grok 4 is a reasoning model with no non-reasoning mode. Parameters `presencePenalty`, `frequencyPenalty`, `stop`, and `reasoning_effort` are **not supported** and will cause errors.

### Configuration by Environment

```json
// Development (fast, low cost)
"XAI": { "Model": "grok-4-1-fast" }

// Staging (balanced)
"XAI": { "Model": "grok-4-latest" }

// Production (best quality, stable)
"XAI": { "Model": "grok-4" }
```

### Dynamic Model Selection

```csharp
public class AdaptiveAIService
{
    public async Task<string> GetInsightsAsync(string query, QueryComplexity complexity)
    {
        var model = complexity switch
        {
            QueryComplexity.Simple => "grok-4-1-fast",    // Fast, agentic tool calling
            QueryComplexity.Medium => "grok-4-latest",    // Balanced, auto-updates
            QueryComplexity.Complex => "grok-4",          // Best reasoning capabilities
            _ => "grok-4-1-fast"
        };

        // Override model temporarily
        _configuration["XAI:Model"] = model;
        return await _aiService.GetInsightsAsync("Context", query);
    }
}

### Model Aliases

xAI provides model aliases for automatic updates:

- `grok-4` → Latest stable Grok 4 version
- `grok-4-latest` → Latest Grok 4 release (cutting edge)
- `grok-4-YYYYMMDD` → Specific date-locked version

**Recommendation**: Use `grok-4` for production stability, `grok-4-latest` for latest features.
```

---

## Performance Tuning

### Timeout Configuration

```json
{
  "XAI": {
    // Short timeout for simple queries
    "TimeoutSeconds": 10,

    // Longer timeout for complex analysis
    "TimeoutSeconds": 30
  }
}
```

### Concurrency Limits

```json
{
  "XAI": {
    // Conservative (prevent rate limiting)
    "MaxConcurrentRequests": 3,

    // Balanced (most cases)
    "MaxConcurrentRequests": 5,

    // Aggressive (high throughput)
    "MaxConcurrentRequests": 10
  }
}
```

### Circuit Breaker Tuning

```json
{
  "XAI": {
    // Development (quick recovery)
    "CircuitBreakerBreakSeconds": 30,

    // Production (stable)
    "CircuitBreakerBreakSeconds": 60,

    // High availability (patient)
    "CircuitBreakerBreakSeconds": 300
  }
}
```

### Temperature Settings

```json
{
  "XAI": {
    // Deterministic (same query → same answer)
    "Temperature": 0.0,

    // Balanced (recommended)
    "Temperature": 0.3,

    // Creative (varied responses)
    "Temperature": 0.7,

    // Highly creative (use with caution)
    "Temperature": 1.0
  }
}
```

---

## Security Configuration

### API Key Rotation

````csharp
// Service for key rotation
public class AIKeyRotationService
{
    private readonly IAIService _aiService;
    private readonly ISecretVaultService _secretVault;

    public async Task RotateKeyAsync(string newKey)
    {
        // 1. Validate new key
        var validation = await _aiService.ValidateApiKeyAsync(newKey);
        if (validation.HttpStatusCode != 200)
        {
            throw new InvalidOperationException("New API key is invalid");
        }

        // 2. Update EncryptedLocalSecretVaultService
        await _secretVault.RotateSecretAsync("XAI-API-KEY", newKey);

        // 3. Update runtime service
        await _aiService.UpdateApiKeyAsync(newKey);

        // 4. Log rotation event
        _logger.LogInformation("AI API key rotated successfully. Old key archived.");
    }
}

### Secret Vault Operations

```csharp
// List all secret keys (without values)
var keys = await _secretVault.ListSecretKeysAsync();

// Export secrets for backup (WARNING: Contains sensitive data)
var backup = await _secretVault.ExportSecretsAsync();

// Import secrets from backup
await _secretVault.ImportSecretsAsync(backupJson);

// Delete a secret
await _secretVault.DeleteSecretAsync("OLD-API-KEY");

// Test vault connectivity
var isHealthy = await _secretVault.TestConnectionAsync();
````

### Key Rotation Schedule

```json
{
  "Security": {
    "AIKeyRotationDays": 90,
    "AIKeyRotationWarningDays": 7
  }
}
```

### Input Validation

```json
{
  "XAI": {
    "MaxContextLength": 10000,
    "MaxQuestionLength": 5000,
    "EnableInputSanitization": true
  }
}
```

---

## Caching Configuration

### XAIService Cache

```json
{
  "XAI": {
    "CacheAbsoluteTTLMinutes": 5,
    "CacheSlidingTTLMinutes": 2,
    "EnableCaching": true
  }
}
```

### GrokRecommendationService Cache

```json
{
  "GrokRecommendation": {
    // Short cache for development
    "CacheDuration": "00:30:00",

    // Standard cache for production
    "CacheDuration": "02:00:00",

    // Long cache for stable data
    "CacheDuration": "04:00:00"
  }
}
```

### GrokSupercomputer Cache

```json
{
  "AppOptions": {
    // Enable/disable caching
    "EnableDataCaching": true,

    // Very short cache (real-time data)
    "EnterpriseDataCacheSeconds": 5,

    // Standard cache
    "EnterpriseDataCacheSeconds": 60,

    // Long cache (historical data)
    "EnterpriseDataCacheSeconds": 300
  }
}
```

---

## Monitoring Configuration

### OpenTelemetry

```json
{
  "OpenTelemetry": {
    "ServiceName": "WileyWidget",
    "ServiceVersion": "1.0.0",
    "Exporters": {
      "SigNoz": {
        "Endpoint": "http://localhost:4318",
        "Protocol": "http/protobuf"
      }
    }
  }
}
```

### Health Checks

```json
{
  "HealthChecks": {
    "AIServices": {
      "Timeout": "00:00:10",
      "FailureStatus": "Unhealthy",
      "Tags": ["ai", "critical"]
    }
  }
}
```

### Logging

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.File", "Serilog.Sinks.Console"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "WileyWidget.Services.XAIService": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/wileywidget-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

---

## Configuration Validation

### Startup Validation

```csharp
// Program.cs
public static void ValidateConfiguration(IConfiguration configuration)
{
    var errors = new List<string>();

    // Check required settings
    if (string.IsNullOrWhiteSpace(configuration["XAI:ApiKey"]))
        errors.Add("XAI:ApiKey is required when XAI:Enabled is true");

    if (string.IsNullOrWhiteSpace(configuration["XAI:Endpoint"]))
        errors.Add("XAI:Endpoint is required");

    // Validate numeric ranges
    var timeout = configuration.GetValue<double>("XAI:TimeoutSeconds");
    if (timeout < 5 || timeout > 300)
        errors.Add("XAI:TimeoutSeconds must be between 5 and 300");

    var concurrent = configuration.GetValue<int>("XAI:MaxConcurrentRequests");
    if (concurrent < 1 || concurrent > 20)
        errors.Add("XAI:MaxConcurrentRequests must be between 1 and 20");

    if (errors.Any())
    {
        throw new InvalidOperationException(
            $"Configuration errors: {string.Join(", ", errors)}");
    }
}
```

---

## Troubleshooting

### Issue: "XAI API key not configured"

**Solution**:

```bash
# Check user secrets
dotnet user-secrets list

# Set key if missing
dotnet user-secrets set "XAI:ApiKey" "xai-YOUR_KEY"
```

### Issue: Configuration not loading

**Solution**:

```csharp
// Add debug logging in Program.cs
var apiKey = configuration["XAI:ApiKey"];
Console.WriteLine($"API Key loaded: {!string.IsNullOrEmpty(apiKey)}");
```

### Issue: Cache not working

**Solution**:

```json
{
  "AppOptions": {
    "EnableDataCaching": true // Ensure this is true
  }
}
```

---

**Document Version**: 1.0
**Last Review**: 2026-01-03
**Maintainer**: WileyWidget Development Team
