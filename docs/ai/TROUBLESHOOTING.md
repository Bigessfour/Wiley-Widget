# AI Troubleshooting Guide

**Version**: 1.0
**Last Updated**: 2026-01-03

## 🔍 Quick Diagnosis

### Is the AI Service Working?

```bash
# 1. Check service health
curl http://localhost:5000/health

# 2. Check AI-specific health
curl http://localhost:5000/health/ai

# 3. Check recent logs
Get-Content logs/startup-*.txt | Select-String "XAIService|AI|Grok" -Context 2

# 4. Verify API key is configured
$env:XAI__ApiKey -ne $null  # Should return True
```

---

## 🚨 Common Issues

### 1. "AI service is disabled"

**Symptoms:**

- All AI requests return "AI service is disabled"
- No AI features available in UI

**Causes:**

- `XAI:Enabled` is `false` in configuration
- API key not configured

**Solutions:**

```json
// appsettings.json
{
  "XAI": {
    "Enabled": true, // ← Must be true
    "ApiKey": "xai-your-key-here"
  }
}
```

**Verification:**

```bash
# Check configuration
dotnet user-secrets list | Select-String "XAI"

# Check environment
$env:XAI__Enabled
$env:XAI__ApiKey
```

---

### 2. "XAI API key not configured"

**Symptoms:**

- Application fails to start
- Error: `InvalidOperationException: XAI API key not configured but XAI is enabled`

**Causes:**

- Missing API key in configuration
- API key is empty string
- Environment variable not set

**Solutions:**

**Development (User Secrets):**

```bash
dotnet user-secrets set "XAI:ApiKey" "xai-your-key-here"
```

**Production (Environment Variables):**

```powershell
$env:XAI__ApiKey = "xai-your-key-here"
```

**Azure Key Vault:**

```json
{
  "KeyVault": {
    "Enabled": true,
    "VaultUri": "https://your-vault.vault.azure.net/",
    "SecretNames": {
      "XAIApiKey": "xai-api-key"
    }
  }
}
```

---

### 3. Timeouts / "Request timed out"

**Symptoms:**

- Requests fail with timeout errors
- p95 latency > 15 seconds
- Circuit breaker opens frequently

**Causes:**

- xAI API is slow or overloaded
- Network issues
- Timeout configured too low
- Large prompts requiring more processing time

**Solutions:**

**1. Increase Timeout:**

```json
{
  "XAI": {
    "TimeoutSeconds": 30 // Increase from default 15s
  }
}
```

**2. Enable Adaptive Timeout:**

```csharp
// Program.cs
services.AddSingleton<AdaptiveTimeoutService>();

// Use in XAIService
var adaptiveTimeout = serviceProvider.GetRequiredService<AdaptiveTimeoutService>();
var recommendedTimeout = adaptiveTimeout.GetRecommendedTimeoutSeconds();
```

**3. Optimize Prompts:**

```csharp
// ❌ SLOW: Verbose prompt
var slowPrompt = "Please analyze this data in detail and provide comprehensive insights...";

// ✅ FAST: Concise prompt
var fastPrompt = "Analyze spending: Total=$1.5M, Spent=$1.6M. Top 3 categories and risk level?";
```

**Verification:**

```bash
# Check average latency
Get-Content logs/startup-*.txt | Select-String "response time" | Measure-Object -Property {[double]$_.Line.Split()[5]} -Average
```

---

### 4. High Error Rate / Circuit Breaker Open

**Symptoms:**

- Many failed requests
- Circuit breaker state = "Open"
- Error: "Service unavailable due to circuit breaker"

**Causes:**

- xAI API issues (rate limiting, downtime)
- Invalid API key
- Network problems
- Too many concurrent requests

**Solutions:**

**1. Check xAI API Status:**

- Visit: <https://status.x.ai>
- Check for outages or degraded performance

**2. Reduce Concurrency:**

```json
{
  "XAI": {
    "MaxConcurrentRequests": 3 // Reduce from default 5
  }
}
```

**3. Increase Circuit Breaker Tolerance:**

```json
{
  "XAI": {
    "CircuitBreakerBreakSeconds": 120, // Wait 2 minutes before retry
    "CircuitBreakerFailureThreshold": 10 // Allow more failures before opening
  }
}
```

**4. Enable Health-Based Routing:**

```csharp
// Program.cs
services.AddSingleton<HealthBasedRoutingService>();

// Use fallback when unhealthy
var routedResult = await healthRouter.RouteWithHealthCheckAsync(
    async ct => await aiService.GetInsightsAsync(context, question, ct),
    async () => "AI service temporarily unavailable. Using cached response.");
```

**Verification:**

```bash
# Check circuit breaker state
curl http://localhost:5000/metrics | Select-String "circuit_breaker"
```

---

### 5. Low Cache Hit Rate (< 50%)

**Symptoms:**

- Slow response times
- High API costs
- Cache hit rate below 50%

**Causes:**

- Cache TTL too short
- Unique queries (no reuse)
- Cache warming not enabled

**Solutions:**

**1. Increase Cache TTL:**

```json
{
  "XAI": {
    "CacheAbsoluteExpirationMinutes": 10, // Increase from 5
    "CacheSlidingExpirationMinutes": 5
  }
}
```

**2. Enable Cache Warming:**

```json
{
  "AI": {
    "CacheWarming": {
      "Enabled": true,
      "DelaySeconds": 10
    }
  }
}
```

```csharp
// Program.cs
services.AddHostedService<AICacheWarmingService>();
```

**3. Normalize Queries:**

```csharp
// ❌ BAD: Many unique queries (low cache reuse)
var query1 = "What is our budget for Q1?";
var query2 = "What is our budget for Q1 2025?";
var query3 = "Budget Q1?";

// ✅ GOOD: Standardized queries (high cache reuse)
var query = "What is our quarterly budget?";
```

**Verification:**

```bash
# Check cache metrics
curl http://localhost:5000/metrics | Select-String "cache_hit"

# Calculate hit rate
$hits = (curl http://localhost:5000/metrics | Select-String "ai_cache_hits_total").ToString().Split()[-1]
$misses = (curl http://localhost:5000/metrics | Select-String "ai_cache_misses_total").ToString().Split()[-1]
$hitRate = [double]$hits / ($hits + $misses) * 100
Write-Host "Cache Hit Rate: $hitRate%"
```

---

### 6. High Cost / Token Usage

**Symptoms:**

- Unexpected API bills
- Daily cost > $10
- High token usage metrics

**Causes:**

- Verbose prompts
- No caching
- Unnecessary API calls
- Using expensive models (grok-4-0709)

**Solutions:**

**1. Enable Aggressive Caching:**

```json
{
  "XAI": {
    "EnableDataCaching": true,
    "CacheAbsoluteExpirationMinutes": 60 // Cache for 1 hour
  }
}
```

**2. Use Cheaper Model:**

```json
{
  "XAI": {
    "Model": "grok-beta" // Fastest and cheapest
  }
}
```

**3. Optimize Prompts:**

```csharp
// ❌ EXPENSIVE: Verbose prompt (500 tokens)
var expensivePrompt = $@"
I need you to provide a comprehensive analysis of...
{longContextData}
...please be thorough and detailed.
";

// ✅ CHEAP: Concise prompt (100 tokens)
var cheapPrompt = $@"
Analyze: Budget=$1.5M, Spent=$1.6M (+7%)
Top 3 categories? Risk level? 2 actions?
";
```

**4. Enable Cost Tracking:**

```csharp
// Program.cs
services.AddSingleton<TokenUsageTracker>();

// Track usage
tokenTracker.TrackUsage(promptTokens, completionTokens, model);

// Check daily cost
var dailyCost = await tokenTracker.GetDailyCostAsync();
if (dailyCost > 10m)
{
    logger.LogWarning("Daily AI cost exceeded $10: ${Cost}", dailyCost);
}
```

**Verification:**

```bash
# Check token usage
curl http://localhost:5000/metrics | Select-String "token"

# Check estimated cost
curl http://localhost:5000/metrics | Select-String "cost"
```

---

### 7. Slow Startup / Application Hangs

**Symptoms:**

- Application takes > 30 seconds to start
- UI freezes on startup
- Logs show "Warming AI cache..."

**Causes:**

- Cache warming enabled with many queries
- Synchronous initialization in UI thread

**Solutions:**

**1. Disable Cache Warming:**

```json
{
  "AI": {
    "CacheWarming": {
      "Enabled": false // Disable for faster startup
    }
  }
}
```

**2. Delay Cache Warming:**

```json
{
  "AI": {
    "CacheWarming": {
      "Enabled": true,
      "DelaySeconds": 60 // Wait 1 minute after startup
    }
  }
}
```

**3. Reduce Warm-Up Queries:**

```csharp
// AICacheWarmingService.cs
private static List<(string, string)> GetBudgetQueries()
{
    return new List<(string, string)>
    {
        // Only 2-3 most common queries
        ("Budget Analysis", "What are our top spending categories?"),
        ("Revenue Analysis", "What are our main revenue sources?")
    };
}
```

**Verification:**

```bash
# Measure startup time
Measure-Command { dotnet run --project src/WileyWidget.WinForms }

# Check for blocking initialization
Get-Content logs/startup-*.txt | Select-String "Warming|Initialize" | Select-Object -First 10
```

---

## 🔬 Advanced Debugging

### Enable Verbose Logging

**appsettings.Development.json:**

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "WileyWidget.Services.XAIService": "Debug",
        "WileyWidget.Services.GrokRecommendationService": "Debug",
        "WileyWidget.Services.GrokSupercomputer": "Debug"
      }
    }
  }
}
```

### Capture HTTP Traffic

**Install Fiddler or Wireshark:**

```powershell
# View HTTP requests to xAI API
$env:HTTP_PROXY = "http://localhost:8888"
dotnet run --project src/WileyWidget.WinForms
```

### Analyze Performance

**Use dotnet-trace:**

```bash
# Install tool
dotnet tool install -g dotnet-trace

# Collect trace
$processId = Get-Process -Name WileyWidget | Select-Object -ExpandProperty Id
dotnet-trace collect -p $processId --providers "Microsoft-Extensions-Logging,WileyWidget"

# Analyze in PerfView or Visual Studio
```

### Dump Memory on Hang

**Use dotnet-dump:**

```bash
# Install tool
dotnet tool install -g dotnet-dump

# Collect dump during hang
$processId = Get-Process -Name WileyWidget | Select-Object -ExpandProperty Id
dotnet-dump collect -p $processId -o hang.dmp

# Analyze
dotnet-dump analyze hang.dmp
> clrthreads  # Show all threads
> clrstack -a  # Show stack trace
```

---

## 📊 Diagnostic Queries

### Check Configuration

```powershell
# List all XAI configuration
dotnet user-secrets list | Select-String "XAI"

# Check environment variables
Get-ChildItem Env: | Where-Object Name -Like "*XAI*"

# Verify appsettings.json
Get-Content appsettings.json | ConvertFrom-Json | Select-Object -ExpandProperty XAI
```

### Check Metrics

```bash
# Overall health
curl http://localhost:5000/health | ConvertFrom-Json

# AI-specific metrics
curl http://localhost:5000/metrics | Select-String "ai_"

# Cache statistics
curl http://localhost:5000/metrics | Select-String "cache"

# Error counts
curl http://localhost:5000/metrics | Select-String "error"
```

### Check Logs

```powershell
# Recent errors
Get-Content logs/startup-*.txt | Select-String "ERROR|FAIL" | Select-Object -Last 20

# AI service calls
Get-Content logs/startup-*.txt | Select-String "XAIService|GetInsights" | Select-Object -Last 20

# Response times
Get-Content logs/startup-*.txt | Select-String "response time:" | Select-Object -Last 10
```

---

## 🆘 Getting Help

### Before Opening an Issue

1. **Check logs**: `logs/startup-YYYYMMDD.txt`
2. **Verify configuration**: `dotnet user-secrets list`
3. **Test API key**: Use Postman or curl to test xAI API directly
4. **Check metrics**: `curl http://localhost:5000/metrics`
5. **Review this guide**: Most issues have solutions above

### Information to Include in Bug Reports

```markdown
1. Symptoms: What is happening? What did you expect?
2. Configuration: appsettings.json (redact API key)
3. Logs: Last 50 lines from logs/startup-\*.txt
4. Metrics: Output of `curl http://localhost:5000/metrics | Select-String "ai_"`
5. Environment: .NET version, OS, xAI model being used
6. Steps to Reproduce: Exact steps to trigger the issue
```

---

## 📚 Additional Resources

- **AI Ecosystem Architecture**: [AI_ECOSYSTEM_ARCHITECTURE.md](./AI_ECOSYSTEM_ARCHITECTURE.md)
- **Configuration Guide**: [CONFIGURATION_GUIDE.md](./CONFIGURATION_GUIDE.md)
- **API Reference**: [API_REFERENCE.md](./API_REFERENCE.md)
- **Performance Guide**: [PERFORMANCE_GUIDE.md](./PERFORMANCE_GUIDE.md)
- **Metrics Dashboard**: [METRICS_DASHBOARD.md](./METRICS_DASHBOARD.md)

---

**Document Version**: 1.0
**Last Updated**: 2026-01-03
**Maintainer**: WileyWidget Development Team
