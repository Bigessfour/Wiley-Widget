using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests
{
    public class BudgetViewModelTests
    {
        [Fact]
        public async Task ImportFromCsvCommand_ParsesAndAddsEntries()
        {
            // Arrange - create a temp CSV
            var csv = "AccountNumber,Description,BudgetedAmount,ActualAmount,FiscalYear,DepartmentId,FundId\n" +
                      "410.1,Office Supplies,150.00,10.00,2025,1,2\n" +
                      "420.2,Travel,1000.50,200.25,2025,2,3\n";

            var tmp = Path.GetTempFileName();
            await File.WriteAllTextAsync(tmp, csv);

            var repoMock = new Mock<IBudgetRepository>();
            var added = new List<BudgetEntry>();
            repoMock.Setup(r => r.AddAsync(It.IsAny<BudgetEntry>()))
                    .Returns<BudgetEntry>(entry => { added.Add(entry); return Task.CompletedTask; });

            var logger = new Mock<ILogger<BudgetViewModel>>();
            var pdfMock = new Mock<WileyWidget.Services.Export.IPdfExportService>();
            var xlsMock = new Mock<WileyWidget.Services.Export.IExcelExportService>();
            var vm = new BudgetViewModel(logger.Object, repoMock.Object, pdfMock.Object, xlsMock.Object);

            // Act
            await vm.ImportFromCsvCommand.ExecuteAsync(tmp, TestContext.Current.CancellationToken);

            // Assert
            added.Count.Should().Be(2);
            vm.BudgetEntries.Should().HaveCount(2);
            vm.BudgetEntries[0].AccountNumber.Should().Be("410.1");

            // Cleanup
            File.Delete(tmp);
        }

        [Fact]
        public async Task LoadByYearCommand_UsesSelectedFiscalYearToLoadEntries()
        {
            // Arrange
            var repo = new Mock<IBudgetRepository>();
            var sample = new BudgetEntry { AccountNumber = "500.1", Description = "Sample", BudgetedAmount = 50m, ActualAmount = 5m, FiscalYear = 2025, DepartmentId = 1 };
            repo.Setup(r => r.GetByFiscalYearAsync(2025)).ReturnsAsync(new[] { sample });

            var logger = new Mock<ILogger<BudgetViewModel>>();
            var pdfMock = new Mock<WileyWidget.Services.Export.IPdfExportService>();
            var xlsMock = new Mock<WileyWidget.Services.Export.IExcelExportService>();
            var vm = new BudgetViewModel(logger.Object, repo.Object, pdfMock.Object, xlsMock.Object);

            // Act
            vm.SelectedFiscalYear = 2025;
            await vm.LoadByYearCommand.ExecuteAsync(null, TestContext.Current.CancellationToken);

            // Assert
            vm.BudgetEntries.Should().HaveCount(1);
            vm.BudgetEntries[0].FiscalYear.Should().Be(2025);
        }

        [Fact]
        public async Task ExportToCsvCommand_WritesFileWithHeaderAndRows()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var repoMock = new Mock<IBudgetRepository>();
                var logger = new Mock<ILogger<BudgetViewModel>>();
                var pdfMock = new Mock<WileyWidget.Services.Export.IPdfExportService>();
                var xlsMock = new Mock<WileyWidget.Services.Export.IExcelExportService>();

                var vm = new BudgetViewModel(logger.Object, repoMock.Object, pdfMock.Object, xlsMock.Object);
                vm.BudgetEntries.Add(new BudgetEntry { AccountNumber = "410.1", Description = "Office", BudgetedAmount = 100m, ActualAmount = 10m, FiscalYear = 2025, DepartmentId = 1 });
                vm.BudgetEntries.Add(new BudgetEntry { AccountNumber = "420.2", Description = "Travel", BudgetedAmount = 200m, ActualAmount = 50m, FiscalYear = 2025, DepartmentId = 2 });

                await vm.ExportToCsvCommand.ExecuteAsync(tmp, TestContext.Current.CancellationToken);

                File.Exists(tmp).Should().BeTrue();
                var content = await File.ReadAllTextAsync(tmp);
                content.Should().Contain("AccountNumber");
                content.Should().Contain("410.1");
                content.Should().Contain("420.2");
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        [Fact]
        public async Task ExportToPdfCommand_CreatesPdfFileUsingService()
        {
            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                var repoMock = new Mock<IBudgetRepository>();
                var logger = new Mock<ILogger<BudgetViewModel>>();
                var pdfMock = new Mock<WileyWidget.Services.Export.IPdfExportService>();
                var xlsMock = new Mock<WileyWidget.Services.Export.IExcelExportService>();

                var vm = new BudgetViewModel(logger.Object, repoMock.Object, pdfMock.Object, xlsMock.Object);
                vm.BudgetEntries.Add(new BudgetEntry { AccountNumber = "410.1", Description = "Office", BudgetedAmount = 100m, ActualAmount = 10m, FiscalYear = 2025, DepartmentId = 1 });

                pdfMock.Setup(p => p.ExportBudgetEntriesToPdfAsync(It.IsAny<IEnumerable<BudgetEntry>>(), tmp))
                       .Returns<IEnumerable<BudgetEntry>, string>((entries, path) => {
                           File.WriteAllText(path, "PDF");
                           return Task.FromResult(path);
                       });

                await vm.ExportToPdfCommand.ExecuteAsync(tmp, TestContext.Current.CancellationToken);

                File.Exists(tmp).Should().BeTrue();
                pdfMock.Verify(p => p.ExportBudgetEntriesToPdfAsync(It.IsAny<IEnumerable<BudgetEntry>>(), tmp), Times.Once);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        [Fact]
        public async Task ExportToExcelCommand_CreatesXlsxFileUsingService()
        {
            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                var repoMock = new Mock<IBudgetRepository>();
                var logger = new Mock<ILogger<BudgetViewModel>>();
                var pdfMock = new Mock<WileyWidget.Services.Export.IPdfExportService>();
                var xlsMock = new Mock<WileyWidget.Services.Export.IExcelExportService>();

                var vm = new BudgetViewModel(logger.Object, repoMock.Object, pdfMock.Object, xlsMock.Object);
                vm.BudgetEntries.Add(new BudgetEntry { AccountNumber = "420.2", Description = "Travel", BudgetedAmount = 200m, ActualAmount = 50m, FiscalYear = 2025, DepartmentId = 2 });

                xlsMock.Setup(x => x.ExportBudgetEntriesAsync(It.IsAny<IEnumerable<BudgetEntry>>(), tmp))
                       .Returns<IEnumerable<BudgetEntry>, string>((entries, path) => {
                           File.WriteAllText(path, "XLSX");
                           return Task.FromResult(path);
                       });

                await vm.ExportToExcelCommand.ExecuteAsync(tmp, TestContext.Current.CancellationToken);

                File.Exists(tmp).Should().BeTrue();
                xlsMock.Verify(x => x.ExportBudgetEntriesAsync(It.IsAny<IEnumerable<BudgetEntry>>(), tmp), Times.Once);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }
    }
}
