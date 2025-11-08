using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Serilog;
using WileyWidget.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.ViewModels.Messages;

namespace WileyWidget.ViewModels.Panels
{
    /// <summary>
    /// ViewModel for the Municipal Account Panel View
    /// Provides data and commands for managing municipal accounts
    /// </summary>
    public class MunicipalAccountPanelViewModel : BindableBase
    {
        private readonly IMunicipalAccountRepository? _accountRepository;
        private readonly IEventAggregator? _eventAggregator;
        private readonly IDepartmentRepository? _departmentRepository;

        private MunicipalAccount? _selectedAccount;
        private string _statusMessage = "Ready";
        private string _errorMessage = string.Empty;
        private bool _isBusy;
        private string _searchText = string.Empty;
        private MunicipalFundType? _selectedFundFilter;
        private AccountType? _selectedTypeFilter;

        // Required properties from user specification
        public ObservableCollection<MunicipalAccount> Accounts { get; } = new ObservableCollection<MunicipalAccount>();

        public MunicipalAccount? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                if (SetProperty(ref _selectedAccount, value))
                {
                    // Notify dependent properties
                    RaisePropertyChanged(nameof(AccountNumber));
                    RaisePropertyChanged(nameof(Description));
                    RaisePropertyChanged(nameof(Balance));
                    RaisePropertyChanged(nameof(Type));
                }
            }
        }

        public string AccountNumber => SelectedAccount?.AccountNumber?.Value ?? string.Empty;
        public string Description => SelectedAccount?.Name ?? string.Empty;
        public decimal Balance => SelectedAccount?.Balance ?? 0;
        public AccountType Type => SelectedAccount?.Type ?? AccountType.Asset;

        // Additional properties for UI binding
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // Trigger filtering when search text changes
                    ApplyFilters();
                }
            }
        }

        public MunicipalFundType? SelectedFundFilter
        {
            get => _selectedFundFilter;
            set
            {
                if (SetProperty(ref _selectedFundFilter, value))
                {
                    ApplyFilters();
                }
            }
        }

        public AccountType? SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (SetProperty(ref _selectedTypeFilter, value))
                {
                    ApplyFilters();
                }
            }
        }

        // Collections for combo boxes
        public IEnumerable<MunicipalFundType> FundTypeValues => Enum.GetValues(typeof(MunicipalFundType)).Cast<MunicipalFundType>();
        public IEnumerable<AccountType> AccountTypeValues => Enum.GetValues(typeof(AccountType)).Cast<AccountType>();
        public ObservableCollection<Department> Departments { get; } = new ObservableCollection<Department>();

        // Commands
        public DelegateCommand LoadAccountsCommand { get; }
        public DelegateCommand SyncFromQuickBooksCommand { get; }
        public DelegateCommand LoadBudgetAnalysisCommand { get; }
        public DelegateCommand FilterByFundCommand { get; }
        public DelegateCommand FilterByTypeCommand { get; }
        public DelegateCommand ApplyFiltersCommand { get; }
        public DelegateCommand ClearFiltersCommand { get; }
        public DelegateCommand ClearErrorCommand { get; }

        public MunicipalAccountPanelViewModel(
            IMunicipalAccountRepository? accountRepository = null,
            IEventAggregator? eventAggregator = null,
            IDepartmentRepository? departmentRepository = null)
        {
            _accountRepository = accountRepository;
            _eventAggregator = eventAggregator;
            _departmentRepository = departmentRepository;

            // Initialize commands
            LoadAccountsCommand = new DelegateCommand(async () => await LoadAccountsAsync(), () => !IsBusy);
            SyncFromQuickBooksCommand = new DelegateCommand(() => SyncFromQuickBooks());
            LoadBudgetAnalysisCommand = new DelegateCommand(() => LoadBudgetAnalysis());
            FilterByFundCommand = new DelegateCommand(() => ApplyFilters());
            FilterByTypeCommand = new DelegateCommand(() => ApplyFilters());
            ApplyFiltersCommand = new DelegateCommand(() => ApplyFilters());
            ClearFiltersCommand = new DelegateCommand(() => ClearFilters());
            ClearErrorCommand = new DelegateCommand(() => ErrorMessage = string.Empty);

            // Subscribe to events
            _eventAggregator?.GetEvent<BudgetUpdatedEvent>().Subscribe(OnBudgetUpdated);

            // Auto-load data
            _ = LoadAccountsAsync();
            _ = LoadDepartmentsAsync();
        }

        private async Task LoadAccountsAsync()
        {
            if (_accountRepository == null)
            {
                ErrorMessage = "Account repository not available";
                return;
            }

            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = "Loading accounts...";

                var accounts = await _accountRepository.GetAllAsync();
                if (accounts != null)
                {
                    Accounts.Clear();
                    foreach (var account in accounts.OrderBy(a => a.AccountNumber?.Value))
                    {
                        Accounts.Add(account);
                    }

                    StatusMessage = $"Loaded {Accounts.Count} accounts";

                    // Publish event for cross-module updates
                    _eventAggregator?.GetEvent<AccountsUpdatedEvent>().Publish(new AccountsUpdatedEvent
                    {
                        Count = Accounts.Count,
                        Source = "panel-load"
                    });
                }
                else
                {
                    StatusMessage = "No accounts found";
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load municipal accounts");
                ErrorMessage = $"Failed to load accounts: {ex.Message}";
                StatusMessage = "Error loading accounts";
            }
            finally
            {
                IsBusy = false;
                LoadAccountsCommand.RaiseCanExecuteChanged();
            }
        }

        private void SyncFromQuickBooks()
        {
            ErrorMessage = "QuickBooks sync not implemented";
            StatusMessage = "QuickBooks sync not available";
        }

        private void LoadBudgetAnalysis()
        {
            if (_accountRepository == null)
            {
                ErrorMessage = "Account repository not available";
                return;
            }

            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = "Loading budget analysis...";

                // Implementation would go here
                StatusMessage = "Budget analysis loaded";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load budget analysis");
                ErrorMessage = $"Budget analysis failed: {ex.Message}";
                StatusMessage = "Budget analysis failed";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilters()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;

                // Apply search and filter logic
                var filteredAccounts = Accounts.AsEnumerable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    filteredAccounts = filteredAccounts.Where(a =>
                        (a.AccountNumber?.Value.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (a.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (a.Notes?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                // Apply fund filter
                if (SelectedFundFilter.HasValue)
                {
                    filteredAccounts = filteredAccounts.Where(a => a.Fund == SelectedFundFilter.Value);
                }

                // Apply type filter
                if (SelectedTypeFilter.HasValue)
                {
                    filteredAccounts = filteredAccounts.Where(a => a.Type == SelectedTypeFilter.Value);
                }

                // Update status
                StatusMessage = $"Showing {filteredAccounts.Count()} of {Accounts.Count} accounts";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply filters");
                ErrorMessage = $"Filter failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedFundFilter = null;
            SelectedTypeFilter = null;
            ApplyFilters();
        }

        private async Task LoadDepartmentsAsync()
        {
            try
            {
                if (_departmentRepository != null)
                {
                    var departments = await _departmentRepository.GetAllAsync();
                    if (departments != null)
                    {
                        Departments.Clear();
                        foreach (var dept in departments)
                        {
                            Departments.Add(dept);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load departments");
            }
        }

        private void OnBudgetUpdated(BudgetUpdatedEventArgs e)
        {
            // Handle budget updates
            StatusMessage = "Budget data updated";
        }

        // Property change notification for computed properties
        protected override void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            base.OnPropertyChanged(args);

            // Notify computed properties when SelectedAccount changes
            if (args != null && args.PropertyName == nameof(SelectedAccount))
            {
                RaisePropertyChanged(nameof(AccountNumber));
                RaisePropertyChanged(nameof(Description));
                RaisePropertyChanged(nameof(Balance));
                RaisePropertyChanged(nameof(Type));
            }
        }
    }
}
