using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.Models;

namespace WileyWidget.WinForms.ViewModels
{
    public partial class AccountsViewModel : ObservableRecipient
    {
        private readonly ILogger<AccountsViewModel> _logger;
        /// <summary>
        /// Represents the _accountsrepository.
        /// </summary>
        /// <summary>
        /// Represents the _accountsrepository.
        /// </summary>
        private readonly IAccountsRepository _accountsRepository;
        /// <summary>
        /// Represents the _municipalaccountrepository.
        /// </summary>
        private readonly IMunicipalAccountRepository _municipalAccountRepository;

        [ObservableProperty]
        private string title = "Municipal Accounts";

        [ObservableProperty]
        /// <summary>
        /// Represents the isloading.
        /// </summary>
        /// <summary>
        /// Represents the isloading.
        /// </summary>
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
            AccountType.Payables,
            AccountType.RetainedEarnings,
            AccountType.Revenue,
            AccountType.Expense
        });

        [ObservableProperty]
        /// <summary>
        /// Represents the totalbalance.
        /// </summary>
        /// <summary>
        /// Represents the totalbalance.
        /// </summary>
        private decimal totalBalance;

        [ObservableProperty]
        /// <summary>
        /// Represents the activeaccountcount.
        /// </summary>
        private int activeAccountCount;

        public AccountsViewModel(
            ILogger<AccountsViewModel> logger,
            IAccountsRepository accountsRepository,
            IMunicipalAccountRepository municipalAccountRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _accountsRepository = accountsRepository ?? throw new ArgumentNullException(nameof(accountsRepository));
            _municipalAccountRepository = municipalAccountRepository ?? throw new ArgumentNullException(nameof(municipalAccountRepository));

            LoadAccountsCommand = new AsyncRelayCommand(LoadAccountsAsync);
            FilterAccountsCommand = new AsyncRelayCommand(FilterAccountsAsync);
        }
        /// <summary>
        /// Gets or sets the loadaccountscommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the loadaccountscommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the loadaccountscommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the loadaccountscommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the loadaccountscommand.
        /// </summary>



        public IAsyncRelayCommand LoadAccountsCommand { get; }
        /// <summary>
        /// Gets or sets the filteraccountscommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the filteraccountscommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the filteraccountscommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the filteraccountscommand.
        /// </summary>
        public IAsyncRelayCommand FilterAccountsCommand { get; }

        public async Task<List<Department>> GetDepartmentsAsync()
        {
            // Use MunicipalAccountRepository to get departments
            // Note: This requires adding GetDepartmentsAsync to IMunicipalAccountRepository
            // For now, return empty list - implement when repository interface is extended
            await Task.CompletedTask;
            _logger.LogWarning("GetDepartmentsAsync not yet implemented in repository pattern");
            return new List<Department>();
        }

        private async Task LoadAccountsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading municipal accounts");

                IReadOnlyList<MunicipalAccount> accountsList;

                // Apply filters if selected
                if (SelectedFund.HasValue && SelectedAccountType.HasValue)
                {
                    accountsList = await _accountsRepository!.GetAccountsByFundAndTypeAsync(
                        SelectedFund.Value,
                        SelectedAccountType.Value);
                }
                else if (SelectedFund.HasValue)
                {
                    accountsList = await _accountsRepository!.GetAccountsByFundAsync(SelectedFund.Value);
                }
                else if (SelectedAccountType.HasValue)
                {
                    accountsList = await _accountsRepository!.GetAccountsByTypeAsync(SelectedAccountType.Value);
                }
                else
                {
                    accountsList = await _accountsRepository!.GetAllAccountsAsync();
                }

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
        /// <summary>
        /// Performs filteraccounts.
        /// </summary>
        /// <summary>
        /// Performs filteraccounts.
        /// </summary>

        private Task FilterAccountsAsync()
        {
            _logger.LogInformation("Applying filters - Fund: {Fund}, Type: {Type}", SelectedFund, SelectedAccountType);
            return LoadAccountsAsync();
        }
    }

}
