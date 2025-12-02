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
using WileyWidget.Models.Validators;

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
        private string? errorMessage;

        [ObservableProperty]
        private ObservableCollection<MunicipalAccountDisplay> accounts = new();

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
        }

        public IAsyncRelayCommand LoadAccountsCommand { get; }
        public IAsyncRelayCommand FilterAccountsCommand { get; }

        private async Task LoadAccountsAsync()
        {
            try
            {
                IsLoading = true;
                // Enhanced logging with filter context
                _logger.LogInformation("Loading municipal accounts with filters {@Filters}", new { Fund = SelectedFund?.ToString() ?? "(all)", AccountType = SelectedAccountType?.ToString() ?? "(all)" });

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
                        Name = account.Name ?? "(Unnamed)",
                        Description = account.FundDescription ?? string.Empty,
                        Type = account.Type.ToString(),
                        Fund = account.Fund.ToString(),
                        Balance = account.Balance,
                        BudgetAmount = account.BudgetAmount,
                        Department = account.Department?.Name ?? "(Unassigned)",
                        IsActive = account.IsActive,
                        HasParent = account.ParentAccountId.HasValue
                    });
                }

                TotalBalance = Accounts.Sum(a => a.Balance);
                ActiveAccountCount = Accounts.Count;

                _logger.LogInformation("Municipal accounts loaded successfully: {Count} accounts, Total Balance: {Balance:C}",
                    ActiveAccountCount, TotalBalance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load municipal accounts");
                Accounts.Clear();
                ErrorMessage = "Failed to load municipal accounts";
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

        /// <summary>
        /// Validates a MunicipalAccount using FluentValidation.
        /// Returns a list of validation error messages (empty if valid).
        /// </summary>
        public IEnumerable<string> ValidateAccount(MunicipalAccount account)
        {
            if (account == null)
            {
                yield return "Account cannot be null.";
                yield break;
            }

            // Use FluentValidation extension method
            foreach (var error in account.Validate())
            {
                yield return error;
            }
        }

        /// <summary>
        /// Saves a MunicipalAccount after validation.
        /// Returns true if successful, false if validation fails or an error occurs.
        /// </summary>
        public async Task<bool> SaveAccountAsync(MunicipalAccount account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));

            var errors = ValidateAccount(account).ToList();
            if (errors.Count > 0)
            {
                ErrorMessage = string.Join("; ", errors);
                return false;
            }

            try
            {
                IsLoading = true;
                if (account.Id == 0)
                {
                    _dbContext.MunicipalAccounts.Add(account);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Created account {@AccountDetails}", new { AccountNumber = account.AccountNumber?.Value, Name = account.Name, DepartmentId = account.DepartmentId, Fund = account.Fund.ToString(), Type = account.Type.ToString() });
                }
                else
                {
                    _dbContext.MunicipalAccounts.Update(account);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Updated account {@AccountDetails}", new { Id = account.Id, AccountNumber = account.AccountNumber?.Value, Name = account.Name, Balance = account.Balance });
                }

                await LoadAccountsAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save account {AccountNumber}", account.AccountNumber?.Value);
                ErrorMessage = "Failed to save account";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Deletes (soft-deletes) a MunicipalAccount by setting IsActive to false.
        /// </summary>
        public async Task<bool> DeleteAccountAsync(int id)
        {
            try
            {
                IsLoading = true;
                var account = await _dbContext.MunicipalAccounts.FindAsync(id);
                if (account == null) return false;

                account.IsActive = false;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Deleted (deactivated) account {@AccountDetails}", new { Id = id, AccountNumber = account.AccountNumber?.Value, Name = account.Name });

                await LoadAccountsAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete account {Id}", id);
                ErrorMessage = "Failed to delete account";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Display model for municipal accounts
    /// </summary>
    public class MunicipalAccountDisplay
    {
        public int Id { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Fund { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public decimal BudgetAmount { get; set; }
        public string Department { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool HasParent { get; set; }
    }
}
