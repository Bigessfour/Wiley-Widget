using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;
using WileyWidget.Integration.Tests.Shared;
using Xunit;

namespace WileyWidget.Integration.Tests.Services
{
    public class CsvExcelImportServiceIntegrationTests : IntegrationTestBase
    {
        public CsvExcelImportServiceIntegrationTests() : base()
        {
        }

        [Fact]
        public async Task ImportTransactionsAsync_ParsesCsv_ReturnsCorrectCounts()
        {
            // Arrange - create temp CSV file
            var tempFile = Path.Combine(Path.GetTempPath(), $"transactions-{System.Guid.NewGuid():N}.csv");
            var csv = "Description,Amount,Date,Type,BudgetEntryId\n\"Test TXN 1\",100.00,2025-12-01,Debit,0\n\"Refund\",-25.50,2025-12-02,Credit,0\n";
            await File.WriteAllTextAsync(tempFile, csv);

            try
            {
                using var scope = CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<CsvExcelImportService>>();
                var svc = new CsvExcelImportService(logger);

                // Act
                var result = await svc.ImportTransactionsAsync(tempFile);

                // Assert
                result.Success.Should().BeTrue();
                result.AccountsImported.Should().Be(2);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { /* best-effort cleanup */ }
            }
        }
    }
}