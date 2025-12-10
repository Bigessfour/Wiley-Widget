using System;
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

        [ObservableProperty]
        private MunicipalFundType? selectedFund;

        [ObservableProperty]
        private AccountType? selectedAccountType;

        public ObservableCollection<MunicipalFundType> AvailableFunds { get; } = new(new[]
        {
            MunicipalFundType.General,
            MunicipalFundType.Enterprise,
            MunicipalFundType.SpecialRevenue,
            MunicipalFundType.CapitalProjects,
            MunicipalFundType.DebtService
        });

        public ObservableCollection<AccountType> AvailableAccountTypes { get; } = new(new[]
        {
            AccountType.Asset,
            AccountType.Liability,
            AccountType.Equity,
            AccountType.Revenue,
            AccountType.Expense
        });

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
        }

        public IAsyncRelayCommand LoadAccountsCommand { get; }
        public IAsyncRelayCommand FilterAccountsCommand { get; }

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

                Accounts.Clear();

                foreach (var account in accountsList)
                {
                    Accounts.Add(new MunicipalAccountDisplay
                    {
                        Id = account.Id,
                        AccountNumber = account.AccountNumber?.Value ?? "N/A",
                        AccountName = account.Name,
                        Description = account.FundDescription ?? string.Empty,
                        AccountType = account.Type.ToString(),
                        FundName = account.Fund.ToString(),
                        CurrentBalance = account.Balance,
                        BudgetAmount = account.BudgetAmount,
                        Department = account.Department?.Name ?? "N/A",
                        IsActive = account.IsActive,
                        HasParent = account.ParentAccountId.HasValue
                    });
                }

                TotalBalance = Accounts.Sum(a => a.CurrentBalance);
                ActiveAccountCount = Accounts.Count;

                _logger.LogInformation("Municipal accounts loaded successfully: {Count} accounts, Total Balance: {Balance:C}",
                    ActiveAccountCount, TotalBalance);
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
            return LoadAccountsAsync();
        }
    }

}
