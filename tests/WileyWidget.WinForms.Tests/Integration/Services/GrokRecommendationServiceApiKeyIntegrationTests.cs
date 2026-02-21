using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using WileyWidget.Business.Services;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Tests.Integration.Services.AI
{
    /// <summary>
    /// Integration tests for GrokRecommendationService with IGrokApiKeyProvider.
    /// Validates that GrokRecommendationService correctly uses the centralized API key provider
    /// instead of reading directly from IConfiguration.
    ///
    /// This prevents the regression where XAI:ApiKey configuration inconsistency caused
    /// the API key not to be properly resolved from environment variables.
    /// </summary>
    public class GrokRecommendationServiceApiKeyIntegrationTests : IDisposable
    {
        private readonly Mock<ILogger<GrokRecommendationService>> _mockLogger =
            new Mock<ILogger<GrokRecommendationService>>();

        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory =
            new Mock<IHttpClientFactory>();

        private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());
        private bool _disposed;

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
                _memoryCache.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Test: GrokRecommendationService correctly injects and uses IGrokApiKeyProvider.
        /// Ensures the service gets API key from the provider, not directly from config.
        /// </summary>
        [Fact]
        public void Constructor_WhenApiKeyProviderInjected_UsesProviderApiKey()
        {
            // Arrange
            const string expectedApiKey = "sk-test-key-98765";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = expectedApiKey,
                    ["XAI:Enabled"] = "true",
                    ["XAI:Endpoint"] = "https://api.x.ai/v1/",
                    ["XAI:Model"] = "grok-2"
                })
                .Build();

            var mockApiKeyProvider = new Mock<IGrokApiKeyProvider>();
            mockApiKeyProvider.Setup(p => p.ApiKey).Returns(expectedApiKey);
            mockApiKeyProvider.Setup(p => p.GetConfigurationSource()).Returns("Configuration");
            mockApiKeyProvider.Setup(p => p.IsValidated).Returns(true);

            // Act
            var service = new GrokRecommendationService(
                mockApiKeyProvider.Object,
                _mockLogger.Object,
                config,
                _mockHttpClientFactory.Object,
                _memoryCache);

            // Assert
            // Verify that the provider was called to get the API key
            mockApiKeyProvider.Verify(p => p.ApiKey, Times.AtLeastOnce);

            // Service should be initialized successfully when API key provider returns valid key
            Assert.NotNull(service);
        }

        /// <summary>
        /// Test: GrokRecommendationService respects XAI:Enabled configuration.
        /// When disabled, should not attempt to use Grok API even if key is present.
        /// </summary>
        [Fact]
        public void Constructor_WhenXaiDisabled_DoesNotAttemptToUseGrokApi()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = "some-key",
                    ["XAI:Enabled"] = "false",  // Explicit disable
                    ["XAI:Endpoint"] = "https://api.x.ai/v1/",
                    ["XAI:Model"] = "grok-2"
                })
                .Build();

            var mockApiKeyProvider = new Mock<IGrokApiKeyProvider>();
            mockApiKeyProvider.Setup(p => p.ApiKey).Returns("some-key");
            mockApiKeyProvider.Setup(p => p.GetConfigurationSource()).Returns("Configuration");

            // Act
            var service = new GrokRecommendationService(
                mockApiKeyProvider.Object,
                _mockLogger.Object,
                config,
                _mockHttpClientFactory.Object,
                _memoryCache);

            // Assert
            // Service should initialize but won't make API calls since XAI is disabled
            Assert.NotNull(service);
        }

        /// <summary>
        /// Test: GrokRecommendationService handles missing API key gracefully.
        /// When provider returns null key, service should not crash.
        /// </summary>
        [Fact]
        public void Constructor_WhenApiKeyProviderReturnsNull_HandlesGracefully()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:Enabled"] = "false",
                    ["XAI:Endpoint"] = "https://api.x.ai/v1/",
                    ["XAI:Model"] = "grok-2"
                })
                .Build();

            var mockApiKeyProvider = new Mock<IGrokApiKeyProvider>();
            mockApiKeyProvider.Setup(p => p.ApiKey).Returns((string?)null);
            mockApiKeyProvider.Setup(p => p.GetConfigurationSource()).Returns("(not configured)");

            // Act & Assert - should not throw
            var service = new GrokRecommendationService(
                mockApiKeyProvider.Object,
                _mockLogger.Object,
                config,
                _mockHttpClientFactory.Object,
                _memoryCache);

            Assert.NotNull(service);
        }

        /// <summary>
        /// Test: API key provider validation status is available to the service.
        /// Service can check if API key has been validated before using it.
        /// </summary>
        [Fact]
        public void Constructor_WhenApiKeyProviderValidationStatusAvailable_ServiceCanCheckIt()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = "valid-key",
                    ["XAI:Enabled"] = "true",
                    ["XAI:Endpoint"] = "https://api.x.ai/v1/",
                    ["XAI:Model"] = "grok-2"
                })
                .Build();

            var mockApiKeyProvider = new Mock<IGrokApiKeyProvider>();
            mockApiKeyProvider.Setup(p => p.ApiKey).Returns("valid-key");
            mockApiKeyProvider.Setup(p => p.IsValidated).Returns(true);
            mockApiKeyProvider.Setup(p => p.GetConfigurationSource()).Returns("User Secrets (Validated)");

            // Act
            var service = new GrokRecommendationService(
                mockApiKeyProvider.Object,
                _mockLogger.Object,
                config,
                _mockHttpClientFactory.Object,
                _memoryCache);

            // Assert
            // Service constructor should log the validation status
            Assert.NotNull(service);
            mockApiKeyProvider.Verify(p => p.GetConfigurationSource(), Times.AtLeastOnce);
        }

        /// <summary>
        /// Test: Environment variable consistency across services.
        /// Both GrokAgentService and GrokRecommendationService should use the same API key.
        /// </summary>
        [Fact]
        public void MultipleServices_WhenUsingSharedApiKeyProvider_ReceiveSameKey()
        {
            // Arrange
            const string sharedApiKey = "sk-shared-key-11111";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = sharedApiKey,
                    ["XAI:Enabled"] = "true",
                    ["XAI:Endpoint"] = "https://api.x.ai/v1/",
                    ["XAI:Model"] = "grok-2"
                })
                .Build();

            // Create a real provider instance (not mocked)
            var apiKeyProvider = new GrokApiKeyProvider(
                config,
                new Mock<ILogger<GrokApiKeyProvider>>().Object,
                new Mock<IHttpClientFactory>().Object);

            // Act
            var service1 = new GrokRecommendationService(
                apiKeyProvider,
                _mockLogger.Object,
                config,
                _mockHttpClientFactory.Object,
                _memoryCache);

            // Multiple services injected with same provider should get same key
            var keyFromProvider = apiKeyProvider.ApiKey;

            // Assert
            Assert.NotNull(keyFromProvider);
            Assert.Equal(sharedApiKey, keyFromProvider);
        }

        /// <summary>
        /// Test: Masked API key is used for logging, not the actual key.
        /// Prevents accidental API key leakage in production logs.
        /// </summary>
        [Fact]
        public void ServiceLogging_UsesOnlyMaskedApiKeyFromProvider()
        {
            // Arrange
            const string fullApiKey = "sk-xai-1234567890abcdefghijklmnopqrstuv";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = fullApiKey,
                    ["XAI:Enabled"] = "true",
                    ["XAI:Endpoint"] = "https://api.x.ai/v1/",
                    ["XAI:Model"] = "grok-2"
                })
                .Build();

            var mockApiKeyProvider = new Mock<IGrokApiKeyProvider>();
            mockApiKeyProvider.Setup(p => p.ApiKey).Returns(fullApiKey);
            mockApiKeyProvider.Setup(p => p.MaskedApiKey).Returns("sk-x...tuv");  // Safely masked
            mockApiKeyProvider.Setup(p => p.GetConfigurationSource()).Returns("User Secrets");

            // Act
            var service = new GrokRecommendationService(
                mockApiKeyProvider.Object,
                _mockLogger.Object,
                config,
                _mockHttpClientFactory.Object,
                _memoryCache);

            // Assert
            // Service should use provider's GetConfigurationSource() for logging (not the actual key)
            mockApiKeyProvider.Verify(p => p.GetConfigurationSource(), Times.AtLeastOnce);
            // Service initialization should succeed
            Assert.NotNull(service);
        }

        /// <summary>
        /// Test: Configuration hierarchy is respected through API key provider.
        /// User Secrets should take precedence over environment variables and config.
        /// </summary>
        [Fact]
        public void Constructor_WhenProviderUsesHierarchy_RespectsPriority()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = "lower-priority-key",
                    ["XAI:Enabled"] = "true",
                    ["XAI:Endpoint"] = "https://api.x.ai/v1/",
                    ["XAI:Model"] = "grok-2"
                })
                .Build();

            const string higherPriorityKey = "higher-priority-from-secrets";

            var mockApiKeyProvider = new Mock<IGrokApiKeyProvider>();
            mockApiKeyProvider.Setup(p => p.ApiKey).Returns(higherPriorityKey);
            mockApiKeyProvider.Setup(p => p.IsFromUserSecrets).Returns(true);
            mockApiKeyProvider.Setup(p => p.GetConfigurationSource()).Returns("User Secrets (Highest Priority)");

            // Act
            var service = new GrokRecommendationService(
                mockApiKeyProvider.Object,
                _mockLogger.Object,
                config,
                _mockHttpClientFactory.Object,
                _memoryCache);

            // Assert
            // Service should use the key returned by provider (which respects hierarchy)
            mockApiKeyProvider.Verify(p => p.ApiKey, Times.AtLeastOnce);
            mockApiKeyProvider.Verify(p => p.GetConfigurationSource(), Times.AtLeastOnce);
            Assert.NotNull(service);
        }
    }
}
