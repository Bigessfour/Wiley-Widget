using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.Models;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for managing municipal accounts with filtering and CRUD operations.
/// Designed to be minimal, testable, and to match the public API expected by tests.
/// </summary>
public partial class AccountsViewModel : ViewModelBase, IDisposable
{
    private readonly ILogger<AccountsViewModel> _typedLogger;
    private readonly IAccountsRepository _accountsRepository;
    private readonly IMunicipalAccountRepository _municipalAccountRepository;
    private CancellationTokenSource? _loadCancellationSource;
    private bool _disposed;

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

    [ObservableProperty]
    private string? searchText;

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
    private decimal totalBalance;

    [ObservableProperty]
    private int activeAccountCount;

    [ObservableProperty]
    private MunicipalAccountDisplay? selectedAccount;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusText;

    public AccountsViewModel(
        ILogger<AccountsViewModel> logger,
        IAccountsRepository accountsRepository,
        IMunicipalAccountRepository municipalAccountRepository)
        : base(logger)
    {
        _typedLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accountsRepository = accountsRepository ?? throw new ArgumentNullException(nameof(accountsRepository));
        _municipalAccountRepository = municipalAccountRepository ?? throw new ArgumentNullException(nameof(municipalAccountRepository));
    }

    [RelayCommand]
    private async Task LoadAccountsAsync()
    {
        _loadCancellationSource?.Cancel();
        _loadCancellationSource?.Dispose();
        _loadCancellationSource = new CancellationTokenSource();
        var token = _loadCancellationSource.Token;

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            StatusText = "Loading accounts...";
            _typedLogger.LogInformation("Loading municipal accounts - Fund: {Fund}, Type: {Type}, Search: {Search}", SelectedFund, SelectedAccountType, SearchText);

            IReadOnlyList<MunicipalAccount> accountsList;

            if (SelectedFund.HasValue && SelectedAccountType.HasValue)
            {
                accountsList = await _accountsRepository.GetAccountsByFundAndTypeAsync(SelectedFund.Value, SelectedAccountType.Value, token);
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

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.Trim();
                accountsList = accountsList
                    .Where(a =>
                        (a.AccountNumber?.Value?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                        (a.Name?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                        (a.FundDescription?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                    .ToList();
            }

            Accounts.Clear();
            foreach (var account in accountsList)
            {
                if (token.IsCancellationRequested) return;

                Accounts.Add(new MunicipalAccountDisplay
                {
                    Id = account.Id,
                    AccountNumber = account.AccountNumber?.Value ?? string.Empty,
                    AccountName = account.Name ?? string.Empty,
                    Description = account.FundDescription ?? string.Empty,
                    AccountType = account.Type.ToString(),
                    FundName = account.Fund.ToString(),
                    CurrentBalance = account.Balance,
                    BudgetAmount = account.BudgetAmount,
                    Department = account.Department?.Name ?? string.Empty,
                    IsActive = account.IsActive,
                    HasParent = account.ParentAccountId.HasValue
                });
            }

            UpdateSummaries();
            StatusText = $"Loaded {ActiveAccountCount} accounts successfully";
            _typedLogger.LogInformation("Municipal accounts loaded: {Count} accounts, Total Balance: {Balance}", ActiveAccountCount, TotalBalance);
        }
        catch (OperationCanceledException)
        {
            _typedLogger.LogInformation("Account loading was cancelled");
            StatusText = "Loading cancelled";
        }
        catch (Exception ex)
        {
            _typedLogger.LogError(ex, "Failed to load municipal accounts");
            ErrorMessage = $"Error loading accounts: {ex.Message}";
            StatusText = "Error loading accounts";
            LoadSampleData();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task FilterAccountsAsync()
    {
        await LoadAccountsAsync();
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        SelectedFund = null;
        SelectedAccountType = null;
        SearchText = null;
        await LoadAccountsAsync();
    }

    [RelayCommand]
    private async Task CreateAccountAsync(MunicipalAccount newAccount)
    {
        if (newAccount == null) throw new ArgumentNullException(nameof(newAccount));

        try
        {
            IsLoading = true;
            StatusText = "Creating account...";
            _typedLogger.LogInformation("Creating new account: {AccountNumber}", newAccount.AccountNumber?.Value);

            var existing = await _municipalAccountRepository.GetByAccountNumberAsync(newAccount.AccountNumber?.Value ?? string.Empty);
            if (existing != null)
            {
                ErrorMessage = $"Account number '{newAccount.AccountNumber?.Value}' already exists";
                _typedLogger.LogWarning("Attempted to create duplicate account: {AccountNumber}", newAccount.AccountNumber?.Value);
                return;
            }

            await _municipalAccountRepository.AddAsync(newAccount);
            await LoadAccountsAsync();

            StatusText = $"Account '{newAccount.AccountNumber?.Value}' created successfully";
            _typedLogger.LogInformation("Account created: {AccountNumber}", newAccount.AccountNumber?.Value);
        }
        catch (Exception ex)
        {
            _typedLogger.LogError(ex, "Error creating account");
            ErrorMessage = $"Error creating account: {ex.Message}";
            StatusText = "Error creating account";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UpdateAccountAsync(MunicipalAccount updatedAccount)
    {
        if (updatedAccount == null) throw new ArgumentNullException(nameof(updatedAccount));

        try
        {
            IsLoading = true;
            StatusText = "Updating account...";
            _typedLogger.LogInformation("Updating account: {AccountNumber}", updatedAccount.AccountNumber?.Value);

            await _municipalAccountRepository.UpdateAsync(updatedAccount);
            await LoadAccountsAsync();

            StatusText = $"Account '{updatedAccount.AccountNumber?.Value}' updated successfully";
            _typedLogger.LogInformation("Account updated: {AccountNumber}", updatedAccount.AccountNumber?.Value);
        }
        catch (Exception ex)
        {
            _typedLogger.LogError(ex, "Error updating account");
            ErrorMessage = $"Error updating account: {ex.Message}";
            StatusText = "Error updating account";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteAccount))]
    private async Task DeleteAccountAsync()
    {
        if (SelectedAccount == null) return;

        try
        {
            IsLoading = true;
            StatusText = "Deleting account...";
            _typedLogger.LogInformation("Deleting account: {AccountNumber}", SelectedAccount.AccountNumber);

            var success = await _municipalAccountRepository.DeleteAsync(SelectedAccount.Id);
            if (success)
            {
                await LoadAccountsAsync();
                SelectedAccount = null;
                StatusText = "Account deleted successfully";
                _typedLogger.LogInformation("Account deleted");
            }
            else
            {
                ErrorMessage = "Failed to delete account";
                StatusText = "Delete failed";
            }
        }
        catch (Exception ex)
        {
            _typedLogger.LogError(ex, "Error deleting account");
            ErrorMessage = $"Error deleting account: {ex.Message}";
            StatusText = "Error deleting account";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanDeleteAccount() => SelectedAccount != null && !IsLoading;

    private void UpdateSummaries()
    {
        TotalBalance = Accounts.Sum(a => a.CurrentBalance);
        ActiveAccountCount = Accounts.Count(a => a.IsActive);
    }

    public async Task<List<Department>> GetDepartmentsAsync()
    {
        try
        {
            _typedLogger.LogInformation("Retrieving departments");
            var accounts = await _municipalAccountRepository.GetAllWithRelatedAsync();
            var departments = accounts
                .Where(a => a.Department != null)
                .Select(a => a.Department!)
                .DistinctBy(d => d.Id)
                .ToList();

            _typedLogger.LogInformation("Retrieved {Count} departments", departments.Count);
            return departments;
        }
        catch (Exception ex)
        {
            _typedLogger.LogError(ex, "Error retrieving departments");
            return new List<Department>();
        }
    }

    private void LoadSampleData()
    {
        Accounts.Clear();
        Accounts.Add(new MunicipalAccountDisplay
        {
            Id = -1,
            AccountNumber = "000-000",
            AccountName = "Sample Account",
            Description = "Sample data fallback",
            AccountType = AccountType.Asset.ToString(),
            FundName = MunicipalFundType.General.ToString(),
            CurrentBalance = 0m,
            BudgetAmount = 0m,
            Department = string.Empty,
            IsActive = true,
            HasParent = false
        });

        UpdateSummaries();
        StatusText = "Loaded sample data";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _loadCancellationSource?.Cancel();
            _loadCancellationSource?.Dispose();
        }
        _disposed = true;
    }
}
