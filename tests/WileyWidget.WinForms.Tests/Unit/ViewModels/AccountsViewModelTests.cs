using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels
{
    /// <summary>
    /// Unit tests for AccountsViewModel - tests account loading, filtering, and command execution.
    /// </summary>
    public class AccountsViewModelTests : IDisposable
    {
        private readonly Mock<ILogger<AccountsViewModel>> _mockLogger;
        private readonly AppDbContext _dbContext;
        private readonly AccountsViewModel _viewModel;

        public AccountsViewModelTests()
        {
            _mockLogger = new Mock<ILogger<AccountsViewModel>>();

            // Create in-memory database for testing
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"AccountsTestDb_{Guid.NewGuid()}")
                .Options;

            _dbContext = new AppDbContext(options);
            _viewModel = new AccountsViewModel(_mockLogger.Object, _dbContext);
        }

        [Fact]
        public void Constructor_WithValidDependencies_InitializesViewModel()
        {
            // Arrange & Act
            var vm = new AccountsViewModel(_mockLogger.Object, _dbContext);

            // Assert
            Assert.NotNull(vm);
            Assert.NotNull(vm.Accounts);
            Assert.NotNull(vm.LoadAccountsCommand);
            Assert.NotNull(vm.FilterAccountsCommand);
            Assert.NotNull(vm.AvailableFunds);
            Assert.NotNull(vm.AvailableAccountTypes);
            Assert.False(vm.IsLoading);
            Assert.Equal("Municipal Accounts", vm.Title);
        }

        [Fact]
        public async Task LoadAccountsCommand_WhenExecuted_LoadsAccountsFromDatabase()
        {
            // Arrange
            await SeedTestData();

            // Act
            await _viewModel.LoadAccountsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal(3, _viewModel.Accounts.Count);
            Assert.True(_viewModel.Accounts.All(a => a.IsActive));
        }

        [Fact]
        public async Task LoadAccountsCommand_WhenExecuted_SetsIsLoadingFlag()
        {
            // Arrange
            var loadingStates = new List<bool>();
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AccountsViewModel.IsLoading))
                {
                    loadingStates.Add(_viewModel.IsLoading);
                }
            };

            await SeedTestData();

            // Act
            await _viewModel.LoadAccountsCommand.ExecuteAsync(null);

            // Assert
            Assert.Contains(true, loadingStates); // Started loading
            Assert.Contains(false, loadingStates); // Finished loading
            Assert.False(_viewModel.IsLoading); // Final state
        }

        [Fact]
        public async Task LoadAccountsCommand_CalculatesTotalBalance()
        {
            // Arrange
            await SeedTestData();

            // Act
            await _viewModel.LoadAccountsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal(7500m, _viewModel.TotalBalance); // 1000 + 2500 + 4000
            Assert.Equal(3, _viewModel.ActiveAccountCount);
        }

        [Fact]
        public async Task FilterAccountsCommand_WithFundFilter_FiltersCorrectly()
        {
            // Arrange
            await SeedTestData();
            await _viewModel.LoadAccountsCommand.ExecuteAsync(null);

            // Act
            _viewModel.SelectedFund = MunicipalFundType.General;
            await _viewModel.FilterAccountsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal(2, _viewModel.Accounts.Count);
            Assert.All(_viewModel.Accounts, a => Assert.Equal("General", a.FundName));
        }

        [Fact]
        public async Task FilterAccountsCommand_WithAccountTypeFilter_FiltersCorrectly()
        {
            // Arrange
            await SeedTestData();
            await _viewModel.LoadAccountsCommand.ExecuteAsync(null);

            // Act
            _viewModel.SelectedAccountType = AccountType.Asset;
            await _viewModel.FilterAccountsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal(2, _viewModel.Accounts.Count);
            Assert.All(_viewModel.Accounts, a => Assert.Equal("Asset", a.AccountType));
        }

        [Fact]
        public async Task FilterAccountsCommand_WithMultipleFilters_AppliesBothFilters()
        {
            // Arrange
            await SeedTestData();
            await _viewModel.LoadAccountsCommand.ExecuteAsync(null);

            // Act
            _viewModel.SelectedFund = MunicipalFundType.General;
            _viewModel.SelectedAccountType = AccountType.Asset;
            await _viewModel.FilterAccountsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal(2, _viewModel.Accounts.Count);
            Assert.All(_viewModel.Accounts, a => Assert.Equal("General", a.FundName));
            Assert.All(_viewModel.Accounts, a => Assert.Equal("Asset", a.AccountType));
        }

        [Fact]
        public async Task FilterAccountsCommand_ClearsFilter_ShowsAllAccounts()
        {
            // Arrange
            await SeedTestData();
            await _viewModel.LoadAccountsCommand.ExecuteAsync(null);

            _viewModel.SelectedFund = MunicipalFundType.General;
            await _viewModel.FilterAccountsCommand.ExecuteAsync(null);
            Assert.Equal(2, _viewModel.Accounts.Count);

            // Act - Clear filter
            _viewModel.SelectedFund = null;
            await _viewModel.FilterAccountsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal(3, _viewModel.Accounts.Count);
        }

        [Fact]
        public void PropertyChanged_IsRaisedForObservableProperties()
        {
            // Arrange
            var propertyChangedEvents = new List<string>();
            _viewModel.PropertyChanged += (s, e) => propertyChangedEvents.Add(e.PropertyName!);

            // Act
            _viewModel.SelectedFund = MunicipalFundType.General;
            _viewModel.SelectedAccountType = AccountType.Asset;
            _viewModel.IsLoading = true;

            // Assert
            Assert.Contains(nameof(AccountsViewModel.SelectedFund), propertyChangedEvents);
            Assert.Contains(nameof(AccountsViewModel.SelectedAccountType), propertyChangedEvents);
            Assert.Contains(nameof(AccountsViewModel.IsLoading), propertyChangedEvents);
        }

        [Fact]
        public async Task LoadAccountsCommand_WithEmptyDatabase_ReturnsEmptyCollection()
        {
            // Arrange - no seed data

            // Act
            await _viewModel.LoadAccountsCommand.ExecuteAsync(null);

            // Assert
            Assert.Empty(_viewModel.Accounts);
            Assert.Equal(0m, _viewModel.TotalBalance);
            Assert.Equal(0, _viewModel.ActiveAccountCount);
        }

        [Fact]
        public void AvailableFunds_ContainsAllExpectedFundTypes()
        {
            // Assert
            Assert.Contains(MunicipalFundType.General, _viewModel.AvailableFunds);
            Assert.Contains(MunicipalFundType.Enterprise, _viewModel.AvailableFunds);
            Assert.Contains(MunicipalFundType.SpecialRevenue, _viewModel.AvailableFunds);
            Assert.Contains(MunicipalFundType.CapitalProjects, _viewModel.AvailableFunds);
            Assert.Contains(MunicipalFundType.DebtService, _viewModel.AvailableFunds);
        }

        [Fact]
        public void AvailableAccountTypes_ContainsAllExpectedTypes()
        {
            // Assert
            Assert.Contains(AccountType.Asset, _viewModel.AvailableAccountTypes);
            Assert.Contains(AccountType.Payables, _viewModel.AvailableAccountTypes);
            Assert.Contains(AccountType.RetainedEarnings, _viewModel.AvailableAccountTypes);
            Assert.Contains(AccountType.Revenue, _viewModel.AvailableAccountTypes);
            Assert.Contains(AccountType.Expense, _viewModel.AvailableAccountTypes);
        }

        private async Task SeedTestData()
        {
            var department = new Department { Id = 1, Name = "Administration", DepartmentCode = "ADMIN" };
            var budgetPeriod = new BudgetPeriod
            {
                Id = 1,
                Year = 2026,
                StartDate = new DateTime(2025, 7, 1),
                EndDate = new DateTime(2026, 6, 30)
            };

            _dbContext.Departments.Add(department);
            _dbContext.BudgetPeriods.Add(budgetPeriod);

            var accounts = new[]
            {
                new MunicipalAccount
                {
                    Id = 1,
                    AccountNumber = new AccountNumber("100-001"),
                    Name = "Cash",
                    Fund = MunicipalFundType.General,
                    Type = AccountType.Asset,
                    Balance = 1000m,
                    BudgetAmount = 1500m,
                    IsActive = true,
                    DepartmentId = 1,
                    BudgetPeriodId = 1,
                    FundDescription = "General Fund Cash"
                },
                new MunicipalAccount
                {
                    Id = 2,
                    AccountNumber = new AccountNumber("200-002"),
                    Name = "Accounts Receivable",
                    Fund = MunicipalFundType.General,
                    Type = AccountType.Asset,
                    Balance = 2500m,
                    BudgetAmount = 3000m,
                    IsActive = true,
                    DepartmentId = 1,
                    BudgetPeriodId = 1,
                    FundDescription = "General Fund Receivables"
                },
                new MunicipalAccount
                {
                    Id = 3,
                    AccountNumber = new AccountNumber("300-003"),
                    Name = "Revenue",
                    Fund = MunicipalFundType.Enterprise,
                    Type = AccountType.Revenue,
                    Balance = 4000m,
                    BudgetAmount = 5000m,
                    IsActive = true,
                    DepartmentId = 1,
                    BudgetPeriodId = 1,
                    FundDescription = "Enterprise Revenue"
                }
            };

            _dbContext.MunicipalAccounts.AddRange(accounts);
            await _dbContext.SaveChangesAsync();
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
        }
    }
}
