using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Xunit;
using WileyWidget.WinForms.Services.AI;

namespace WileyWidget.Services.Tests
{
    public class XAIModelDiscoveryIntegrationTests
    {
        [Fact]
        public async Task ListAvailableModelsAsync_ReturnsNonEmpty_WhenApiKeyPresent()
        {
            var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey)) return; // Skip if no key

            var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var service = new GrokAgentService(config, logger: null, httpClientFactory: null);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var models = (await service.ListAvailableModelsAsync(cts.Token)).ToList();
            Assert.NotEmpty(models);
        }

        [Fact]
        public async Task AutoSelectModelAsync_ReturnsModel_WhenApiKeyPresent()
        {
            var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey)) return; // Skip if no key

            var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var service = new GrokAgentService(config, logger: null, httpClientFactory: null);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var selected = await service.AutoSelectModelAsync(cts.Token);
            Assert.False(string.IsNullOrWhiteSpace(selected));
        }
    }
}
