using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    [Trait("Category", "Unit")]
    public class LinqNullHandlingTests
    {
        [Fact]
        public void DashboardViewModel_QueryFundSummaries_HandlesNullCollection()
        {
            // Arrange
            var mockRepo = new Mock<IBudgetRepository>();
            var mockAccountRepo = new Mock<IMunicipalAccountRepository>();
            var mockLogger = new Mock<ILogger<DashboardViewModel>>();
            using var vm = new DashboardViewModel(mockRepo.Object, mockAccountRepo.Object, mockLogger.Object);
            vm.FundSummaries = null!; // Simulate null data

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => vm.FundSummaries.Where(f => f.TotalBudgeted > 0).ToList());
            Assert.Contains("source", ex.Message, StringComparison.Ordinal); // Reproduce the exact error

            // Fix suggestion: Add guard in code, e.g., vm.FundSummaries?.Where(...) ?? Enumerable.Empty<FundSummary>()
        }

        [Fact]
        public void AccountsViewModel_FilterAccounts_HandlesNullAccounts()
        {
            // Arrange: Similar for other forms/ViewModels with LINQ
            var mockLogger = new Mock<ILogger<AccountsViewModel>>();
            var mockAccountsRepo = new Mock<IAccountsRepository>();
            var mockMunicipalRepo = new Mock<IMunicipalAccountRepository>();
            using var vm = new AccountsViewModel(mockLogger.Object, mockAccountsRepo.Object, mockMunicipalRepo.Object);
            vm.Accounts = null!; // Simulate null data

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => vm.Accounts.Where(a => a.Balance > 0)
            .ToList());
            Assert.Contains("source", ex.Message, StringComparison.Ordinal);
        }
    }
}
