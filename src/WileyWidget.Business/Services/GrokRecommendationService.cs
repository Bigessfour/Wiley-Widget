using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WileyWidget.Business.Services;

/// <summary>
/// Service for AI-driven rate recommendations using xAI Grok API.
/// Implements xAI API integration with fallback to rule-based recommendations.
/// </summary>
public class GrokRecommendationService : IGrokRecommendationService, IHealthCheck
{
    private readonly ILogger<GrokRecommendationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly bool _useGrokApi;
    private readonly string? _apiKey;
    private readonly string _apiEndpoint;
    private readonly string _model;

    // Resilience policies
    private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
    private readonly AsyncRetryPolicy _retryPolicy;

    // Metrics
    private readonly Meter _meter;
    private readonly Counter<long> _apiCalls;
    private readonly Counter<long> _fallbackUsed;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheClears;
    private readonly Counter<long> _validationFailures;
    private readonly Histogram<double> _responseTime;

    // Configuration
    private readonly TimeSpan _cacheDuration;
    private readonly decimal _minAdjustmentFactor = 0.8m;
    private readonly decimal _maxAdjustmentFactor = 2.0m;
    private readonly HashSet<string> _knownDepartments = new() { "Water", "Sewer", "Trash", "Apartments", "Electric", "Gas" };

    // Cache index for clearing cached entries
    private const string CacheIndexKey = "grok_cache_keys";
    private readonly object _cacheIndexLock = new object();

    // Response records
    private record GrokResponse(
        Dictionary<string, decimal> factors,
        string explanation);

    public GrokRecommendationService(
        ILogger<GrokRecommendationService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<WileyWidget.Business.Configuration.GrokRecommendationOptions>? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));

        // Apply configured cache duration if provided via options
        _cacheDuration = options?.Value.CacheDuration ?? TimeSpan.FromHours(2);

        // Load xAI configuration
        _apiKey = _configuration["XAI:ApiKey"];
        _apiEndpoint = _configuration["XAI:Endpoint"] ?? "https://api.x.ai/v1/chat/completions";
        _model = _configuration["XAI:Model"] ?? "grok-beta";
        _useGrokApi = !string.IsNullOrWhiteSpace(_apiKey) &&
                      _configuration.GetValue<bool>("XAI:Enabled", false);

        // Initialize resilience policies
        _circuitBreaker = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromMinutes(5),
                onBreak: (ex, breakDelay) =>
                {
                    _logger.LogWarning(ex, "Circuit breaker opened for {BreakDelay}", breakDelay);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset");
                });

        _retryPolicy = Policy
            .Handle<HttpRequestException>(ex => !ex.Message.Contains("401")) // Don't retry auth failures
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (ex, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(ex, "Retry {RetryCount} after {TimeSpan}", retryCount, timeSpan);
                });

        // Initialize metrics
        _meter = new Meter("GrokRecommendationService", "1.0.0");
        _apiCalls = _meter.CreateCounter<long>("api_calls_total", description: "Total number of API calls made");
        _fallbackUsed = _meter.CreateCounter<long>("fallback_used_total", description: "Total number of times fallback was used");
        _cacheHits = _meter.CreateCounter<long>("cache_hits_total", description: "Total number of cache hits");
        _cacheClears = _meter.CreateCounter<long>("cache_clears_total", description: "Total number of times cache was cleared");
        _validationFailures = _meter.CreateCounter<long>("validation_failures_total", description: "Total number of validation failures");
        _responseTime = _meter.CreateHistogram<double>("response_time_seconds", description: "Response time in seconds");

        if (!_useGrokApi)
        {
            _logger.LogWarning("xAI Grok API disabled - using rule-based recommendations (set XAI:Enabled=true and provide XAI:ApiKey)");
        }
    }

    public async Task<RecommendationResult> GetRecommendedAdjustmentFactorsAsync(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin = 15.0m,
        CancellationToken cancellationToken = default)
    {
        // Input validation
        ValidateInput(departmentExpenses, targetProfitMargin);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Querying for recommendations with {ProfitMargin}% margin",
                targetProfitMargin);

            // Check cache first
            var cacheKey = GenerateCacheKey(departmentExpenses, targetProfitMargin);
            if (_cache.TryGetValue(cacheKey, out RecommendationResult? cachedResult))
            {
                _cacheHits.Add(1);
                _logger.LogDebug("Cache hit (key hash: {CacheKeyHash})", GetCacheKeyHash(cacheKey));
                return cachedResult!;
            }

            RecommendationResult result;

            if (_useGrokApi && _circuitBreaker.CircuitState == CircuitState.Closed)
            {
                try
                {
                    result = await _retryPolicy.ExecuteAsync(() =>
                        QueryGrokApiAsync(departmentExpenses, targetProfitMargin, cancellationToken));

                    // Validate AI response
                    var validationResult = ValidateRecommendationResponse(result.AdjustmentFactors, departmentExpenses);
                    if (!validationResult.IsValid)
                    {
                        _validationFailures.Add(1);
                        _logger.LogWarning("AI response validation failed: {Errors}",
                            string.Join(", ", validationResult.Errors));
                        throw new InvalidOperationException("AI response validation failed");
                    }

                    _apiCalls.Add(1, new KeyValuePair<string, object?>("method", "GetAdjustmentFactors"));
                }
                catch (Exception apiEx)
                {
                    _logger.LogWarning(apiEx, "Grok API call failed - falling back to rule-based recommendations");
                    _fallbackUsed.Add(1);
                    var factors = CalculateRuleBasedRecommendations(departmentExpenses, targetProfitMargin);
                    result = new RecommendationResult(
                        AdjustmentFactors: factors,
                        Explanation: GenerateRuleBasedExplanation(departmentExpenses, targetProfitMargin),
                        FromGrokApi: false,
                        ApiModelUsed: "rule-based",
                        Warnings: Array.Empty<string>());
                }
            }
            else
            {
                if (_circuitBreaker.CircuitState != CircuitState.Closed)
                {
                    _logger.LogWarning("Circuit breaker is {State} - using fallback", _circuitBreaker.CircuitState);
                }
                _fallbackUsed.Add(1);
                var factors = CalculateRuleBasedRecommendations(departmentExpenses, targetProfitMargin);
                result = new RecommendationResult(
                    AdjustmentFactors: factors,
                    Explanation: GenerateRuleBasedExplanation(departmentExpenses, targetProfitMargin),
                    FromGrokApi: false,
                    ApiModelUsed: "rule-based",
                    Warnings: Array.Empty<string>());
            }

            // Cache the result (store with absolute expiration)
            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _cacheDuration });

            // Maintain index of cache keys to support clearing
            try
            {
                lock (_cacheIndexLock)
                {
                    var index = _cache.GetOrCreate(CacheIndexKey, (ICacheEntry entry) =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = _cacheDuration.Add(TimeSpan.FromDays(1));
                        return new HashSet<string>();
                    });
                    index.Add(cacheKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update cache index for key: {CacheKeyHash}", GetCacheKeyHash(cacheKey));
            }

            _logger.LogDebug("Cached recommendations (key hash: {CacheKeyHash}) for {CacheDuration}", GetCacheKeyHash(cacheKey), _cacheDuration);

            _responseTime.Record(stopwatch.Elapsed.TotalSeconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations");
            throw;
        }
    }

    private async Task<RecommendationResult> QueryGrokApiAsync(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

        var prompt = BuildRecommendationPrompt(departmentExpenses, targetProfitMargin);

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "You are a financial analyst specializing in municipal utility rate optimization. Provide precise, data-driven recommendations based on industry standards and cost analysis." },
                new { role = "user", content = prompt }
            },
            temperature = 0.3, // Lower temperature for more consistent results
            max_tokens = 500
        };

        _logger.LogDebug("Sending request to Grok API: {Endpoint} with model {Model}", _apiEndpoint, _model);

        var response = await _circuitBreaker.ExecuteAsync(() =>
            client.PostAsJsonAsync(_apiEndpoint, requestBody, cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);

        // Extract the content from Grok's response structure in a robust way
        if (result == null)
            throw new InvalidOperationException("Grok API returned null response");

        string? content = null;
        try
        {
            var root = result.RootElement;
            if (root.TryGetProperty("choices", out var choicesEl) && choicesEl.ValueKind == JsonValueKind.Array && choicesEl.GetArrayLength() > 0)
            {
                var firstChoice = choicesEl[0];
                if (firstChoice.TryGetProperty("message", out var messageEl) && messageEl.ValueKind == JsonValueKind.Object)
                {
                    if (messageEl.TryGetProperty("content", out var contentEl))
                    {
                        if (contentEl.ValueKind == JsonValueKind.String) content = contentEl.GetString();
                        else content = contentEl.GetRawText();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Grok API response content");
        }

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Grok API returned empty or invalid content");

        // Parse response (content is non-null due to check above)
        var safeContent = content!;
        var recommendationResult = ParseGrokResponse(safeContent, departmentExpenses.Keys, targetProfitMargin);

        _logger.LogInformation("Grok API recommendations received: {Count} departments", recommendationResult.AdjustmentFactors.Count);
        return recommendationResult;
    }

    private Dictionary<string, decimal> CalculateRuleBasedRecommendations(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin)
    {
        var adjustmentFactor = 1.0m + (targetProfitMargin / 100m);
        var recommendations = new Dictionary<string, decimal>();

        foreach (var dept in departmentExpenses.Keys)
        {
            // Apply department-specific variance based on typical operating costs
            var variance = dept switch
            {
                "Water" => 0.0m,        // Base rate
                "Sewer" => 0.02m,       // Higher treatment costs
                "Trash" => -0.05m,      // More efficient operations
                "Apartments" => 0.03m,  // Bundled overhead
                "Electric" => 0.04m,    // Higher infrastructure costs
                "Gas" => 0.01m,         // Moderate overhead
                _ => 0.0m
            };

            var factor = Math.Round(adjustmentFactor + variance, 4);
            recommendations[dept] = factor;
        }

        _logger.LogInformation("Rule-based recommendations: {Recommendations}",
            string.Join(", ", recommendations.Select(r => $"{r.Key}={r.Value:F4}")));

        return recommendations;
    }

    private decimal CalculateSingleRuleBasedFactor(string department, decimal targetProfitMargin)
    {
        var adjustmentFactor = 1.0m + (targetProfitMargin / 100m);
        var variance = department switch
        {
            "Water" => 0.0m,        // Base rate
            "Sewer" => 0.02m,       // Higher treatment costs
            "Trash" => -0.05m,      // More efficient operations
            "Apartments" => 0.03m,  // Bundled overhead
            "Electric" => 0.04m,    // Higher infrastructure costs
            "Gas" => 0.01m,         // Moderate overhead
            _ => 0.0m
        };
        return Math.Round(adjustmentFactor + variance, 4);
    }

    private string BuildRecommendationPrompt(Dictionary<string, decimal> expenses, decimal margin)
    {
        var expenseList = string.Join(", ", expenses.Select(e => $"{e.Key}: ${e.Value:N2}"));

        return $$"""
You are a senior municipal finance analyst specializing in utility rate design.

Given monthly departmental expenses: {{expenseList}}
Target profit margin: {{margin}}%

Provide rate adjustment factors (multipliers) that achieve full cost recovery plus the target margin, considering typical municipal patterns (e.g., higher infrastructure costs for Electric/Water, efficiency in Trash, bundled overhead in Apartments).

Respond EXACTLY with valid JSON in this format and NOTHING else — no markdown, no code blocks, no extra text:

{
  "factors": {
    "Water": 1.15,
    "Sewer": 1.18,
    ...
  },
  "explanation": "Multi-paragraph professional explanation suitable for city council and public presentation. Reference specific departmental differences and overall rationale."
}
""";
    }

    private static string ExtractJsonObject(string content)
    {
        // Use regex to find the outermost { ... } JSON object
        var match = Regex.Match(content, @"\{(?:[^{}]|(?<open>\{)|(?<-open>\}))*(?(open)(?!))\}");
        return match.Success ? match.Value : content;
    }

    private RecommendationResult ParseGrokResponse(string content, IEnumerable<string> expectedDepartments, decimal targetProfitMargin)
    {
        // Robust extraction: strip potential markdown/code blocks first
        var json = ExtractJsonObject(content);

        var response = JsonSerializer.Deserialize<GrokResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize Grok response");

        // Validate all departments present; supplement missing with rule-based
        var warnings = new List<string>();
        foreach (var dept in expectedDepartments)
        {
            if (!response.factors.ContainsKey(dept))
            {
                response.factors[dept] = CalculateSingleRuleBasedFactor(dept, targetProfitMargin);
                warnings.Add($"Missing factor for {dept}; used rule-based fallback");
            }
            else
            {
                // Round to 4 decimal places
                response.factors[dept] = Math.Round(response.factors[dept], 4);
            }
        }

        return new RecommendationResult(
            AdjustmentFactors: response.factors,
            Explanation: response.explanation,
            FromGrokApi: true,
            ApiModelUsed: _model,
            Warnings: warnings);
    }

    private void ValidateInput(Dictionary<string, decimal> expenses, decimal margin)
    {
        if (expenses == null || !expenses.Any())
            throw new ArgumentException("Department expenses cannot be null or empty", nameof(expenses));

        if (margin < 0 || margin > 50)
            throw new ArgumentOutOfRangeException(nameof(margin), "Profit margin must be between 0% and 50%");

        foreach (var expense in expenses)
        {
            if (expense.Value < 0)
                throw new ArgumentException($"Expense for {expense.Key} cannot be negative", nameof(expenses));
            if (string.IsNullOrWhiteSpace(expense.Key))
                throw new ArgumentException("Department names cannot be null or empty", nameof(expenses));
            if (!_knownDepartments.Contains(expense.Key))
                throw new ArgumentException($"Unknown department '{expense.Key}'. Known departments are: {string.Join(", ", _knownDepartments)}", nameof(expenses));
        }
    }

    private string GenerateCacheKey(Dictionary<string, decimal> expenses, decimal margin)
    {
        // Normalize numeric values to fixed precision and use stable ordering
        var sortedExpenses = string.Join("_", expenses.OrderBy(e => e.Key)
            .Select(e => $"{e.Key}:{e.Value.ToString("F4", CultureInfo.InvariantCulture)}"));
        return $"rec_{sortedExpenses}_{margin.ToString("F4", CultureInfo.InvariantCulture)}";
    }

    private static string GetCacheKeyHash(string key)
    {
        // Use SHA256 and return the first 8 hex chars for compact, non-sensitive logging
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hashBytes).Substring(0, 8);
    }

    private ValidationResult ValidateRecommendationResponse(
        Dictionary<string, decimal> recommendations,
        Dictionary<string, decimal> originalExpenses)
    {
        var result = new ValidationResult();

        // Check if all departments are covered
        var missingDepartments = originalExpenses.Keys.Except(recommendations.Keys).ToList();
        if (missingDepartments.Any())
        {
            result.AddError($"Missing recommendations for departments: {string.Join(", ", missingDepartments)}");
        }

        // Check factor ranges
        foreach (var recommendation in recommendations)
        {
            if (recommendation.Value < _minAdjustmentFactor || recommendation.Value > _maxAdjustmentFactor)
            {
                result.AddWarning($"Adjustment factor for {recommendation.Key} ({recommendation.Value}) is outside normal range [{_minAdjustmentFactor}, {_maxAdjustmentFactor}]");
            }
        }

        return result;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_useGrokApi)
        {
            return HealthCheckResult.Healthy("AI service disabled - using rule-based recommendations");
        }

        if (_circuitBreaker.CircuitState != CircuitState.Closed)
        {
            return HealthCheckResult.Unhealthy($"Circuit breaker is {_circuitBreaker.CircuitState}");
        }

        try
        {
            // Quick health check with a simple request
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var testRequest = new
            {
                model = _model,
                messages = new[] { new { role = "user", content = "Hello" } },
                max_tokens = 10
            };

            using var response = await client.PostAsJsonAsync(_apiEndpoint, testRequest, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Grok API is responding");
            }
            else
            {
                return HealthCheckResult.Unhealthy($"Grok API returned {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Grok API health check failed", ex);
        }
    }

    private class ValidationResult
    {
        public bool IsValid => !Errors.Any();
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();

        public void AddError(string error) => Errors.Add(error);
        public void AddWarning(string warning) => Warnings.Add(warning);
    }

    /// <summary>
    /// Clears cached recommendation results and explanations maintained by this service.
    /// This removes any previously cached entries created by this service's caching logic.
    /// </summary>
    public void ClearCache()
    {
        try
        {
            lock (_cacheIndexLock)
            {
                if (_cache.TryGetValue(CacheIndexKey, out HashSet<string>? index) && index != null)
                {
                    foreach (var key in index.ToArray())
                    {
                        _cache.Remove(key);
                    }

                    _cache.Remove(CacheIndexKey);
                    _logger.LogInformation("Cleared Grok recommendation cache (removed {EntryCount} entries)", index.Count);

                    // Telemetry: record cache clear event with removed count
                    try
                    {
                        _cacheClears.Add(1, new KeyValuePair<string, object?>("removed", index.Count));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to record cache clear telemetry");
                    }

                    return;
                }
            }

            _logger.LogInformation("No Grok recommendation cache entries found to clear");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear Grok recommendation cache");
        }
    }

    public async Task<string> GetRecommendationExplanationAsync(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin = 15.0m,
        CancellationToken cancellationToken = default)
    {
        if (departmentExpenses == null)
            throw new ArgumentNullException(nameof(departmentExpenses));
        try
        {
            _logger.LogInformation("Requesting explanation from Grok API");

            var explanationCacheKey = $"rec_expl_{GenerateCacheKey(departmentExpenses, targetProfitMargin)}";
            if (_cache.TryGetValue(explanationCacheKey, out string? cachedExplanation))
            {
                _cacheHits.Add(1);
                _logger.LogDebug("Explanation cache hit (key hash: {CacheKeyHash})", GetCacheKeyHash(explanationCacheKey));
                return cachedExplanation!;
            }

            string explanation;

            if (_useGrokApi)
            {
                try
                {
                    explanation = await QueryGrokForExplanationAsync(departmentExpenses, targetProfitMargin, cancellationToken);
                    if (string.IsNullOrWhiteSpace(explanation))
                    {
                        _logger.LogWarning("Grok API returned empty/whitespace explanation (post-validate); using fallback explanation");
                        explanation = GenerateRuleBasedExplanation(departmentExpenses, targetProfitMargin);
                    }
                }
                catch (Exception apiEx)
                {
                    _logger.LogWarning(apiEx, "Grok API explanation failed - using fallback");
                    explanation = GenerateRuleBasedExplanation(departmentExpenses, targetProfitMargin);
                }
            }
            else
            {
                explanation = GenerateRuleBasedExplanation(departmentExpenses, targetProfitMargin);
            }

            // Cache the explanation (avoid storing empty/whitespace)
            if (!string.IsNullOrWhiteSpace(explanation))
            {
                _cache.Set(explanationCacheKey, explanation, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _cacheDuration });

                // Track explanation key in index
                try
                {
                    lock (_cacheIndexLock)
                    {
                        var index = _cache.GetOrCreate(CacheIndexKey, (ICacheEntry entry) =>
                        {
                            entry.AbsoluteExpirationRelativeToNow = _cacheDuration.Add(TimeSpan.FromDays(1));
                            return new HashSet<string>();
                        });
                        index.Add(explanationCacheKey);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update cache index for explanation key: {CacheKeyHash}", GetCacheKeyHash(explanationCacheKey));
                }

                _logger.LogDebug("Cached explanation (key hash: {CacheKeyHash}) for {CacheDuration}", GetCacheKeyHash(explanationCacheKey), _cacheDuration);
            }

            return explanation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting explanation from Grok API");
            throw;
        }
    }

    private async Task<string> QueryGrokForExplanationAsync(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

        var expenseList = string.Join(", ", departmentExpenses.Select(e => $"{e.Key}: ${e.Value:N2}"));
        var prompt = $@"Provide a clear, professional explanation for municipal utility rate adjustments.
Monthly expenses are: {expenseList}.
Target profit margin: {targetProfitMargin}%.
Explain why these adjustments are necessary in 2-3 paragraphs suitable for public presentation.";

        var requestBody = new
        {
            model = "grok-beta",
            messages = new[]
            {
                new { role = "system", content = "You are a municipal finance expert explaining utility rate adjustments to city officials and the public." },
                new { role = "user", content = prompt }
            },
            temperature = 0.7,
            max_tokens = 400
        };

        var response = await client.PostAsJsonAsync(_apiEndpoint, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);

        if (result == null)
            throw new InvalidOperationException("Grok API returned null response");

        string? explanation = null;
        try
        {
            var root = result.RootElement;
            if (root.TryGetProperty("choices", out var choicesEl) && choicesEl.ValueKind == JsonValueKind.Array && choicesEl.GetArrayLength() > 0)
            {
                var choice = choicesEl[0];
                if (choice.TryGetProperty("message", out var messageEl) && messageEl.ValueKind == JsonValueKind.Object)
                {
                    if (messageEl.TryGetProperty("content", out var contentEl))
                    {
                        if (contentEl.ValueKind == JsonValueKind.String)
                        {
                            explanation = contentEl.GetString();
                        }
                        else if (contentEl.ValueKind == JsonValueKind.Object || contentEl.ValueKind == JsonValueKind.Array)
                        {
                            explanation = contentEl.GetRawText();
                        }
                        else if (contentEl.ValueKind == JsonValueKind.Null || contentEl.ValueKind == JsonValueKind.Undefined)
                        {
                            explanation = null;
                        }
                        else
                        {
                            explanation = contentEl.ToString();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract explanation content from Grok response - using fallback");
            explanation = null;
        }

        if (string.IsNullOrWhiteSpace(explanation))
        {
            _logger.LogWarning("Grok API returned empty or whitespace explanation content; using fallback explanation");
            return GenerateRuleBasedExplanation(departmentExpenses, targetProfitMargin);
        }

        // Trim and normalize whitespace to avoid returning spurious blank strings
        return explanation!.Trim();
    }

    private string GenerateRuleBasedExplanation(Dictionary<string, decimal> departmentExpenses, decimal targetProfitMargin)
    {
        var totalExpenses = departmentExpenses.Values.Sum();
        var departmentCount = departmentExpenses.Count;

        return $@"Based on your monthly expenses totaling ${totalExpenses:N2} across {departmentCount} departments and a target profit margin of {targetProfitMargin}%,
the recommended adjustments ensure full cost recovery plus your desired margin.

Water and Sewer departments typically show higher expenses requiring proportional rate increases due to infrastructure and treatment costs.
Trash service is often operating efficiently with lower adjustment needs.
Apartments and bundled services reflect shared utility costs with recommended margins to cover overhead and maintenance reserves.

These adjustments are calculated to maintain financial sustainability while ensuring fair and competitive rates for all customers.";
    }
}
