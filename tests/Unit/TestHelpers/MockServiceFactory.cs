using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using WileyWidget.Services;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;

namespace Unit.TestHelpers
{
    /// <summary>
    /// Factory for creating mock services for unit testing
    /// </summary>
    public static class MockServiceFactory
    {
        /// <summary>
        /// Creates a mock IAIService with default behavior
        /// </summary>
        public static Mock<IAIService> CreateMockAIService()
        {
            var mock = new Mock<IAIService>();

            // Setup default GetInsightsAsync behavior
            mock.Setup(x => x.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Mock AI response for testing");

            // Setup GetInsightsWithStatusAsync if it exists
            if (mock.Object.GetType().GetMethod("GetInsightsWithStatusAsync") != null)
            {
                mock.Setup(x => x.GetInsightsWithStatusAsync(It.IsAny<object>(), It.IsAny<string>()))
                    .ReturnsAsync(("Mock AI response with status", "success"));
            }

            return mock;
        }

        /// <summary>
        /// Creates a mock IGrokSupercomputer with default behavior
        /// </summary>
        public static Mock<IGrokSupercomputer> CreateMockGrokSupercomputer()
        {
            var mock = new Mock<IGrokSupercomputer>();

            // Setup FetchEnterpriseDataAsync
            mock.Setup(x => x.FetchEnterpriseDataAsync(It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>()))
                .ReturnsAsync(new ReportData
                {
                    Enterprises = new List<Enterprise>
                    {
                        new Enterprise { Id = 1, Name = "Test Enterprise", CurrentRate = 50.0m, MonthlyExpenses = 1000.0m }
                    }
                });

            // Setup RunReportCalcsAsync
            mock.Setup(x => x.RunReportCalcsAsync(It.IsAny<ReportData>()))
                .ReturnsAsync(new AnalyticsData
                {
                    SummaryStats = new Dictionary<string, decimal> { ["TotalRevenue"] = 10000.0m }
                });

            // Setup AnalyzeBudgetDataAsync
            mock.Setup(x => x.AnalyzeBudgetDataAsync(It.IsAny<BudgetData>()))
                .ReturnsAsync(new BudgetInsights
                {
                    HealthScore = 85,
                    Recommendations = new List<string>
                    {
                        "Mock recommendation: Consider cost optimization",
                        "Mock recommendation: Review budget allocations"
                    },
                    Variances = new List<BudgetVariance>
                    {
                        new BudgetVariance
                        {
                            Category = "Test Category",
                            Budgeted = 1000.0m,
                            Actual = 950.0m,
                            Variance = -50.0m
                        }
                    },
                    Projections = new List<BudgetProjection>
                    {
                        new BudgetProjection
                        {
                            Period = "End of Year",
                            ProjectedAmount = 12000.0m,
                            ConfidenceLevel = 80
                        }
                    }
                });

            // Setup GenerateComplianceReportAsync
            mock.Setup(x => x.GenerateComplianceReportAsync(It.IsAny<Enterprise>()))
                .ReturnsAsync(new ComplianceReport
                {
                    EnterpriseId = 1,
                    GeneratedDate = DateTime.Now,
                    ComplianceScore = 95,
                    Violations = new List<ComplianceViolation>(),
                    Recommendations = new List<string> { "Mock compliance recommendation" }
                });

            // Setup AnalyzeMunicipalDataAsync
            mock.Setup(x => x.AnalyzeMunicipalDataAsync(It.IsAny<object>(), It.IsAny<string>()))
                .ReturnsAsync("Mock municipal data analysis result");

            // Setup GenerateRecommendationsAsync
            mock.Setup(x => x.GenerateRecommendationsAsync(It.IsAny<object>()))
                .ReturnsAsync("Mock AI-generated recommendations for optimization");

            // Setup AnalyzeMunicipalAccountsWithAIAsync
            mock.Setup(x => x.AnalyzeMunicipalAccountsWithAIAsync(It.IsAny<IEnumerable<MunicipalAccount>>(), It.IsAny<BudgetData>()))
                .ReturnsAsync("Mock AI analysis of municipal accounts: Identified optimization opportunities in account structure");

            return mock;
        }

        /// <summary>
        /// Creates a mock IAIService that throws exceptions for testing error handling
        /// </summary>
        public static Mock<IAIService> CreateMockAIServiceWithErrors()
        {
            var mock = new Mock<IAIService>();

            mock.Setup(x => x.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Mock API error for testing"));

            return mock;
        }

        /// <summary>
        /// Creates a mock IGrokSupercomputer that throws exceptions for testing error handling
        /// </summary>
        public static Mock<IGrokSupercomputer> CreateMockGrokSupercomputerWithErrors()
        {
            var mock = new Mock<IGrokSupercomputer>();

            mock.Setup(x => x.AnalyzeBudgetDataAsync(It.IsAny<BudgetData>()))
                .ThrowsAsync(new InvalidOperationException("Mock analysis error for testing"));

            mock.Setup(x => x.AnalyzeMunicipalAccountsWithAIAsync(It.IsAny<IEnumerable<MunicipalAccount>>(), It.IsAny<BudgetData>()))
                .ThrowsAsync(new InvalidOperationException("Mock account analysis error for testing"));

            return mock;
        }

        /// <summary>
        /// Creates a mock IAIService with slow responses for testing timeouts
        /// </summary>
        public static Mock<IAIService> CreateMockAIServiceWithDelay(int delayMs = 5000)
        {
            var mock = new Mock<IAIService>();

            mock.Setup(x => x.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await Task.Delay(delayMs);
                    return "Delayed mock response";
                });

            return mock;
        }
    }
}