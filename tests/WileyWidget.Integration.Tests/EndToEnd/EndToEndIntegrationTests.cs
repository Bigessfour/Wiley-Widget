using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using WileyWidget.Models;
using Xunit;
using WileyWidget.TestUtilities;

namespace WileyWidget.Integration.Tests.EndToEnd
{
    public class EndToEndIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public async Task EndToEnd_Pipeline_Persists_AIInsight_For_QuickBooksInvoices()
        {
            // Arrange
            var options = TestHelpers.CreateInMemoryOptions();

            var qbDataService = new TestHelpers.FakeQuickBooksDataService();
            var aiMock = new Mock<WileyWidget.Services.Abstractions.IAIService>();
            aiMock.Setup(a => a.AnalyzeDataAsync(It.IsAny<string>(), It.IsAny<string>(), default)).ReturnsAsync("AI analysis result");

            // Act - fetch invoices from mock QB service
            var invoices = qbDataService.FindInvoices();

            await using (var context = new WileyWidget.Data.AppDbContext(options))
            {
                await context.Database.EnsureCreatedAsync();

                foreach (var inv in invoices)
                {
                    // Simulate AI analysis of invoice
                    var analysis = await aiMock.Object.AnalyzeDataAsync(inv.DocNumber ?? "unknown", "invoice");

                    // Persist AIInsight
                    var insight = new AIInsight
                    {
                        Query = inv.DocNumber ?? "invoice",
                        Response = analysis,
                        Content = analysis,
                        Priority = "Medium"
                    };

                    context.AIInsights.Add(insight);
                }

                await context.SaveChangesAsync();
            }

            // Assert
            await using (var context = new WileyWidget.Data.AppDbContext(options))
            {
                var stored = await context.AIInsights.ToListAsync();
                stored.Should().NotBeEmpty();
                stored.First().Content.Should().Contain("AI analysis result");
            }
        }
    }
}
