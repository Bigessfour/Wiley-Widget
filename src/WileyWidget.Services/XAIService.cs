using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Telemetry;
using Polly.CircuitBreaker;
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
    private readonly IJARVISPersonalityService? _jarvisPersonality;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly ITelemetryService? _telemetryService;
    private readonly bool _enabled;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly double _temperature;
    private readonly int _maxTokens;
    // private readonly dynamic _telemetryClient; // Commented out until Azure is configured
    private bool _disposed;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public XAIService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<XAIService> logger,
        IWileyWidgetContextService contextService,
        IAILoggingService aiLoggingService,
        IMemoryCache memoryCache,
        ITelemetryService? telemetryService = null,
        IJARVISPersonalityService? jarvisPersonality = null
        // TelemetryClient telemetryClient = null // Commented out until Azure is configured
        )
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextService = contextService ?? throw new ArgumentNullException(nameof(contextService));
        _aiLoggingService = aiLoggingService ?? throw new ArgumentNullException(nameof(aiLoggingService));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _telemetryService = telemetryService;
        _jarvisPersonality = jarvisPersonality;
        // _telemetryClient = telemetryClient; // Commented out until Azure is configured

        _apiKey = configuration["XAI:ApiKey"];

        // DEBUG: Log what configuration sees
        Console.WriteLine($"[XAISERVICE DEBUG] XAI:ApiKey from config = {(_apiKey != null ? _apiKey.Substring(0, Math.Min(15, _apiKey.Length)) + "..." : "NULL")} ({_apiKey?.Length ?? 0} chars)");
        Console.WriteLine($"[XAISERVICE DEBUG] Config type = {configuration.GetType().FullName}");

        _enabled = bool.Parse(configuration["XAI:Enabled"] ?? "false");
        _endpoint = configuration["XAI:Endpoint"] ?? "https://api.x.ai/v1/responses";
        _model = configuration["XAI:Model"] ?? "grok-4";
        _temperature = double.Parse(configuration["XAI:Temperature"] ?? "0.3", CultureInfo.InvariantCulture);
        _maxTokens = int.Parse(configuration["XAI:MaxTokens"] ?? "800", CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(_apiKey) && _enabled)
        {
            throw new InvalidOperationException("XAI API key not configured but XAI is enabled");
        }

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
        if (_enabled)
        {
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
        }

        // Initialize concurrency control (limit to 5 concurrent requests to avoid throttling)
        var maxConcurrentRequests = int.Parse(configuration["XAI:MaxConcurrentRequests"] ?? "5", CultureInfo.InvariantCulture);
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);

        // Create or fall back to a default HttpClient if the factory returns null (tests may not set up a named client)
        var createdClient = httpClientFactory.CreateClient("GrokClient");
        _httpClient = createdClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        // Set default headers only if not already set by the named client
        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization") && _enabled)
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        _logger.LogInformation("✓ XAIService initialized with resilient HttpClient (timeout: {Timeout}s)",
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
            ("ai.model", _model),
            ("ai.provider", "xAI"));

        // Validate and sanitize inputs to prevent injection attacks
        // Do this before the try so ArgumentException for invalid inputs can propagate to callers/tests
        ValidateAndSanitizeInputs(ref context, ref question);

        if (!_enabled)
        {
            return "AI service is disabled.";
        }

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
                _aiLoggingService.LogQuery(question, context, _model);
                return cachedResponse;
            }

            apiCallSpan?.SetTag("ai.cache_hit", false);

            // Acquire concurrency semaphore to limit concurrent requests
            await _concurrencySemaphore.WaitAsync(cancellationToken);
            semaphoreEntered = true;

            var systemContext = await _contextService.BuildCurrentSystemContextAsync(cancellationToken);

            // Log the query
            _aiLoggingService.LogQuery(question, $"{context} | {systemContext}", _model);

            // Get JARVIS system prompt and append context
            var baseSystemPrompt = _jarvisPersonality?.GetSystemPrompt() 
                ?? "You are a helpful AI assistant for a municipal utility management application called Wiley Widget.";
            var finalSystemPrompt = $"{baseSystemPrompt} System Context: {systemContext}. Context: {context}";

            var request = new
            {
                input = new[]
                {
                    new
                    {
                        role = "system",
                        content = finalSystemPrompt
                    },
                    new
                    {
                        role = "user",
                        content = question
                    }
                },
                model = _model,
                stream = false,
                temperature = _temperature,
                max_tokens = _maxTokens,
                store = true
            };

            // Execute HTTP request with resilient HttpClient
            var response = await _httpClient.PostAsJsonAsync(_endpoint, request, cancellationToken);

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

            if (result?.output?.Length > 0 && result.output[0]?.content?.Length > 0)
            {
                var content = result.output[0].content[0]?.text;
                if (!string.IsNullOrEmpty(content))
                {
                    var totalTokens = result.usage?.TotalTokens ?? 0;
                    var responseTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                    _aiLoggingService.LogResponse(question, content, responseTimeMs, totalTokens);

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

            return $"The request timed out (timeout) after {_httpClient.Timeout.TotalSeconds} seconds. The xAI service may be experiencing high load. Please try again later.";
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
        if (!_enabled)
        {
            return "AI service is disabled.";
        }

        var startTime = DateTime.UtcNow;

        var systemContext = await _contextService.BuildCurrentSystemContextAsync(cancellationToken);

        // Log the query
        _aiLoggingService.LogQuery(question, $"{context} | {systemContext}", _model);

        var request = new
        {
            input = new[]
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
            model = _model,
            stream = false,
            temperature = _temperature,
            max_tokens = _maxTokens,
            store = true
        };

        // Execute HTTP request with resilient HttpClient
        var response = await _httpClient.PostAsJsonAsync(_endpoint, request, cancellationToken);

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

        if (result?.output?.Length > 0 && result.output[0]?.content?.Length > 0)
        {
            var content = result.output[0].content[0]?.text;
            if (!string.IsNullOrEmpty(content))
            {
                var totalTokens = result.usage?.TotalTokens ?? 0;
                var responseTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                _aiLoggingService.LogResponse(question, content, responseTimeMs, totalTokens);

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

        if (!_enabled)
        {
            return new AIResponseResult("AI service is disabled.", 503, "Disabled", "AI service is disabled.");
        }

        var startTime = DateTime.UtcNow;

        var systemContext = await _contextService.BuildCurrentSystemContextAsync(cancellationToken);
        _aiLoggingService.LogQuery(question, $"{context} | {systemContext}", _model);

        var request = new
        {
            messages = new[]
            {
                new { role = "system", content = $"You are a helpful AI assistant for Wiley Widget. System Context: {systemContext}. Context: {context}" },
                new { role = "user", content = question }
            },
            model = _model,
            stream = false,
            temperature = _temperature,
            max_tokens = _maxTokens
        };

        // Execute HTTP request with resilient HttpClient
        var response = await _httpClient.PostAsJsonAsync(_endpoint, request, cancellationToken);

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

        var content = xaiResponse?.output?.Length > 0 && xaiResponse.output[0]?.content?.Length > 0 ? xaiResponse.output[0].content[0]?.text : null;

        if (!string.IsNullOrEmpty(content))
        {
            var totalTokens = xaiResponse?.usage?.TotalTokens ?? 0;
            var responseTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _aiLoggingService.LogResponse(question, content, responseTimeMs, totalTokens);
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

        if (!_enabled)
        {
            return new AIResponseResult("AI service is disabled.", 503, "Disabled", "AI service is disabled.");
        }

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
                model = _model,
                stream = false,
                temperature = 0.0,
                max_tokens = _maxTokens
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = JsonContent.Create(request, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            };

            // Add the provided key on this request without altering the client default headers
            message.Headers.Remove("Authorization");
            message.Headers.Add("Authorization", $"Bearer {apiKey}");

            // Execute HTTP request with resilient HttpClient
            var response = await _httpClient.SendAsync(message, cancellationToken);

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

            var content = xaiResponse?.output?.Length > 0 && xaiResponse.output[0]?.content?.Length > 0 ? xaiResponse.output[0].content[0]?.text : null;
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
        public OutputItem[] output { get; set; }
        public XAIError error { get; set; }

        public class OutputItem
        {
            public ContentItem[] content { get; set; }
            public string role { get; set; }
        }

        public class ContentItem
        {
            public string type { get; set; }
            public string text { get; set; }
        }

        public class XAIError
        {
            public string message { get; set; }
            public string type { get; set; }
            public string code { get; set; }
        }

        public Usage usage { get; set; }

        public class Usage
        {
            [JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }

            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }
        }
    }

    /// <summary>
    /// xAI API stream chunk model
    /// </summary>
    private class XAIStreamChunk
    {
        public Choice[] choices { get; set; }

        public class Choice
        {
            public Delta delta { get; set; }
            public string finish_reason { get; set; }
        }

        public class Delta
        {
            public string content { get; set; }
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
    public Task UpdateApiKeyAsync(string newApiKey, CancellationToken cancellationToken = default)
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

        if (!_enabled)
        {
            return new AIResponseResult("AI service is disabled.", 503, "Disabled", "AI service is disabled.");
        }

        try
        {
            _logger.LogInformation("Sending prompt to xAI service, length: {Length}", prompt.Length);

            var request = new
            {
                input = new[]
                {
                    new { role = "user", content = prompt }
                },
                model = _model,
                stream = false,
                temperature = _temperature,
                max_tokens = _maxTokens,
                store = true
            };

            var response = await _httpClient.PostAsJsonAsync(_endpoint, request, cancellationToken: cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<XAIResponse>();
                var message = result?.output?.Length > 0 && result.output[0]?.content?.Length > 0 ? result.output[0].content[0]?.text : "No response content";

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

    public async System.Collections.Generic.IAsyncEnumerable<string> StreamResponseAsync(string prompt, string? systemMessage = null, [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            yield break;

        if (!_enabled)
        {
            yield return "AI service is disabled.";
            yield break;
        }

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        
        var messages = new System.Collections.Generic.List<object>();
        if (!string.IsNullOrEmpty(systemMessage))
        {
            messages.Add(new { role = "system", content = systemMessage });
        }
        messages.Add(new { role = "user", content = prompt });

        var requestBody = new
        {
            messages = messages.ToArray(),
            model = _model,
            stream = true,
            temperature = _temperature,
            max_tokens = _maxTokens
        };

        requestMessage.Content = JsonContent.Create(requestBody);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            yield return $"Error: {response.StatusCode}";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("data: "))
            {
                var data = line.Substring(6).Trim();
                if (data == "[DONE]") break;

                XAIStreamChunk? chunk = null;
                try
                {
                    chunk = JsonSerializer.Deserialize<XAIStreamChunk>(data);
                }
                catch { /* Skip malformed chunks */ }

                var content = chunk?.choices?.FirstOrDefault()?.delta?.content;
                if (!string.IsNullOrEmpty(content))
                {
                    yield return content;
                }
            }
        }
    }

    /// <summary>
    /// Send a message to the AI service with conversation history context.
    /// </summary>
    public async Task<string> SendMessageAsync(string message, object conversationHistory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        // For now, delegate to GetInsightsAsync with simple context
        // In the future, this could be extended to use the conversationHistory for multi-turn conversations
        var context = "Conversational assistant";
        return await GetInsightsAsync(context, message, CancellationToken.None);
    }
}
