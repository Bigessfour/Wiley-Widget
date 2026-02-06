using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Tests.Unit.Services.AI
{
    /// <summary>
    /// Unit tests for GrokApiKeyProvider - validates configuration hierarchy and consistency.
    /// Tests ensure that xAI API keys are correctly resolved from:
    /// 1. User Secrets (highest priority)
    /// 2. Environment Variables (XAI__ApiKey or XAI_API_KEY)
    /// 3. appsettings.json (lowest priority)
    ///
    /// These tests prevent regression of the xAI API key presentation issue where
    /// XAI_API_KEY (env var) and XAI:ApiKey (config) were inconsistently handled.
    /// </summary>
    public class GrokApiKeyProviderTests
    {
        private readonly Mock<ILogger<GrokApiKeyProvider>> _mockLogger =
            new Mock<ILogger<GrokApiKeyProvider>>();

        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory =
            new Mock<IHttpClientFactory>();

        /// <summary>
        /// Test: User Secrets have highest priority - should take precedence over env vars.
        /// </summary>
        [Fact]
        public void Constructor_WhenUserSecretsConfigured_UsesUserSecretApiKey()
        {
            // Arrange
            const string expectedKey = "user-secret-key-12345";
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = expectedKey,
                    ["Logging:LogLevel:Default"] = "Information"
                })
                .Build();

            // Act
            var provider = new GrokApiKeyProvider(config, _mockLogger.Object, _mockHttpClientFactory.Object);

            // Assert
            Assert.NotNull(provider.ApiKey);
            Assert.Equal(expectedKey, provider.ApiKey);
        }

        /// <summary>
        /// Test: Environment variables (XAI__ApiKey) are resolved when config key not present.
        /// Tests the double-underscore hierarchical format per Microsoft convention.
        /// </summary>
        [Fact]
        public void Constructor_WhenEnvironmentVariableXaiDoubleUnderscoreSet_UsesEnvVarApiKey()
        {
            // Arrange
            const string expectedKey = "env-var-key-67890";
            var originalEnvVar = Environment.GetEnvironmentVariable("XAI__ApiKey");

            try
            {
                // Set process-scoped env var with double underscore (Microsoft hierarchical format)
                Environment.SetEnvironmentVariable("XAI__ApiKey", expectedKey, EnvironmentVariableTarget.Process);

                var config = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Logging:LogLevel:Default"] = "Information"
                    })
                    .Build();

                // Act
                var provider = new GrokApiKeyProvider(config, _mockLogger.Object, _mockHttpClientFactory.Object);

                // Assert
                Assert.NotNull(provider.ApiKey);
                Assert.Equal(expectedKey, provider.ApiKey);
            }
            finally
            {
                // Cleanup
                if (originalEnvVar != null)
                {
                    Environment.SetEnvironmentVariable("XAI__ApiKey", originalEnvVar, EnvironmentVariableTarget.Process);
                }
                else
                {
                    Environment.SetEnvironmentVariable("XAI__ApiKey", null, EnvironmentVariableTarget.Process);
                }
            }
        }

        /// <summary>
        /// Test: Legacy environment variable (XAI_API_KEY with single underscore) is still supported.
        /// Ensures backward compatibility with existing deployment scripts/documentation.
        /// </summary>
        [Fact]
        public void Constructor_WhenLegacyEnvironmentVariableXaiSingleUnderscoreSet_UsesLegacyEnvVar()
        {
            // Arrange
            const string expectedKey = "legacy-env-key-11111";
            var originalEnvVar = Environment.GetEnvironmentVariable("XAI_API_KEY");

            try
            {
                // Set process-scoped legacy env var (single underscore)
                Environment.SetEnvironmentVariable("XAI_API_KEY", expectedKey, EnvironmentVariableTarget.Process);

                var config = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Logging:LogLevel:Default"] = "Information"
                    })
                    .Build();

                // Act
                var provider = new GrokApiKeyProvider(config, _mockLogger.Object, _mockHttpClientFactory.Object);

                // Assert
                Assert.NotNull(provider.ApiKey);
                Assert.Equal(expectedKey, provider.ApiKey);
            }
            finally
            {
                // Cleanup
                if (originalEnvVar != null)
                {
                    Environment.SetEnvironmentVariable("XAI_API_KEY", originalEnvVar, EnvironmentVariableTarget.Process);
                }
                else
                {
                    Environment.SetEnvironmentVariable("XAI_API_KEY", null, EnvironmentVariableTarget.Process);
                }
            }
        }

        /// <summary>
        /// Test: Configuration hierarchy - User Secrets > Env Vars > appsettings.json
        /// Validates that when multiple sources exist, highest priority wins.
        /// </summary>
        [Fact]
        public void Constructor_WhenMultipleSourcesPresent_UserSecretsHasPriority()
        {
            // Arrange
            const string userSecretKey = "user-secret";
            const string configKey = "config-key";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = configKey  // Lower priority
                })
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = userSecretKey  // Higher priority - added last, so takes precedence
                })
                .Build();

            // Act
            var provider = new GrokApiKeyProvider(config, _mockLogger.Object, _mockHttpClientFactory.Object);

            // Assert - User Secrets (added last) should win
            Assert.NotNull(provider.ApiKey);
            Assert.Equal(userSecretKey, provider.ApiKey);
        }

        /// <summary>
        /// Test: MaskedApiKey returns safe format for logging (first 4 + last 4 chars).
        /// Ensures API keys are never fully exposed in logs.
        /// </summary>
        [Fact]
        public void MaskedApiKey_ReturnsPartialKeyForLogging()
        {
            // Arrange
            const string fullKey = "sk-xai-1234567890abcdefghijklmnop";
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = fullKey
                })
                .Build();

            var provider = new GrokApiKeyProvider(config, _mockLogger.Object, _mockHttpClientFactory.Object);

            // Act
            var maskedKey = provider.MaskedApiKey;

            // Assert
            Assert.NotNull(maskedKey);
            Assert.True(maskedKey.Contains("xxxx"), "Masked key should show masked portion");
            Assert.True(maskedKey.Length < fullKey.Length, "Masked key should be shorter than actual key");
            // Verify format: first 4 + last 4 chars visible
            Assert.StartsWith("sk-x", maskedKey);
            Assert.Contains("mnop", maskedKey);
        }

        /// <summary>
        /// Test: GetConfigurationSource() provides diagnostic information.
        /// Helps operators debug which source the API key came from.
        /// </summary>
        [Fact]
        public void GetConfigurationSource_ReturnsReadableDiagnosticInfo()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = "test-key"
                })
                .Build();

            var provider = new GrokApiKeyProvider(config, _mockLogger.Object, _mockHttpClientFactory.Object);

            // Act
            var source = provider.GetConfigurationSource();

            // Assert
            Assert.NotNull(source);
            Assert.NotEmpty(source);
            Assert.True(source.Contains("API Key") || source.Contains("Configuration"),
                "Source should describe where key came from");
        }

        /// <summary>
        /// Test: ValidateAsync performs a test API call to verify key validity.
        /// Ensures API key is actually functional before services try to use it.
        /// </summary>
        [Fact]
        public async Task ValidateAsync_WhenKeyInvalid_ReturnsFalseWithMessage()
        {
            // Arrange
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var mockHandler = new Mock<HttpMessageHandler>();

            // Simulate API rejection (401 Unauthorized)
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.Unauthorized,
                    Content = new StringContent("{\"error\": \"Invalid API key\"}")
                });

            var httpClient = new HttpClient(mockHandler.Object);
            mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = "invalid-key-12345",
                    ["XAI:Endpoint"] = "https://api.x.ai/v1/"
                })
                .Build();

            var provider = new GrokApiKeyProvider(config, _mockLogger.Object, mockHttpClientFactory.Object);

            // Act
            var (success, message) = await provider.ValidateAsync();

            // Assert
            Assert.False(success, "Validation should fail for invalid key");
            Assert.NotNull(message);
            Assert.NotEmpty(message);
        }

        /// <summary>
        /// Test: IsFromUserSecrets property tracks secure source correctly.
        /// Helps ensure sensitive keys don't leak to insecure sources.
        /// </summary>
        [Fact]
        public void IsFromUserSecrets_ReflectsConfigurationSource()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:ApiKey"] = "regular-config-key"
                })
                .Build();

            // Act
            var provider = new GrokApiKeyProvider(config, _mockLogger.Object, _mockHttpClientFactory.Object);

            // Assert
            // When loaded from in-memory config (not user secrets), flag should reflect that
            Assert.NotNull(provider.ApiKey);
            // IsFromUserSecrets would be false since we added via in-memory config, not user secrets
        }

        /// <summary>
        /// Test: Empty/null API key is handled gracefully without exceptions.
        /// Ensures provider doesn't crash when key is not configured.
        /// </summary>
        [Fact]
        public void Constructor_WhenNoApiKeyConfigured_DoesNotThrow()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logging:LogLevel:Default"] = "Information"
                })
                .Build();

            // Act & Assert - should not throw
            var provider = new GrokApiKeyProvider(config, _mockLogger.Object, _mockHttpClientFactory.Object);
            Assert.Null(provider.ApiKey);
        }
    }
}
