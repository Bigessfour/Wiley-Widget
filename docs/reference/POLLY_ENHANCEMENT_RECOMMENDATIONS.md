# Polly Resilience Enhancement Recommendations

## Executive Summary

Based on analysis of the Wiley Widget codebase and [Polly v8 samples](https://github.com/App-vNext/Polly/tree/main/samples), the following services and operations would significantly benefit from comprehensive Polly resilience patterns.

**Status**: ✅ EnterpriseResourceLoader fully implemented with Polly v8 | 🔄 Other areas identified for enhancement

---

## 1. ✅ **IMPLEMENTED: EnterpriseResourceLoader**

**Location**: `src/WileyWidget/Startup/EnterpriseResourceLoader.cs`

**Implementation Status**: **COMPLETE** with Polly v8 modern API

### Resilience Patterns Implemented

- ✅ **Timeout Strategy**: 60-second pessimistic timeout for cold starts
- ✅ **Circuit Breaker**: 50% failure ratio, 30s sampling window, 2-minute break
- ✅ **Retry with Jitter**: 3 retries, exponential backoff, jitter prevents thundering herd
- ✅ **Context Pooling**: Uses `ResilienceContextPool.Shared` for performance
- ✅ **Telemetry Integration**: Full SigNoz and ErrorReporting integration

### Key Features

```csharp
// Modern Polly v8 pipeline with proper layering
new ResiliencePipelineBuilder<ResourceDictionary?>()
    .AddTimeout(60s)           // Outermost: prevents infinite hangs
    .AddCircuitBreaker(50%)    // Middle: prevents cascade failures
    .AddRetry(3x + jitter)     // Innermost: handles transient errors
    .Build();
```

---

## 2. ✅ **IMPLEMENTED: XAIService (AI API Calls)**

**Location**: `src/WileyWidget.Services/XAIService.cs`

**Implementation Status**: **COMPLETE** with Polly v8 modern API

### Resilience Patterns Implemented

- ✅ **Rate Limiter**: 50 requests/minute sliding window
- ✅ **Timeout Strategy**: Configurable (default 15s)
- ✅ **Circuit Breaker**: 50% failure ratio, 30s sampling window, 1-minute break
- ✅ **Retry with Jitter**: 3 retries, exponential backoff, jitter prevents thundering herd
- ✅ **Context Pooling**: Uses `ResilienceContextPool.Shared` for performance
- ✅ **Telemetry Integration**: Full SigNoz and ErrorReporting integration

### Key Features

```csharp
// Modern Polly v8 pipeline with proper layering
new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRateLimiter(/* ... */)
    .AddTimeout(/* ... */)
    .AddCircuitBreaker(/* ... */)
    .AddRetry(/* ... */)
    .Build();
```

All HTTP calls now use the new pipeline. Legacy Polly v7 code has been removed. Logging and telemetry hooks are integrated. Input validation and sanitization are enforced.

**Next Steps:**

- Add/Update unit tests for retry, circuit breaker, timeout, and context pooling scenarios
- Externalize pipeline configuration to `appsettings.json`
- Register pipeline via DI for flexibility

---

## 3. 🔄 **HIGH PRIORITY: QuickBooksService (OAuth + API)**

**Location**: `src/WileyWidget.Services/QuickBooksService.cs`

**Current State**: NO Polly implementation, relies on Intuit SDK retry logic

### Critical Operations Needing Resilience

#### A. OAuth Token Refresh (Lines 400-500)

```csharp
// Current: No resilience
private async Task<OAuth2TokenResponse> RefreshTokenAsync(string refreshToken)
{
    // Direct HTTP call to TokenEndpoint - FRAGILE!
    var response = await _httpClient.PostAsync(TokenEndpoint, content);
}

// Recommended: Add resilience pipeline
private readonly ResiliencePipeline<HttpResponseMessage> _oauthPipeline;

_oauthPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddTimeout(TimeSpan.FromSeconds(10))
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
    {
        FailureRatio = 0.7,
        BreakDuration = TimeSpan.FromMinutes(5),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError)
            .Handle<HttpRequestException>()
    })
    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = 2, // OAuth tokens are sensitive - limit retries
        Delay = TimeSpan.FromSeconds(1),
        UseJitter = true,
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)
            .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError)
    })
    .Build();
```

#### B. DataService Operations (QBO API Calls)

```csharp
// Current: No resilience wrapper around Intuit SDK
var queryService = new QueryService<T>(serviceContext);
var result = await queryService.ExecuteAsync(query); // Can fail!

// Recommended: Wrap DataService calls
private readonly ResiliencePipeline _qboApiPipeline;

_qboApiPipeline = new ResiliencePipelineBuilder()
    .AddTimeout(TimeSpan.FromSeconds(30))
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(60),
        MinimumThroughput = 3,
        BreakDuration = TimeSpan.FromMinutes(3),
        OnOpened = args =>
        {
            _logger.LogCritical("QuickBooks API Circuit Breaker OPEN - service unavailable");
            // Notify user via event
            QBOServiceUnavailable?.Invoke(this, EventArgs.Empty);
            return ValueTask.CompletedTask;
        }
    })
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(2),
        UseJitter = true,
        ShouldHandle = new PredicateBuilder()
            .Handle<System.Net.Http.HttpRequestException>()
            .Handle<Intuit.Ipp.Exception.IdsException>(ex => IsTransientQBOError(ex))
    })
    .Build();

private async Task<T> ExecuteQBOOperationAsync<T>(Func<Task<T>> operation)
{
    return await _qboApiPipeline.ExecuteAsync(
        async (ctx) => await operation(),
        CancellationToken.None);
}
```

#### C. Cloudflare Tunnel Management (Lines 600-700)

```csharp
// Current: Basic process management, no resilience
private async Task<string> StartCloudflaredTunnelAsync()
{
    // Direct process start - can fail
    _cloudflaredProcess = Process.Start(startInfo);
}

// Recommended: Add retry for tunnel startup
private readonly ResiliencePipeline _tunnelPipeline;

_tunnelPipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Linear,
        ShouldHandle = new PredicateBuilder()
            .Handle<System.ComponentModel.Win32Exception>()
            .Handle<InvalidOperationException>(),
        OnRetry = args =>
        {
            _logger.LogWarning("Cloudflare tunnel startup failed, retry {Attempt}/3",
                args.AttemptNumber + 1);
            return ValueTask.CompletedTask;
        }
    })
    .Build();
```

---

## 4. 🔄 **MEDIUM PRIORITY: Database Operations**

**Location**: `src/WileyWidget/Configuration/DatabaseResiliencePolicy.cs`

**Current State**: Has some Polly v7 policies, needs modernization

### Current Implementation Issues

```csharp
// Line 20: Legacy Polly v7 API
public static readonly AsyncRetryPolicy _authenticationRetryPolicy = Policy
    .Handle<SqlException>(ex => ex.Number == 18456)
    .WaitAndRetryAsync(/* ... */);
```

### Recommended Enhancements

```csharp
public static class DatabaseResiliencePolicy
{
    // Modern Polly v8 implementation
    public static ResiliencePipeline CreateDatabaseReadPipeline(ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(30))
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.6,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromMinutes(2),
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>(ex => IsTransientSqlError(ex))
                    .Handle<TimeoutException>()
                    .Handle<DbException>()
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(100),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>(ex => IsTransientSqlError(ex))
                    .Handle<DbUpdateConcurrencyException>() // EF Core specific
            })
            .Build();
    }

    public static ResiliencePipeline CreateDatabaseWritePipeline(ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(45)) // Writes take longer
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2, // Fewer retries for writes
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Linear, // Predictable for writes
                ShouldHandle = new PredicateBuilder()
                    .Handle<DbUpdateConcurrencyException>()
                    .Handle<SqlException>(ex => ex.Number == -2) // Timeout
            })
            .Build();
    }

    private static bool IsTransientSqlError(SqlException ex) =>
        ex.Number is
            -2 or      // Timeout
            4060 or    // Cannot open database
            40197 or   // Service unavailable
            40501 or   // Service busy
            40613 or   // Database unavailable
            49918 or   // Cannot process request
            49919 or   // Too many requests
            49920 or   // Server busy
            11001;     // Network error
}
```

---

## 5. 🔄 **MEDIUM PRIORITY: HealthCheckHostedService**

**Location**: `src/WileyWidget/HealthCheckHostedService.cs`

**Current State**: Uses legacy Polly v7 (lines 12-14)

### Current Issues

```csharp
// Lines 12-14: Old imports
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

// Line 217: ExecuteHealthCheckWithPollyAsync uses legacy API
private async Task<HealthCheckResult> ExecuteHealthCheckWithPollyAsync(
    string serviceName,
    Task<HealthCheckResult> healthCheckTask)
{
    // TODO: Modernize to Polly v8
}
```

### Recommended Implementation

```csharp
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

public class HealthCheckHostedService : IHostedService
{
    private readonly ResiliencePipeline<HealthCheckResult> _healthCheckPipeline;

    public HealthCheckHostedService(/* params */)
    {
        _healthCheckPipeline = new ResiliencePipelineBuilder<HealthCheckResult>()
            .AddTimeout(TimeSpan.FromSeconds(10)) // Health checks should be fast
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HealthCheckResult>
            {
                FailureRatio = 0.8, // Tolerant during startup
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromMinutes(1),
                ShouldHandle = new PredicateBuilder<HealthCheckResult>()
                    .HandleResult(r => r.Status == HealthStatus.Unhealthy)
                    .Handle<Exception>(),
                OnOpened = args =>
                {
                    _logger.LogWarning("Health check circuit breaker opened for degraded service");
                    return ValueTask.CompletedTask;
                }
            })
            .AddRetry(new RetryStrategyOptions<HealthCheckResult>
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Linear,
                ShouldHandle = new PredicateBuilder<HealthCheckResult>()
                    .HandleResult(r => r.Status == HealthStatus.Unhealthy)
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
            })
            .Build();
    }

    private async Task<HealthCheckResult> ExecuteHealthCheckWithResilienceAsync(
        string serviceName,
        Func<Task<HealthCheckResult>> healthCheckFunc)
    {
        var context = ResilienceContextPool.Shared.Get();
        context.Properties.Set(new ResiliencePropertyKey<string>("ServiceName"), serviceName);

        try
        {
            return await _healthCheckPipeline.ExecuteAsync(
                async (ctx) => await healthCheckFunc(),
                context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for {ServiceName}", serviceName);
            return HealthCheckResult.Unhealthy($"{serviceName} check failed", ex);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }
}
```

---

## 6. 🔄 **LOW PRIORITY: File I/O Operations**

**Locations**: Various (resource loading, logging, Excel import)

### Candidates

- `AILoggingService.ExportLogsAsync()` (Line 387)
- Excel file operations
- License file loading
- Configuration file access

### Recommended Pattern

```csharp
public static class FileOperationResiliencePolicy
{
    public static ResiliencePipeline CreateFileReadPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(5))
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<IOException>()
                    .Handle<UnauthorizedAccessException>()
                    .Handle<FileNotFoundException>()
            })
            .Build();
    }

    public static ResiliencePipeline CreateFileWritePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(10))
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Linear,
                ShouldHandle = new PredicateBuilder()
                    .Handle<IOException>(ex => !ex.Message.Contains("being used"))
            })
            .Build();
    }
}
```

---

## Implementation Priority Matrix

| Service                     | Priority   | Complexity | Impact   | Estimated Effort |
| --------------------------- | ---------- | ---------- | -------- | ---------------- |
| ✅ EnterpriseResourceLoader | **DONE**   | High       | Critical | **COMPLETE**     |
| XAIService                  | **HIGH**   | Medium     | High     | 4-6 hours        |
| QuickBooksService           | **HIGH**   | High       | Critical | 6-8 hours        |
| DatabaseResiliencePolicy    | **MEDIUM** | Medium     | High     | 2-4 hours        |
| HealthCheckHostedService    | **MEDIUM** | Low        | Medium   | 2-3 hours        |
| File I/O Operations         | **LOW**    | Low        | Low      | 1-2 hours        |

---

## Testing Strategy

### Unit Tests Required for Each Service

```csharp
[Theory]
[InlineData(1)] // First retry succeeds
[InlineData(2)] // Second retry succeeds
[InlineData(3)] // Third retry succeeds
public async Task ResiliencePipeline_RetriesTransientFailures(int succeedOnAttempt);

[Fact]
public async Task ResiliencePipeline_CircuitBreaker_OpensAfterThreshold();

[Fact]
public async Task ResiliencePipeline_CircuitBreaker_HalfOpenAfterBreakDuration();

[Fact]
public async Task ResiliencePipeline_Timeout_CancelsOperation();

[Fact]
public async Task ResiliencePipeline_Jitter_PreventsThunderingHerd();

[Fact]
public async Task ResiliencePipeline_ContextPool_ReusesContexts();
```

---

## Configuration Best Practices

### 1. Externalize Policy Configuration

```csharp
// appsettings.json
{
  "Resilience": {
    "XAI": {
      "Timeout": "00:00:15",
      "Retry": {
        "MaxAttempts": 3,
        "BaseDelay": "00:00:00.500",
        "UseJitter": true
      },
      "CircuitBreaker": {
        "FailureRatio": 0.5,
        "BreakDuration": "00:01:00"
      }
    },
    "QuickBooks": {
      "Timeout": "00:00:30",
      "Retry": {
        "MaxAttempts": 3,
        "BaseDelay": "00:00:02"
      }
    }
  }
}
```

### 2. Register Pipelines via DI

```csharp
// Startup.cs or Program.cs
services.AddResiliencePipeline<string, HttpResponseMessage>("xai-api", builder =>
{
    var config = configuration.GetSection("Resilience:XAI");
    builder
        .AddTimeout(config.GetValue<TimeSpan>("Timeout"))
        .AddCircuitBreaker(/* from config */)
        .AddRetry(/* from config */);
});

// Usage in service
public XAIService(ResiliencePipelineProvider<string> pipelineProvider)
{
    _pipeline = pipelineProvider.GetPipeline<HttpResponseMessage>("xai-api");
}
```

### 3. Telemetry Integration

```csharp
.AddRetry(new RetryStrategyOptions
{
    OnRetry = args =>
    {
        // SigNoz telemetry
        _telemetryService.RecordRetry(
            serviceName: "XAI",
            attempt: args.AttemptNumber,
            delay: args.RetryDelay);

        // Application Insights
        _telemetryClient.TrackEvent("Retry", new Dictionary<string, string>
        {
            ["Service"] = "XAI",
            ["Attempt"] = args.AttemptNumber.ToString()
        });

        return ValueTask.CompletedTask;
    }
})
```

---

## References

- ✅ [Polly v8 Samples (GitHub)](https://github.com/App-vNext/Polly/tree/main/samples)
- ✅ [EnterpriseResourceLoader Implementation](c:/Users/biges/Desktop/Wiley_Widget/src/WileyWidget/Startup/EnterpriseResourceLoader.cs)
- ✅ [ResiliencePolicyConfiguration](c:/Users/biges/Desktop/Wiley_Widget/src/WileyWidget/Startup/ResiliencePolicyConfiguration.cs)
- 📚 [Polly Documentation](https://www.pollydocs.org/)
- 📚 [Microsoft Resilience Patterns](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-resilient-entity-framework-core-sql-connections)

---

**Document Version**: 1.0
**Last Updated**: 2025-11-08
**Author**: GitHub Copilot
**Status**: ✅ Analysis Complete | 🔄 Implementation In Progress
