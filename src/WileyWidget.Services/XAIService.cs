using System;
using System.Collections.Generic;
using System.Threading.RateLimiting;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Telemetry;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Polly.RateLimiting;
using Polly.Registry;
// using Microsoft.ApplicationInsights;

namespace WileyWidget.Services;

/// <summary>
/// xAI service implementation for AI-powered insights and analysis
/// </summary>
public class XAIService : IAIService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<XAIService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWileyWidgetContextService _contextService;
    private readonly IAILoggingService _aiLoggingService;
    private readonly IMemoryCache _memoryCache;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly SigNozTelemetryService? _telemetryService;
    // private readonly dynamic _telemetryClient; // Commented out until Azure is configured
    private readonly ResiliencePipeline<HttpResponseMessage> _httpPipeline;
    private readonly IAIAssistantService? _assistantService;
    private readonly IAIPersonalityService? _personalityService;
    private readonly IFinancialInsightsService? _insightsService;
    private bool _disposed;

    /// <summary>
    /// Constructor with dependency injection
    /// Retrieves XAI API key from encrypted vault (machine-scope environment variables migrated at startup)
    /// Falls back to configuration for backward compatibility
    /// </summary>
    public XAIService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<XAIService> logger,
        IWileyWidgetContextService contextService,
        IAILoggingService aiLoggingService,
        IMemoryCache memoryCache,
        ISecretVaultService? secretVault = null,
        SigNozTelemetryService? telemetryService = null,
        IAIAssistantService? assistantService = null,
        IAIPersonalityService? personalityService = null,
        IFinancialInsightsService? insightsService = null
        // TelemetryClient telemetryClient = null // Commented out until Azure is configured
        )
    {
    _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextService = contextService ?? throw new ArgumentNullException(nameof(contextService));
        _aiLoggingService = aiLoggingService ?? throw new ArgumentNullException(nameof(aiLoggingService));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _telemetryService = telemetryService;
        _assistantService = assistantService;
        _personalityService = personalityService;
        _insightsService = insightsService;
    if (httpClientFactory is null) throw new ArgumentNullException(nameof(httpClientFactory));
        // _telemetryClient = telemetryClient; // Commented out until Azure is configured

        // Priority order for API key:
        // 1. Encrypted vault (machine-scope env vars migrated at startup)
        // 2. Configuration file or environment variables
        _apiKey = null;

        // Try vault first (preferred - encrypted at rest)
        if (secretVault != null)
        {
            _apiKey = secretVault.GetSecret("XAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogDebug("Loaded XAI API key from encrypted vault");
            }
        }

        // Fall back to configuration if not in vault
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _apiKey = configuration["XAI:ApiKey"];
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogDebug("Loaded XAI API key from configuration (consider migrating to vault for security)");
            }
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("XAI API key not configured. Set XAI_API_KEY environment variable at machine scope or add to configuration.");
        }

        var baseUrl = configuration["XAI:BaseUrl"] ?? "https://api.x.ai/v1/";
        var timeoutSeconds = double.Parse(configuration["XAI:TimeoutSeconds"] ?? "15", CultureInfo.InvariantCulture);
    // Allow tests to override circuit-breaker break duration (seconds) via configuration
    // Use TryParse to avoid throwing if configuration is malformed; default to 60 seconds
    var circuitBreakerBreakSeconds = 60;
    if (!string.IsNullOrWhiteSpace(configuration["XAI:CircuitBreakerBreakSeconds"]) &&
        int.TryParse(configuration["XAI:CircuitBreakerBreakSeconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBreak))
    {
        circuitBreakerBreakSeconds = parsedBreak;
    }

        // Validate API key format (basic check) - wrapped to handle exceptions gracefully
            try
            {
                if (_apiKey.Length < 20)
                {
                    throw new InvalidOperationException("API key appears to be invalid (too short)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "XAI API key validation failed during construction");
                throw; // Re-throw to prevent invalid service creation
            }

        // Initialize concurrency control (limit to 5 concurrent requests to avoid throttling)
        var maxConcurrentRequests = int.Parse(configuration["XAI:MaxConcurrentRequests"] ?? "5", CultureInfo.InvariantCulture);
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);

    // Create or fall back to a default HttpClient if the factory returns null (tests may not set up a named client)
    var createdClient = httpClientFactory.CreateClient("AIServices");
    _httpClient = createdClient ?? new HttpClient();
    _httpClient.BaseAddress = new Uri(baseUrl);
    _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        // Set default headers only if not already set by the named client
        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        // Create Polly v8 resilience pipeline with modern API
        // Following Microsoft's recommended patterns for resilience
        _httpPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            // 1. RATE LIMITER - Prevent client-side throttling (50 requests/minute)
            .AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 50,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }))
            // 2. TIMEOUT - Prevent hanging requests (15s configured timeout)
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds),
                OnTimeout = args =>
                {
                    _logger.LogError("xAI API timeout after {Timeout}s", args.Timeout.TotalSeconds);
                    // Outcome is not available on OnTimeout arguments in this runtime; record a timeout exception instead
                    var tex = new TimeoutException($"xAI API timeout after {args.Timeout.TotalSeconds} seconds");
                    _telemetryService?.RecordException(tex, ("xai.timeout", "request_timeout"));
                    return ValueTask.CompletedTask;
                }
            })
            // 3. CIRCUIT BREAKER - Fail fast during outages
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,                    // Open if 50% of requests fail
                SamplingDuration = TimeSpan.FromSeconds(30),  // Sample window
                MinimumThroughput = 5,                 // Minimum requests before evaluating
                BreakDuration = TimeSpan.FromSeconds(circuitBreakerBreakSeconds), // Stay open for configured seconds
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                    .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError)
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(),
                OnOpened = args =>
                {
                    _logger.LogError("xAI API Circuit Breaker OPEN - too many failures");
                    _telemetryService?.RecordException(args.Outcome.Exception,
                        ("xai.circuit_breaker", "opened"));
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("xAI API Circuit Breaker CLOSED - resuming normal operation");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("xAI API Circuit Breaker HALF-OPEN - testing recovery");
                    return ValueTask.CompletedTask;
                }
            })
            // 4. RETRY - Handle transient errors with smart backoff
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(500),        // Base delay
                UseJitter = true,                               // Critical for AI APIs with rate limits
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)
                    .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.BadGateway)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.ServiceUnavailable)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.GatewayTimeout)
                    .Handle<HttpRequestException>(),
                OnRetry = args =>
                {
                    var statusCode = args.Outcome.Result?.StatusCode.ToString() ?? "Unknown";
                    _logger.LogWarning("xAI API retry {Attempt}/3 after {Delay}ms due to {StatusCode}",
                        args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds, statusCode);
                    // Prefer logging the exception when available, otherwise log a string error
                    if (args.Outcome.Exception != null)
                    {
                        _aiLoggingService.LogError("Retry", args.Outcome.Exception);
                    }
                    else
                    {
                        _aiLoggingService.LogError("Retry", $"HTTP {statusCode}", "Retry");
                    }
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        _logger.LogInformation("✓ XAIService initialized with Polly v8 resilience pipeline (rate limit: 50/min, timeout: {Timeout}s, circuit breaker: 50% failure ratio, retry: 3x exponential with jitter)",
            timeoutSeconds);
    }

    /// <summary>
    /// Sanitizes user input to prevent injection attacks and ensure safe API usage
    /// </summary>
    /// <param name="input">The input string to sanitize</param>
    /// <returns>Sanitized string safe for API usage</returns>
    private static string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove or escape potentially dangerous characters
        return input
            .Replace("\\", "\\\\", StringComparison.Ordinal)  // Escape backslashes first
            .Replace("\"", "\\\"", StringComparison.Ordinal)  // Escape quotes
            .Replace("\n", " ", StringComparison.Ordinal)     // Replace newlines with spaces
            .Replace("\r", " ", StringComparison.Ordinal)     // Replace carriage returns with spaces
            .Replace("\t", " ", StringComparison.Ordinal)     // Replace tabs with spaces
            .Replace("\0", "", StringComparison.Ordinal)      // Remove null characters
            .Trim();                // Trim whitespace
    }

    /// <summary>
    /// Validates and sanitizes context and question inputs
    /// </summary>
    /// <param name="context">The context string to validate and sanitize</param>
    /// <param name="question">The question string to validate and sanitize</param>
    private static void ValidateAndSanitizeInputs(ref string context, ref string question)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(context))
            throw new ArgumentException("Context cannot be null or empty", nameof(context));
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question cannot be null or empty", nameof(question));

        // Check for excessively long inputs (potential DoS)
        if (context.Length > 10000)
            throw new ArgumentException("Context is too long (maximum 10,000 characters)", nameof(context));
        if (question.Length > 5000)
            throw new ArgumentException("Question is too long (maximum 5,000 characters)", nameof(question));

        // Sanitize inputs
        context = SanitizeInput(context);
        question = SanitizeInput(question);
    }

    /// <summary>
    /// Get AI insights for the provided context and question
    /// </summary>
    public async Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default)
    {
        // Start telemetry tracking for AI API call
        using var apiCallSpan = _telemetryService?.StartActivity("ai.xai.get_insights",
            ("ai.model", _configuration["XAI:Model"] ?? "grok-4-0709"),
            ("ai.provider", "xAI"));

        // Validate and sanitize inputs to prevent injection attacks
        // Do this before the try so ArgumentException for invalid inputs can propagate to callers/tests
        ValidateAndSanitizeInputs(ref context, ref question);

        var startTime = DateTime.UtcNow;

        // Track whether we successfully entered the concurrency semaphore so we only release when acquired
        var semaphoreEntered = false;

        try
        {

            apiCallSpan?.SetTag("ai.question_length", question?.Length ?? 0);
            apiCallSpan?.SetTag("ai.context_length", context?.Length ?? 0);

            // Create cache key from sanitized inputs
            var cacheKey = $"XAI:{context.GetHashCode(StringComparison.OrdinalIgnoreCase)}:{question.GetHashCode(StringComparison.OrdinalIgnoreCase)}";

            // Check cache first
            if (_memoryCache.TryGetValue(cacheKey, out string cachedResponse))
            {
                Log.Information("Cache hit for XAI query: {Question}", question);
                apiCallSpan?.SetTag("ai.cache_hit", true);
                _aiLoggingService.LogQuery(question, context, _configuration["XAI:Model"] ?? "grok-4-0709");
                return cachedResponse;
            }

            apiCallSpan?.SetTag("ai.cache_hit", false);

    // Acquire concurrency semaphore to limit concurrent requests.
    // Use safe acquisition to avoid SemaphoreSlim throwing OperationCanceledException
    var acquired = await AcquireSemaphoreSafeAsync(_concurrencySemaphore, cancellationToken).ConfigureAwait(false);
    if (!acquired)
    {
        _logger?.LogDebug("XAI request canceled before starting");
        return string.Empty;
    }
    semaphoreEntered = true;

            var model = _configuration["XAI:Model"] ?? "grok-4-0709";

            var systemContext = await _contextService.BuildCurrentSystemContextAsync(cancellationToken);

            // Log the query
            _aiLoggingService.LogQuery(question, $"{context} | {systemContext}", model);

            // Track telemetry for API call start - commented out until Azure is configured
            // _telemetryClient?.TrackEvent("XAIServiceRequest", new Dictionary<string, string>
            // {
            //     ["Model"] = model,
            //     ["QuestionLength"] = question?.Length.ToString() ?? "0",
            //     ["ContextLength"] = context?.Length.ToString() ?? "0"
            // });

            // Build system prompt with personality if available
            string systemPrompt;
            if (_personalityService != null)
            {
                systemPrompt = _personalityService.BuildSystemPrompt(systemContext, context);
            }
            else
            {
                systemPrompt = $"You are a helpful AI assistant for a municipal utility management application called Wiley Widget. System Context: {systemContext}. Context: {context}";
            }

            var request = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = systemPrompt
                    },
                    new
                    {
                        role = "user",
                        content = question
                    }
                },
                model = model,
                stream = false,
                temperature = _personalityService?.CurrentPersonality.Temperature ?? 0.7
            };

            // Execute HTTP request with Polly v8 resilience pipeline
            var response = await _httpPipeline.ExecuteAsync(
                async context => await _httpClient.PostAsJsonAsync("chat/completions", request, context.CancellationToken),
                ResilienceContextPool.Shared.Get(cancellationToken));

            // Handle non-successful status codes gracefully
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                Log.Error("xAI API returned non-success status {Status} with body: {Body}", status, body);
                _aiLoggingService.LogError(question, body ?? string.Empty, response.StatusCode.ToString());

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    // Authentication or permission problem - surface clear guidance
                    return "AI service returned 403 Forbidden. Please verify the configured API key and permissions.";
                }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return "AI service is rate limiting requests. Please try again shortly.";
                }

                return "AI service returned an error. Please try again later.";
            }

            var result = await response.Content.ReadFromJsonAsync<XAIResponse>(cancellationToken: cancellationToken);
            if (result?.error != null)
            {
                Log.Error("xAI API error: {ErrorType} - {ErrorMessage}", result.error.type, result.error.message);
                _aiLoggingService.LogError(question, result.error.message, result.error.type ?? "API Error");
                return $"API error: {result.error.message}";
            }

            if (result?.choices?.Length > 0)
            {
                var content = result.choices[0].message?.content;
                if (!string.IsNullOrEmpty(content))
                {
                    var responseTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                    _aiLoggingService.LogResponse(question, content, responseTimeMs, 0);

                    // Track successful response telemetry - commented out until Azure is configured
                    // _telemetryClient?.TrackEvent("XAIServiceSuccess", new Dictionary<string, string>
                    // {
                    //     ["Model"] = model,
                    //     ["ResponseTimeMs"] = responseTimeMs.ToString(),
                    //     ["ResponseLength"] = content.Length.ToString()
                    // });

                    // Track response time metric - commented out until Azure is configured
                    // _telemetryClient?.TrackMetric("XAIServiceResponseTime", responseTimeMs, new Dictionary<string, string>
                    // {
                    //     ["Model"] = model,
                    //     ["Operation"] = "GetInsights"
                    // });

                    Log.Information("Successfully received xAI response for question: {Question}", question);

                    // Cache the successful response (5 minute expiration for supercompute tasks)
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                        .SetSlidingExpiration(TimeSpan.FromMinutes(2));
                    _memoryCache.Set(cacheKey, content, cacheOptions);

                    return content;
                }
            }

            Log.Warning("xAI API returned empty or invalid response");
            _aiLoggingService.LogError(question, "Empty or invalid response from XAI API", "Empty Response");
            return "I apologize, but I received an empty response. Please try rephrasing your question.";
        }
        catch (InvalidOperationException ex)
        {
            Log.Error(ex, "xAI API authentication failed: {Message}", ex.Message);
            _aiLoggingService.LogError(question, ex);

            // Track authentication failure telemetry - commented out until Azure is configured
            // _telemetryClient?.TrackEvent("XAIServiceAuthFailure", new Dictionary<string, string>
            // {
            //     ["ErrorType"] = "Authentication",
            //     ["ExceptionType"] = ex.GetType().Name
            // });

            return "Authentication failed. Please check your API key configuration.";
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Network error calling xAI API: {Message}", ex.Message);
            _aiLoggingService.LogError(question, ex);

            // Track network error telemetry - commented out until Azure is configured
            // _telemetryClient?.TrackEvent("XAIServiceNetworkError", new Dictionary<string, string>
            // {
            //     ["ErrorType"] = "Network",
            //     ["ExceptionType"] = ex.GetType().Name,
            //     ["StatusCode"] = ex.StatusCode?.ToString() ?? "Unknown"
            // });

            return "I'm experiencing network connectivity issues. Please check your internet connection and try again.";
        }
        catch (OperationCanceledException ex) when (ex is not TaskCanceledException)
        {
            // OperationCanceledException from Polly timeout, rate limiter, or explicit cancellation
            // TaskCanceledException (which extends OCE) is handled separately below
            Log.Warning(ex, "xAI API operation was cancelled. Source: {Source}, IsCancellationRequested: {IsCancelled}",
                ex.Source ?? "Unknown", ex.CancellationToken.IsCancellationRequested);
            _aiLoggingService.LogError(question, $"Operation cancelled: {ex.Message}", "OperationCancelled");

            if (ex.CancellationToken.IsCancellationRequested)
            {
                // User or application initiated cancellation
                return "The AI request was cancelled.";
            }

            // Polly timeout or rate limiter
            return "The AI service is currently busy. Please try again in a moment.";
        }
        catch (TaskCanceledException ex)
        {
            Log.Error(ex, "xAI API request timed out after {TimeoutSeconds} seconds", _httpClient.Timeout.TotalSeconds);
            _aiLoggingService.LogError(question, $"Request timed out after {_httpClient.Timeout.TotalSeconds} seconds", "Timeout");

            // Track timeout telemetry - commented out until Azure is configured
            // _telemetryClient?.TrackEvent("XAIServiceTimeout", new Dictionary<string, string>
            // {
            //     ["ErrorType"] = "Timeout",
            //     ["TimeoutSeconds"] = _httpClient.Timeout.TotalSeconds.ToString()
            // });

            return $"The request timed out after {_httpClient.Timeout.TotalSeconds} seconds. The xAI service may be experiencing high load. Please try again later.";
        }
        catch (BrokenCircuitException<HttpResponseMessage> ex)
        {
            Log.Warning(ex, "xAI API circuit breaker is open (generic): {Message}", ex.Message);
            _aiLoggingService.LogError(question, ex.Message, "CircuitBreakerOpen");
            return "error: xAI service circuit breaker is open";
        }
        catch (BrokenCircuitException ex)
        {
            // Circuit breaker is open - fail fast and return an error-like message for tests/consumers
            Log.Warning(ex, "xAI API circuit breaker is open: {Message}", ex.Message);
            _aiLoggingService.LogError(question, ex.Message, "CircuitBreakerOpen");
            return "error: xAI service circuit breaker is open";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error in xAI service: {Message}", ex.Message);
            _aiLoggingService.LogError(question, ex);

            // Track unexpected error telemetry - commented out until Azure is configured
            // _telemetryClient?.TrackEvent("XAIServiceUnexpectedError", new Dictionary<string, string>
            // {
            //     ["ErrorType"] = "Unexpected",
            //     ["ExceptionType"] = ex.GetType().Name
            // });

            return "I encountered an unexpected error. Please try again later.";
        }
        finally
        {
            // Release the concurrency semaphore only if we acquired it
            if (semaphoreEntered)
            {
                _concurrencySemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Get AI insights for multiple context and question pairs (batched for efficiency)
    /// </summary>
    public async Task<Dictionary<string, string>> BatchGetInsightsAsync(
        IEnumerable<(string context, string question)> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests == null) throw new ArgumentNullException(nameof(requests));

        var results = new Dictionary<string, string>();
        var apiRequests = new List<(string cacheKey, string context, string question, int index)>();

        // First pass: check cache and prepare API requests
        int index = 0;
        foreach (var (context, question) in requests)
        {
            // Validate and sanitize inputs
            var sanitizedContext = context;
            var sanitizedQuestion = question;
            ValidateAndSanitizeInputs(ref sanitizedContext, ref sanitizedQuestion);

            var cacheKey = $"XAI:{sanitizedContext.GetHashCode(StringComparison.OrdinalIgnoreCase)}:{sanitizedQuestion.GetHashCode(StringComparison.OrdinalIgnoreCase)}";

            // Check cache first
            if (_memoryCache.TryGetValue(cacheKey, out string cachedResponse))
            {
                Log.Information("Cache hit for batched XAI query: {Question}", sanitizedQuestion);
                results[cacheKey] = cachedResponse;
            }
            else
            {
                apiRequests.Add((cacheKey, sanitizedContext, sanitizedQuestion, index));
            }
            index++;
        }

        // Process API requests in batches to avoid overwhelming the service
        const int batchSize = 3; // Process 3 requests at a time
        for (int i = 0; i < apiRequests.Count; i += batchSize)
        {
            var batch = apiRequests.Skip(i).Take(batchSize);
            var tasks = batch.Select(async req =>
            {
                // Use semaphore to limit concurrency
                await _concurrencySemaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await GetInsightsInternalAsync(req.context, req.question, req.cacheKey, cancellationToken);
                    return (req.cacheKey, result);
                }
                finally
                {
                    _concurrencySemaphore.Release();
                }
            });

            var batchResults = await Task.WhenAll(tasks);
            foreach (var (key, response) in batchResults)
            {
                results[key] = response;
            }

            // Small delay between batches to be respectful to the API
            if (i + batchSize < apiRequests.Count)
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        return results;
    }

    /// <summary>
    /// Internal method for getting insights (used by batch processing)
    /// </summary>
    private async Task<string> GetInsightsInternalAsync(string context, string question, string cacheKey, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var model = _configuration["XAI:Model"] ?? "grok-4-0709";

        var systemContext = await _contextService.BuildCurrentSystemContextAsync(cancellationToken);

        // Log the query
        _aiLoggingService.LogQuery(question, $"{context} | {systemContext}", model);

        var request = new
        {
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = $"You are a helpful AI assistant for a municipal utility management application called Wiley Widget. System Context: {systemContext}. Context: {context}"
                },
                new
                {
                    role = "user",
                    content = question
                }
            },
            model = model,
            stream = false,
            temperature = 0.7
        };

        // Execute HTTP request with Polly v8 resilience pipeline
        var response = await _httpPipeline.ExecuteAsync(
            async context => await _httpClient.PostAsJsonAsync("chat/completions", request, context.CancellationToken),
            ResilienceContextPool.Shared.Get(cancellationToken));

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Log.Error("xAI API returned non-success status {Status} with body: {Body}", status, body);
            _aiLoggingService.LogError(question, body ?? string.Empty, response.StatusCode.ToString());

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return "AI service returned 403 Forbidden. Please verify the configured API key and permissions.";
            }

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                return "AI service is rate limiting requests. Please try again shortly.";
            }

            return "AI service returned an error. Please try again later.";
        }

        var result = await response.Content.ReadFromJsonAsync<XAIResponse>(cancellationToken: cancellationToken);
        if (result?.error != null)
        {
            Log.Error("xAI API error: {ErrorType} - {ErrorMessage}", result.error.type, result.error.message);
            _aiLoggingService.LogError(question, result.error.message, result.error.type ?? "API Error");
            return $"API error: {result.error.message}";
        }

        if (result?.choices?.Length > 0)
        {
            var content = result.choices[0].message?.content;
            if (!string.IsNullOrEmpty(content))
            {
                var responseTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                _aiLoggingService.LogResponse(question, content, responseTimeMs, 0);

                Log.Information("Successfully received xAI response for question: {Question}", question);

                // Cache the successful response
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));
                _memoryCache.Set(cacheKey, content, cacheOptions);

                return content;
            }
        }

        Log.Warning("xAI API returned empty or invalid response");
        _aiLoggingService.LogError(question, "Empty or invalid response from XAI API", "Empty Response");
        return "I apologize, but I received an empty response. Please try rephrasing your question.";
    }

    /// <summary>
    /// Analyze data and provide insights
    /// </summary>
    public async Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default)
    {
        var question = $"Please analyze the following {analysisType} data and provide insights: {data}";
        return await GetInsightsAsync("Data Analysis", question, cancellationToken);
    }

    /// <summary>
    /// Typed insights method which returns status codes and error details for UI handling
    /// </summary>
    public async Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken cancellationToken = default)
    {
        // Validate and sanitize
        ValidateAndSanitizeInputs(ref context, ref question);

        var startTime = DateTime.UtcNow;
        var model = _configuration["XAI:Model"] ?? "grok-4-0709";

        var systemContext = await _contextService.BuildCurrentSystemContextAsync(cancellationToken);
        _aiLoggingService.LogQuery(question, $"{context} | {systemContext}", model);

        var request = new
        {
            messages = new[]
            {
                new { role = "system", content = $"You are a helpful AI assistant for Wiley Widget. System Context: {systemContext}. Context: {context}" },
                new { role = "user", content = question }
            },
            model = model,
            stream = false,
            temperature = 0.7
        };

        // Execute HTTP request with Polly v8 resilience pipeline
        var response = await _httpPipeline.ExecuteAsync(
            async context => await _httpClient.PostAsJsonAsync("chat/completions", request, context.CancellationToken),
            ResilienceContextPool.Shared.Get(cancellationToken));

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Log.Error("xAI API returned non-success status {Status} with body: {Body}", status, body);
            _aiLoggingService.LogError(question, body ?? string.Empty, response.StatusCode.ToString());

            var errorCode = response.StatusCode == System.Net.HttpStatusCode.Forbidden ? "AuthFailure" :
                            response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ? "RateLimited" : "ServerError";

            var userMessage = response.StatusCode == System.Net.HttpStatusCode.Forbidden
                ? "AI service returned 403 Forbidden. Please verify the configured API key and permissions."
                : response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    ? "AI service is rate limiting requests. Please try again shortly."
                    : "AI service returned an error. Please try again later.";

            return new AIResponseResult(userMessage, status, errorCode, body);
        }

        var xaiResponse = await response.Content.ReadFromJsonAsync<XAIResponse>(cancellationToken: cancellationToken);
        if (xaiResponse?.error != null)
        {
            Log.Error("xAI API error: {ErrorType} - {ErrorMessage}", xaiResponse.error.type, xaiResponse.error.message);
            _aiLoggingService.LogError(question, xaiResponse.error.message, xaiResponse.error.type ?? "API Error");
            return new AIResponseResult($"API error: {xaiResponse.error.message}", 500, xaiResponse.error.type, xaiResponse.error.message);
        }

        var content = xaiResponse?.choices?.Length > 0 ? xaiResponse.choices[0].message?.content : null;

        if (!string.IsNullOrEmpty(content))
        {
            var responseTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _aiLoggingService.LogResponse(question, content, responseTimeMs, 0);
            Log.Information("Successfully received xAI response for question: {Question}", question);
            return new AIResponseResult(content, 200, null, null);
        }

        return new AIResponseResult("I apologize, but I received an empty response.", 204, "EmptyResponse", null);
    }

    /// <summary>
    /// Validate an API key by issuing a lightweight API call using the supplied key (does not mutate the service's configured key).
    /// Returns an AIResponseResult that includes HTTP status and any provider error body for UI handling.
    /// </summary>
    public async Task<AIResponseResult> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new AIResponseResult("API key is empty", 400, "InvalidKey", null);

        try
        {
            // Prepare a minimal validation request. Use explicit Authorization header for this request only.
            var request = new
            {
                messages = new[]
                {
                    new { role = "system", content = "Validation ping" },
                    new { role = "user", content = "Ping" }
                },
                model = _configuration["XAI:Model"] ?? "grok-4-0709",
                stream = false,
                temperature = 0.0
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(request, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            };

            // Add the provided key on this request without altering the client default headers
            message.Headers.Remove("Authorization");
            message.Headers.Add("Authorization", $"Bearer {apiKey}");

            // Execute HTTP request with Polly v8 resilience pipeline
            var response = await _httpPipeline.ExecuteAsync(
                async context => await _httpClient.SendAsync(message, context.CancellationToken),
                ResilienceContextPool.Shared.Get(cancellationToken));

            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorCode = response.StatusCode == System.Net.HttpStatusCode.Forbidden ? "AuthFailure" :
                                response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ? "RateLimited" : "ServerError";
                return new AIResponseResult(body ?? "Validation failed", status, errorCode, body);
            }

            // Success
            var xaiResponse = await response.Content.ReadFromJsonAsync<XAIResponse>(cancellationToken: cancellationToken);
            if (xaiResponse?.error != null)
            {
                return new AIResponseResult(xaiResponse.error.message ?? "API error", 500, xaiResponse.error.type, xaiResponse.error.message);
            }

            var content = xaiResponse?.choices?.Length > 0 ? xaiResponse.choices[0].message?.content : null;
            return new AIResponseResult(content ?? "OK", 200, null, null);
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "Network error while validating API key");
            return new AIResponseResult(ex.Message, 0, "NetworkError", ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            Log.Warning(ex, "Timeout while validating API key");
            return new AIResponseResult("Timeout", 0, "Timeout", ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error while validating API key");
            return new AIResponseResult(ex.Message, 0, "Unexpected", ex.Message);
        }
    }

    // Adapter overload to satisfy the Abstractions interface which declares
    // ValidateApiKeyAsync(string) without a CancellationToken parameter.
    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey)
        => ValidateApiKeyAsync(apiKey, CancellationToken.None);

    /// <summary>
    /// Review application areas and provide recommendations
    /// </summary>
    public async Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default)
    {
        var question = $"Please review the {areaName} area with current state: {currentState}. Provide recommendations for improvement.";
        return await GetInsightsAsync("Application Review", question, cancellationToken);
    }

    /// <summary>
    /// Generate mock data suggestions
    /// </summary>
    public async Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default)
    {
        var question = $"Please suggest mock data for {dataType} with these requirements: {requirements}";
        return await GetInsightsAsync("Mock Data Generation", question, cancellationToken);
    }

    /// <summary>
    /// xAI API response model
    /// </summary>
    private class XAIResponse
    {
        public Choice[] choices { get; set; }
        public XAIError error { get; set; }

        public class Choice
        {
            public Message message { get; set; }
        }

        public class Message
        {
            public string content { get; set; }
        }

        public class XAIError
        {
            public string message { get; set; }
            public string type { get; set; }
            public string code { get; set; }
        }
    }

    /// <summary>
    /// Dispose of managed resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose pattern implementation
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
                _concurrencySemaphore?.Dispose();
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Update the runtime API key used by the HttpClient for subsequent requests.
    /// This is used after rotating/persisting a new key so the live service reflects the change.
    /// </summary>
    public Task UpdateApiKeyAsync(string newApiKey)
    {
        if (string.IsNullOrWhiteSpace(newApiKey))
            throw new ArgumentException("newApiKey cannot be null or empty", nameof(newApiKey));

        // Update the default Authorization header for the client
        try
        {
            if (_httpClient.DefaultRequestHeaders.Contains("Authorization"))
                _httpClient.DefaultRequestHeaders.Remove("Authorization");

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {newApiKey}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update XAIService API key in HttpClient headers");
            throw;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a prompt to the xAI service and returns the response
    /// </summary>
    /// <param name="prompt">The prompt to send</param>
    /// <returns>The AI response result</returns>
    public async Task<AIResponseResult> SendPromptAsync(string prompt, System.Threading.CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));

        try
        {
            _logger.LogInformation("Sending prompt to xAI service, length: {Length}", prompt.Length);

            var request = new
            {
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                model = "grok-beta",
                stream = false,
                temperature = 0.7
            };

            var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken: cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<XAIResponse>();
                var message = result?.choices?.FirstOrDefault()?.message?.content ?? "No response content";

                _logger.LogInformation("Successfully received response from xAI");
                return new AIResponseResult(message, (int)response.StatusCode);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("xAI API returned error status {StatusCode}: {Error}", response.StatusCode, errorContent);
                return new AIResponseResult($"xAI API error: {errorContent}", (int)response.StatusCode, "APIError", errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending prompt to xAI service");
            return new AIResponseResult($"Error: {ex.Message}", 500, "InternalError", ex.Message);
        }
    }

    /// <summary>
    /// Sends a user message with conversation history to the xAI service and returns a ChatResponse.
    /// This method handles the full chat flow including tool call detection and resolution.
    /// Conversation history is maintained by the caller and appended with the user's message.
    /// </summary>
    public async Task<ChatResponse> SendMessageAsync(string userMessage, List<ChatMessage> conversationHistory, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("User message cannot be null or empty", nameof(userMessage));
        if (conversationHistory == null)
            throw new ArgumentNullException(nameof(conversationHistory));

        try
        {
            // Sanitize user input
            var sanitizedMessage = SanitizeInput(userMessage);

            // Add user message to conversation history
            conversationHistory.Add(ChatMessage.CreateUserMessage(sanitizedMessage));

            _logger.LogInformation("Sending message to xAI service (conversation history length: {Length})", conversationHistory.Count);

            // Get system context for the prompt
            var systemContext = await _contextService.BuildCurrentSystemContextAsync(ct);

            // Build messages array from conversation history
            var messages = new List<object>();

            // Build system prompt with personality if available
            string systemPrompt;
            if (_personalityService != null)
            {
                systemPrompt = _personalityService.BuildSystemPrompt(systemContext, "Assist the user with their queries about budget management, utilities, and financial operations.");
            }
            else
            {
                systemPrompt = $"You are a helpful AI assistant for a municipal utility management application called Wiley Widget. System Context: {systemContext}. Assist the user with their queries about budget management, utilities, and financial operations.";
            }

            // Add system message
            messages.Add(new
            {
                role = "system",
                content = systemPrompt
            });

            // Add conversation history
            foreach (var msg in conversationHistory)
            {
                messages.Add(new
                {
                    role = msg.IsUser ? "user" : "assistant",
                    content = msg.Message
                });
            }

            var model = _configuration["XAI:Model"] ?? "grok-4-0709";

            var request = new
            {
                messages = messages.ToArray(),
                model = model,
                stream = false,
                temperature = 0.7
            };

            // Log the query
            _aiLoggingService.LogQuery(sanitizedMessage, $"Conversation with {conversationHistory.Count} messages", model);

            // Execute HTTP request with Polly v8 resilience pipeline
            var response = await _httpPipeline.ExecuteAsync(
                async context => await _httpClient.PostAsJsonAsync("chat/completions", request, context.CancellationToken),
                ResilienceContextPool.Shared.Get(ct));

            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync(ct);
                Log.Error("xAI API returned non-success status {Status} with body: {Body}", status, body);
                _aiLoggingService.LogError(sanitizedMessage, body ?? string.Empty, response.StatusCode.ToString());

                var errorMsg = response.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "AI service returned 403 Forbidden. Please verify your API key."
                    : response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                        ? "AI service is rate limiting. Please try again shortly."
                        : "AI service returned an error. Please try again later.";

                return new ChatResponse(errorMsg);
            }

            var xaiResponse = await response.Content.ReadFromJsonAsync<XAIResponse>(cancellationToken: ct);

            if (xaiResponse?.error != null)
            {
                Log.Error("xAI API error: {ErrorType} - {ErrorMessage}", xaiResponse.error.type, xaiResponse.error.message);
                _aiLoggingService.LogError(sanitizedMessage, xaiResponse.error.message, xaiResponse.error.type ?? "API Error");
                return new ChatResponse($"API error: {xaiResponse.error.message}");
            }

            var content = xaiResponse?.choices?.FirstOrDefault()?.message?.content ?? string.Empty;

            if (string.IsNullOrEmpty(content))
            {
                Log.Warning("xAI API returned empty response");
                _aiLoggingService.LogError(sanitizedMessage, "Empty response from XAI API", "EmptyResponse");
                return new ChatResponse("I received an empty response. Please try rephrasing your question.");
            }

            // Add AI response to conversation history
            conversationHistory.Add(ChatMessage.CreateAIMessage(content));

            _aiLoggingService.LogResponse(sanitizedMessage, content, 0, 0);
            Log.Information("Successfully received xAI response");

            return new ChatResponse(content);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in SendMessageAsync: {Message}", ex.Message);
            _aiLoggingService.LogError(userMessage, ex);
            return new ChatResponse($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a tool call and returns the result.
    /// Tool calls are detected by the xAI provider when sending messages.
    /// This implementation currently returns a placeholder; extend with actual tool implementations.
    /// </summary>
    public async Task<ToolCallResult> ExecuteToolCallAsync(ToolCall toolCall, CancellationToken ct = default)
    {
        if (toolCall == null)
            throw new ArgumentNullException(nameof(toolCall));

        try
        {
            _logger.LogInformation("Executing tool call: {ToolName} (ID: {ToolId})", toolCall.Name, toolCall.Id);

            // Route to appropriate tool handler based on tool name
            var result = toolCall.Name switch
            {
                "get_budget_data" => await ExecuteGetBudgetDataAsync(toolCall, ct),
                "analyze_budget_trends" => await ExecuteAnalyzeBudgetTrendsAsync(toolCall, ct),
                "get_account_details" => await ExecuteGetAccountDetailsAsync(toolCall, ct),
                "generate_report" => await ExecuteGenerateReportAsync(toolCall, ct),
                _ => ToolCallResult.Error(toolCall.Id, $"Unknown tool: {toolCall.Name}")
            };

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing tool call {ToolName}: {Message}", toolCall.Name, ex.Message);
            _logger.LogError(ex, "Tool execution failed");
            return ToolCallResult.Error(toolCall.Id, $"Error executing tool: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the get_budget_data tool
    /// </summary>
    private Task<ToolCallResult> ExecuteGetBudgetDataAsync(ToolCall toolCall, CancellationToken ct)
    {
        try
        {
            // Parse arguments (would require IBudgetRepository or similar)
            var accountId = toolCall.Arguments.TryGetValue("account_id", out var id) ? id?.ToString() : null;
            var fiscalYear = toolCall.Arguments.TryGetValue("fiscal_year", out var year) ? year?.ToString() : null;

            _logger.LogInformation("Getting budget data for account {AccountId}, fiscal year {FiscalYear}", accountId, fiscalYear);

            // Placeholder implementation - would integrate with IBudgetRepository
            var content = $"Budget data retrieved for account {accountId} in fiscal year {fiscalYear}. " +
                         "Integration with data layer pending.";

            return Task.FromResult(ToolCallResult.Success(toolCall.Id, content));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in get_budget_data tool");
            return Task.FromResult(ToolCallResult.Error(toolCall.Id, ex.Message));
        }
    }

    /// <summary>
    /// Executes the analyze_budget_trends tool
    /// </summary>
    private Task<ToolCallResult> ExecuteAnalyzeBudgetTrendsAsync(ToolCall toolCall, CancellationToken ct)
    {
        try
        {
            var period = toolCall.Arguments.TryGetValue("period", out var p) ? p?.ToString() : "quarterly";

            _logger.LogInformation("Analyzing budget trends for period: {Period}", period);

            // Placeholder implementation
            var content = $"Budget trend analysis for {period} period. " +
                         "Detailed analysis logic would be implemented here.";

            return Task.FromResult(ToolCallResult.Success(toolCall.Id, content));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in analyze_budget_trends tool");
            return Task.FromResult(ToolCallResult.Error(toolCall.Id, ex.Message));
        }
    }

    /// <summary>
    /// Executes the get_account_details tool
    /// </summary>
    private Task<ToolCallResult> ExecuteGetAccountDetailsAsync(ToolCall toolCall, CancellationToken ct)
    {
        try
        {
            var accountId = toolCall.Arguments.TryGetValue("account_id", out var id) ? id?.ToString() : null;

            _logger.LogInformation("Getting account details for {AccountId}", accountId);

            // Placeholder implementation
            var content = $"Account details for {accountId}. " +
                         "Detailed account information would be retrieved here.";

            return Task.FromResult(ToolCallResult.Success(toolCall.Id, content));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in get_account_details tool");
            return Task.FromResult(ToolCallResult.Error(toolCall.Id, ex.Message));
        }
    }

    /// <summary>
    /// Executes the generate_report tool
    /// </summary>
    private Task<ToolCallResult> ExecuteGenerateReportAsync(ToolCall toolCall, CancellationToken ct)
    {
        try
        {
            var reportType = toolCall.Arguments.TryGetValue("report_type", out var type) ? type?.ToString() : "summary";

            _logger.LogInformation("Generating {ReportType} report", reportType);

            // Placeholder implementation
            var content = $"Generated {reportType} report. " +
                         "Report generation logic would be implemented here.";

            return Task.FromResult(ToolCallResult.Success(toolCall.Id, content));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in generate_report tool");
            return Task.FromResult(ToolCallResult.Error(toolCall.Id, ex.Message));
        }
    }

    private static async Task<bool> AcquireSemaphoreSafeAsync(SemaphoreSlim sem, CancellationToken cancellationToken)
    {
        if (sem == null) throw new ArgumentNullException(nameof(sem));

        if (sem.Wait(0)) return true;

        var poll = TimeSpan.FromMilliseconds(200);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (await sem.WaitAsync(poll).ConfigureAwait(false))
                    return true;
            }
            catch
            {
                // ignore transient errors while polling
            }
        }

        return false;
    }

    /// <summary>
    /// Get tool definitions for xAI function calling integration.
    /// These definitions follow the official xAI tool calling specification.
    /// Includes both server-side tools (executed by xAI) and client-side tools (executed locally).
    /// Reference: https://docs.x.ai/docs/guides/tools/overview
    /// </summary>
    public List<object> GetToolDefinitions()
    {
        var toolsEnabled = _configuration.GetValue<bool>("FeatureFlags:EnableXAIToolCalling", true);
        if (!toolsEnabled)
        {
            _logger.LogDebug("xAI tool calling is disabled via configuration");
            return new List<object>();
        }

        var tools = new List<object>();

        // CLIENT-SIDE TOOLS: Executed locally by Wiley Widget
        // These require client-side execution and results must be returned to xAI

        // File operations
        tools.Add(new
        {
            type = "function",
            function = new
            {
                name = "read_file",
                description = "Read the contents of a file from the Wiley Widget workspace. Returns the full text content of the specified file. Use this to examine source code, configuration files, or documentation.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        file_path = new
                        {
                            type = "string",
                            description = "The absolute or relative path to the file to read (e.g., 'src/MainForm.cs' or 'appsettings.json')"
                        },
                        start_line = new
                        {
                            type = "integer",
                            description = "Optional: Starting line number (1-indexed) to read from. Omit to read entire file."
                        },
                        end_line = new
                        {
                            type = "integer",
                            description = "Optional: Ending line number (1-indexed) to read to. Omit to read entire file."
                        }
                    },
                    required = new[] { "file_path" }
                }
            }
        });

        // Code search
        tools.Add(new
        {
            type = "function",
            function = new
            {
                name = "semantic_search",
                description = "Perform a semantic search across the Wiley Widget codebase to find relevant code snippets, classes, methods, comments, or documentation. Uses AI-powered semantic understanding to find contextually relevant results.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "The search query - can be natural language (e.g., 'authentication logic') or specific code terms (e.g., 'MainForm initialization')"
                        },
                        max_results = new
                        {
                            type = "integer",
                            description = "Maximum number of results to return (default: 10, max: 50)"
                        }
                    },
                    required = new[] { "query" }
                }
            }
        });

        // Pattern search
        tools.Add(new
        {
            type = "function",
            function = new
                {  name = "grep_search",
                description = "Search for a specific pattern or text string in files across the workspace. Returns matching lines with file paths and line numbers. Supports both literal text and regular expressions.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        pattern = new
                        {
                            type = "string",
                            description = "The text pattern or regex to search for (e.g., 'IAIService' or 'async.*Task')"
                        },
                        is_regex = new
                        {
                            type = "boolean",
                            description = "Whether the pattern is a regular expression (default: false)"
                        },
                        include_pattern = new
                        {
                            type = "string",
                            description = "Optional: Glob pattern to filter files (e.g., '**/*.cs' for C# files only)"
                        }
                    },
                    required = new[] { "pattern" }
                }
            }
        });

        // Directory listing
        tools.Add(new
        {
            type = "function",
            function = new
            {
                name = "list_directory",
                description = "List files and subdirectories in a specified directory path within the workspace. Returns names and types (file/directory).",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        directory_path = new
                        {
                            type = "string",
                            description = "The path to the directory to list (e.g., 'src' or 'src/WileyWidget.WinForms')"
                        },
                        recursive = new
                        {
                            type = "boolean",
                            description = "Whether to list subdirectories recursively (default: false)"
                        }
                    },
                    required = new[] { "directory_path" }
                }
            }
        });

        // Error diagnostics
        tools.Add(new
        {
            type = "function",
            function = new
            {
                name = "get_errors",
                description = "Get compilation errors, warnings, and lint issues from the workspace. Returns diagnostic information including file paths, line numbers, and error messages.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        file_path = new
                        {
                            type = "string",
                            description = "Optional: Specific file path to get errors for. Omit to get all workspace errors."
                        },
                        severity = new
                        {
                            type = "string",
                            description = "Optional: Filter by severity - 'error', 'warning', or 'info' (default: all)",
                            @enum = new[] { "error", "warning", "info", "all" }
                        }
                    },
                    required = new string[] { }
                }
            }
        });

        // Get recent file changes
        tools.Add(new
        {
            type = "function",
            function = new
            {
                name = "get_git_changes",
                description = "Get recent file changes from Git repository. Shows modified, added, and deleted files with their change status.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        include_staged = new
                        {
                            type = "boolean",
                            description = "Include staged changes (default: true)"
                        },
                        include_unstaged = new
                        {
                            type = "boolean",
                            description = "Include unstaged changes (default: true)"
                        }
                    },
                    required = new string[] { }
                }
            }
        });

        // WILEY WIDGET BUSINESS LOGIC TOOLS: Query application data and operations
        // These provide Grok with "vision" into the application state

        // Get enterprise details
        tools.Add(new
        {
            type = "function",
            function = new
            {
                name = "get_enterprise_details",
                description = "Get detailed information about a specific enterprise including financial data, budget allocations, and cash flow status. Use this when user asks about a specific company, organization, or enterprise.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        enterprise_id = new
                        {
                            type = "integer",
                            description = "Optional: Numeric enterprise ID. Use if known."
                        },
                        enterprise_name = new
                        {
                            type = "string",
                            description = "Optional: Enterprise name for lookup. Supports partial matches."
                        }
                    },
                    required = new string[] { }
                }
            }
        });

        // Run budget analysis
        tools.Add(new
        {
            type = "function",
            function = new
            {
                name = "run_budget_analysis",
                description = "Run a comprehensive budget analysis for a fiscal year. Returns budget vs actual spending, variances, trends, and fiscal health indicators. Use this when user asks about budget performance, spending analysis, or fiscal year review.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        fiscal_year = new
                        {
                            type = "integer",
                            description = "Fiscal year to analyze (e.g., 2024). Defaults to current fiscal year if omitted."
                        },
                        department = new
                        {
                            type = "string",
                            description = "Optional: Filter analysis to specific department (e.g., 'Operations', 'Finance')"
                        }
                    },
                    required = new string[] { }
                }
            }
        });

        // Search audit trail
        tools.Add(new
        {
            type = "function",
            function = new
            {
                name = "search_audit_trail",
                description = "Search the audit log for recent system activities, user actions, data changes, and system events. Returns timestamped audit entries with details. Use this when user asks 'what changed?', 'who did X?', or 'recent activity'.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        entity_type = new
                        {
                            type = "string",
                            description = "Optional: Filter by entity type - 'Budget', 'Enterprise', 'User', 'Account', etc."
                        },
                        action = new
                        {
                            type = "string",
                            description = "Optional: Filter by action - 'Create', 'Update', 'Delete', 'View', 'Export', etc."
                        },
                        start_date = new
                        {
                            type = "string",
                            description = "Optional: Start date for audit search (ISO 8601 format: YYYY-MM-DD)"
                        },
                        end_date = new
                        {
                            type = "string",
                            description = "Optional: End date for audit search (ISO 8601 format: YYYY-MM-DD)"
                        },
                        max_results = new
                        {
                            type = "integer",
                            description = "Maximum number of results to return (default: 20, max: 50)"
                        }
                    },
                    required = new string[] { }
                }
            }
        });

        // List enterprises
        tools.Add(new
        {
            type = "function",
            function = new
            {
                name = "list_enterprises",
                description = "List all enterprises in the system with basic information and financial health indicators. Returns enterprise names, IDs, and cash flow status (positive/negative). Use this when user asks 'what enterprises do we have?', 'list all companies', or 'show organizations'.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        include_inactive = new
                        {
                            type = "boolean",
                            description = "Include inactive/archived enterprises (default: false)"
                        }
                    },
                    required = new string[] { }
                }
            }
        });

        // Get current UI state
        tools.Add(new
        {
            type = "function",
            function = new
            {
                name = "get_current_ui_state",
                description = "Get the current operational state of the Wiley Widget application including active views, selected data, user context, and open forms. Returns real-time information about what the user is currently viewing. Use this when providing contextual help or when user asks 'what am I looking at?'",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        include_selections = new
                        {
                            type = "boolean",
                            description = "Include currently selected items/records (default: true)"
                        }
                    },
                    required = new string[] { }
                }
            }
        });

        _logger.LogInformation("✓ Generated {Count} client-side tool definitions for xAI integration", tools.Count);
        return tools;
    }

    /// <summary>
    /// Enhanced GetInsightsAsync with xAI agentic tool calling support.
    /// Implements the official xAI tool calling pattern with automatic server-side execution
    /// and client-side tool detection.
    /// Reference: https://docs.x.ai/docs/guides/tools/overview
    /// </summary>
    /// <param name="context">Context for the AI query</param>
    /// <param name="question">User question</param>
    /// <param name="tools">Optional: Client-side tool definitions. If null, uses GetToolDefinitions()</param>
    /// <param name="includeServerSideTools">Include xAI server-side tools (web_search, x_search, code_execution)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI response with tool call results integrated</returns>
    public async Task<string> GetInsightsWithToolsAsync(
        string context,
        string question,
        List<object>? tools = null,
        bool includeServerSideTools = true,
        CancellationToken cancellationToken = default)
    {
        var toolsEnabled = _configuration.GetValue<bool>("FeatureFlags:EnableXAIToolCalling", true);
        if (!toolsEnabled)
        {
            _logger.LogDebug("xAI tool calling disabled - falling back to standard GetInsightsAsync");
            return await GetInsightsAsync(context, question, cancellationToken);
        }

        // Start telemetry tracking
        using var apiCallSpan = _telemetryService?.StartActivity("ai.xai.get_insights_with_tools",
            ("ai.model", _configuration["XAI:Model"] ?? "grok-4-1-fast"),
            ("ai.provider", "xAI"),
            ("ai.tool_calling", "enabled"));

        // Validate inputs
        ValidateAndSanitizeInputs(ref context, ref question);

        // Get tool definitions if not provided
        tools ??= GetToolDefinitions();

        // Add server-side tools if requested
        var allTools = new List<object>(tools);
        if (includeServerSideTools)
        {
            // Add xAI server-side tools following official API specification
            allTools.Add(new { type = "web_search" });  // Real-time web search
            allTools.Add(new { type = "x_search" });    // X (Twitter) search
            // Note: code_execution available but not auto-enabled for security
            _logger.LogDebug("Added xAI server-side tools: web_search, x_search");
        }

        var semaphoreEntered = false;
        try
        {
            var acquired = await AcquireSemaphoreSafeAsync(_concurrencySemaphore, cancellationToken).ConfigureAwait(false);
            if (!acquired)
            {
                _logger?.LogDebug("XAI tool calling request canceled before starting");
                return string.Empty;
            }
            semaphoreEntered = true;

            var model = _configuration["XAI:Model"] ?? "grok-4-1-fast";  // Recommended model for agentic tool calling
            var systemContext = await _contextService.BuildCurrentSystemContextAsync(cancellationToken);

            // Build agentic tool calling request following xAI specification
            var request = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = $"You are an AI assistant for Wiley Widget, a municipal utility management application. System Context: {systemContext}. Context: {context}"
                    },
                    new
                    {
                        role = "user",
                        content = question
                    }
                },
                model = model,
                tools = allTools.ToArray(),
                stream = false,  // Non-streaming for initial implementation (streaming recommended for production)
                temperature = 0.7
            };

            _logger.LogInformation("Executing xAI agentic tool calling request with {ToolCount} tools", allTools.Count);
            _aiLoggingService.LogQuery(question, $"{context} | {systemContext} | Tools: {allTools.Count}", model);

            // Execute HTTP request with Polly resilience pipeline
            var response = await _httpPipeline.ExecuteAsync(
                async context => await _httpClient.PostAsJsonAsync("chat/completions", request, context.CancellationToken),
                ResilienceContextPool.Shared.Get(cancellationToken));

            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                Log.Error("xAI tool calling API returned non-success status {Status}: {Body}", status, body);
                _aiLoggingService.LogError(question, body ?? string.Empty, response.StatusCode.ToString());

                return response.StatusCode switch
                {
                    HttpStatusCode.Forbidden => "AI service authentication failed. Please verify XAI_API_KEY.",
                    HttpStatusCode.TooManyRequests => "AI service rate limit exceeded. Try again shortly.",
                    HttpStatusCode.BadRequest when body?.Contains("tools") == true => "Tool calling not supported by selected model. Use grok-4-1-fast or grok-4-fast.",
                    _ => $"AI service error ({status}). Please try again later."
                };
            }

            var result = await response.Content.ReadFromJsonAsync<XAIToolCallingResponse>(cancellationToken: cancellationToken);

            if (result?.error != null)
            {
                Log.Error("xAI tool calling API error: {ErrorType} - {ErrorMessage}", result.error.type, result.error.message);
                _aiLoggingService.LogError(question, result.error.message, result.error.type ?? "API Error");
                return $"AI error: {result.error.message}";
            }

            if (result?.choices == null || result.choices.Length == 0)
            {
                Log.Warning("xAI tool calling API returned no choices");
                return "No response from AI service";
            }

            var choice = result.choices[0];
            var content = choice?.message?.content ?? "[No content]";

            // Log tool call metrics following xAI response structure
            if (result.usage?.server_side_tool_usage != null)
            {
                var toolUsage = JsonSerializer.Serialize(result.usage.server_side_tool_usage);
                _logger.LogInformation("xAI server-side tool usage: {ToolUsage}", toolUsage);
            }

            // Check for client-side tool calls that need execution
            if (choice?.message?.tool_calls != null && choice.message.tool_calls.Length > 0)
            {
                var clientToolCalls = choice.message.tool_calls.Where(tc => IsClientSideToolCall(tc)).ToArray();

                if (clientToolCalls.Length > 0 && _assistantService != null)
                {
                    _logger.LogInformation("🔧 Executing {Count} client-side tool calls", clientToolCalls.Length);

                    // Tool execution loop: Execute client tools and send results back to xAI
                    // Max 5 rounds to prevent infinite loops
                    const int maxToolRounds = 5;
                    var currentMessages = new List<object>
                    {
                        new
                        {
                            role = "system",
                            content = $"You are an AI assistant for Wiley Widget, a municipal utility management application. System Context: {systemContext}. Context: {context}"
                        },
                        new
                        {
                            role = "user",
                            content = question
                        },
                        new
                        {
                            role = "assistant",
                            content = content,
                            tool_calls = choice.message.tool_calls.Select(tc => new
                            {
                                id = tc.id,
                                type = tc.type,
                                function = new
                                {
                                    name = tc.function?.name,
                                    arguments = tc.function?.arguments
                                }
                            }).ToArray()
                        }
                    };

                    for (int round = 0; round < maxToolRounds; round++)
                    {
                        _logger.LogDebug("Tool execution round {Round}/{Max}", round + 1, maxToolRounds);

                        // Execute all client tool calls in parallel
                        var toolExecutionTasks = clientToolCalls.Select(async tc =>
                        {
                            try
                            {
                                // Parse tool arguments from JSON string to Dictionary
                                var args = new Dictionary<string, object>();
                                if (!string.IsNullOrWhiteSpace(tc.function?.arguments))
                                {
                                    try
                                    {
                                        var jsonDoc = JsonDocument.Parse(tc.function.arguments);
                                        args = JsonSerializer.Deserialize<Dictionary<string, object>>(tc.function.arguments) 
                                            ?? new Dictionary<string, object>();
                                    }
                                    catch (JsonException ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to parse tool arguments, using empty dictionary");
                                    }
                                }

                                var toolCall = new ToolCall(
                                    tc.id ?? Guid.NewGuid().ToString(),
                                    tc.function?.name ?? "unknown",
                                    args
                                );

                                _logger.LogDebug("Executing tool: {ToolName} (ID: {ToolId})", toolCall.Name, toolCall.Id);
                                var toolResult = await _assistantService.ExecuteToolAsync(toolCall, cancellationToken);

                                return new
                                {
                                    role = "tool",
                                    tool_call_id = toolCall.Id,
                                    content = !toolResult.IsError
                                        ? (toolResult.Content ?? "Tool executed successfully")
                                        : $"Error: {toolResult.ErrorMessage ?? "Tool execution failed"}"
                                };
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Tool execution failed: {ToolName}", tc.function?.name);
                                return new
                                {
                                    role = "tool",
                                    tool_call_id = tc.id ?? "unknown",
                                    content = $"Error executing tool: {ex.Message}"
                                };
                            }
                        });

                        var toolResults = await Task.WhenAll(toolExecutionTasks);

                        // Add tool results to conversation
                        foreach (var toolResult in toolResults)
                        {
                            currentMessages.Add(toolResult);
                        }

                        _logger.LogInformation("✓ Executed {Count} tools, sending results to xAI", toolResults.Length);

                        // Send continuation request to xAI with tool results
                        var continuationRequest = new
                        {
                            messages = currentMessages.ToArray(),
                            model = model,
                            tools = allTools.ToArray(),
                            stream = false,
                            temperature = 0.7
                        };

                        var continuationResponse = await _httpPipeline.ExecuteAsync(
                            async context => await _httpClient.PostAsJsonAsync("chat/completions", continuationRequest, context.CancellationToken),
                            ResilienceContextPool.Shared.Get(cancellationToken));

                        if (!continuationResponse.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Tool continuation request failed: {Status}", continuationResponse.StatusCode);
                            content += "\n\n[Tool execution completed but continuation request failed]";
                            break;
                        }

                        var continuationResult = await continuationResponse.Content.ReadFromJsonAsync<XAIToolCallingResponse>(cancellationToken: cancellationToken);

                        if (continuationResult?.choices == null || continuationResult.choices.Length == 0)
                        {
                            _logger.LogWarning("No continuation response from xAI");
                            break;
                        }

                        var continuationChoice = continuationResult.choices[0];
                        var continuationContent = continuationChoice?.message?.content ?? string.Empty;

                        // Check if there are more tool calls to execute
                        var moreToolCalls = continuationChoice?.message?.tool_calls?
                            .Where(tc => IsClientSideToolCall(tc))
                            .ToArray();

                        if (moreToolCalls == null || moreToolCalls.Length == 0)
                        {
                            // No more tool calls - we have the final answer
                            content = continuationContent;
                            _logger.LogInformation("✓ Tool calling complete after {Rounds} rounds", round + 1);

                            // Add final assistant message to history
                            currentMessages.Add(new
                            {
                                role = "assistant",
                                content = continuationContent
                            });
                            break;
                        }

                        // More tool calls needed - continue loop
                        _logger.LogInformation("🔄 {Count} more tool calls requested by xAI", moreToolCalls.Length);
                        clientToolCalls = moreToolCalls;

                        // Add assistant message with new tool calls
                        currentMessages.Add(new
                        {
                            role = "assistant",
                            content = continuationContent,
                            tool_calls = moreToolCalls.Select(tc => new
                            {
                                id = tc.id,
                                type = tc.type,
                                function = new
                                {
                                    name = tc.function?.name,
                                    arguments = tc.function?.arguments
                                }
                            }).ToArray()
                        });

                        // Safety check: prevent infinite loops
                        if (round >= maxToolRounds - 1)
                        {
                            _logger.LogWarning("⚠ Max tool execution rounds reached ({Max})", maxToolRounds);
                            content += "\n\n[Tool execution stopped: maximum rounds reached]";
                            break;
                        }
                    }
                }
                else if (clientToolCalls.Length > 0)
                {
                    _logger.LogWarning("⚠ Received {Count} client-side tool calls but IAIAssistantService not available", clientToolCalls.Length);
                    content += $"\n\n[Tool calls pending: {string.Join(", ", clientToolCalls.Select(tc => tc.function?.name))} - execution service not configured]";
                }
            }

            _aiLoggingService.LogResponse(question, content, 0, 0);
            // Telemetry: Track response length via logging
            _logger.LogDebug("xAI response length: {Length} characters", content.Length);

            // Cache the response
            var cacheKey = $"XAI_TOOLS:{context.GetHashCode(StringComparison.OrdinalIgnoreCase)}:{question.GetHashCode(StringComparison.OrdinalIgnoreCase)}";
            _memoryCache.Set(cacheKey, content, TimeSpan.FromMinutes(10));

            return content;
        }
        catch (HttpRequestException hrEx)
        {
            Log.Error(hrEx, "HTTP error during xAI tool calling request");
            _aiLoggingService.LogError(question, hrEx.Message, "HttpRequestException");
            return $"Network error: {hrEx.Message}. Please check your connection.";
        }
        catch (TaskCanceledException tcEx)
        {
            Log.Warning(tcEx, "xAI tool calling request timed out");
            _aiLoggingService.LogError(question, "Request timeout", "TaskCanceledException");
            return "Request timed out. Try a simpler query or check if xAI servers are accessible.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error during xAI tool calling");
            _telemetryService?.RecordException(ex, ("ai.operation", "get_insights_with_tools"));
            _aiLoggingService.LogError(question, ex.Message, ex.GetType().Name);
            return $"Unexpected error: {ex.Message}";
        }
        finally
        {
            if (semaphoreEntered)
            {
                _concurrencySemaphore?.Release();
            }
        }
    }

    /// <summary>
    /// Determine if a tool call is client-side (requires local execution) vs server-side (handled by xAI).
    /// Reference: https://docs.x.ai/docs/guides/tools/overview#server-side-tool-call-and-client-side-tool-call
    /// </summary>
    private static bool IsClientSideToolCall(XAIToolCall? toolCall)
    {
        if (toolCall?.function?.name == null) return false;

        // Server-side tools executed by xAI (no client action needed)
        var serverSideTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "web_search", "web_search_with_snippets", "browse_page",           // WEB_SEARCH category
            "x_user_search", "x_keyword_search", "x_semantic_search", "x_thread_fetch",  // X_SEARCH category
            "code_execution",                                                   // CODE_EXECUTION category
            "view_x_video", "view_image",                                     // Media understanding
            "collections_search"                                               // COLLECTIONS_SEARCH
        };

        return !serverSideTools.Contains(toolCall.function.name);
    }

    // Response models for xAI tool calling API
    private class XAIToolCallingResponse
    {
        public string? id { get; set; }
        public string? @object { get; set; }
        public long created { get; set; }
        public string? model { get; set; }
        public XAIChoice[]? choices { get; set; }
        public XAIUsage? usage { get; set; }
        public XAIResponse.XAIError? error { get; set; }  // Reference nested type from XAIResponse
    }

    private class XAIChoice
    {
        public int index { get; set; }
        public XAIMessage? message { get; set; }
        public string? finish_reason { get; set; }
    }

    private class XAIMessage
    {
        public string? role { get; set; }
        public string? content { get; set; }
        public XAIToolCall[]? tool_calls { get; set; }
    }

    private class XAIToolCall
    {
        public string? id { get; set; }
        public string? type { get; set; }
        public XAIFunction? function { get; set; }
    }

    private class XAIFunction
    {
        public string? name { get; set; }
        public string? arguments { get; set; }
    }

    private class XAIUsage
    {
        public int prompt_tokens { get; set; }
        public int completion_tokens { get; set; }
        public int total_tokens { get; set; }
        public int? reasoning_tokens { get; set; }
        public int? cached_prompt_text_tokens { get; set; }
        public Dictionary<string, int>? server_side_tool_usage { get; set; }
    }

    /// <summary>
    /// Stream AI responses with real-time tool call notifications.
    /// Implements xAI streaming API with Server-Sent Events (SSE) format.
    /// Reference: https://docs.x.ai/docs/guides/streaming
    /// </summary>
    public async IAsyncEnumerable<StreamChunk> StreamInsightsAsync(
        string context,
        string question,
        string? previousResponseId = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidateAndSanitizeInputs(ref context, ref question);

        var model = _configuration["XAI:Model"] ?? "grok-4-1-fast";
        var systemContext = await _contextService.BuildCurrentSystemContextAsync(cancellationToken);

        var request = new
        {
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = $"You are an AI assistant for Wiley Widget. System Context: {systemContext}. Context: {context}"
                },
                new
                {
                    role = "user",
                    content = question
                }
            },
            model = model,
            stream = true,  // Enable streaming
            tools = GetToolDefinitions().ToArray(),
            previous_response_id = previousResponseId
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(request)
        };

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        string? responseId = null;
        var contentBuilder = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", StringComparison.Ordinal)) continue;

            var jsonData = line.Substring(6); // Remove "data: " prefix
            if (jsonData == "[DONE]")
            {
                yield return new StreamChunk("done", ResponseId: responseId);
                break;
            }

            XAIStreamEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<XAIStreamEvent>(jsonData);
            }
            catch (JsonException)
            {
                continue; // Skip malformed chunks
            }

            if (evt?.id != null) responseId = evt.id;

            var delta = evt?.choices?.FirstOrDefault()?.delta;
            if (delta?.content != null)
            {
                contentBuilder.Append(delta.content);
                yield return new StreamChunk("content_delta", Content: delta.content, ResponseId: responseId);
            }

            if (delta?.tool_calls != null)
            {
                foreach (var tc in delta.tool_calls)
                {
                    if (tc.function?.name != null)
                    {
                        // Parse tool arguments from JSON string
                        var args = new Dictionary<string, object>();
                        if (!string.IsNullOrWhiteSpace(tc.function.arguments))
                        {
                            try
                            {
                                args = JsonSerializer.Deserialize<Dictionary<string, object>>(tc.function.arguments)
                                    ?? new Dictionary<string, object>();
                            }
                            catch (JsonException)
                            {
                                // Use empty dictionary if parsing fails
                            }
                        }

                        var toolCall = new ToolCall(
                            tc.id ?? Guid.NewGuid().ToString(),
                            tc.function.name,
                            args
                        );
                        yield return new StreamChunk("tool_call", ToolCall: toolCall, ResponseId: responseId);
                    }
                }
            }
        }

        _aiLoggingService.LogResponse(question, contentBuilder.ToString(), 0, 0);
    }

    /// <summary>
    /// Execute client-side tool call using IAIAssistantService integration.
    /// </summary>
    public async Task<ToolCallResult> ExecuteClientToolAsync(ToolCall toolCall, CancellationToken cancellationToken = default)
    {
        if (_assistantService == null)
        {
            _logger.LogWarning("IAIAssistantService not available - cannot execute client tool: {ToolName}", toolCall.Name);
            return new ToolCallResult(
                toolCall.Id,
                "Tool execution service not configured",
                IsError: true,
                ErrorMessage: "IAIAssistantService not configured"
            );
        }

        _logger.LogInformation("Executing client-side tool: {ToolName}", toolCall.Name);

        try
        {
            return await _assistantService.ExecuteToolAsync(toolCall, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing client tool: {ToolName}", toolCall.Name);
            return new ToolCallResult(
                toolCall.Id,
                string.Empty,
                IsError: true,
                ErrorMessage: $"Tool execution error: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Upload documents to xAI collections for RAG functionality.
    /// Reference: https://docs.x.ai/docs/guides/tools/advanced-usage#collections-search
    /// </summary>
    public async Task<CollectionUploadResult> UploadToCollectionAsync(
        string collectionName,
        IEnumerable<CollectionDocument> documents,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException("Collection name cannot be empty", nameof(collectionName));

        var docList = documents.ToList();
        if (docList.Count == 0)
            throw new ArgumentException("Must provide at least one document", nameof(documents));

        _logger.LogInformation("Uploading {Count} documents to collection: {CollectionName}", docList.Count, collectionName);

        try
        {
            var request = new
            {
                collection_name = collectionName,
                documents = docList.Select(d => new
                {
                    id = d.Id,
                    content = d.Content,
                    metadata = d.Metadata ?? new Dictionary<string, string>()
                }).ToArray()
            };

            var response = await _httpPipeline.ExecuteAsync(
                async context => await _httpClient.PostAsJsonAsync("collections/upload", request, context.CancellationToken),
                ResilienceContextPool.Shared.Get(cancellationToken));

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Collection upload failed: {Status} - {Error}", response.StatusCode, errorBody);
                return new CollectionUploadResult(
                    collectionName,
                    0,
                    "failed",
                    $"HTTP {(int)response.StatusCode}: {errorBody}"
                );
            }

            var result = await response.Content.ReadFromJsonAsync<CollectionUploadResponse>(cancellationToken: cancellationToken);
            _logger.LogInformation("✓ Uploaded {Count} documents to collection: {CollectionId}", docList.Count, result?.collection_id);

            return new CollectionUploadResult(
                result?.collection_id ?? collectionName,
                result?.uploaded_count ?? docList.Count,
                "success"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading to collection: {CollectionName}", collectionName);
            return new CollectionUploadResult(
                collectionName,
                0,
                "error",
                ex.Message
            );
        }
    }

    /// <summary>
    /// Search xAI collections using natural language query.
    /// </summary>
    public async Task<CollectionSearchResult> SearchCollectionAsync(
        string collectionName,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException("Collection name cannot be empty", nameof(collectionName));
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        _logger.LogInformation("Searching collection: {CollectionName} with query: {Query}", collectionName, query);

        try
        {
            var request = new
            {
                collection_name = collectionName,
                query = query,
                max_results = maxResults
            };

            var response = await _httpPipeline.ExecuteAsync(
                async context => await _httpClient.PostAsJsonAsync("collections/search", request, context.CancellationToken),
                ResilienceContextPool.Shared.Get(cancellationToken));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Collection search returned {Status}", response.StatusCode);
                return new CollectionSearchResult(query, Array.Empty<CollectionMatch>(), 0);
            }

            var result = await response.Content.ReadFromJsonAsync<CollectionSearchResponse>(cancellationToken: cancellationToken);
            var matches = result?.matches?.Select(m => new CollectionMatch(
                m.document_id ?? string.Empty,
                m.content ?? string.Empty,
                m.score,
                m.metadata
            )).ToArray() ?? Array.Empty<CollectionMatch>();

            _logger.LogInformation("✓ Found {Count} matches in collection: {CollectionName}", matches.Length, collectionName);
            return new CollectionSearchResult(query, matches, matches.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching collection: {CollectionName}", collectionName);
            return new CollectionSearchResult(query, Array.Empty<CollectionMatch>(), 0);
        }
    }

    // Response models for streaming and collections
    private class XAIStreamEvent
    {
        public string? id { get; set; }
        public XAIStreamChoice[]? choices { get; set; }
    }

    private class XAIStreamChoice
    {
        public XAIStreamDelta? delta { get; set; }
    }

    private class XAIStreamDelta
    {
        public string? content { get; set; }
        public XAIToolCall[]? tool_calls { get; set; }
    }

    private class CollectionUploadResponse
    {
        public string? collection_id { get; set; }
        public int uploaded_count { get; set; }
    }

    private class CollectionSearchResponse
    {
        public CollectionMatchResponse[]? matches { get; set; }
    }

    private class CollectionMatchResponse
    {
        public string? document_id { get; set; }
        public string? content { get; set; }
        public double score { get; set; }
        public Dictionary<string, string>? metadata { get; set; }
    }
}
