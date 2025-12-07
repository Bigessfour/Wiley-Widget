using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Threading;
using WileyWidget.Models;
using WileyWidget.Abstractions.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for the Accounts view. Orchestrates UI interactions and delegates
    /// all business logic to IAccountService (MVVM purity - Phase 2 refactoring).
    /// </summary>
    public partial class AccountsViewModel : ObservableRecipient
    {
        private readonly ILogger<AccountsViewModel> _logger;
        private readonly IAccountService _accountService;
        private readonly IAccountMapper _mapper;

        [ObservableProperty]
        private string title = "Municipal Accounts";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private ObservableCollection<MunicipalAccountDisplay> accounts = new();

        /// <summary>
        /// Flat search text used to filter accounts locally when the service does not provide text search.
        /// </summary>
        [ObservableProperty]
        private string? searchText;

        /// <summary>
        /// Hierarchical account nodes built from the flat account list (for tree views).
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<HierarchicalAccountNode> hierarchicalAccounts = new();

        [ObservableProperty]
        private MunicipalFundType? selectedFund;

        [ObservableProperty]
        private AccountType? selectedAccountType;

        [ObservableProperty]
        private decimal totalBalance;

        [ObservableProperty]
        private int activeAccountCount;

        [ObservableProperty]
        private MunicipalAccountDisplay? selectedAccount;

        public AccountsViewModel(
            ILogger<AccountsViewModel> logger,
            IAccountService accountService,
            IAccountMapper mapper)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

            try
            {
                LoadAccountsCommand = new AsyncRelayCommand(LoadAccountsAsync);
                FilterAccountsCommand = new AsyncRelayCommand(FilterAccountsAsync);
                DeleteAccountCommand = new AsyncRelayCommand<int>(async id => await DeleteAccountAsync(id));
                AddAccountCommand = new AsyncRelayCommand(async () => await Task.CompletedTask);
                EditAccountCommand = new AsyncRelayCommand<int>(async id => await Task.CompletedTask);

                _logger.LogInformation("AccountsViewModel constructed with IAccountService");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AccountsViewModel constructor failed");
                throw;
            }
        }

        public IAsyncRelayCommand LoadAccountsCommand { get; }
        public IAsyncRelayCommand FilterAccountsCommand { get; }
        public IAsyncRelayCommand DeleteAccountCommand { get; }
        public IAsyncRelayCommand AddAccountCommand { get; }
        public IAsyncRelayCommand EditAccountCommand { get; }

        private async Task LoadAccountsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading municipal accounts with filters {@Filters}",
                    new { Fund = SelectedFund?.ToString() ?? "(all)", AccountType = SelectedAccountType?.ToString() ?? "(all)" });

                // Delegate business logic to AccountService (returns already-mapped display objects)
                var result = await _accountService.LoadAccountsAsync(SelectedFund, SelectedAccountType, SearchText, cancellationToken);

                // Update observable collection (service now handles all filtering including search)
                Accounts.Clear();
                foreach (var display in result.Accounts)
                {
                    Accounts.Add(display);
                }

                // Build hierarchical view based on account number parent semantics
                BuildHierarchy(Accounts);

                // Update computed properties
                TotalBalance = result.TotalBalance;
                ActiveAccountCount = result.ActiveAccountCount;

                _logger.LogInformation("Municipal accounts loaded successfully: {Count} accounts, Total Balance: {Balance:C}",
                    ActiveAccountCount, TotalBalance);
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogDebug(oce, "Loading accounts operation was canceled");
                return;
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

        private Task FilterAccountsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Applying filters - Fund: {Fund}, Type: {Type}", SelectedFund, SelectedAccountType);
            return LoadAccountsAsync(cancellationToken);
        }

        /// <summary>
        /// Builds a simple hierarchical account tree from the flat account list using dot-separated account numbers.
        /// This does not modify the display DTOs; it creates lightweight nodes for UI tree binding.
        /// </summary>
        /// <param name="flatList">Flat list of accounts (display DTOs)</param>
        private void BuildHierarchy(IEnumerable<MunicipalAccountDisplay> flatList)
        {
            try
            {
                var nodes = new Dictionary<string, HierarchicalAccountNode>(StringComparer.OrdinalIgnoreCase);
                var roots = new List<HierarchicalAccountNode>();

                foreach (var a in flatList)
                {
                    var node = new HierarchicalAccountNode(a);
                    // Use account number string as key
                    var key = (a.AccountNumber ?? string.Empty).Trim();
                    if (!nodes.ContainsKey(key)) nodes[key] = node;
                }

                // Second pass: wire children to parents
                foreach (var kvp in nodes)
                {
                    var key = kvp.Key;
                    var node = kvp.Value;
                    var parentNumber = GetParentAccountNumber(key);
                    if (!string.IsNullOrWhiteSpace(parentNumber) && nodes.TryGetValue(parentNumber!, out var parent))
                    {
                        parent.Children.Add(node);
                    }
                    else
                    {
                        roots.Add(node);
                    }
                }

                HierarchicalAccounts.Clear();
                foreach (var r in roots.OrderBy(n => n.Account?.AccountNumber))
                {
                    HierarchicalAccounts.Add(r);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build account hierarchy");
                HierarchicalAccounts.Clear();
            }
        }

        private static string? GetParentAccountNumber(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber)) return null;
            var parts = accountNumber.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1) return null;
            return string.Join('.', parts.Take(parts.Length - 1));
        }

        /// <summary>
        /// Lightweight node used by tree controls to present hierarchical accounts.
        /// </summary>
        public class HierarchicalAccountNode
        {
            public MunicipalAccountDisplay? Account { get; }
            public ObservableCollection<HierarchicalAccountNode> Children { get; } = new();

            public HierarchicalAccountNode(MunicipalAccountDisplay? account)
            {
                Account = account;
            }

            public override string ToString() => Account?.AccountNumber + " - " + Account?.Name;
        }

        /// <summary>
        /// Validates a MunicipalAccount using the service layer.
        /// Returns a list of validation error messages (empty if valid).
        /// </summary>
        public IEnumerable<string> ValidateAccount(MunicipalAccount account)
        {
            // Delegate validation to service layer
            return _accountService.ValidateAccount(account);
        }

        /// <summary>
        /// Saves a MunicipalAccount after validation (delegates to service).
        /// Returns true if successful, false if validation fails or an error occurs.
        /// </summary>
        public async Task<bool> SaveAccountAsync(MunicipalAccount account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));

            IsLoading = true;
            try
            {
                // Delegate save operation to service layer
                var result = await _accountService.SaveAccountAsync(account);

                if (!result.Success)
                {
                    ErrorMessage = string.Join("; ", result.ValidationErrors);
                    return false;
                }

                // Reload accounts to refresh UI
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
        /// Deletes (soft-deletes) a MunicipalAccount (delegates to service).
        /// </summary>
        public async Task<bool> DeleteAccountAsync(int id)
        {
            IsLoading = true;
            try
            {
                // Delegate delete operation to service layer
                var success = await _accountService.DeleteAccountAsync(id);

                if (!success)
                {
                    ErrorMessage = "Account not found";
                    return false;
                }

                // Reload accounts to refresh UI
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
}
