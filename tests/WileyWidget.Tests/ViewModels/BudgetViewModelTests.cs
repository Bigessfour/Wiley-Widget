using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.Tests.ViewModels
{
    public class BudgetViewModelTests
    {
        [Fact]
        public async Task LoadBudgets_PopulatesAvailableEntitiesAndIncludesAllEntities()
        {
            // Arrange
            var mockBudgetRepo = new Mock<IBudgetRepository>();
            var mockEnterpriseRepo = new Mock<IEnterpriseRepository>();
            var mockReportExport = new Mock<WileyWidget.Services.Abstractions.IReportExportService>();
            var mockLogger = new Mock<ILogger<BudgetViewModel>>();

            int year = 2025;

            var entries = new List<BudgetEntry>
            {
                new BudgetEntry { Id = 1, Fund = new Fund { Name = "Sanitation District" }, FiscalYear = year },
                new BudgetEntry { Id = 2, Fund = new Fund { Name = "Town Utility" }, FiscalYear = year }
            };

            mockBudgetRepo.Setup(m => m.GetByFiscalYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(entries);
            mockEnterpriseRepo.Setup(m => m.GetAllAsync()).ReturnsAsync(new List<Enterprise>
            {
                new Enterprise { Name = "Town Utility" },
                new Enterprise { Name = "Wiley Sanitation District" }
            });

            var vm = new BudgetViewModel(mockLogger.Object, mockBudgetRepo.Object, mockReportExport.Object, mockEnterpriseRepo.Object);
            vm.SelectedFiscalYear = year;

            // Act
            await vm.LoadBudgetsCommand.ExecuteAsync(null);

            // Assert
            vm.AvailableEntities.Should().NotBeNull();
            vm.AvailableEntities.Should().Contain("All Entities");
            vm.AvailableEntities.Should().Contain("Sanitation District");
            vm.AvailableEntities.Should().Contain("Town Utility");
        }

        [Fact]
        public async Task ApplyFilters_SelectedEntityFiltersEntries()
        {
            // Arrange
            var mockBudgetRepo = new Mock<IBudgetRepository>();
            var mockReportExport = new Mock<WileyWidget.Services.Abstractions.IReportExportService>();
            var mockEnterpriseRepo = new Mock<IEnterpriseRepository>();
            var mockLogger = new Mock<ILogger<BudgetViewModel>>();

            var vm = new BudgetViewModel(mockLogger.Object, mockBudgetRepo.Object, mockReportExport.Object, mockEnterpriseRepo.Object);

            var entries = new[]
            {
                new BudgetEntry { Id = 1, Fund = new Fund { Name = "Sanitation District" }, AccountNumber = "410.1", Description = "Sewer" },
                new BudgetEntry { Id = 2, Fund = new Fund { Name = "Town Utility" }, AccountNumber = "420.1", Description = "Water" },
                new BudgetEntry { Id = 3, Fund = new Fund { Name = "Sanitation District" }, AccountNumber = "430.1", Description = "Sanitation Capital" }
            };

            vm.BudgetEntries = new ObservableCollection<BudgetEntry>(entries);

            // Select sanitation entity - should match two entries
            vm.SelectedEntity = "Sanitation District";

            // Act
            await vm.ApplyFiltersCommand.ExecuteAsync(null);

            // Assert
            vm.FilteredBudgetEntries.Should().HaveCount(2);
            vm.FilteredBudgetEntries.Select(e => e.Id).Should().Contain(new[] { 1, 3 });
        }
    }
}
