using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using WileyWidget.Services;
using Polly;
using Polly.Retry;
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
    // private readonly dynamic _telemetryClient; // Commented out until Azure is configured
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
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
        IMemoryCache memoryCache
        // TelemetryClient telemetryClient = null // Commented out until Azure is configured
        )
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextService = contextService ?? throw new ArgumentNullException(nameof(contextService));
        _aiLoggingService = aiLoggingService ?? throw new ArgumentNullException(nameof(aiLoggingService));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        // _telemetryClient = telemetryClient; // Commented out until Azure is configured

        _apiKey = configuration["XAI:ApiKey"] ?? throw new ArgumentNullException("XAI:ApiKey", "XAI API key not configured");

        var baseUrl = configuration["XAI:BaseUrl"] ?? "https://api.x.ai/v1/";
        var timeoutSeconds = double.Parse(configuration["XAI:TimeoutSeconds"] ?? "15");

        // Validate API key format (basic check) - wrapped to handle exceptions gracefully
        try
        {
            if (_apiKey.Length < 20)
            {
                throw new ArgumentException("API key appears to be invalid (too short)", "XAI:ApiKey");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "XAI API key validation failed during construction");
            throw; // Re-throw to prevent invalid service creation
        }

        // Initialize concurrency control (limit to 5 concurrent requests to avoid throttling)
        var maxConcurrentRequests = int.Parse(configuration["XAI:MaxConcurrentRequests"] ?? "5");
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);

        _httpClient = httpClientFactory.CreateClient("AIServices");
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        // Set default headers only if not already set by the named client
        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        // Create Polly retry policy with exponential backoff
        _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .OrResult(response => response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            .OrResult(response => response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
            .OrResult(response => response.StatusCode == System.Net.HttpStatusCode.BadGateway)
            .OrResult(response => response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            .OrResult(response => response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: (attemptCount, context) =>
                    TimeSpan.FromMilliseconds(Math.Pow(2, attemptCount) * 500), // Exponential backoff: 500ms, 1s, 2s
                onRetryAsync: async (outcome, timespan, attemptNumber, context) =>
                {
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "Unknown";
                    Log.Warning("xAI API request failed (attempt {Attempt}/{MaxAttempts}). Status: {StatusCode}. Retrying in {DelayMs}ms",
                        attemptNumber, 3, statusCode, timespan.TotalMilliseconds);
                    await Task.CompletedTask;

                    // Track retry telemetry - commented out until Azure is configured
                    // _telemetryClient?.TrackEvent("XAIServiceRetry", new Dictionary<string, string>
                    // {
                    //     ["Attempt"] = attemptNumber.ToString(),
                    //     ["StatusCode"] = statusCode,
                    //     ["DelayMs"] = timespan.TotalMilliseconds.ToString()
                    // });
                });
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
            .Replace("\\", "\\\\")  // Escape backslashes first
            .Replace("\"", "\\\"")  // Escape quotes
            .Replace("\n", " ")     // Replace newlines with spaces
            .Replace("\r", " ")     // Replace carriage returns with spaces
            .Replace("\t", " ")     // Replace tabs with spaces
            .Replace("\0", "")      // Remove null characters
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
        // Validate and sanitize inputs to prevent injection attacks
        ValidateAndSanitizeInputs(ref context, ref question);

        // Create cache key from sanitized inputs
        var cacheKey = $"XAI:{context.GetHashCode()}:{question.GetHashCode()}";

        // Check cache first
        if (_memoryCache.TryGetValue(cacheKey, out string cachedResponse))
        {
            Log.Information("Cache hit for XAI query: {Question}", question);
            _aiLoggingService.LogQuery(question, context, _configuration["XAI:Model"] ?? "grok-4-0709");
            return cachedResponse;
        }

        // Acquire concurrency semaphore to limit concurrent requests
        await _concurrencySemaphore.WaitAsync(cancellationToken);

        try
        {
            var startTime = DateTime.UtcNow;
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

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken));

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
            // Always release the concurrency semaphore
            _concurrencySemaphore.Release();
        }
    }

    /// <summary>
    /// Get AI insights for multiple context and question pairs (batched for efficiency)
    /// </summary>
    public async Task<Dictionary<string, string>> BatchGetInsightsAsync(
        IEnumerable<(string context, string question)> requests,
        CancellationToken cancellationToken = default)
    {
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

            var cacheKey = $"XAI:{sanitizedContext.GetHashCode()}:{sanitizedQuestion.GetHashCode()}";

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

        var response = await _retryPolicy.ExecuteAsync(() =>
            _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken));

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

        var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken));

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

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.SendAsync(message, cancellationToken));

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
}
