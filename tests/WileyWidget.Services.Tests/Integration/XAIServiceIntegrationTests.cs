using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Telemetry;

namespace WileyWidget.Services.Tests.Integration;

/// <summary>
/// Integration tests for XAIService with mocked HTTP responses
/// Demonstrates testing AI service without calling real xAI API
/// </summary>
public class XAIServiceIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<XAIService>> _mockLogger;
    private readonly Mock<IWileyWidgetContextService> _mockContextService;
    private readonly Mock<IAILoggingService> _mockAILoggingService;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ITelemetryService> _mockTelemetryService;
    private readonly MockHttpMessageHandler _mockHttpHandler;
    private readonly HttpClient _mockHttpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public XAIServiceIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<XAIService>>();
        _mockContextService = new Mock<IWileyWidgetContextService>();
        _mockAILoggingService = new Mock<IAILoggingService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockTelemetryService = new Mock<ITelemetryService>();

        // Setup mock HTTP handler with default success response
        _mockHttpHandler = new MockHttpMessageHandler();
        _mockHttpHandler.SetupSuccessResponse();

        // Create mock HTTP client factory
        _mockHttpClient = new HttpClient(_mockHttpHandler) { BaseAddress = new Uri("https://api.x.ai/") };
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory
            .Setup(f => f.CreateClient("GrokClient"))
            .Returns(_mockHttpClient);
        _httpClientFactory = mockFactory.Object;

        // Setup configuration
        var configData = new Dictionary<string, string>
        {
            { "XAI:Enabled", "true" },
            { "XAI:ApiKey", "test-api-key-1234567890abcdef" },
            { "XAI:Endpoint", "https://api.x.ai/v1/chat/completions" },
            { "XAI:Model", "grok-beta" },
            { "XAI:Temperature", "0.3" },
            { "XAI:MaxTokens", "800" },
            { "XAI:TimeoutSeconds", "15" },
            { "XAI:MaxConcurrentRequests", "5" },
            { "XAI:CircuitBreakerBreakSeconds", "60" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();
    }

    [Fact]
    public async Task GetInsightsAsync_WithValidInput_ReturnsSuccessResponse()
    {
        // Arrange
        using var service = CreateService();
        var context = "Budget Analysis";
        var question = "What are our top spending categories?";

        _mockHttpHandler.SetupSuccessResponse("The top spending categories are: Salaries (45%), Operations (30%), Maintenance (15%).");

        // Act
        var result = await service.GetInsightsAsync(context, question);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Salaries", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Operations", result, StringComparison.OrdinalIgnoreCase);

        // Verify logging
        _mockAILoggingService.Verify(
            x => x.LogQuery(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
        _mockAILoggingService.Verify(
            x => x.LogResponse(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task GetInsightsAsync_WithAPIError_ReturnsErrorMessage()
    {
        // Arrange
        using var service = CreateService();
        var context = "Budget Analysis";
        var question = "Invalid question";

        _mockHttpHandler.SetupErrorResponse(HttpStatusCode.BadRequest, "Invalid request");

        // Act
        var result = await service.GetInsightsAsync(context, question);

        // Assert
        Assert.Contains("error", result, StringComparison.OrdinalIgnoreCase);

        // Verify error logging
        _mockAILoggingService.Verify(
            x => x.LogError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task GetInsightsAsync_WithTimeout_ReturnsTimeoutError()
    {
        // Arrange
        using var service = CreateService();
        var context = "Budget Analysis";
        var question = "Complex analysis";

        _mockHttpHandler.SetupTimeoutResponse();

        // Act
        var result = await service.GetInsightsAsync(context, question);

        // Assert
        Assert.Contains("timeout", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetInsightsAsync_WithCaching_ReturnsFromCache()
    {
        // Arrange
        using var service = CreateService();
        var context = "Budget Analysis";
        var question = "What is our budget?";

        _mockHttpHandler.SetupSuccessResponse("Budget is $1.5M");

        // Act - First call
        var result1 = await service.GetInsightsAsync(context, question);

        // Act - Second call (should hit cache)
        _mockHttpHandler.RequestCount = 0; // Reset counter
        var result2 = await service.GetInsightsAsync(context, question);

        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(0, _mockHttpHandler.RequestCount); // Second call didn't hit API
    }

    [Fact]
    public async Task BatchGetInsightsAsync_WithMultipleQueries_ReturnsAllResults()
    {
        // Arrange
        using var service = CreateService();
        var requests = new[]
        {
            ("Budget", "What is our total budget?"),
            ("Revenue", "What are our revenue sources?"),
            ("Expenses", "What are our top expenses?")
        };

        _mockHttpHandler.SetupSuccessResponse("Test response");

        // Act
        var results = await service.BatchGetInsightsAsync(requests);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results.Values, result => Assert.NotNull(result));

        // Verify requests were made (allowing for some caching/batching)
        Assert.True(_mockHttpHandler.RequestCount > 0);
    }

    [Fact]
    public async Task SendPromptAsync_WithCustomPrompt_ReturnsResponse()
    {
        // Arrange
        using var service = CreateService();
        var prompt = "Analyze the following data: [budget data]";

        _mockHttpHandler.SetupSuccessResponse("Analysis complete: Budget is balanced.");

        // Act
        var result = await service.SendPromptAsync(prompt);

        // Assert
        Assert.True(result.Content.Contains("balanced", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithValidKey_ReturnsTrue()
    {
        // Arrange
        using var service = CreateService();
        var apiKey = "xai-valid-key-123456789";

        _mockHttpHandler.SetupSuccessResponse("OK");

        // Act
        var result = await service.ValidateApiKeyAsync(apiKey);

        // Assert
        Assert.Equal(200, result.HttpStatusCode);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithInvalidKey_ReturnsFalse()
    {
        // Arrange
        using var service = CreateService();
        var apiKey = "invalid-key";

        _mockHttpHandler.SetupErrorResponse(HttpStatusCode.Unauthorized, "Invalid API key");

        // Act
        var result = await service.ValidateApiKeyAsync(apiKey);

        // Assert
        Assert.Equal(401, result.HttpStatusCode);
    }

    [Fact]
    public async Task GetInsightsAsync_WithTokenTracking_LogsTokenUsage()
    {
        // Arrange
        using var service = CreateService();
        var context = "Budget";
        var question = "What is our budget?";

        // Setup response with token usage
        _mockHttpHandler.SetupSuccessResponseWithTokens(
            "Budget is $1.5M",
            promptTokens: 50,
            completionTokens: 20);

        // Act
        var result = await service.GetInsightsAsync(context, question);

        // Assert
        Assert.NotNull(result);

        // Verify token usage was logged
        _mockAILoggingService.Verify(
            x => x.LogResponse(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                70), // Total tokens (50 + 20)
            Times.Once);
    }

    private XAIService CreateService()
    {
        return new XAIService(
            _httpClientFactory,
            _configuration,
            _mockLogger.Object,
            _mockContextService.Object,
            _mockAILoggingService.Object,
            _memoryCache,
            _mockTelemetryService.Object);
    }

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _memoryCache?.Dispose();
                _mockHttpHandler?.Dispose();
                _mockHttpClient?.Dispose();
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Mock HTTP message handler for testing XAI API calls without real network requests
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private HttpResponseMessage? _responseMessage;
    private Func<HttpRequestMessage, Task<HttpResponseMessage>>? _responseFactory;
    public int RequestCount { get; set; }

    /// <summary>
    /// Sets up a success response with default content
    /// </summary>
    public void SetupSuccessResponse(string content = "Test AI response")
    {
        var responseJson = $@"{{
            ""id"": ""test-123"",
            ""object"": ""chat.completion"",
            ""created"": {DateTimeOffset.UtcNow.ToUnixTimeSeconds()},
            ""model"": ""grok-beta"",
            ""choices"": [{{
                ""index"": 0,
                ""message"": {{
                    ""role"": ""assistant"",
                    ""content"": ""{content}""
                }},
                ""finish_reason"": ""stop""
            }}],
            ""usage"": {{
                ""prompt_tokens"": 50,
                ""completion_tokens"": 20,
                ""total_tokens"": 70
            }}
        }}";

        _responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };
    }

    /// <summary>
    /// Sets up a success response with specific token counts
    /// </summary>
    public void SetupSuccessResponseWithTokens(string content, int promptTokens, int completionTokens)
    {
        var responseJson = $@"{{
            ""id"": ""test-123"",
            ""object"": ""chat.completion"",
            ""created"": {DateTimeOffset.UtcNow.ToUnixTimeSeconds()},
            ""model"": ""grok-beta"",
            ""choices"": [{{
                ""index"": 0,
                ""message"": {{
                    ""role"": ""assistant"",
                    ""content"": ""{content}""
                }},
                ""finish_reason"": ""stop""
            }}],
            ""usage"": {{
                ""prompt_tokens"": {promptTokens},
                ""completion_tokens"": {completionTokens},
                ""total_tokens"": {promptTokens + completionTokens}
            }}
        }}";

        _responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };
    }

    /// <summary>
    /// Sets up an error response
    /// </summary>
    public void SetupErrorResponse(HttpStatusCode statusCode, string errorMessage)
    {
        var errorJson = $@"{{
            ""error"": {{
                ""message"": ""{errorMessage}"",
                ""type"": ""invalid_request_error"",
                ""code"": ""{statusCode}""
            }}
        }}";

        _responseMessage = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(errorJson, System.Text.Encoding.UTF8, "application/json")
        };
    }

    /// <summary>
    /// Sets up a timeout response
    /// </summary>
    public void SetupTimeoutResponse()
    {
        _responseFactory = async (request) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30)); // Simulate timeout
            throw new TaskCanceledException("Request timed out");
        };
    }

    /// <summary>
    /// Sets up a custom response factory for advanced scenarios
    /// </summary>
    public void SetupCustomResponse(Func<HttpRequestMessage, Task<HttpResponseMessage>> factory)
    {
        _responseFactory = factory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;

        if (_responseFactory != null)
        {
            return await _responseFactory(request);
        }

        if (_responseMessage != null)
        {
            // Clone the response for each request
            var response = new HttpResponseMessage(_responseMessage.StatusCode);
            if (_responseMessage.Content != null)
            {
                var content = await _responseMessage.Content.ReadAsStringAsync();
                response.Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json");
            }
            return response;
        }

        throw new InvalidOperationException("No response configured for mock HTTP handler");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _responseMessage?.Dispose();
        }
        base.Dispose(disposing);
    }
}
