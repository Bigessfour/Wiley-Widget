using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.WinForms.Models;

namespace WileyWidget.WinForms.ViewModels
{
    public partial class AccountsViewModel : ObservableRecipient
    {
        private readonly ILogger<AccountsViewModel> _logger;
        private readonly AppDbContext _dbContext;

        [ObservableProperty]
        private string title = "Municipal Accounts";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<MunicipalAccountDisplay> accounts = new();

        // Keep a full list of rows returned from DB so we can re-filter in-memory quickly without re-querying unless caller asks for full reload
        private List<MunicipalAccountDisplay> _allAccounts = new();

        [ObservableProperty]
        private List<MunicipalFundType>? availableFunds;

        [ObservableProperty]
        private List<AccountType>? availableAccountTypes;

        [ObservableProperty]
        private MunicipalFundType? selectedFund;

        [ObservableProperty]
        private AccountType? selectedAccountType;

        [ObservableProperty]
        private decimal totalBalance;

        [ObservableProperty]
        private int activeAccountCount;

        public AccountsViewModel(
            ILogger<AccountsViewModel> logger,
            AppDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
            LoadAccountsCommand = new AsyncRelayCommand(LoadAccountsAsync);
            FilterAccountsCommand = new AsyncRelayCommand(FilterAccountsAsync);

            // Startup: populate selection lists and load accounts (fire-and-forget so UI can construct quickly)
#pragma warning disable CS4014
            InitializeAsync();
#pragma warning restore CS4014
        }

        private async Task InitializeAsync()
        {
            try
            {
                IsLoading = true;
                await PopulateAvailableFiltersAsync();
                await LoadAccountsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Accounts view model");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task PopulateAvailableFiltersAsync()
        {
            try
            {
                var funds = await _dbContext.MunicipalAccounts
                    .Select(a => a.Fund)
                    .Distinct()
                    .ToListAsync();

                AvailableFunds = funds.OrderBy(f => f.ToString()).ToList();

                var types = await _dbContext.MunicipalAccounts
                    .Select(a => a.Type)
                    .Distinct()
                    .ToListAsync();

                AvailableAccountTypes = types.OrderBy(t => t.ToString()).ToList();
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number == 4060)
            {
                // Login / database not found (Error 4060) — fall back to empty lists for UI continuity
                _logger.LogWarning(sqlEx, "DB login/database not found (4060) while loading filters — using empty filter lists.");
                AvailableFunds = new List<MunicipalFundType>();
                AvailableAccountTypes = new List<AccountType>();
            }
            catch (Exception retryEx) when (retryEx.GetType().Name == "RetryLimitExceededException")
            {
                _logger.LogError(retryEx, "DB retry limit exceeded while loading filters; returning empty lists.");
                AvailableFunds = new List<MunicipalFundType>();
                AvailableAccountTypes = new List<AccountType>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load AvailableFunds or AvailableAccountTypes");
                AvailableFunds = new List<MunicipalFundType>();
                AvailableAccountTypes = new List<AccountType>();
            }
        }

        public IAsyncRelayCommand LoadAccountsCommand { get; }
        public IAsyncRelayCommand FilterAccountsCommand { get; }

        // Summary calculators (exposed so UI/frameworks can reuse them)
        public TotalBalanceTableSummaryCalculator TotalBalanceCalculator { get; } = new TotalBalanceTableSummaryCalculator();
        public ActiveAccountCountTableSummaryCalculator ActiveAccountCountCalculator { get; } = new ActiveAccountCountTableSummaryCalculator();

        private async Task LoadAccountsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading municipal accounts");

                var accountsQuery = _dbContext.MunicipalAccounts
                    .Include(a => a.Department)
                    .Include(a => a.BudgetPeriod)
                    .Include(a => a.ParentAccount)
                    .Where(a => a.IsActive)
                    .AsNoTracking();

                // Apply filters if selected
                if (SelectedFund.HasValue)
                {
                    accountsQuery = accountsQuery.Where(a => a.Fund == SelectedFund.Value);
                }

                if (SelectedAccountType.HasValue)
                {
                    accountsQuery = accountsQuery.Where(a => a.Type == SelectedAccountType.Value);
                }

                var accountsList = await accountsQuery
                    .OrderBy(a => a.AccountNumber_Value)
                    .ToListAsync();

                // Convert to lightweight display models and cache full collection
                _allAccounts = accountsList.Select(account => new MunicipalAccountDisplay
                {
                    AccountNumber = account.AccountNumber?.Value ?? "N/A",
                    AccountName = account.Name,
                    DepartmentName = account.Department?.Name ?? "N/A",
                    FundType = account.Fund.ToString(),
                    AccountType = account.Type.ToString(),
                    CurrentBalance = account.Balance,
                    BudgetAmount = account.BudgetAmount,
                    IsActive = account.IsActive
                }).ToList();

                // Apply any in-memory filter (SelectedFund / SelectedAccountType)
                ApplyFiltersToAccounts();

                TotalBalance = TotalBalanceCalculator.Calculate(Accounts);
                ActiveAccountCount = ActiveAccountCountCalculator.Calculate(Accounts);

                _logger.LogInformation("Municipal accounts loaded successfully: {Count} accounts, Total Balance: {Balance:C}",
                    ActiveAccountCount, TotalBalance);
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number == 4060)
            {
                _logger.LogWarning(sqlEx, "DB login/database not found (4060) while loading accounts — showing empty results.");
                Accounts.Clear();
            }
            catch (Exception retryEx) when (retryEx.GetType().Name == "RetryLimitExceededException")
            {
                _logger.LogError(retryEx, "DB retry limit exceeded while loading accounts; returning empty results.");
                Accounts.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load municipal accounts");
                Accounts.Clear();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private Task FilterAccountsAsync()
        {
            _logger.LogInformation("Applying filters - Fund: {Fund}, Type: {Type}", SelectedFund, SelectedAccountType);

            try
            {
                ApplyFiltersToAccounts();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to filter accounts in-memory");
            }

            return Task.CompletedTask;
        }

        private void ApplyFiltersToAccounts()
        {
            var filtered = _allAccounts.AsEnumerable();

            if (SelectedFund.HasValue)
            {
                var fundString = SelectedFund.Value.ToString();
                filtered = filtered.Where(a => string.Equals(a.FundType, fundString, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedAccountType.HasValue)
            {
                var typeString = SelectedAccountType.Value.ToString();
                filtered = filtered.Where(a => string.Equals(a.AccountType, typeString, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();

            // Update observable collection in a simple and safe way
            Accounts.Clear();
            foreach (var row in filteredList)
            {
                Accounts.Add(row);
            }

            // Update summaries
            TotalBalance = TotalBalanceCalculator.Calculate(Accounts);
            ActiveAccountCount = ActiveAccountCountCalculator.Calculate(Accounts);
        }

        partial void OnSelectedFundChanged(MunicipalFundType? value)
        {
            // When the selected fund changes, refresh in-memory filter for a responsive UI
            _ = FilterAccountsCommand.ExecuteAsync(null);
        }

        partial void OnSelectedAccountTypeChanged(AccountType? value)
        {
            _ = FilterAccountsCommand.ExecuteAsync(null);
        }
    }


}
