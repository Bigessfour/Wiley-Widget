# WileyWidget AI Performance Guide

**Version**: 1.0
**Last Updated**: 2026-01-03

## 📋 Table of Contents

1. [Performance Overview](#performance-overview)
2. [Caching Strategies](#caching-strategies)
3. [Batch Processing](#batch-processing)
4. [Query Optimization](#query-optimization)
5. [Cost Optimization](#cost-optimization)
6. [Monitoring Performance](#monitoring-performance)

---

## Performance Overview

### Key Performance Metrics

| Metric                    | Target | Acceptable | Poor  |
| ------------------------- | ------ | ---------- | ----- |
| **Response Time** (p95)   | < 2s   | < 5s       | > 5s  |
| **Cache Hit Rate**        | > 70%  | > 50%      | < 50% |
| **Fallback Usage**        | < 5%   | < 10%      | > 10% |
| **Circuit Breaker State** | Closed | Closed     | Open  |
| **Concurrent Requests**   | 3-5    | 5-10       | > 10  |

### Performance Bottlenecks

1. **API Latency** (1-3s per request)
2. **Token Processing** (increases with prompt length)
3. **Network Overhead** (TLS handshake, DNS resolution)
4. **Serialization/Deserialization** (JSON parsing)

---

## Caching Strategies

### Multi-Layer Caching

```text
┌─────────────────────────────────────────────────┐
│ Layer 1: In-Memory Cache (IMemoryCache)        │
│ - TTL: 5 minutes (absolute)                    │
│ - Sliding: 2 minutes                            │
│ - Scope: Per-instance                           │
└─────────────────────────────────────────────────┘
                     ↓ Miss
┌─────────────────────────────────────────────────┐
│ Layer 2: Distributed Cache (Redis) [Optional]  │
│ - TTL: 1 hour                                   │
│ - Scope: Cross-instance                         │
└─────────────────────────────────────────────────┘
                     ↓ Miss
┌─────────────────────────────────────────────────┐
│ Layer 3: xAI API                                │
│ - Latency: 1-3 seconds                          │
│ - Cost: $$$ per request                         │
└─────────────────────────────────────────────────┘
```

### Cache Warming

Pre-populate cache with common queries on startup:

```csharp
public class AICacheWarmingService : IHostedService
{
    private readonly IAIService _aiService;
    private readonly ILogger<AICacheWarmingService> _logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Warming AI cache with common queries...");

        var commonQueries = new[]
        {
            ("Budget Analysis", "What are our monthly expenses?"),
            ("Revenue Forecast", "What is our projected revenue?"),
            ("Compliance Status", "Are we compliant with regulations?")
        };

        // Execute queries in parallel to warm cache
        await Task.WhenAll(commonQueries.Select(q =>
            _aiService.GetInsightsAsync(q.Item1, q.Item2, cancellationToken)));

        _logger.LogInformation("Cache warming completed");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

// Register in Program.cs
services.AddHostedService<AICacheWarmingService>();
```

### Smart Cache Invalidation

```csharp
public class SmartCacheInvalidationService
{
    private readonly IMemoryCache _cache;
    private readonly IGrokRecommendationService _recommendationService;

    // Invalidate when relevant data changes
    public async Task OnExpenseDataChangedAsync()
    {
        _recommendationService.ClearCache();
        _logger.LogInformation("Recommendation cache cleared due to expense data change");
    }

    // Invalidate on schedule (e.g., nightly)
    public async Task ScheduledCacheClearAsync()
    {
        _recommendationService.ClearCache();
        // Clear other caches as needed
        _logger.LogInformation("Scheduled cache clear completed");
    }
}
```

---

## Batch Processing

### Batch API Requests

```csharp
// ❌ SLOW: Sequential requests
public async Task<List<string>> GetInsightsSequentialAsync(List<string> questions)
{
    var results = new List<string>();
    foreach (var question in questions)
    {
        var result = await _aiService.GetInsightsAsync("Context", question);
        results.Add(result);
    }
    return results; // Takes: 3 questions × 2s = 6 seconds
}

// ✅ FAST: Batch processing
public async Task<Dictionary<string, string>> GetInsightsBatchAsync(List<string> questions)
{
    var requests = questions.Select(q => ("Context", q));
    var results = await _aiService.BatchGetInsightsAsync(requests);
    return results; // Takes: 3 questions ÷ 3 per batch × 2s = 2 seconds
}
```

### Parallel Processing with Semaphore

```csharp
public class ParallelAIProcessor
{
    private readonly IAIService _aiService;
    private readonly SemaphoreSlim _semaphore = new(3, 3); // Max 3 concurrent

    public async Task<List<string>> ProcessQuestionsAsync(List<string> questions)
    {
        var tasks = questions.Select(async question =>
        {
            await _semaphore.WaitAsync();
            try
            {
                return await _aiService.GetInsightsAsync("Context", question);
            }
            finally
            {
                _semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }
}
```

---

## Query Optimization

### Prompt Engineering for Speed

```csharp
// ❌ SLOW: Verbose prompt (500 tokens)
var slowPrompt = @"
I need you to carefully analyze this municipal budget data and provide
a comprehensive analysis including all aspects of spending patterns,
revenue projections, compliance status, and future recommendations.
Please be thorough and detailed in your response...
[more verbose text]
";

// ✅ FAST: Concise prompt (100 tokens)
var fastPrompt = @"
Analyze budget: Total=$1.5M, Spent=$1.6M (+7%)
Provide:
1. Top 3 spending categories
2. Risk level (High/Medium/Low)
3. 2 corrective actions
";
```

### Request JSON Responses

```csharp
// ✅ FAST: Structured JSON (easy to parse)
var prompt = @"
Analyze spending and respond with ONLY valid JSON (no markdown):
{
  ""riskLevel"": ""High"",
  ""topSpending"": [""Water"", ""Sewer"", ""Trash""],
  ""actions"": [""Reduce overhead"", ""Optimize procurement""]
}
";

var response = await _aiService.SendPromptAsync(prompt);
var data = JsonSerializer.Deserialize<SpendingAnalysis>(response);
```

### Model Selection for Speed

```csharp
public class AdaptiveModelSelector
{
    public async Task<string> GetInsightsAsync(string question, QueryComplexity complexity)
    {
        var model = complexity switch
        {
            QueryComplexity.Simple => "grok-beta",       // Fast (1s)
            QueryComplexity.Medium => "grok-2-latest",   // Medium (2s)
            QueryComplexity.Complex => "grok-4-0709",    // Slow (3s)
            _ => "grok-beta"
        };

        // Temporarily override model
        var originalModel = _configuration["XAI:Model"];
        _configuration["XAI:Model"] = model;

        try
        {
            return await _aiService.GetInsightsAsync("Context", question);
        }
        finally
        {
            _configuration["XAI:Model"] = originalModel;
        }
    }
}
```

---

## Cost Optimization

### Token Usage Tracking

```csharp
public class TokenUsageTracker
{
    private readonly Meter _meter;
    private readonly Counter<long> _promptTokens;
    private readonly Counter<long> _completionTokens;
    private readonly Counter<decimal> _estimatedCost;

    public TokenUsageTracker()
    {
        _meter = new Meter("WileyWidget.AI.Costs", "1.0.0");
        _promptTokens = _meter.CreateCounter<long>("prompt_tokens_total");
        _completionTokens = _meter.CreateCounter<long>("completion_tokens_total");
        _estimatedCost = _meter.CreateCounter<decimal>("estimated_cost_usd");
    }

    public void TrackUsage(int promptTokens, int completionTokens, string model)
    {
        _promptTokens.Add(promptTokens, new KeyValuePair<string, object?>("model", model));
        _completionTokens.Add(completionTokens, new KeyValuePair<string, object?>("model", model));

        // Estimate cost (adjust rates as needed)
        var cost = model switch
        {
            "grok-beta" => (promptTokens * 0.000001m) + (completionTokens * 0.000002m),
            "grok-4-0709" => (promptTokens * 0.000003m) + (completionTokens * 0.000006m),
            _ => 0m
        };

        _estimatedCost.Add(cost, new KeyValuePair<string, object?>("model", model));
    }
}
```

### Cost-Saving Strategies

1. **Aggressive Caching**: Increase TTL for stable data
2. **Batch Processing**: Reduce overhead per request
3. **Model Selection**: Use cheaper models for simple queries
4. **Prompt Optimization**: Reduce token counts
5. **Rate Limiting**: Prevent accidental overuse

```csharp
// Cost budget enforcement
public class CostBudgetEnforcer
{
    private decimal _dailyBudgetUSD = 10.00m;
    private decimal _spentTodayUSD = 0m;

    public async Task<string> GetInsightsWithBudgetAsync(string context, string question)
    {
        if (_spentTodayUSD >= _dailyBudgetUSD)
        {
            _logger.LogWarning("Daily AI budget exceeded: ${Spent}/{Budget}",
                _spentTodayUSD, _dailyBudgetUSD);
            return "AI service temporarily unavailable due to budget limits.";
        }

        var result = await _aiService.GetInsightsAsync(context, question);

        // Track spend (implement token counting)
        _spentTodayUSD += EstimateCost(context, question, result);

        return result;
    }
}
```

---

## Monitoring Performance

### Real-Time Performance Dashboard

```csharp
public class AIPerformanceDashboard
{
    private readonly Histogram<double> _responseTime;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly Gauge<double> _cacheHitRate;

    public void RecordRequest(TimeSpan duration, bool cacheHit)
    {
        _responseTime.Record(duration.TotalSeconds);

        if (cacheHit)
            _cacheHits.Add(1);
        else
            _cacheMisses.Add(1);

        // Calculate cache hit rate
        var totalRequests = _cacheHits + _cacheMisses;
        if (totalRequests > 0)
        {
            var hitRate = (double)_cacheHits / totalRequests;
            _cacheHitRate.Record(hitRate);
        }
    }
}
```

### Performance Alerts

```yaml
# Prometheus alert rules
groups:
  - name: ai_performance
    rules:
      - alert: SlowAIResponse
        expr: histogram_quantile(0.95, ai_response_time_seconds) > 5.0
        for: 5m
        annotations:
          summary: "95th percentile AI response time > 5 seconds"
          description: "AI service is slow. Check API status and network."

      - alert: LowCacheHitRate
        expr: ai_cache_hit_rate < 0.5
        for: 10m
        annotations:
          summary: "Cache hit rate < 50%"
          description: "Review caching strategy and TTL settings."

      - alert: HighConcurrency
        expr: ai_concurrent_requests > 8
        for: 5m
        annotations:
          summary: "High concurrent AI requests (>8)"
          description: "May trigger rate limiting. Review usage patterns."
```

### Performance Profiling

```csharp
public class AIPerformanceProfiler
{
    private readonly SigNozTelemetryService _telemetry;

    public async Task<string> ProfiledGetInsightsAsync(string context, string question)
    {
        using var activity = _telemetry.StartActivity("ai.insights.profiled",
            ("context_length", context.Length),
            ("question_length", question.Length));

        var sw = Stopwatch.StartNew();

        // Measure cache check
        var cacheStart = sw.Elapsed;
        var cached = CheckCache(context, question);
        var cacheTime = sw.Elapsed - cacheStart;
        activity?.SetTag("cache_time_ms", cacheTime.TotalMilliseconds);

        if (cached != null)
        {
            activity?.SetTag("cache_hit", true);
            return cached;
        }

        // Measure API call
        var apiStart = sw.Elapsed;
        var result = await _aiService.GetInsightsAsync(context, question);
        var apiTime = sw.Elapsed - apiStart;
        activity?.SetTag("api_time_ms", apiTime.TotalMilliseconds);

        // Measure serialization
        var serializeStart = sw.Elapsed;
        CacheResult(context, question, result);
        var serializeTime = sw.Elapsed - serializeStart;
        activity?.SetTag("serialize_time_ms", serializeTime.TotalMilliseconds);

        activity?.SetTag("total_time_ms", sw.Elapsed.TotalMilliseconds);

        return result;
    }
}
```

---

## Performance Benchmarks

### Typical Performance Characteristics

| Operation               | Cache Hit | Cache Miss | Batch (3 requests) |
| ----------------------- | --------- | ---------- | ------------------ |
| **Chat Query**          | 5ms       | 1500ms     | 2000ms             |
| **Rate Recommendation** | 5ms       | 2000ms     | N/A                |
| **Budget Analysis**     | 10ms      | 2500ms     | 3500ms             |
| **Compliance Report**   | 10ms      | 3000ms     | N/A                |

### Optimization Checklist

- [ ] Enable caching (check `EnableDataCaching` in config)
- [ ] Set appropriate cache TTL (balance freshness vs performance)
- [ ] Use batch processing for multiple queries
- [ ] Optimize prompts (concise, structured)
- [ ] Select appropriate model (grok-beta for speed)
- [ ] Limit concurrent requests (3-5 optimal)
- [ ] Monitor cache hit rate (target >60%)
- [ ] Track token usage for cost control
- [ ] Implement query result pagination
- [ ] Use connection pooling (automatic with HttpClientFactory)

---

**Document Version**: 1.0
**Last Review**: 2026-01-03
**Maintainer**: WileyWidget Development Team
