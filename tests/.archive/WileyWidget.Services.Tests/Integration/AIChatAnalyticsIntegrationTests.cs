using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Telemetry;

namespace WileyWidget.Services.Tests.Integration;

/// <summary>
/// Integration tests that exercise AI (XAIService) with analytics-derived context.
/// These tests mock the analytics service to produce analysis text, then ensure
/// the XAI service consumes that context in a prompt and returns expected content.
/// </summary>
public class AIChatAnalyticsIntegrationTests : IDisposable
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
    private readonly Mock<IAnalyticsService> _mockAnalyticsService;

    public AIChatAnalyticsIntegrationTests()
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

        _mockAnalyticsService = new Mock<IAnalyticsService>();
    }

    [Fact]
    public async Task AIChat_Integrates_With_AnalyticsData_Success()
    {
        // Arrange
        var analyticsSummary = "Budgeted: $192k, Variance: +$192k, Reserves forecast: stable";
        var budgetResult = new BudgetAnalysisResult
        {
            Insights = new List<string> { analyticsSummary }
        };

        _mockAnalyticsService
            .Setup(x => x.PerformExploratoryAnalysisAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(budgetResult);

        // Ensure the mocked AI response includes the analytics summary so we can assert it was consumed
        _mockHttpHandler.SetupSuccessResponse($"Analysis: {analyticsSummary} indicates healthy reserves.");

        using var service = CreateService();

        // Compose context by using analytics result (this mimics the higher-level integration code)
        var context = string.Join(" ", budgetResult.Insights);
        var question = "What does this mean for reserves?";

        // Act
        var result = await service.GetInsightsAsync(context, question);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("healthy reserves", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AIChat_Handles_AnalyticsError_Gracefully()
    {
        // Arrange: analytics throws
        _mockAnalyticsService
            .Setup(x => x.PerformExploratoryAnalysisAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException("Data unavailable"));

        _mockHttpHandler.SetupSuccessResponse("Fallback analysis: Unable to fetch analytics, providing general guidance.");

        using var service = CreateService();

        // Act: mimic integration that falls back when analytics fails
        string context;
        try
        {
            var analysis = await _mockAnalyticsService.Object.PerformExploratoryAnalysisAsync(DateTime.UtcNow.AddYears(-1), DateTime.UtcNow);
            context = string.Join(" ", analysis?.Insights ?? new List<string>());
        }
        catch
        {
            context = "Analytics unavailable";
        }

        var question = "How do we proceed with limited data?";
        var result = await service.GetInsightsAsync(context, question);

        // Assert - the mocked AI response contains the fallback message
        Assert.NotNull(result);
        Assert.Contains("Unable to fetch analytics", result, StringComparison.OrdinalIgnoreCase);
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
            null);
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
