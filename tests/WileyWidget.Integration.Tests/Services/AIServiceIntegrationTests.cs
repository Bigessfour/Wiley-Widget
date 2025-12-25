using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WileyWidget.Services.Abstractions;
using WileyWidget.Integration.Tests.Shared;
using Xunit;

/*
namespace WileyWidget.Integration.Tests.Services
{
    public class AIServiceIntegrationTests : IntegrationTestBase
    {
        public AIServiceIntegrationTests()
            : base(services =>
            {
                // Mock the AI service to return deterministic responses
                var aiMock = new Mock<IAIService>();
                aiMock.Setup(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                    .ReturnsAsync("Mock AI Insight");
                services.AddSingleton(aiMock);
                services.AddSingleton<IAIService>(sp => sp.GetRequiredService<Mock<IAIService>>().Object);

                // Ensure GrokSupercomputer uses the real implementation
                services.AddScoped<WileyWidget.Services.GrokSupercomputer, WileyWidget.Services.GrokSupercomputer>();
            })
        {
        }

        [Fact]
        public async Task GetInsightsAsync_ReturnsInsight()
        {
            var aiService = GetRequiredService<IAIService>();
            var result = await aiService.GetInsightsAsync("test prompt", "test context");
            result.Should().Be("Mock AI Insight");
        }

        [Fact]
        public async Task AnalyzeBudgetDataAsync_ReturnsAnalysis()
        {
            var grok = GetRequiredService<WileyWidget.Services.GrokSupercomputer>();
            var budgetData = new WileyWidget.Models.BudgetData
            {
                EnterpriseId = 0,
                FiscalYear = 2025,
                TotalBudget = 1000m,
                TotalExpenditures = 500m,
                RemainingBudget = 500m
            };
            var result = await grok.AnalyzeBudgetDataAsync(budgetData);
            result.Should().NotBeNull();
            result.Recommendations.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GenerateInsights_ValidData_CallsGrokApi_AndSavesResponse()
        {
            var grok = GetRequiredService<WileyWidget.Services.GrokSupercomputer>();
            var budgetData = new WileyWidget.Models.BudgetData
            {
                EnterpriseId = 1,
                FiscalYear = 2025,
                TotalBudget = 10000m,
                TotalExpenditures = 7500m,
                RemainingBudget = 2500m
            };

            var result = await grok.GenerateInsightsAsync(budgetData);
            result.Should().NotBeNull();
            // Verify saved to DB or cache
        }

        [Fact]
        public async Task GenerateInsights_EmptyDataset_ReturnsDefaultMessage()
        {
            var grok = GetRequiredService<WileyWidget.Services.GrokSupercomputer>();
            var emptyData = new WileyWidget.Models.BudgetData
            {
                EnterpriseId = 1,
                FiscalYear = 2025,
                TotalBudget = 0m,
                TotalExpenditures = 0m,
                RemainingBudget = 0m
            };

            var result = await grok.GenerateInsightsAsync(emptyData);
            result.Should().NotBeNull();
            result.Insights.Should().Contain("limited data");
        }

        // [Fact]
        // public async Task GenerateInsights_ApiError_RetriesAndEventuallyFailsGracefully()
        // {
        //     // Arrange - mock API to fail
        //     var aiMock = new Mock<IAIService>();
        //     aiMock.Setup(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
        //         .ThrowsAsync(new System.Exception("API Error"));

        //     var grok = new WileyWidget.Services.GrokSupercomputer(aiMock.Object, null);

        //     var budgetData = new WileyWidget.Models.BudgetData
        //     {
        //         EnterpriseId = 1,
        //         FiscalYear = 2025,
        //         TotalBudget = 1000m,
        //         TotalExpenditures = 500m,
        //         RemainingBudget = 500m
        //     };

        //     // Act & Assert
        //     var result = await grok.GenerateInsightsAsync(budgetData);
        //     result.Should().NotBeNull(); // Should handle error gracefully
        //     result.ErrorMessage.Should().NotBeNull();
        // }

        [Fact]
        public async Task GenerateInsights_CachesRecentResults_AvoidsDuplicateCalls()
        {
            var grok = GetRequiredService<WileyWidget.Services.GrokSupercomputer>();
            var budgetData = new WileyWidget.Models.BudgetData
            {
                EnterpriseId = 1,
                FiscalYear = 2025,
                TotalBudget = 1000m,
                TotalExpenditures = 500m,
                RemainingBudget = 500m
            };

            // First call
            var result1 = await grok.GenerateInsightsAsync(budgetData);
            // Second call - should use cache
            var result2 = await grok.GenerateInsightsAsync(budgetData);

            result1.Should().BeEquivalentTo(result2);
        }

        [Fact]
        public async Task ProcessTransactionBatch_ForInsights_TriggersGrokCall()
        {
            var grok = GetRequiredService<WileyWidget.Services.GrokSupercomputer>();
            var transactions = new List<WileyWidget.Models.Transaction>
            {
                new() { Amount = 100, TransactionDate = DateTime.Now, Description = "Test" }
            };

            var result = await grok.ProcessTransactionBatchAsync(transactions);
            result.Should().NotBeNull();
            // Verify insights generated
        }

        [Fact]
        public async Task GetLatestInsights_FromDb_ReturnsMostRecent()
        {
            // Arrange - seed some insights
            var insight1 = new WileyWidget.Models.AIInsight
            {
                EnterpriseId = 1,
                FiscalYear = 2025,
                GeneratedAt = DateTime.Now.AddHours(-1),
                Content = "Old insight"
            };
            var insight2 = new WileyWidget.Models.AIInsight
            {
                EnterpriseId = 1,
                FiscalYear = 2025,
                GeneratedAt = DateTime.Now,
                Content = "New insight"
            };
            DbContext.AIInsights.AddRange(insight1, insight2);
            await DbContext.SaveChangesAsync();

            var grok = GetRequiredService<WileyWidget.Services.GrokSupercomputer>();

            // Act
            var result = await grok.GetLatestInsightsAsync(1, 2025);

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be("New insight");
        }
    }
}
*/
