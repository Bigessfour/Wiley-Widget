using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Xunit;
using WileyWidget.WinForms.Services.AI;

namespace WileyWidget.Services.Tests
{
    public class GrokAgentServiceIntegrationTests
    {
        [Fact]
        public async Task ValidateApiKeyAsync_WithEnvironmentKey_ReturnsSuccess()
        {
            var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY") ?? Environment.GetEnvironmentVariable("Grok:ApiKey");
            if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.StartsWith("xai-", StringComparison.OrdinalIgnoreCase))
            {
                // No valid xAI API key in environment; skip this integration test.
                return;
            }

            var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var service = new GrokAgentService(config, logger: null, httpClientFactory: null);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var (success, message) = await service.ValidateApiKeyAsync(cts.Token);

            Assert.True(success, $"ValidateApiKeyAsync returned failure: {message}");
        }
    }
}
