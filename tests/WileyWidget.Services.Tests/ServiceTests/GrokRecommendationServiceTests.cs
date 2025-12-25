using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Business.Services;
using WileyWidget.Services.Tests.TestHelpers;
using Xunit;

namespace WileyWidget.Services.Tests.ServiceTests
{
    public class GrokRecommendationServiceTests
    {
        [Fact]
        public async Task GetRecommendedAdjustmentFactorsAsync_AllSuccess_ReturnsParsedRecommendations()
        {
            // Arrange
            var logger = new Mock<ILogger<GrokRecommendationService>>();
            var responseJson = "{\"choices\":[{\"message\":{\"content\":\"{\\\"Water\\\":1.15,\\\"Sewer\\\":1.12}\"}}]}";
            var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse(responseJson));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai/") };
            var svc = new GrokRecommendationService(logger.Object, httpClient);

            var deptExpenses = new Dictionary<string, decimal>
            {
                ["Water"] = 1000m,
                ["Sewer"] = 800m
            };

            // Act
            var result = await svc.GetRecommendedAdjustmentFactorsAsync(deptExpenses);

            // Assert
            result.Should().ContainKey("Water").And.ContainKey("Sewer");
            result["Water"].Should().Be(1.15m);
            result["Sewer"].Should().Be(1.12m);
        }

        [Fact]
        public async Task GetRecommendedAdjustmentFactorsAsync_InvalidContent_ReturnsFallback()
        {
            // Arrange
            var logger = new Mock<ILogger<GrokRecommendationService>>();
            // Grok returns content that is not JSON - parsing should fall back to defaults
            var responseJson = "{\"choices\":[{\"message\":{\"content\":\"not-a-json\"}}]}";
            var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse(responseJson));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai/") };
            var svc = new GrokRecommendationService(logger.Object, httpClient);

            var deptExpenses = new Dictionary<string, decimal> { ["Water"] = 1000m };

            // Act
            var result = await svc.GetRecommendedAdjustmentFactorsAsync(deptExpenses);

            // Assert - fallback keys should exist
            result.Should().ContainKey("Water").And.ContainKey("Sewer").And.ContainKey("Trash").And.ContainKey("Apartments");
            result["Water"].Should().Be(1.15m);
        }

        [Fact]
        public async Task GetRecommendedAdjustmentFactorsAsync_HttpError_Throws()
        {
            // Arrange
            var logger = new Mock<ILogger<GrokRecommendationService>>();
            var handler = new FakeHttpMessageHandler((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("error") }));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai/") };
            var svc = new GrokRecommendationService(logger.Object, httpClient);

            var deptExpenses = new Dictionary<string, decimal> { ["Water"] = 1000m };

            // Act / Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => svc.GetRecommendedAdjustmentFactorsAsync(deptExpenses));
        }

        [Fact]
        public async Task GetRecommendationExplanationAsync_ParsesExplanation()
        {
            // Arrange
            var logger = new Mock<ILogger<GrokRecommendationService>>();
            var responseJson = "{\"choices\":[{\"message\":{\"content\":\"This is the explanation text\"}}]}";
            var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse(responseJson));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai/") };
            var svc = new GrokRecommendationService(logger.Object, httpClient);

            var deptExpenses = new Dictionary<string, decimal> { ["Water"] = 1000m };

            // Act
            var result = await svc.GetRecommendationExplanationAsync(deptExpenses);

            // Assert
            result.Should().Be("This is the explanation text");
        }

        [Fact]
        public async Task GetRecommendationExplanationAsync_InvalidResponse_ReturnsFallbackString()
        {
            // Arrange
            var logger = new Mock<ILogger<GrokRecommendationService>>();
            var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse("{}"));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai/") };
            var svc = new GrokRecommendationService(logger.Object, httpClient);

            var deptExpenses = new Dictionary<string, decimal> { ["Water"] = 1000m };

            // Act
            var result = await svc.GetRecommendationExplanationAsync(deptExpenses);

            // Assert
            result.Should().Be("Unable to generate explanation from Grok API response.");
        }

        [Fact]
        public async Task GetRecommendedAdjustmentFactorsAsync_Cancellation_ThrowsOperationCanceled()
        {
            // Arrange
            var logger = new Mock<ILogger<GrokRecommendationService>>();
            var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse("{\"choices\":[]}"));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai/") };
            var svc = new GrokRecommendationService(logger.Object, httpClient);

            var deptExpenses = new Dictionary<string, decimal> { ["Water"] = 1000m };
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // cancel immediately

            // Act / Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.GetRecommendedAdjustmentFactorsAsync(deptExpenses, cancellationToken: cts.Token));
        }
    }
}
