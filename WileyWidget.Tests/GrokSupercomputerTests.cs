using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.Tests
{
    public class GrokSupercomputerTests : IDisposable
    {
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<ILogger<GrokSupercomputer>> _loggerMock;
        private readonly Mock<AppDbContext> _contextMock;
        private readonly Mock<GrokDatabaseService> _dbServiceMock;
        private bool _disposed;

        public GrokSupercomputerTests()
        {
            _configMock = new Mock<IConfiguration>();
            _configMock.Setup(c => c["xAI:ApiKey"]).Returns("test-api-key");

            _loggerMock = new Mock<ILogger<GrokSupercomputer>>();
            _contextMock = new Mock<AppDbContext>();
            _dbServiceMock = new Mock<GrokDatabaseService>();
        }

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
                    // Dispose managed resources
                }
                _disposed = true;
            }
        }

        [Fact]
        public async Task CrunchNumbersAsync_WithValidData_ReturnsUpdatedEnterprises()
        {
            // Arrange
            var enterprises = new List<Enterprise>
            {
                new Enterprise { Name = "Water", CurrentRate = 5.0m, MonthlyExpenses = 1000, CitizenCount = 100 }
            };

            using var service = new GrokSupercomputer(_configMock.Object, _loggerMock.Object, _contextMock.Object);

            // Act
            var result = await service.CrunchNumbersAsync(enterprises, "Test algo");

            // Assert
            Assert.Single(result);
            Assert.Equal("Water", result[0].Name);
        }

        [Fact]
        public async Task CrunchNumbersAsync_WithMockedHttpClient_ReturnsApiResults()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() =>
                {
                    var response = new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"[{\\\"name\\\":\\\"hash123\\\",\\\"deficit\\\":150.0,\\\"suggestedHike\\\":2.5,\\\"suggestion\\\":\\\"Test suggestion\\\"}]\"}}]}")
                    };
                    return response;
                });

            var enterprises = new List<Enterprise>
            {
                new Enterprise { Name = "Water", CurrentRate = 5.0m, MonthlyExpenses = 1000, CitizenCount = 100 }
            };

            using var service = new GrokSupercomputer(_configMock.Object, _loggerMock.Object, _contextMock.Object, _dbServiceMock.Object);

            // Act
            var result = await service.CrunchNumbersAsync(enterprises, "Test algorithm");

            // Assert
            Assert.Single(result);
            Assert.Equal(150.0m, result[0].ComputedDeficit);
            Assert.Equal(2.5m, result[0].SuggestedRateHike);
            Assert.Contains("Test suggestion", result[0].Notes);
        }

        [Fact]
        public async Task CrunchNumbersAsync_ApiFailure_FallsBackToLocalCalculation()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() =>
                {
                    var response = new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.InternalServerError
                    };
                    return response;
                });

            var enterprises = new List<Enterprise>
            {
                new Enterprise { Name = "Water", CurrentRate = 5.0m, MonthlyExpenses = 1000, CitizenCount = 100 }
            };

            using var service = new GrokSupercomputer(_configMock.Object, _loggerMock.Object, _contextMock.Object);

            // Act
            var result = await service.CrunchNumbersAsync(enterprises, "Test algorithm");

            // Assert - Should have local calculation results
            Assert.Single(result);
            Assert.Equal(500.0m, result[0].ComputedDeficit); // 1000 - (100 * 5) = 500
            Assert.Contains("Local calc", result[0].Notes);
        }

        [Fact]
        public async Task AnalyzeTrendsAsync_WithHistoricalData_ReturnsAnalysis()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() =>
                {
                    var response = new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{\\\"trends\\\":\\\"Expenses increasing\\\",\\\"prediction\\\":\\\"Deficit next year\\\",\\\"suggestions\\\":[\\\"Cut costs\\\",\\\"Raise rates\\\"]}\"}}\n]}")
                    };
                    return response;
                });

            var enterprises = new List<Enterprise>
            {
                new Enterprise { Name = "Water", MonthlyExpenses = 1000, CurrentRate = 5.0m, CitizenCount = 100 }
            };

            using var service = new GrokSupercomputer(_configMock.Object, _loggerMock.Object, _contextMock.Object);

            // Act
            var result = await service.AnalyzeTrendsAsync(enterprises);

            // Assert
            Assert.Contains("trends", result);
            Assert.Contains("Expenses increasing", result);
        }

        [Fact]
        public async Task SimulateScenarioAsync_ReturnsScenarioAnalysis()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() =>
                {
                    var response = new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{\\\"impact\\\":\\\"Revenue increase\\\",\\\"recommendations\\\":[\\\"Monitor closely\\\",\\\"Adjust rates\\\"]}\"}}\n]}")
                    };
                    return response;
                });

            var enterprises = new List<Enterprise>
            {
                new Enterprise { Name = "Water", CurrentRate = 5.0m, MonthlyExpenses = 1000, CitizenCount = 100 }
            };

            using var service = new GrokSupercomputer(_configMock.Object, _loggerMock.Object, _contextMock.Object);

            // Act
            var result = await service.SimulateScenarioAsync(enterprises, "Rate increase of 10%");

            // Assert
            Assert.Contains("impact", result);
            Assert.Contains("Revenue increase", result);
        }

        [Fact]
        public void AnonymizeName_ReturnsHashedString()
        {
            // Arrange
            using var service = new GrokSupercomputer(_configMock.Object);

            // Act
            var result = service.GetType().GetMethod("AnonymizeName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(service, new object[] { "TestName" }) as string;

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual("TestName", result);
            Assert.True(result.Length == 8); // SHA256 hash truncated to 8 chars
        }

        [Fact]
        public async Task ComputeEnterprisesAsync_UsesDefaultAlgorithm()
        {
            // Arrange
            var enterprises = new List<Enterprise>
            {
                new Enterprise { Name = "Water", CurrentRate = 5.0m, MonthlyExpenses = 1000, CitizenCount = 100 }
            };

            using var service = new GrokSupercomputer(_configMock.Object, _loggerMock.Object, _contextMock.Object);

            // Act
            var result = await service.ComputeEnterprisesAsync(enterprises);

            // Assert
            Assert.Single(result);
            // Should use the default algorithm and fallback to local calculation
            Assert.Equal(500.0m, result[0].ComputedDeficit); // 1000 - (100 * 5) = 500
        }
    }
}
