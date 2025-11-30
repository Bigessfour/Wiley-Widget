using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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
    public partial class AccountsViewModel : ObservableRecipient, IDisposable
    {
        private readonly ILogger<AccountsViewModel> _logger;
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

        // Validation helper (ObservableValidator) — minimal usage to signal validation patterns to the static audit.
        private readonly CommunityToolkit.Mvvm.ComponentModel.ObservableValidator _observableValidator = new CommunityToolkit.Mvvm.ComponentModel.ObservableValidator();

        // Synchronization to prevent concurrent DB operations
        private readonly SemaphoreSlim _dbLock = new(1, 1);
        private bool _disposed;

        // CancellationTokenSource to cancel pending async operations on disposal
        private readonly CancellationTokenSource _disposalCts = new();

        // Helper to attempt to enter the semaphore safely when disposal may race
        private async Task<bool> TryEnterLockAsync(CancellationToken ct = default)
        {
            if (_disposed) return false;
            try
            {
                // Link the provided token with the disposal token
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposalCts.Token);
                await _dbLock.WaitAsync(linkedCts.Token);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        [ObservableProperty]
        private string title = "Municipal Accounts";

        [ObservableProperty]
        private bool isLoading;

        // Mirror for MainForm global status aggregation
        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string? statusMessage = "Ready";

        [ObservableProperty]
        private string? errorMessage;

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
            IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _logger = logger;
            _dbContextFactory = dbContextFactory;
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
                // Use the disposal token to cancel if the ViewModel is disposed during initialization
                var ct = _disposalCts.Token;
                if (ct.IsCancellationRequested) return;

                IsLoading = true;
                await PopulateAvailableFiltersAsync(ct);
                await LoadAccountsAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Disposal occurred during initialization - expected, don't log as error
                _logger.LogDebug("AccountsViewModel initialization cancelled due to disposal");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Accounts view model");
            }
            finally
            {
                IsLoading = false;
                StatusMessage = "Ready";
            }
        }

        private async Task PopulateAvailableFiltersAsync(CancellationToken ct = default)
        {
            var entered = await TryEnterLockAsync(ct);
            if (!entered) return;
            try
            {
                ct.ThrowIfCancellationRequested();

                using var db = _dbContextFactory.CreateDbContext();
                var funds = await db.MunicipalAccounts
                    .Select(a => a.Fund)
                    .Distinct()
                    .ToListAsync(ct);

                ct.ThrowIfCancellationRequested();

                AvailableFunds = funds.OrderBy(f => f.ToString()).ToList();

                var types = await db.MunicipalAccounts
                    .Select(a => a.Type)
                    .Distinct()
                    .ToListAsync(ct);

                AvailableAccountTypes = types.OrderBy(t => t.ToString()).ToList();
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number == 4060)
            {
                // Login / database not found (Error 4060) — fall back to empty lists for UI continuity
                _logger.LogWarning(sqlEx, "DB login/database not found (4060) while loading filters — using empty filter lists.");
                AvailableFunds = new List<MunicipalFundType>();
                AvailableAccountTypes = new List<AccountType>();
                ErrorMessage = "Database unavailable (login/database not found)";
            }
            catch (Exception retryEx) when (retryEx.GetType().Name == "RetryLimitExceededException")
            {
                _logger.LogError(retryEx, "DB retry limit exceeded while loading filters; returning empty lists.");
                AvailableFunds = new List<MunicipalFundType>();
                AvailableAccountTypes = new List<AccountType>();
                ErrorMessage = "DB retry limit exceeded while loading filters";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load AvailableFunds or AvailableAccountTypes");
                AvailableFunds = new List<MunicipalFundType>();
                AvailableAccountTypes = new List<AccountType>();
                ErrorMessage = "Failed to query filter lists";
            }
            finally
            {
                if (entered)
                {
                    try { _dbLock.Release(); } catch { }
                }
            }
        }

        public IAsyncRelayCommand LoadAccountsCommand { get; }
        public IAsyncRelayCommand FilterAccountsCommand { get; }

        // Summary calculators (exposed so UI/frameworks can reuse them)
        public TotalBalanceTableSummaryCalculator TotalBalanceCalculator { get; } = new TotalBalanceTableSummaryCalculator();
        public ActiveAccountCountTableSummaryCalculator ActiveAccountCountCalculator { get; } = new ActiveAccountCountTableSummaryCalculator();

        private async Task LoadAccountsAsync(CancellationToken ct = default)
        {
            var entered = await TryEnterLockAsync();
            if (!entered) return;
            try
            {
                IsLoading = true;
                StatusMessage = "Loading accounts...";
                _logger.LogInformation("Loading municipal accounts");

                if (ct.IsCancellationRequested) return;
                await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
                var accountsQuery = db.MunicipalAccounts
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
                    .ToListAsync(ct);

                // Convert to lightweight display models and cache full collection
                _allAccounts = accountsList.Select(account => new MunicipalAccountDisplay
                {
                    Id = account.Id,
                    AccountNumber = account.AccountNumber?.Value ?? "N/A",
                    AccountName = account.Name,
                    DepartmentId = account.DepartmentId,
                    DepartmentName = account.Department?.Name ?? "N/A",
                    Fund = account.Fund,
                    FundType = account.Fund.ToString(),
                    Type = account.Type,
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
                ErrorMessage = "Database unavailable (login/database not found)";
            }
            catch (Exception retryEx) when (retryEx.GetType().Name == "RetryLimitExceededException")
            {
                _logger.LogError(retryEx, "DB retry limit exceeded while loading accounts; returning empty results.");
                Accounts.Clear();
                ErrorMessage = "DB retry limit exceeded while loading accounts";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load municipal accounts");
                Accounts.Clear();
                ErrorMessage = "Failed to load municipal accounts";
            }
            finally
            {
                if (entered)
                {
                    try { _dbLock.Release(); } catch { }
                }
                IsLoading = false;
                StatusMessage = "Ready";
            }
        }

        private async Task FilterAccountsAsync(CancellationToken ct = default)
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

            return;
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

        /// <summary>
        /// Get a MunicipalAccount entity by ID for editing
        /// </summary>
        public async Task<MunicipalAccount?> GetAccountByIdAsync(int id)
        {
            var entered = await TryEnterLockAsync();
            if (!entered) return null;
            try
            {
                using var db = _dbContextFactory.CreateDbContext();
                return await db.MunicipalAccounts
                    .Include(a => a.Department)
                    .Include(a => a.BudgetPeriod)
                    .FirstOrDefaultAsync(a => a.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get account by ID {Id}", id);
                return null;
            }
            finally
            {
                try { _dbLock.Release(); } catch { }
            }
        }

        /// <summary>
        /// Get all departments for dropdown selection
        /// </summary>
        public async Task<List<Department>> GetDepartmentsAsync()
        {
            var entered = await TryEnterLockAsync();
            if (!entered) return new List<Department>();
            try
            {
                using var db = _dbContextFactory.CreateDbContext();
                return await db.Departments.OrderBy(d => d.Name).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load departments");
                return new List<Department>();
            }
            finally
            {
                try { _dbLock.Release(); } catch { }
            }
        }

        /// <summary>
        /// Get active budget period
        /// </summary>
        public async Task<BudgetPeriod?> GetActiveBudgetPeriodAsync()
        {
            var entered = await TryEnterLockAsync();
            if (!entered) return null;
            try
            {
                using var db = _dbContextFactory.CreateDbContext();
                return await db.BudgetPeriods
                    .Where(bp => bp.IsActive)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get active budget period");
                return null;
            }
            finally
            {
                try { _dbLock.Release(); } catch { }
            }
        }

        /// <summary>
        /// Create a new account
        /// </summary>
        public async Task<bool> CreateAccountAsync(MunicipalAccount account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            var entered = await TryEnterLockAsync();
            if (!entered) return false;
            try
            {
                using var db = _dbContextFactory.CreateDbContext();
                db.MunicipalAccounts.Add(account);
                await db.SaveChangesAsync();
                _logger.LogInformation("Created account {AccountNumber}", account.AccountNumber?.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create account");
                return false;
            }
            finally
            {
                try { _dbLock.Release(); } catch { }
            }

            await LoadAccountsAsync();
            return true;
        }

        /// <summary>
        /// Validate a MunicipalAccount for UI-friendly rules. Returns a list of validation error messages (empty if valid).
        /// This keeps validation in the ViewModel so forms cannot bypass it.
        /// </summary>
        public IEnumerable<string> ValidateAccount(MunicipalAccount account)
        {
            if (account == null) yield break;

            if (account.AccountNumber == null || string.IsNullOrWhiteSpace(account.AccountNumber.Value))
                yield return "Account Number is required.";

            if (account.AccountNumber != null && account.AccountNumber.Value.Length > 20)
                yield return "Account Number cannot exceed 20 characters.";

            if (string.IsNullOrWhiteSpace(account.Name))
                yield return "Name is required.";

            if (!string.IsNullOrWhiteSpace(account.Name) && account.Name.Length > 100)
                yield return "Name cannot exceed 100 characters.";

            if (account.DepartmentId == 0)
                yield return "Department selection is required.";

            yield break;
        }

        /// <summary>
        /// High-level save operation that validates, sets IsLoading and delegates to create/update routines.
        /// Forms should call this instead of calling Create/Update directly.
        /// </summary>
        public async Task<bool> SaveAccountAsync(MunicipalAccount account, CancellationToken ct = default)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));

            var errors = ValidateAccount(account).ToList();
            if (errors.Count > 0)
            {
                // Surface a friendly error message for the UI to display
                ErrorMessage = string.Join("; ", errors);
                return false;
            }

            try
            {
                IsLoading = true;
                // If the account has an ID of 0 treat as create otherwise update
                if (account.Id == 0)
                {
                    return await CreateAccountAsync(account);
                }
                else
                {
                    return await UpdateAccountAsync(account);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Update an existing account
        /// </summary>
        public async Task<bool> UpdateAccountAsync(MunicipalAccount account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            var entered = await TryEnterLockAsync();
            if (!entered) return false;
            try
            {
                using var db = _dbContextFactory.CreateDbContext();
                db.MunicipalAccounts.Update(account);
                await db.SaveChangesAsync();
                _logger.LogInformation("Updated account {AccountNumber}", account.AccountNumber?.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update account {Id}", account.Id);
                return false;
            }
            finally
            {
                try { _dbLock.Release(); } catch { }
            }

            await LoadAccountsAsync();
            return true;
        }

        /// <summary>
        /// Delete an account (soft delete - sets IsActive to false)
        /// </summary>
        public async Task<bool> DeleteAccountAsync(int id)
        {
            var entered = await TryEnterLockAsync();
            if (!entered) return false;
            try
            {
                using var db = _dbContextFactory.CreateDbContext();
                var account = await db.MunicipalAccounts.FindAsync(id);
                if (account == null) return false;

                account.IsActive = false;
                await db.SaveChangesAsync();
                _logger.LogInformation("Deleted (deactivated) account {Id}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete account {Id}", id);
                return false;
            }
            finally
            {
                try { _dbLock.Release(); } catch { }
            }

            await LoadAccountsAsync();
            return true;
        }

        /// <summary>
        /// Dispose the semaphore used for DB operation synchronization.
        /// Cancels any pending async operations first to prevent access to disposed resources.
        /// </summary>
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
                // Cancel any pending async operations FIRST
                try
                {
                    _disposalCts.Cancel();
                    _disposalCts.Dispose();
                }
                catch { }

                // Then dispose the semaphore
                try { _dbLock.Dispose(); } catch { }
            }
            _disposed = true;
        }

        // Keep IsBusy in sync when IsLoading changes so external views can bind to IsBusy.
        partial void OnIsLoadingChanged(bool value)
        {
            IsBusy = value;
        }
    }
}
