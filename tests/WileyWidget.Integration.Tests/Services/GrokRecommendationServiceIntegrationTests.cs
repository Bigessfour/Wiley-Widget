using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using RichardSzalay.MockHttp;
using WileyWidget.Business.Interfaces;
using WileyWidget.Integration.Tests.Shared;
using Xunit;

namespace WileyWidget.Integration.Tests.Services
{
    public class GrokRecommendationServiceIntegrationTests : IntegrationTestBase
    {
        public GrokRecommendationServiceIntegrationTests()
            : base(services =>
            {
                var handler = new MockHttpMessageHandler();

                // Grok API returns "choices[0].message.content" which itself contains a JSON object
                var responseJson = "{\"choices\":[{\"message\":{\"content\":\"{\\\"Water\\\":1.15,\\\"Sewer\\\":1.12}\"}}]}";

                handler.When("https://api.x.ai/v1/chat/completions")
                       .Respond("application/json", responseJson);

                services.ReplaceGrokWithMockHttp(handler);
            })
        {
        }

        [Fact]
        public async Task GetRecommendedAdjustmentFactorsAsync_ParsesGrokResponse()
        {
            var svc = GetRequiredService<IGrokRecommendationService>();

            var deptExpenses = new Dictionary<string, decimal>
            {
                ["Water"] = 1000m,
                ["Sewer"] = 800m
            };

            var result = await svc.GetRecommendedAdjustmentFactorsAsync(deptExpenses);

            result.Should().ContainKey("Water").And.ContainKey("Sewer");
            result["Water"].Should().Be(1.15m);
            result["Sewer"].Should().Be(1.12m);
        }
    }
}