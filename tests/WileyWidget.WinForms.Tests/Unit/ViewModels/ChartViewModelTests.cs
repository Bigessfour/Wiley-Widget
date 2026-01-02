using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels
{
    [Trait("Category", "Unit")]
    public class ChartViewModelTests
    {
        [Fact]
        public void ExportData_WritesCsvToProvidedPath()
        {
            // Arrange
            var tmp = Path.Combine(Path.GetTempPath(), "wileytests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tmp);

            var pathProvider = new FakePathProvider(tmp);
            var logger = NullLogger<ChartViewModel>.Instance;
            var dashboardService = new Mock<IDashboardService>();
            var budgetRepo = new Mock<IBudgetRepository>();
            var config = new ConfigurationBuilder().Build();

            var vm = new ChartViewModel(logger, dashboardService.Object, budgetRepo.Object, pathProvider, config);

            vm.DepartmentDetails.Add(new DepartmentSummary
            {
                DepartmentName = "Public Works",
                Budgeted = 100000m,
                Actual = 80000m,
                Variance = 20000m,
                VariancePercentage = 20m
            });

            // Act
            vm.ExportDataCommand.Execute(null);

            // Assert - wait for LastExportedFilePath or error for robustness under heavy test load
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline && vm.LastExportedFilePath == null && string.IsNullOrEmpty(vm.ErrorMessage))
            {
                System.Threading.Thread.Sleep(50);
            }

            // If LastExportedFilePath wasn't set, fallback to scanning the directory (backward compatible)
            string[] files = Array.Empty<string>();
            if (vm.LastExportedFilePath != null)
            {
                files = new[] { vm.LastExportedFilePath };
            }
            else if (string.IsNullOrEmpty(vm.ErrorMessage))
            {
                files = Directory.GetFiles(tmp, "BudgetChart_FY*.csv");
            }

            files.Length.Should().Be(1, because: vm.ErrorMessage ?? "Expected exactly one export file to exist");

            // Verify ViewModel reported no errors
            // (helps diagnose failures when file creation silently fails)
            vm.ErrorMessage.Should().BeNullOrEmpty();

            var content = File.ReadAllText(files[0]);
            content.Should().Contain("Public Works");

            // Cleanup
            try { Directory.Delete(tmp, true); } catch { }
        }

        private class FakePathProvider : IPathProvider
        {
            private readonly string _path;
            public FakePathProvider(string path) => _path = path;
            public string GetExportDirectory() => _path;
        }
    }
}
