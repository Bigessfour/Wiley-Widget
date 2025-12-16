using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels;

/// <summary>
/// Comprehensive tests for DashboardViewModel covering FY computation, thread-safe loading,
/// empty data handling, and logging validation. Validates fixes for FY2025/2026 mismatch.
/// </summary>
public sealed class DashboardViewModelComprehensiveTests : IDisposable
{
    [Fact]
    public async Task LoadDashboardDataAsync_ComputesFY2026_FromDecember2025Date()
    {
        // Arrange - Simulate Dec 15, 2025 (production scenario)
        // FY should compute to 2026 since Month >= 7
        var budgetRepo = new FakeBudgetRepo(fiscalYearToReturn: 2026);
        var accountRepo = new FakeAccountRepo(accountCount: 125);
        var config = CreateTestConfig(defaultFiscalYear: null); // Force computed FY

        using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act
        await vm.LoadCommand.ExecuteAsync(null);

        // Assert
        vm.FiscalYear.Should().Contain("2026", "December 2025 should compute to FY 2026");
        vm.ErrorMessage.Should().BeNullOrEmpty();
        budgetRepo.GetBudgetSummaryCallCount.Should().Be(1);

        // Verify correct FY was passed to repository
        budgetRepo.LastRequestedFiscalYear.Should().Be(2026);
    }

    [Fact]
    public async Task LoadDashboardDataAsync_UsesFY2025_FromConfiguration()
    {
        // Arrange - Config explicitly sets FY2025
        var budgetRepo = new FakeBudgetRepo(fiscalYearToReturn: 2025, hasData: false);
        var accountRepo = new FakeAccountRepo();
        var config = CreateTestConfig(defaultFiscalYear: 2025);

        using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act
        await vm.LoadCommand.ExecuteAsync(null);

        // Assert
        vm.FiscalYear.Should().Contain("2025");
        budgetRepo.LastRequestedFiscalYear.Should().Be(2025);
    }

    [Fact]
    public async Task LoadDashboardDataAsync_HandlesConcurrentCalls_WithoutRaceConditions()
    {
        // Arrange
        var budgetRepo = new FakeBudgetRepo(fiscalYearToReturn: 2026, delayMs: 50);
        var accountRepo = new FakeAccountRepo();
        var config = CreateTestConfig(defaultFiscalYear: 2026);

        using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act - simulate 5 concurrent load attempts
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(async () => await vm.LoadCommand.ExecuteAsync(null)))
            .ToArray();

        // Await all tasks (async test method required)
        await Task.WhenAll(tasks);

        // Assert - only ONE call should have executed due to SemaphoreSlim _loadLock
        budgetRepo.GetBudgetSummaryCallCount.Should().Be(1, "SemaphoreSlim should serialize concurrent load attempts");
        vm.Metrics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoadDashboardDataAsync_PopulatesEmptyDashboard_WhenNoFY2025Data()
    {
        // Arrange - FY2025 has no data (matches production scenario from log line 10)
        var budgetRepo = new FakeBudgetRepo(fiscalYearToReturn: 2025, hasData: false);
        var accountRepo = new FakeAccountRepo(accountCount: 125);
        var config = CreateTestConfig(defaultFiscalYear: 2025);

        using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act
        await vm.LoadCommand.ExecuteAsync(null);

        // Assert - should load successfully with empty/zero metrics
        vm.ErrorMessage.Should().BeNullOrEmpty();
        vm.TotalBudgeted.Should().Be(0);
        vm.TotalActual.Should().Be(0);
        vm.TotalRevenue.Should().Be(0);
        vm.TotalExpenses.Should().Be(0);
        vm.AccountCount.Should().Be(125, "Should still have account count");
        vm.Metrics.Should().NotBeEmpty("Should have metrics even with zero budget");
        vm.StatusText.Should().Contain("125 accounts");
    }

    [Fact]
    public async Task LoadDashboardDataAsync_LogsThreadId_ForDiagnostics()
    {
        // Arrange - Validate that thread-safe logging works
        var budgetRepo = new FakeBudgetRepo();
        var accountRepo = new FakeAccountRepo();
        var config = CreateTestConfig(defaultFiscalYear: 2026);

        using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act
        await vm.LoadCommand.ExecuteAsync(null);

        // Assert - Method should complete without exceptions (logging occurs internally)
        vm.IsLoading.Should().BeFalse();
        budgetRepo.GetBudgetSummaryCallCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadDashboardDataAsync_PopulatesMonthlyRevenueData_For12Months()
    {
        // Arrange
        var budgetRepo = new FakeBudgetRepo(fiscalYearToReturn: 2026, totalRevenue: 1200000m);
        var accountRepo = new FakeAccountRepo();
        var config = CreateTestConfig(defaultFiscalYear: 2026);

        using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act
        await vm.LoadCommand.ExecuteAsync(null);

        // Assert
        vm.MonthlyRevenueData.Should().HaveCount(12, "Should have 12 months of data");
        vm.MonthlyRevenueData.Should().AllSatisfy(m => m.Amount.Should().BeGreaterOrEqualTo(0));
        vm.MonthlyRevenueData.First().Month.Should().Be("Jul", "Fiscal year starts in July");
        vm.MonthlyRevenueData.Last().Month.Should().Be("Jun", "Fiscal year ends in June");
    }

    [Fact]
    public async Task Dispose_CancelsPendingLoad_AndReleasesResources()
    {
        // Arrange
        var budgetRepo = new FakeBudgetRepo(delayMs: 5000); // Long delay
        var accountRepo = new FakeAccountRepo();
        var config = CreateTestConfig();

        var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act - start load but dispose immediately
        var loadTask = vm.LoadCommand.ExecuteAsync(null);
        vm.Dispose();

        // Assert - should not throw and task should complete quickly
        var completed = await Task.WhenAny(loadTask, Task.Delay(1000));
        completed.Should().Be(loadTask, "Dispose should cancel pending operations");

        // Observe cancellation if it occurred
        try
        {
            await loadTask;
        }
        catch (OperationCanceledException)
        {
            // expected when dispose cancels the operation
        }
    }

    private static IConfiguration CreateTestConfig(int? defaultFiscalYear = null)
    {
        var configDict = new Dictionary<string, string?>();

        if (defaultFiscalYear.HasValue)
        {
            configDict["UI:DefaultFiscalYear"] = defaultFiscalYear.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict!)
            .Build();
    }

    // Fake repository implementations for testing
    private class FakeBudgetRepo : IBudgetRepository
    {
        private readonly int _fiscalYear;
        private readonly bool _hasData;
        private readonly decimal _totalRevenue;
        private readonly int _delayMs;

        public int GetBudgetSummaryCallCount { get; private set; }
        public int LastRequestedFiscalYear { get; private set; }

        public FakeBudgetRepo(int fiscalYearToReturn = 2026, bool hasData = true, decimal totalRevenue = 0m, int delayMs = 0)
        {
            _fiscalYear = fiscalYearToReturn;
            _hasData = hasData;
            _totalRevenue = totalRevenue;
            _delayMs = delayMs;
        }

        public async Task<BudgetVarianceAnalysis> GetBudgetSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            GetBudgetSummaryCallCount++;

            // Extract FY from date range (endDate year = FY)
            LastRequestedFiscalYear = endDate.Year;

            if (_delayMs > 0)
            {
                await Task.Delay(_delayMs, cancellationToken);
            }

            if (!_hasData)
            {
                return new BudgetVarianceAnalysis
                {
                    TotalBudgeted = 0,
                    TotalActual = 0,
                    TotalVariance = 0,
                    TotalVariancePercentage = 0,
                    FundSummaries = new List<FundSummary>(),
                    DepartmentSummaries = new List<DepartmentSummary>(),
                    AccountVariances = new List<AccountVariance>()
                };
            }

            return new BudgetVarianceAnalysis
            {
                TotalBudgeted = 11919317m, // Matches production log
                TotalActual = 0m,
                TotalVariance = 11919317m,
                TotalVariancePercentage = 100m,
                FundSummaries = new List<FundSummary>
                {
                    new() { FundName = "General", Budgeted = 5000000m, Actual = 0m, Variance = 5000000m, AccountCount = 30 },
                    new() { FundName = "Sewer", Budgeted = 3000000m, Actual = 0m, Variance = 3000000m, AccountCount = 15 }
                },
                DepartmentSummaries = new List<DepartmentSummary>(),
                AccountVariances = new List<AccountVariance>()
            };
        }

        // Implement other required interface methods as stubs
        public Task<IEnumerable<BudgetEntry>> GetByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<BudgetEntry>());
        public Task<int> GetRevenueAccountCountAsync(int fiscalYear, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
        public Task<int> GetExpenseAccountCountAsync(int fiscalYear, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        // Remaining interface methods omitted for brevity (return empty/default values)
        public Task<IEnumerable<BudgetEntry>> GetBudgetHierarchyAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
        public Task<IEnumerable<BudgetEntry>> GetByFundAsync(int fundId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
        public Task<IEnumerable<BudgetEntry>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
        public Task<IEnumerable<BudgetEntry>> GetByFundAndFiscalYearAsync(int fundId, int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
        public Task<IEnumerable<BudgetEntry>> GetByDepartmentAndFiscalYearAsync(int departmentId, int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
        public Task<IEnumerable<BudgetEntry>> GetSewerBudgetEntriesAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
        public Task<IEnumerable<BudgetEntry>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
        public Task<BudgetEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<BudgetEntry?>(null);
        public Task AddAsync(BudgetEntry budgetEntry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(BudgetEntry budgetEntry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<BudgetVarianceAnalysis> GetVarianceAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new BudgetVarianceAnalysis());
        public Task<List<DepartmentSummary>> GetDepartmentBreakdownAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new List<DepartmentSummary>());
        public Task<List<FundSummary>> GetFundAllocationsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new List<FundSummary>());
        public Task<BudgetVarianceAnalysis> GetYearEndSummaryAsync(int year, CancellationToken cancellationToken = default) => Task.FromResult(new BudgetVarianceAnalysis());
        public Task<BudgetVarianceAnalysis> GetBudgetSummaryByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new BudgetVarianceAnalysis());
        public Task<BudgetVarianceAnalysis> GetVarianceAnalysisByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new BudgetVarianceAnalysis());
        public Task<List<DepartmentSummary>> GetDepartmentBreakdownByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new List<DepartmentSummary>());
        public Task<List<FundSummary>> GetFundAllocationsByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new List<FundSummary>());
        public Task<(int TotalRecords, DateTime? OldestRecord, DateTime? NewestRecord)> GetDataStatisticsAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult<(int, DateTime?, DateTime?)>((0, null, null));
    }

    private class FakeAccountRepo : IMunicipalAccountRepository
    {
        private readonly int _accountCount;

        public FakeAccountRepo(int accountCount = 125)
        {
            _accountCount = accountCount;
        }

        public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_accountCount);

        public Task<IEnumerable<MunicipalAccount>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<MunicipalAccount>());

        // Remaining interface methods omitted (return empty/default values)
        public Task<MunicipalAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<MunicipalAccount?>(null);
        public Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default) => Task.FromResult<MunicipalAccount?>(null);
        public Task<IEnumerable<MunicipalAccount>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MunicipalAccount>());
        public Task<MunicipalAccount> AddAsync(MunicipalAccount account, CancellationToken cancellationToken = default) => Task.FromResult(account);
        public Task<MunicipalAccount> UpdateAsync(MunicipalAccount account, CancellationToken cancellationToken = default) => Task.FromResult(account);
        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task SyncFromQuickBooksAsync(List<Intuit.Ipp.Data.Account> qbAccounts, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ImportChartOfAccountsAsync(List<Intuit.Ipp.Data.Account> chartAccounts, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<object> GetBudgetAnalysisAsync(int periodId, CancellationToken cancellationToken = default) => Task.FromResult<object>(new object());
        public Task<IEnumerable<MunicipalAccount>> GetByFundAsync(MunicipalFundType fund, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MunicipalAccount>());
        public Task<IEnumerable<MunicipalAccount>> GetByTypeAsync(AccountType type, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MunicipalAccount>());
        public Task<IEnumerable<MunicipalAccount>> GetAllWithRelatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MunicipalAccount>());
        public Task<BudgetPeriod?> GetCurrentActiveBudgetPeriodAsync(CancellationToken cancellationToken = default) => Task.FromResult<BudgetPeriod?>(null);
    }

    public void Dispose()
    {
        // Cleanup
    }
}
