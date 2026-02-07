using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Tests.Unit.Services.AI
{
    /// <summary>
    /// Unit tests for GrokAgentService - validates API key loading, HTTP header configuration, and authentication responses.
    /// Tests ensure that:
    /// 1. API keys are correctly loaded from configuration/environment variables
    /// 2. API keys are properly set in HttpClient Authorization headers
    /// 3. HTTP requests to xAI API succeed with valid credentials (no 401 Unauthorized responses)
    /// 4. Response status codes are correctly logged
    /// </summary>
    public class GrokAgentServiceTests : IDisposable
    {
        private const string TestApiKey = "test-xai-key-1234567890abcdefghij";
        private readonly Mock<ILogger<GrokAgentService>> _mockLogger;
        private readonly Mock<IGrokApiKeyProvider> _mockApiKeyProvider;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly IConfiguration _config;
        private HttpClient? _httpClient;
        private bool _disposed;

        public GrokAgentServiceTests()
        {
            _mockLogger = new Mock<ILogger<GrokAgentService>>();
            _mockApiKeyProvider = new Mock<IGrokApiKeyProvider>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();

            // Setup default configuration
            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:Model"] = "grok-4.1",
                    ["XAI:Endpoint"] = "https://api.x.ai/v1",
                    ["Logging:LogLevel:Default"] = "Information"
                })
                .Build();

            // Setup default API key provider mock
            _mockApiKeyProvider
                .Setup(x => x.ApiKey)
                .Returns(TestApiKey);

            _mockApiKeyProvider
                .Setup(x => x.GetConfigurationSource())
                .Returns("environment variable");

            _mockApiKeyProvider
                .Setup(x => x.IsValidated)
                .Returns(true);

            _mockApiKeyProvider
                .Setup(x => x.ValidateAsync())
                .ReturnsAsync((true, "API key is valid"));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _httpClient?.Dispose();
                _httpClient = null;
            }

            _disposed = true;
        }

        /// <summary>
        /// Test: API key is correctly loaded from IGrokApiKeyProvider when machine-scope environment variable is set.
        /// Validates that the constructor properly retrieves and stores the API key.
        /// </summary>
        [Fact]
        public void Constructor_WithApiKeyInEnvironment_LoadsApiKeySuccessfully()
        {
            // Arrange - Setup mock HTTP client
            _httpClient = new HttpClient();
            _mockHttpClientFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            // Act
            var service = new GrokAgentService(
                _mockApiKeyProvider.Object,
                _config,
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            // Assert
            _mockApiKeyProvider.Verify(x => x.ApiKey, Times.AtLeastOnce);
            _mockApiKeyProvider.Verify(x => x.GetConfigurationSource(), Times.AtLeastOnce);
            _mockApiKeyProvider.Verify(x => x.IsValidated, Times.AtLeastOnce);
        }

        /// <summary>
        /// Test: API key is properly set in HttpClient Authorization header with Bearer scheme.
        /// Verifies that subsequent requests will include the API key for authentication.
        /// </summary>
        [Fact]
        public void Constructor_SetsAuthorizationHeaderWithApiKey()
        {
            // Arrange
            _httpClient = new HttpClient();
            _mockHttpClientFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            // Act
            var service = new GrokAgentService(
                _mockApiKeyProvider.Object,
                _config,
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            // Assert - Verify Authorization header is set with Bearer token
            Assert.NotNull(_httpClient.DefaultRequestHeaders.Authorization);
            Assert.Equal("Bearer", _httpClient.DefaultRequestHeaders.Authorization.Scheme);
            Assert.Equal(TestApiKey, _httpClient.DefaultRequestHeaders.Authorization.Parameter);
        }

        /// <summary>
        /// Test: When no API key is provided, Authorization header is not set.
        /// Ensures graceful degradation when API key is missing.
        /// </summary>
        [Fact]
        public void Constructor_WithoutApiKey_DoesNotSetAuthorizationHeader()
        {
            // Arrange
            _mockApiKeyProvider
                .Setup(x => x.ApiKey)
                .Returns((string?)null);

            _httpClient = new HttpClient();
            _mockHttpClientFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            // Act
            var service = new GrokAgentService(
                _mockApiKeyProvider.Object,
                _config,
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            // Assert - Authorization header should not be set
            Assert.Null(_httpClient.DefaultRequestHeaders.Authorization);
        }

        /// <summary>
        /// Test: Verifies that GetSimpleResponse handles 200 OK response correctly (no 401 error).
        /// Tests the happy path with valid credentials and successful API response.
        /// </summary>
        [Fact]
        public async Task GetSimpleResponse_WithValidApiKey_Returns200OkResponse()
        {
            // Arrange
            _httpClient = new HttpClient(new MockHttpMessageHandler(
                (req, ct) =>
                {
                    // Verify Authorization header is present
                    Assert.NotNull(req.Headers.Authorization);
                    Assert.Equal("Bearer", req.Headers.Authorization.Scheme);

                    // Return success response
                    var responseContent = new
                    {
                        id = "response-123",
                        output = new[]
                        {
                            new
                            {
                                content = new[]
                                {
                                    new { text = "Hello, World!" }
                                }
                            }
                        }
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(responseContent);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    });
                }));

            _mockHttpClientFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            var service = new GrokAgentService(
                _mockApiKeyProvider.Object,
                _config,
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            // Act
            var result = await service.GetSimpleResponse(
                "What is 2+2?",
                "You are a helpful assistant.",
                "grok-4.1");

            // Assert - Should not be a 401 or error response
            Assert.NotNull(result);
            Assert.DoesNotContain("401", result);
            Assert.DoesNotContain("Unauthorized", result);
            Assert.Contains("Hello", result);
        }

        /// <summary>
        /// Test: Verifies proper handling of 401 Unauthorized response when API key is invalid.
        /// Ensures the service detects when credentials are rejected.
        /// </summary>
        [Fact]
        public async Task GetSimpleResponse_With401Response_HandlesUnauthorizedError()
        {
            // Arrange
            _httpClient = new HttpClient(new MockHttpMessageHandler(
                (req, ct) =>
                {
                    // Return 401 Unauthorized
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        Content = new StringContent(
                            "{\"error\": \"Invalid API key\"}",
                            Encoding.UTF8,
                            "application/json")
                    });
                }));

            _mockHttpClientFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            var service = new GrokAgentService(
                _mockApiKeyProvider.Object,
                _config,
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            // Act
            var result = await service.GetSimpleResponse(
                "What is 2+2?",
                "You are a helpful assistant.",
                "grok-4.1");

            // Assert - Should contain error indication
            Assert.NotNull(result);
            Assert.Contains("401", result);
        }

        /// <summary>
        /// Test: Verifies ValidateApiKeyAsync succeeds with valid credentials (no 401).
        /// Tests that the API key provider can authenticate successfully against the API.
        /// </summary>
        [Fact]
        public async Task ValidateApiKeyAsync_WithValidApiKey_Returns200OkAndTrue()
        {
            // Arrange
            _httpClient = new HttpClient(new MockHttpMessageHandler(
                (req, ct) =>
                {
                    // Verify Authorization header is set
                    Assert.NotNull(req.Headers.Authorization);
                    Assert.Equal("Bearer", req.Headers.Authorization.Scheme);
                    Assert.Equal(TestApiKey, req.Headers.Authorization.Parameter);

                    // Return successful validation response
                    var responseContent = new
                    {
                        id = "response-123",
                        output = new[]
                        {
                            new
                            {
                                content = new[]
                                {
                                    new { text = "hi hello world" }
                                }
                            }
                        }
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(responseContent);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    });
                }));

            _mockHttpClientFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            var service = new GrokAgentService(
                _mockApiKeyProvider.Object,
                _config,
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            // Act
            var (success, message) = await service.ValidateApiKeyAsync();

            // Assert
            Assert.True(success);
            Assert.DoesNotContain("401", message);
            Assert.DoesNotContain("Unauthorized", message);
        }

        /// <summary>
        /// Test: Verifies ValidateApiKeyAsync handles 401 Unauthorized correctly.
        /// Tests that invalid API keys are properly detected.
        /// </summary>
        [Fact]
        public async Task ValidateApiKeyAsync_With401Unauthorized_ReturnsFalseWithError()
        {
            // Arrange
            _httpClient = new HttpClient(new MockHttpMessageHandler(
                (req, ct) =>
                {
                    // Return 401 Unauthorized
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        Content = new StringContent(
                            "{\"error\": \"Invalid authentication credentials\"}",
                            Encoding.UTF8,
                            "application/json")
                    });
                }));

            _mockHttpClientFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            var service = new GrokAgentService(
                _mockApiKeyProvider.Object,
                _config,
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            // Act
            var (success, message) = await service.ValidateApiKeyAsync();

            // Assert
            Assert.False(success);
            // Service returns "HTTP Unauthorized: {...error...}" for 401 responses
            Assert.Contains("Unauthorized", message);
        }

        /// <summary>
        /// Mock HTTP message handler for testing HTTP requests without hitting real endpoints.
        /// </summary>
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public MockHttpMessageHandler(
                Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return await _handler(request, cancellationToken);
            }
        }
    }
}
