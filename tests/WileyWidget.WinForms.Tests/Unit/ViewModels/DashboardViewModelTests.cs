using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels
{
    public class DashboardViewModelTests
    {
        [Fact]
        public async Task LoadDashboard_StopsRetries_OnObjectDisposedException()
        {
            var budgetRepo = new ThrowingBudgetRepo();
            var accountRepo = new FakeAccountRepo();

            using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance);

            // Execute the load command (should not throw) and should stop retries on ObjectDisposedException
            await vm.LoadCommand.ExecuteAsync(null);

            Assert.False(vm.IsLoading);
            Assert.Contains("aborted", vm.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, budgetRepo.GetBudgetSummaryCallCount);
        }

        private class ThrowingBudgetRepo : IBudgetRepository
        {
            public int GetBudgetSummaryCallCount { get; private set; }

            public Task<IEnumerable<BudgetEntry>> GetBudgetHierarchyAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BudgetEntry>>(Array.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BudgetEntry>>(Array.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetByFundAsync(int fundId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BudgetEntry>>(Array.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BudgetEntry>>(Array.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetByFundAndFiscalYearAsync(int fundId, int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BudgetEntry>>(Array.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetByDepartmentAndFiscalYearAsync(int departmentId, int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BudgetEntry>>(Array.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetSewerBudgetEntriesAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BudgetEntry>>(Array.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BudgetEntry>>(Array.Empty<BudgetEntry>());
            public Task<BudgetEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<BudgetEntry?>(null);
            public Task AddAsync(BudgetEntry budgetEntry, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task UpdateAsync(BudgetEntry budgetEntry, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<BudgetVarianceAnalysis> GetBudgetSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
            {
                GetBudgetSummaryCallCount++;
                throw new ObjectDisposedException("TestBudgetRepo");
            }
            public Task<BudgetVarianceAnalysis> GetVarianceAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new BudgetVarianceAnalysis());
            public Task<List<DepartmentSummary>> GetDepartmentBreakdownAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new List<DepartmentSummary>());
            public Task<List<FundSummary>> GetFundAllocationsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new List<FundSummary>());
            public Task<BudgetVarianceAnalysis> GetYearEndSummaryAsync(int year, CancellationToken cancellationToken = default) => Task.FromResult(new BudgetVarianceAnalysis());
            public Task<BudgetVarianceAnalysis> GetBudgetSummaryByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new BudgetVarianceAnalysis());
            public Task<BudgetVarianceAnalysis> GetVarianceAnalysisByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new BudgetVarianceAnalysis());
            public Task<List<DepartmentSummary>> GetDepartmentBreakdownByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new List<DepartmentSummary>());
            public Task<List<FundSummary>> GetFundAllocationsByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new List<FundSummary>());
            public Task<(int TotalRecords, DateTime? OldestRecord, DateTime? NewestRecord)> GetDataStatisticsAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult<(int TotalRecords, DateTime? OldestRecord, DateTime? NewestRecord)>((0, null, null));
            public Task<int> GetRevenueAccountCountAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult<int>(0);
            public Task<int> GetExpenseAccountCountAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult<int>(0);
        }

        private class FakeAccountRepo : IMunicipalAccountRepository
        {
            public Task<IEnumerable<MunicipalAccount>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<MunicipalAccount>>(Array.Empty<MunicipalAccount>());
            public Task<MunicipalAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<MunicipalAccount?>(null);
            public Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default) => Task.FromResult<MunicipalAccount?>(null);
            public Task<IEnumerable<MunicipalAccount>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<MunicipalAccount>>(Array.Empty<MunicipalAccount>());
            public Task<MunicipalAccount> AddAsync(MunicipalAccount account, CancellationToken cancellationToken = default) => Task.FromResult(account);
            public Task<MunicipalAccount> UpdateAsync(MunicipalAccount account, CancellationToken cancellationToken = default) => Task.FromResult(account);
            public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(true);
            public Task SyncFromQuickBooksAsync(List<Intuit.Ipp.Data.Account> qbAccounts, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task ImportChartOfAccountsAsync(List<Intuit.Ipp.Data.Account> chartAccounts, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<object> GetBudgetAnalysisAsync(int periodId, CancellationToken cancellationToken = default) => Task.FromResult<object>(new object());
            public Task<IEnumerable<MunicipalAccount>> GetByFundAsync(MunicipalFundType fund, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<MunicipalAccount>>(Array.Empty<MunicipalAccount>());
            public Task<IEnumerable<MunicipalAccount>> GetByTypeAsync(AccountType type, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<MunicipalAccount>>(Array.Empty<MunicipalAccount>());
            public Task<IEnumerable<MunicipalAccount>> GetAllWithRelatedAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<MunicipalAccount>>(Array.Empty<MunicipalAccount>());
            public Task<BudgetPeriod?> GetCurrentActiveBudgetPeriodAsync(CancellationToken cancellationToken = default) => Task.FromResult<BudgetPeriod?>(null);
            public Task<int> GetCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        }
    }
}
