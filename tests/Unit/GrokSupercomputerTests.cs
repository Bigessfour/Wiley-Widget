using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using Unit.TestHelpers;

namespace Unit.Services
{
    /// <summary>
    /// Unit tests for GrokSupercomputer
    /// </summary>
    public class GrokSupercomputerTests : IDisposable
    {
        private readonly Mock<ILogger<GrokSupercomputer>> _loggerMock;
        private readonly Mock<IEnterpriseRepository> _enterpriseRepositoryMock;
        private readonly Mock<IBudgetRepository> _budgetRepositoryMock;
        private readonly Mock<IAuditRepository> _auditRepositoryMock;
        private readonly Mock<IAILoggingService> _aiLoggingServiceMock;
        private readonly Mock<IAIService> _aiServiceMock;
        private readonly MemoryCache _memoryCache;
        private readonly Mock<IOptions<AppOptions>> _appOptionsMock;

        public GrokSupercomputerTests()
        {
            _loggerMock = new Mock<ILogger<GrokSupercomputer>>();
            _enterpriseRepositoryMock = new Mock<IEnterpriseRepository>();
            _budgetRepositoryMock = new Mock<IBudgetRepository>();
            _auditRepositoryMock = new Mock<IAuditRepository>();
            _aiLoggingServiceMock = new Mock<IAILoggingService>();
            _aiServiceMock = new Mock<IAIService>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _appOptionsMock = new Mock<IOptions<AppOptions>>();

            // Setup app options
            _appOptionsMock.Setup(o => o.Value).Returns(new AppOptions
            {
                BudgetVarianceHighThresholdPercent = 10.0m,
                BudgetVarianceLowThresholdPercent = -5.0m,
                AIHighConfidence = 80,
                AILowConfidence = 60
            });
        }

        [Fact]
        public async Task AnalyzeBudgetDataAsync_ValidBudgetData_ReturnsInsights()
        {
            // Arrange
            var budgetData = new BudgetData
            {
                EnterpriseId = 1,
                FiscalYear = 2024,
                TotalBudget = 100000.0m,
                TotalExpenditures = 95000.0m,
                RemainingBudget = 5000.0m
            };

            _aiServiceMock.Setup(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("AI-powered budget analysis insights");

            var service = CreateService();

            // Act
            var result = await service.AnalyzeBudgetDataAsync(budgetData);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HealthScore > 0);
            Assert.NotEmpty(result.Recommendations);
            Assert.NotEmpty(result.Variances);
            Assert.NotEmpty(result.Projections);
            _aiServiceMock.Verify(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task AnalyzeBudgetDataAsync_NullBudgetData_ThrowsArgumentNullException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.AnalyzeBudgetDataAsync(null!));
        }

        [Fact]
        public async Task AnalyzeBudgetDataAsync_AiServiceFails_ContinuesWithBasicAnalysis()
        {
            // Arrange
            var budgetData = new BudgetData
            {
                EnterpriseId = 1,
                FiscalYear = 2024,
                TotalBudget = 100000.0m,
                TotalExpenditures = 95000.0m,
                RemainingBudget = 5000.0m
            };

            _aiServiceMock.Setup(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new HttpRequestException("AI service error"));

            var service = CreateService();

            // Act
            var result = await service.AnalyzeBudgetDataAsync(budgetData);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HealthScore > 0);
            // Should still have basic recommendations even if AI fails
            Assert.NotEmpty(result.Recommendations);
        }

        [Fact]
        public async Task AnalyzeMunicipalAccountsWithAIAsync_ValidData_ReturnsAnalysis()
        {
            // Arrange
            var accounts = new List<MunicipalAccount>
            {
                new MunicipalAccount { Id = 1, Name = "Test Account", Balance = 1000.0m }
            };

            var budgetData = new BudgetData
            {
                EnterpriseId = 1,
                FiscalYear = 2024,
                TotalBudget = 100000.0m,
                TotalExpenditures = 95000.0m
            };

            _aiServiceMock.Setup(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("AI analysis of municipal accounts");

            var service = CreateService();

            // Act
            var result = await service.AnalyzeMunicipalAccountsWithAIAsync(accounts, budgetData);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("AI analysis", result);
            _aiServiceMock.Verify(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task AnalyzeMunicipalAccountsWithAIAsync_AiServiceFails_ReturnsFallbackMessage()
        {
            // Arrange
            var accounts = new List<MunicipalAccount>
            {
                new MunicipalAccount { Id = 1, Name = "Test Account", Balance = 1000.0m }
            };

            var budgetData = new BudgetData
            {
                EnterpriseId = 1,
                FiscalYear = 2024,
                TotalBudget = 100000.0m,
                TotalExpenditures = 95000.0m
            };

            _aiServiceMock.Setup(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new HttpRequestException("AI service error"));

            var service = CreateService();

            // Act
            var result = await service.AnalyzeMunicipalAccountsWithAIAsync(accounts, budgetData);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Basic municipal account analysis", result);
            Assert.Contains("AI analysis failed", result);
        }

        [Fact]
        public async Task FetchEnterpriseDataAsync_ValidParameters_ReturnsReportData()
        {
            // Arrange
            var expectedEnterprises = new List<Enterprise>
            {
                new Enterprise { Id = 1, Name = "Test Enterprise" }
            };

            _enterpriseRepositoryMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(expectedEnterprises);

            var service = CreateService();

            // Act
            var result = await service.FetchEnterpriseDataAsync(1, DateTime.Now.AddDays(-30), DateTime.Now, "test");

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Enterprises);
        }

        [Fact]
        public async Task RunReportCalcsAsync_ValidData_ReturnsAnalyticsData()
        {
            // Arrange
            var reportData = new ReportData
            {
                Enterprises = new List<Enterprise>
                {
                    new Enterprise { Id = 1, Name = "Test Enterprise", CurrentRate = 50.0m, MonthlyExpenses = 1000.0m }
                }
            };

            var service = CreateService();

            // Act
            var result = await service.RunReportCalcsAsync(reportData);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.SummaryStats);
        }

        [Fact]
        public async Task GenerateComplianceReportAsync_ValidEnterprise_ReturnsReport()
        {
            // Arrange
            var enterprise = new Enterprise
            {
                Id = 1,
                Name = "Test Enterprise",
                CurrentRate = 50.0m,
                MonthlyExpenses = 1000.0m
            };

            var service = CreateService();

            // Act
            var result = await service.GenerateComplianceReportAsync(enterprise);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(enterprise.Id, result.EnterpriseId);
            Assert.True(result.ComplianceScore >= 0);
        }

        [Fact]
        public async Task AnalyzeMunicipalDataAsync_ValidData_ReturnsAnalysis()
        {
            // Arrange
            var testData = new { TestProperty = "test value" };
            var context = "Test context";

            _aiServiceMock.Setup(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("AI analysis result");

            var service = CreateService();

            // Act
            var result = await service.AnalyzeMunicipalDataAsync(testData, context);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("AI analysis", result);
        }

        [Fact]
        public async Task GenerateRecommendationsAsync_ValidData_ReturnsRecommendations()
        {
            // Arrange
            var testData = new { TestProperty = "test value" };

            _aiServiceMock.Setup(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("AI-generated recommendations");

            var service = CreateService();

            // Act
            var result = await service.GenerateRecommendationsAsync(testData);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("AI-generated", result);
        }

        private GrokSupercomputer CreateService()
        {
            return new GrokSupercomputer(
                _loggerMock.Object,
                _enterpriseRepositoryMock.Object,
                _budgetRepositoryMock.Object,
                _auditRepositoryMock.Object,
                _aiLoggingServiceMock.Object,
                _aiServiceMock.Object,
                _memoryCache,
                _appOptionsMock.Object);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _memoryCache?.Dispose();
            }
        }

        ~GrokSupercomputerTests()
        {
            Dispose(false);
        }
    }
}
