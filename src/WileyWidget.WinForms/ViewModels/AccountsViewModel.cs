using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.Models;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for managing municipal accounts with filtering and CRUD operations.
/// Designed to be minimal, testable, and to match the public API expected by tests.
/// </summary>
/// <summary>
/// ViewModel for managing municipal accounts with filtering, CRUD operations, and data aggregation.
/// </summary>
public partial class AccountsViewModel : ObservableRecipient, IDisposable, ILazyLoadViewModel
{
    private bool _isDataLoaded;
    public bool IsDataLoaded
    {
        get => _isDataLoaded;
        private set => SetProperty(ref _isDataLoaded, value);
    }

    public async Task OnVisibilityChangedAsync(bool isVisible)
    {
        if (isVisible && !IsDataLoaded && !IsLoading)
        {
            await LoadAccountsAsync();
            IsDataLoaded = true;
        }
    }

        private readonly ILogger<AccountsViewModel> _logger;
        private readonly IAccountsRepository _accountsRepository;
        private readonly IMunicipalAccountRepository _municipalAccountRepository;
        private CancellationTokenSource? _loadCancellationSource;
        private bool _disposed;

        /// <summary>
        /// Gets or sets the panel title.
        /// </summary>
        [ObservableProperty]
        private string title = "Municipal Accounts";

        /// <summary>
        /// Gets or sets whether data is currently loading.
        /// </summary>
        [ObservableProperty]
        private bool isLoading;

        /// <summary>
        /// Gets the collection of municipal account display models for UI binding.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<MunicipalAccountDisplay> accounts = new();

        /// <summary>
        /// Gets or sets the currently selected fund filter.
        /// </summary>
        [ObservableProperty]
        private MunicipalFundType? selectedFund;

        /// <summary>
        /// Gets or sets the currently selected account type filter.
        /// </summary>
        [ObservableProperty]
        private AccountType? selectedAccountType;

        /// <summary>
        /// Gets or sets the search text for filtering accounts.
        /// </summary>
        [ObservableProperty]
        private string? searchText;

        /// <summary>
        /// Gets the available fund types for filtering.
        /// </summary>
        public ObservableCollection<MunicipalFundType> AvailableFunds { get; } = new(new[]
        {
            MunicipalFundType.General,
            MunicipalFundType.SpecialRevenue,
            MunicipalFundType.CapitalProjects,
            MunicipalFundType.DebtService,
            MunicipalFundType.Enterprise,
            MunicipalFundType.InternalService,
            MunicipalFundType.Trust,
            MunicipalFundType.Agency
        });

        /// <summary>
        /// Gets the available account types for filtering.
        /// </summary>
        public ObservableCollection<AccountType> AvailableAccountTypes { get; } = new(new[]
        {
            AccountType.Asset,
            AccountType.Payables,
            AccountType.RetainedEarnings,
            AccountType.Revenue,
            AccountType.Expense
        });

        /// <summary>
        /// Gets or sets the calculated total balance across all accounts.
        /// </summary>
        [ObservableProperty]
        private decimal totalBalance;

        /// <summary>
        /// Gets or sets the count of active accounts.
        /// </summary>
        [ObservableProperty]
        private int activeAccountCount;

        /// <summary>
        /// Gets or sets the currently selected account for editing/deletion.
        /// </summary>
        [ObservableProperty]
        private MunicipalAccountDisplay? selectedAccount;

        /// <summary>
        /// Gets or sets the error message for display in the UI.
        /// </summary>
        [ObservableProperty]
        private string? errorMessage;

        /// <summary>
        /// Gets or sets the status text for display in the status bar.
        /// </summary>
        [ObservableProperty]
        private string? statusText;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountsViewModel"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="accountsRepository">Repository for account data operations.</param>
        /// <param name="municipalAccountRepository">Repository for municipal-specific account operations.</param>
        public AccountsViewModel(
            ILogger<AccountsViewModel> logger,
            IAccountsRepository accountsRepository,
            IMunicipalAccountRepository municipalAccountRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _accountsRepository = accountsRepository ?? throw new ArgumentNullException(nameof(accountsRepository));
            _municipalAccountRepository = municipalAccountRepository ?? throw new ArgumentNullException(nameof(municipalAccountRepository));

            // Optimization: Defer data loading until the associated panel becomes visible.
            // This is handled by ILazyLoadViewModel via OnVisibilityChangedAsync.
        }

        /// <summary>
        /// Gets the command to load accounts from the repository.
        /// </summary>
        [RelayCommand]
        private async Task LoadAccountsAsync(CancellationToken cancellationToken = default)
        {
            // Cancel any ongoing load operation
            _loadCancellationSource?.Cancel();
            _loadCancellationSource = new CancellationTokenSource();
            var token = _loadCancellationSource.Token;

            try
            {
                IsLoading = true;
                ErrorMessage = null;
                StatusText = "Loading accounts...";
                _logger.LogInformation("Loading municipal accounts with filters - Fund: {Fund}, Type: {Type}, Search: {Search}",
                    SelectedFund, SelectedAccountType, SearchText);

                IReadOnlyList<MunicipalAccount> accountsList;

                // Apply filters based on selection
                if (SelectedFund.HasValue && SelectedAccountType.HasValue)
                {
                    accountsList = await _accountsRepository.GetAccountsByFundAndTypeAsync(
                        SelectedFund.Value,
                        SelectedAccountType.Value,
                        token);
                }
                else if (SelectedFund.HasValue)
                {
                    accountsList = await _accountsRepository.GetAccountsByFundAsync(SelectedFund.Value, token);
                }
                else if (SelectedAccountType.HasValue)
                {
                    accountsList = await _accountsRepository.GetAccountsByTypeAsync(SelectedAccountType.Value, token);
                }
                else
                {
                    accountsList = await _accountsRepository.GetAllAccountsAsync(token);
                }

                // Apply search filter if provided
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var searchLower = SearchText.ToLowerInvariant();
                    accountsList = accountsList
                        .Where(a =>
                            (a.AccountNumber?.Value?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (a.Name?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (a.FundDescription?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false))
                        .ToList();
                }

                // Clear and repopulate accounts collection
                Accounts.Clear();

                foreach (var account in accountsList)
                {
                    if (token.IsCancellationRequested) return;

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

                // Update summary calculations
                UpdateSummaries();

                StatusText = $"Loaded {ActiveAccountCount} accounts successfully";
                _logger.LogInformation("Municipal accounts loaded: {Count} accounts, Total Balance: {Balance:C}",
                    ActiveAccountCount, TotalBalance);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Account loading was cancelled");
                StatusText = "Loading cancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load municipal accounts");
                ErrorMessage = $"Error loading accounts: {ex.Message}";
                StatusText = "Error loading accounts";

                // Fallback to sample data for better UX
                LoadSampleData();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Gets the command to apply filters and reload data.
        /// </summary>
        [RelayCommand]
        private async Task FilterAccountsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Applying filters - Fund: {Fund}, Type: {Type}, Search: {Search}",
                SelectedFund, SelectedAccountType, SearchText);
            await LoadAccountsAsync();
        }

        /// <summary>
        /// Gets the command to clear all filters.
        /// </summary>
        [RelayCommand]
        private async Task ClearFiltersAsync(CancellationToken cancellationToken = default)
        {
            SelectedFund = null;
            SelectedAccountType = null;
            SearchText = null;
            _logger.LogInformation("Filters cleared, reloading accounts");
            await LoadAccountsAsync();
        }

        /// <summary>
        /// Gets the command to create a new account.
        /// </summary>
        [RelayCommand]
        private async Task CreateAccountAsync(MunicipalAccount newAccount, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(newAccount);

                IsLoading = true;
                StatusText = "Creating account...";
                _logger.LogInformation("Creating new account: {AccountNumber}", newAccount.AccountNumber?.Value);

                // Validate uniqueness
                var existing = await _municipalAccountRepository.GetByAccountNumberAsync(
                    newAccount.AccountNumber?.Value ?? string.Empty);
                if (existing != null)
                {
                    ErrorMessage = $"Account number '{newAccount.AccountNumber?.Value}' already exists";
                    _logger.LogWarning("Attempted to create duplicate account: {AccountNumber}", newAccount.AccountNumber?.Value);
                    return;
                }

                await _municipalAccountRepository.AddAsync(newAccount);
                await LoadAccountsAsync();

                StatusText = $"Account '{newAccount.AccountNumber?.Value}' created successfully";
                _logger.LogInformation("Account created: {AccountNumber}", newAccount.AccountNumber?.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account");
                ErrorMessage = $"Error creating account: {ex.Message}";
                StatusText = "Error creating account";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Gets the command to update an existing account.
        /// </summary>
        [RelayCommand]
        private async Task UpdateAccountAsync(MunicipalAccount updatedAccount, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(updatedAccount);

                IsLoading = true;
                StatusText = "Updating account...";
                _logger.LogInformation("Updating account: {AccountNumber}", updatedAccount.AccountNumber?.Value);

                await _municipalAccountRepository.UpdateAsync(updatedAccount);
                await LoadAccountsAsync();

                StatusText = $"Account '{updatedAccount.AccountNumber?.Value}' updated successfully";
                _logger.LogInformation("Account updated: {AccountNumber}", updatedAccount.AccountNumber?.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account");
                ErrorMessage = $"Error updating account: {ex.Message}";
                StatusText = "Error updating account";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Gets the command to delete an account.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDeleteAccount))]
        private async Task DeleteAccountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (SelectedAccount == null) return;

                IsLoading = true;
                StatusText = "Deleting account...";
                _logger.LogInformation("Deleting account: {AccountNumber}", SelectedAccount.AccountNumber);

                var success = await _municipalAccountRepository.DeleteAsync(SelectedAccount.Id);
                if (success)
                {
                    await LoadAccountsAsync();
                    SelectedAccount = null;
                    StatusText = "Account deleted successfully";
                    _logger.LogInformation("Account deleted: {AccountNumber}", SelectedAccount?.AccountNumber);
                }
                else
                {
                    ErrorMessage = "Failed to delete account";
                    StatusText = "Delete failed";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account");
                ErrorMessage = $"Error deleting account: {ex.Message}";
                StatusText = "Error deleting account";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanDeleteAccount() => SelectedAccount != null && !IsLoading;

        /// <summary>
        /// Gets all departments for dropdown/filtering purposes.
        /// </summary>
        /// <returns>List of all departments.</returns>
        public async Task<List<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Retrieving departments");
                // Note: This would ideally come from a dedicated DepartmentRepository
                // For now, we'll extract departments from accounts
                var accounts = await _municipalAccountRepository.GetAllWithRelatedAsync();
                var departments = accounts
                    .Where(a => a.Department != null)
                    .Select(a => a.Department!)
                    .DistinctBy(d => d.Id)
                    .ToList();

                _logger.LogInformation("Retrieved {Count} departments", departments.Count);
                return departments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving departments");
                return new List<Department>();
            }
        }

        /// <summary>
        /// Updates summary properties (TotalBalance, ActiveAccountCount) based on current accounts.
        /// </summary>
        private void UpdateSummaries()
        {
            TotalBalance = Accounts.Sum(a => a.CurrentBalance);
            ActiveAccountCount = Accounts.Count(a => a.IsActive);
            _logger.LogDebug("Summaries updated - Total: {Total:C}, Active: {Active}", TotalBalance, ActiveAccountCount);
        }

        /// <summary>
        /// Loads realistic sample data when the repository fails.
        /// </summary>
        private void LoadSampleData()
        {
            try
            {
                _logger.LogWarning("Loading sample data as fallback");
                Accounts.Clear();

                var sampleAccounts = new[]
                {
                    new MunicipalAccountDisplay
                    {
                        Id = 1,
                        AccountNumber = "1000",
                        AccountName = "Cash - General Fund",
                        Description = "Primary operating cash account",
                        AccountType = "Asset",
                        FundName = "General",
                        CurrentBalance = 125000.00m,
                        BudgetAmount = 150000.00m,
                        Department = "Finance",
                        IsActive = true,
                        HasParent = false
                    },
                    new MunicipalAccountDisplay
                    {
                        Id = 2,
                        AccountNumber = "4000",
                        AccountName = "Property Tax Revenue",
                        Description = "Property tax collections",
                        AccountType = "Revenue",
                        FundName = "General",
                        CurrentBalance = 850000.00m,
                        BudgetAmount = 900000.00m,
                        Department = "Tax Collector",
                        IsActive = true,
                        HasParent = false
                    },
                    new MunicipalAccountDisplay
                    {
                        Id = 3,
                        AccountNumber = "5100",
                        AccountName = "Salaries & Wages",
                        Description = "Employee compensation",
                        AccountType = "Expense",
                        FundName = "General",
                        CurrentBalance = 450000.00m,
                        BudgetAmount = 500000.00m,
                        Department = "Human Resources",
                        IsActive = true,
                        HasParent = false
                    },
                    new MunicipalAccountDisplay
                    {
                        Id = 4,
                        AccountNumber = "2100",
                        AccountName = "Water Utility Equipment",
                        Description = "Capital equipment for water department",
                        AccountType = "Asset",
                        FundName = "Enterprise",
                        CurrentBalance = 2500000.00m,
                        BudgetAmount = 3000000.00m,
                        Department = "Water Dept",
                        IsActive = true,
                        HasParent = false
                    },
                    new MunicipalAccountDisplay
                    {
                        Id = 5,
                        AccountNumber = "6200",
                        AccountName = "Infrastructure Repairs",
                        Description = "Capital project expenses",
                        AccountType = "Expense",
                        FundName = "CapitalProjects",
                        CurrentBalance = 175000.00m,
                        BudgetAmount = 250000.00m,
                        Department = "Public Works",
                        IsActive = true,
                        HasParent = false
                    }
                };

                foreach (var account in sampleAccounts)
                {
                    Accounts.Add(account);
                }

                UpdateSummaries();
                StatusText = "Showing sample data (repository unavailable)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sample data");
            }
        }

        /// <summary>
        /// Partial method hook for search text changes.
        /// </summary>
        partial void OnSearchTextChanged(string? value)
        {
            // Debounce search - trigger filter after user stops typing
            _ = Task.Run(async () =>
            {
                await Task.Delay(300); // 300ms debounce
                if (SearchText == value) // Only proceed if text hasn't changed
                {
                    await FilterAccountsAsync();
                }
            });
        }

        /// <summary>
        /// Partial method hook for selected fund changes.
        /// </summary>
        partial void OnSelectedFundChanged(MunicipalFundType? value)
        {
            _ = FilterAccountsAsync();
        }

        /// <summary>
        /// Partial method hook for selected account type changes.
        /// </summary>
        partial void OnSelectedAccountTypeChanged(AccountType? value)
        {
            _ = FilterAccountsAsync();
        }

        /// <summary>
        /// Disposes managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes managed resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(); false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _loadCancellationSource?.Cancel();
                    _loadCancellationSource?.Dispose();
                }
                _disposed = true;
            }
        }
    }
