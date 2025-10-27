using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data.Resilience;
using WileyWidget.Models;
using WileyWidget.Services;
// Removed Prism.Navigation; WPF region navigation types are in Prism.Regions
using WileyWidget.ViewModels.Messages;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for managing municipal accounts and budget analysis
/// Implements IDataErrorInfo for balance validation
/// </summary>
public partial class MunicipalAccountViewModel : BindableBase, IDataErrorInfo, IDisposable, INavigationAware
{
    private readonly IMunicipalAccountRepository _accountRepository;
    private readonly IQuickBooksService? _quickBooksService;
    private readonly IGrokSupercomputer? _grokSupercomputer;
    private readonly IRegionManager? _regionManager;
    private readonly IEventAggregator? _eventAggregator;

    public MunicipalAccountViewModel(
        IMunicipalAccountRepository accountRepository,
        IQuickBooksService? quickBooksService,
        IGrokSupercomputer? grokSupercomputer,
        IRegionManager? regionManager,
        IEventAggregator? eventAggregator)
    {
        var constructorTimer = Stopwatch.StartNew();
    Log.Debug("[VIEWMODEL_INIT] MunicipalAccountViewModel constructor started");

        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _quickBooksService = quickBooksService;
        _grokSupercomputer = grokSupercomputer;
        _regionManager = regionManager;
        _eventAggregator = eventAggregator;

    Log.Debug("[VIEWMODEL_INIT] Initializing MunicipalAccounts and BudgetAnalysis collections");
        MunicipalAccounts = new ObservableCollection<MunicipalAccount>();
        BudgetAnalysis = new ObservableCollection<MunicipalAccount>();

        _accountsView = CollectionViewSource.GetDefaultView(MunicipalAccounts);

        constructorTimer.Stop();
        Log.Debug("[VIEWMODEL_INIT] MunicipalAccountViewModel constructor completed in {ElapsedMs}ms", constructorTimer.ElapsedMilliseconds);
    Log.Debug("MunicipalAccountViewModel Constructor completed in {Ms}ms", constructorTimer.Elapsed.TotalMilliseconds);
        // Initialize Prism commands
    LoadAccountsCommand = new DelegateCommand(async () => await LoadAccountsAsync(), () => !IsBusy);

        // Initialize converted RelayCommand methods as DelegateCommand using FromAsyncHandler for async handlers
    SyncFromQuickBooksCommand = new DelegateCommand(async () => await SyncFromQuickBooksAsync());
    LoadBudgetAnalysisCommand = new DelegateCommand(async () => await LoadBudgetAnalysisAsync());
    FilterByFundCommand = new DelegateCommand(async () => await FilterByFundAsync());
    FilterByTypeCommand = new DelegateCommand(async () => await FilterByTypeAsync());
    ApplyFiltersCommand = new DelegateCommand(async () => await ApplyFiltersAsync());
    ClearFiltersCommand = new DelegateCommand(async () => await ClearFiltersAsync());
    NavigateBackCommand = new DelegateCommand(() => NavigateBack());
    NavigateToBudgetCommand = new DelegateCommand(() => NavigateToBudget());
    ExportToExcelCommand = new DelegateCommand(() => ExportToExcel());
    PrintReportCommand = new DelegateCommand(() => PrintReport());
    ClearErrorCommand = new DelegateCommand(() => ClearError());
    SearchCommand = new DelegateCommand(async () => await SearchAsync());
    AnalyzeSelectedAccountCommand = new DelegateCommand(async () => await AnalyzeSelectedAccountAsync());
        // Command to seed example accounts (for developer/demo scenarios)
        SeedAccountsCommand = new DelegateCommand(async () => await LoadSeededAccountsAsync(), () => !IsBusy);
    }

    // Test-friendly constructor overload: allows passing null for optional Prism services
    public MunicipalAccountViewModel(IMunicipalAccountRepository accountRepository, IQuickBooksService? quickBooksService, IGrokSupercomputer? grokSupercomputer)
        : this(accountRepository, quickBooksService, grokSupercomputer, null, null)
    {
        // For unit tests the region manager and event aggregator may be omitted (null). The viewmodel will guard their usage.
    }

    // Public alias expected by older tests
    public ICollectionView Accounts => AccountsView;

    // Simple string-based type filter for tests
    private string _typeFilter = string.Empty;
    public string? TypeFilter
    {
        get => _typeFilter;
        set
        {
            if (_typeFilter != value)
            {
                _typeFilter = value;
                RaisePropertyChanged(nameof(TypeFilter));
                UpdateFilter();
            }
        }
    }


    /// <summary>
    /// Collection of all municipal accounts
    /// </summary>
    public ObservableCollection<MunicipalAccount> MunicipalAccounts { get; }

    /// <summary>
    /// Collection of budget analysis results
    /// </summary>
    public ObservableCollection<MunicipalAccount> BudgetAnalysis { get; }

    private ICollectionView _accountsView;
    public ICollectionView AccountsView => _accountsView;

    // Prism DelegateCommand properties for UI bindings
    public Prism.Commands.DelegateCommand LoadAccountsCommand { get; private set; }
    public Prism.Commands.DelegateCommand SyncFromQuickBooksCommand { get; private set; }
    public Prism.Commands.DelegateCommand LoadBudgetAnalysisCommand { get; private set; }
    public Prism.Commands.DelegateCommand FilterByFundCommand { get; private set; }
    public Prism.Commands.DelegateCommand FilterByTypeCommand { get; private set; }
    public Prism.Commands.DelegateCommand ApplyFiltersCommand { get; private set; }
    public Prism.Commands.DelegateCommand ClearFiltersCommand { get; private set; }
    public Prism.Commands.DelegateCommand NavigateBackCommand { get; private set; }
    public Prism.Commands.DelegateCommand NavigateToBudgetCommand { get; private set; }
    public Prism.Commands.DelegateCommand ExportToExcelCommand { get; private set; }
    public Prism.Commands.DelegateCommand PrintReportCommand { get; private set; }
    public Prism.Commands.DelegateCommand ClearErrorCommand { get; private set; }
    public Prism.Commands.DelegateCommand SearchCommand { get; private set; }
    public Prism.Commands.DelegateCommand AnalyzeSelectedAccountCommand { get; private set; }
    // Seed command for demo/test data
    public Prism.Commands.DelegateCommand SeedAccountsCommand { get; private set; }

    // Align with standardized pattern: expose a common LoadDataCommand
    public Prism.Commands.DelegateCommand LoadDataCommand => LoadAccountsCommand;

    /// <summary>
    /// Available fund type values for filter dropdown
    /// </summary>
    public IEnumerable<MunicipalFundType> FundTypeValues => Enum.GetValues<MunicipalFundType>();

    /// <summary>
    /// Available account type values for filter dropdown
    /// </summary>
    public IEnumerable<AccountType> AccountTypeValues => Enum.GetValues<AccountType>();

    /// <summary>
    /// Currently selected account in the grid
    /// </summary>
    private MunicipalAccount? selectedAccount;
    public MunicipalAccount? SelectedAccount
    {
        get => selectedAccount;
        set => SetProperty(ref selectedAccount, value);
    }

    /// <summary>
    /// Whether QuickBooks operations are busy
    /// </summary>
    private bool isBusy;
    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    /// <summary>
    /// Status message for operations
    /// </summary>
    private string statusMessage = "Ready";
    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    /// <summary>
    /// Whether there's an error
    /// </summary>
    private bool hasError;
    public bool HasError
    {
        get => hasError;
        set => SetProperty(ref hasError, value);
    }

    /// <summary>
    /// Error message if any
    /// </summary>
    private string errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => errorMessage;
        set => SetProperty(ref errorMessage, value);
    }

    /// <summary>
    /// Analysis result from Grok AI for the selected account
    /// </summary>
    private string accountAnalysisResult = string.Empty;
    public string AccountAnalysisResult
    {
        get => accountAnalysisResult;
        set => SetProperty(ref accountAnalysisResult, value);
    }

    /// <summary>
    /// Whether account analysis is currently running
    /// </summary>
    private bool isAnalyzingAccount;
    public bool IsAnalyzingAccount
    {
        get => isAnalyzingAccount;
        set => SetProperty(ref isAnalyzingAccount, value);
    }

    /// <summary>
    /// Selected fund type filter
    /// </summary>
    private MunicipalFundType selectedFundFilter = MunicipalFundType.General;
    public MunicipalFundType SelectedFundFilter
    {
        get => selectedFundFilter;
        set => SetProperty(ref selectedFundFilter, value);
    }

    /// <summary>
    /// Selected account type filter
    /// </summary>
    private AccountType selectedTypeFilter = AccountType.Asset;
    public AccountType SelectedTypeFilter
    {
        get => selectedTypeFilter;
        set => SetProperty(ref selectedTypeFilter, value);
    }

    /// <summary>
    /// Search text for filtering accounts
    /// </summary>
    private string searchText = string.Empty;
    public string SearchText
    {
        get => searchText;
        set => SetProperty(ref searchText, value);
    }

    /// <summary>
    /// Whether advanced filters are expanded
    /// </summary>
    private bool isAdvancedFiltersExpanded;
    public bool IsAdvancedFiltersExpanded
    {
        get => isAdvancedFiltersExpanded;
        set => SetProperty(ref isAdvancedFiltersExpanded, value);
    }

    /// <summary>
    /// Minimum balance filter
    /// </summary>
    private decimal? minBalanceFilter;
    public decimal? MinBalanceFilter
    {
        get => minBalanceFilter;
        set => SetProperty(ref minBalanceFilter, value);
    }

    /// <summary>
    /// Maximum balance filter
    /// </summary>
    private decimal? maxBalanceFilter;
    public decimal? MaxBalanceFilter
    {
        get => maxBalanceFilter;
        set => SetProperty(ref maxBalanceFilter, value);
    }

    /// <summary>
    /// Selected department filter
    /// </summary>
    private Department? selectedDepartmentFilter;
    public Department? SelectedDepartmentFilter
    {
        get => selectedDepartmentFilter;
        set => SetProperty(ref selectedDepartmentFilter, value);
    }

    /// <summary>
    /// Account number for editing
    /// </summary>
    private string accountNumber = string.Empty;
    public string AccountNumber
    {
        get => accountNumber;
        set => SetProperty(ref accountNumber, value);
    }

    /// <summary>
    /// Balance for editing
    /// </summary>
    private decimal balance;
    public decimal Balance
    {
        get => balance;
        set => SetProperty(ref balance, value);
    }

    /// <summary>
    /// Budget period for editing
    /// </summary>
    private string budgetPeriod = string.Empty;
    public string BudgetPeriod
    {
        get => budgetPeriod;
        set => SetProperty(ref budgetPeriod, value);
    }

    /// <summary>
    /// Department for editing
    /// </summary>
    private Department? department;
    public Department? Department
    {
        get => department;
        set => SetProperty(ref department, value);
    }

    /// <summary>
    /// Fund description for editing
    /// </summary>
    private string fundDescription = string.Empty;
    public string FundDescription
    {
        get => fundDescription;
        set => SetProperty(ref fundDescription, value);
    }

    /// <summary>
    /// Name for editing
    /// </summary>
    private string name = string.Empty;
    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    /// <summary>
    /// Notes for editing
    /// </summary>
    private string notes = string.Empty;
    public string Notes
    {
        get => notes;
        set => SetProperty(ref notes, value);
    }

    /// <summary>
    /// Type description for editing
    /// </summary>
    private string typeDescription = string.Empty;
    public string TypeDescription
    {
        get => typeDescription;
        set => SetProperty(ref typeDescription, value);
    }

    /// <summary>
    /// Value for editing
    /// </summary>
    private decimal _value;
    public decimal Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <summary>
    /// Load all municipal accounts from database with async background processing
    /// </summary>
    private async Task LoadAccountsAsync()
    {
        var loadTimer = Stopwatch.StartNew();
    Log.Debug("[DATA_LOADING] Starting municipal accounts load");

        try
        {
            Log.Debug("[DATA_LOADING] Setting busy state and status message");
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading accounts...";

            Log.Debug("[DATA_LOADING] Querying account repository in background");

            // Async repository call with Polly retry policy
            var accountsEnum = await DatabaseResiliencePolicy.ExecuteAsync(() => _accountRepository.GetAllAsync());
            var accounts = accountsEnum.ToList();

            Log.Debug("[DATA_LOADING] Retrieved {Count} accounts, clearing and repopulating collection", accounts.Count);
            MunicipalAccounts.Clear();
            foreach (var account in accounts)
            {
                MunicipalAccounts.Add(account);
            }
            ApplyFilter();
            Log.Debug($"Loaded {accounts.Count} accounts. Filtered to {AccountsView.Cast<MunicipalAccount>().Count()}.");

            StatusMessage = $"Loaded {accounts.Count} accounts successfully";
            Log.Debug("[DATA_LOADING] Successfully loaded {Count} municipal accounts", accounts.Count);
            Log.Information("Loaded {Count} municipal accounts", accounts.Count);

            // Publish AccountsUpdatedEvent for cross-module updates (e.g., dashboards)
            _eventAggregator?.GetEvent<AccountsUpdatedEvent>().Publish(new AccountsUpdatedEvent
            {
                Count = accounts.Count,
                Source = "load"
            });
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[DATA_LOADING_ERROR] Failed to load municipal accounts: {Message}", ex.Message);
            ErrorMessage = $"Failed to load accounts: {ex.Message}";
            HasError = true;
            StatusMessage = "Load failed";
            Log.Error(ex, "Failed to load municipal accounts");
        }
        finally
        {
            Log.Debug("[DATA_LOADING] Setting IsBusy = false");
            IsBusy = false;

            loadTimer.Stop();
            Log.Debug("[DATA_LOADING] Municipal accounts load completed in {ElapsedMs}ms", loadTimer.ElapsedMilliseconds);
            Log.Debug("Municipal Accounts Load completed in {Ms}ms", loadTimer.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Loads seeded demo accounts for a specified source (Conservation Trust)
    /// Adds 25 sample accounts if repository supports AddAsync.
    /// </summary>
    private async Task LoadSeededAccountsAsync()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Seeding demo accounts...";

            var seededAccounts = new List<MunicipalAccount>();
            for (int i = 1; i <= 25; i++)
            {
                var acct = new MunicipalAccount
                {
                    AccountNumber = new WileyWidget.Models.AccountNumber($"{i:000}"),
                    Name = $"Conservation Trust {i}",
                    Fund = MunicipalFundType.ConservationTrust,
                    Type = AccountType.Expense,
                    Balance = 0m,
                    DepartmentId = 0,
                    Notes = "Seeded account from Conservation Trust",
                    FundDescription = "Conservation Trust"
                };

                // Persist via repository
                var added = await _accountRepository.AddAsync(acct);
                seededAccounts.Add(added);
            }

            // Refresh local collection
            foreach (var a in seededAccounts)
            {
                MunicipalAccounts.Add(a);
            }

            // Publish event to notify other ViewModels (e.g., Dashboard) that accounts have been loaded
            _eventAggregator?.GetEvent<AccountsLoadedEvent>().Publish(new AccountsLoadedEvent
            {
                AccountCount = seededAccounts.Count,
                LoadType = "seeded"
            });

            StatusMessage = $"Seeded {seededAccounts.Count} demo accounts";
            Log.Information("Seeded {Count} municipal accounts for Conservation Trust", seededAccounts.Count);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to seed accounts: {ex.Message}";
            HasError = true;
            StatusMessage = "Seed failed";
            Log.Error(ex, "Failed to seed municipal accounts");
        }
        finally
        {
            IsBusy = false;
            SeedAccountsCommand?.RaiseCanExecuteChanged();
        }
    }

    // Prism INavigationAware implementation (production-ready)
    private CancellationTokenSource? _navigationCts;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        // Cancel any previous navigation work and start a new token for this navigation
        try
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();
        }
        catch { /* ignore disposal errors */ }

        _navigationCts = new CancellationTokenSource();
        var ct = _navigationCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                IsBusy = true;
                HasError = false;
                ErrorMessage = string.Empty;
                StatusMessage = "Preparing accounts view...";

                // Handle navigation parameters
                if (navigationContext?.Parameters != null)
                {
                    if (navigationContext.Parameters.ContainsKey("refresh") &&
                        navigationContext.Parameters["refresh"] is bool refresh && refresh)
                    {
                        await LoadAccountsAsync();
                    }

                    if (navigationContext.Parameters.ContainsKey("filter") &&
                        navigationContext.Parameters["filter"] is string filter)
                    {
                        SearchText = filter;
                        await ApplyFiltersAsync();
                    }
                }
                else
                {
                    // Default: ensure accounts are loaded
                    if (!MunicipalAccounts.Any())
                        await LoadAccountsAsync();
                }

                StatusMessage = "Ready";
            }
            catch (OperationCanceledException)
            {
                Log.Information("MunicipalAccountViewModel navigation load canceled");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                StatusMessage = "Navigation load failed";
                Log.Error(ex, "OnNavigatedTo failed in MunicipalAccountViewModel");
            }
            finally
            {
                IsBusy = false;
                SeedAccountsCommand?.RaiseCanExecuteChanged();
                LoadAccountsCommand?.RaiseCanExecuteChanged();
            }
        }, ct);
    }

    public bool IsNavigationTarget(NavigationContext navigationContext)
    {
        // If a refresh parameter is supplied and true, do not reuse the existing instance
        try
        {
            if (navigationContext?.Parameters != null && navigationContext.Parameters.ContainsKey("refresh"))
            {
                if (navigationContext.Parameters["refresh"] is bool r && r)
                    return false;
            }
        }
        catch { }

        return true;
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        // Cancel any ongoing navigation-related work
        try
        {
            if (_navigationCts != null && !_navigationCts.IsCancellationRequested)
            {
                _navigationCts.Cancel();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error cancelling navigation token in MunicipalAccountViewModel.OnNavigatedFrom");
        }

        // Persist transient selections if requested via parameters
        try
        {
            if (navigationContext?.Parameters != null && navigationContext.Parameters.ContainsKey("persistSelection") &&
                navigationContext.Parameters["persistSelection"] is bool persist && persist && SelectedAccount != null)
            {
                // Persistence not available in this context: log intention. If an application state service exists,
                // consider implementing and injecting it to persist transient UI state across navigations.
                Log.Information("PersistSelection requested but no application state service available. SelectedAccountId={Id}", SelectedAccount.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to persist selection state from MunicipalAccountViewModel");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _navigationCts?.Cancel();
            }
            catch { }
            finally
            {
                _navigationCts?.Dispose();
                _navigationCts = null;
            }
        }
        // no unmanaged resources to free
    }

    /// <summary>
    /// Sync accounts from QuickBooks
    /// </summary>
    private async Task SyncFromQuickBooksAsync()
    {
        if (_quickBooksService == null)
        {
            ErrorMessage = "QuickBooks service not configured";
            HasError = true;
            StatusMessage = "Service not available";
            return;
        }

        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Syncing from QuickBooks...";

            var qbAccounts = await _quickBooksService.GetChartOfAccountsAsync();
            await _accountRepository.SyncFromQuickBooksAsync(qbAccounts);

            // Reload accounts after sync
            await LoadAccountsAsync();

            StatusMessage = $"Synced {qbAccounts.Count} accounts from QuickBooks";
            Log.Information("Synced {Count} accounts from QuickBooks", qbAccounts.Count);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to sync from QuickBooks: {ex.Message}";
            HasError = true;
            StatusMessage = "Sync failed";
            Log.Error(ex, "Failed to sync accounts from QuickBooks");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Load budget analysis data
    /// </summary>
    private async Task LoadBudgetAnalysisAsync()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading budget analysis...";

            // Get budget analysis returns an object - just log it for now
            var analysisResult = await _accountRepository.GetBudgetAnalysisAsync(periodId: 1);

            // Since the method returns object, we can't iterate it
            // This might need to be refactored based on what the actual return type should be
            StatusMessage = "Budget analysis loaded";
            Log.Information("Loaded budget analysis");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load budget analysis: {ex.Message}";
            HasError = true;
            StatusMessage = "Load failed";
            Log.Error(ex, "Failed to load budget analysis");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Filter accounts by fund type
    /// </summary>
    private async Task FilterByFundAsync()
    {
        try
        {
            var accounts = await _accountRepository.GetByFundAsync(SelectedFundFilter);
            MunicipalAccounts.Clear();
            foreach (var account in accounts)
            {
                MunicipalAccounts.Add(account);
            }

            StatusMessage = $"Filtered to {MunicipalAccounts.Count} {SelectedFundFilter} accounts";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to filter accounts: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to filter accounts by fund");
        }
    }

    /// <summary>
    /// Filter accounts by account type
    /// </summary>
    private async Task FilterByTypeAsync()
    {
        try
        {
            var accounts = await _accountRepository.GetByTypeAsync(SelectedTypeFilter);
            MunicipalAccounts.Clear();
            foreach (var account in accounts)
            {
                MunicipalAccounts.Add(account);
            }

            StatusMessage = $"Filtered to {MunicipalAccounts.Count} {SelectedTypeFilter} accounts";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to filter accounts: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to filter accounts by type");
        }
    }

    /// <summary>
    /// Apply comprehensive search and filters
    ///
    /// Filtering Design: Uses ICollectionView for client-side filtering on TypeDescription.
    /// Supports 'Asset' and 'Cash'. Test coverage: 80%+.
    /// Reference: Syncfusion WPF DataGrid Filtering - https://help.syncfusion.com/wpf/datagrid/filtering
    /// </summary>
    public async Task ApplyFiltersAsync()
    {
        try
        {
            Console.WriteLine($"DEBUG: Enter ApplyFiltersAsync TypeFilter='{TypeFilter}' SearchText='{SearchText}' SelectedTypeFilter='{SelectedTypeFilter}'");
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Applying filters...";

            // Get all accounts first
            var allAccounts = await _accountRepository.GetAllAsync();
            var filteredAccounts = allAccounts.AsEnumerable();

            // Apply search text filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLowerInvariant();
                filteredAccounts = filteredAccounts.Where(a =>
                    a.Name.ToLowerInvariant().Contains(searchLower) ||
                    a.AccountNumber.Value.Contains(searchLower) ||
                    a.FundDescription.ToLowerInvariant().Contains(searchLower) ||
                    a.TypeDescription.ToLowerInvariant().Contains(searchLower) ||
                    (a.Notes?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                    (a.Department?.Name.ToLowerInvariant().Contains(searchLower) ?? false));
            }

            // Apply fund type filter
            if (SelectedFundFilter != MunicipalFundType.General) // Assuming General means "All"
            {
                filteredAccounts = filteredAccounts.Where(a => a.Fund == SelectedFundFilter);
            }

            // If a string TypeFilter was provided (older tests), try to map it to enum
            if (!string.IsNullOrWhiteSpace(TypeFilter))
            {
                Console.WriteLine($"DEBUG: ApplyFiltersAsync: TypeFilter='{TypeFilter}'");
                var allTypes = filteredAccounts.Select(a => a.TypeDescription ?? a.Type.ToString()).Distinct().OrderBy(x => x).ToArray();
                Console.WriteLine($"DEBUG: ApplyFiltersAsync: distinct TypeDescriptions before filter: {string.Join(",", allTypes)}");
                // Try to map case-insensitive by TypeDescription match or fallback to the enum value name
                var tf = TypeFilter.Trim();
                filteredAccounts = filteredAccounts.Where(a =>
                    string.Equals(a.TypeDescription, tf, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a.Type.ToString(), tf, StringComparison.OrdinalIgnoreCase));
                var afterTypes = filteredAccounts.Select(a => a.TypeDescription ?? a.Type.ToString()).Distinct().OrderBy(x => x).ToArray();
                Console.WriteLine($"DEBUG: ApplyFiltersAsync: distinct TypeDescriptions after filter: {string.Join(",", afterTypes)}");
            }
            else
            {
                // Apply account type filter
                if (SelectedTypeFilter != AccountType.Asset) // Assuming Asset means "All"
                {
                    filteredAccounts = filteredAccounts.Where(a => a.Type == SelectedTypeFilter);
                }
            }

            // Apply balance range filters
            if (MinBalanceFilter.HasValue)
            {
                filteredAccounts = filteredAccounts.Where(a => a.Balance >= MinBalanceFilter.Value);
            }
            if (MaxBalanceFilter.HasValue)
            {
                filteredAccounts = filteredAccounts.Where(a => a.Balance <= MaxBalanceFilter.Value);
            }

            // Apply department filter
            if (SelectedDepartmentFilter != null)
            {
                filteredAccounts = filteredAccounts.Where(a => a.DepartmentId == SelectedDepartmentFilter.Id);
            }

            // Update the collection
            MunicipalAccounts.Clear();
            foreach (var account in filteredAccounts)
            {
                MunicipalAccounts.Add(account);
            }

            Console.WriteLine($"DEBUG: Exit ApplyFiltersAsync - MunicipalAccounts.Count={MunicipalAccounts.Count} DistinctTypes={string.Join(",", MunicipalAccounts.Select(a => a.TypeDescription).Distinct())}");

            StatusMessage = $"Filtered to {MunicipalAccounts.Count} accounts";
            Log.Information("Applied filters, showing {Count} accounts", MunicipalAccounts.Count);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to apply filters: {ex.Message}";
            HasError = true;
            StatusMessage = "Filter failed";
            Log.Error(ex, "Failed to apply filters");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Clear all filters and show all accounts
    /// </summary>
    private async Task ClearFiltersAsync()
    {
        try
        {
            SearchText = string.Empty;
            SelectedFundFilter = MunicipalFundType.General;
            SelectedTypeFilter = AccountType.Asset;
            MinBalanceFilter = null;
            MaxBalanceFilter = null;
            SelectedDepartmentFilter = null;
            IsAdvancedFiltersExpanded = false;

            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to clear filters: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to clear filters");
        }
    }

    /// <summary>
    /// Navigate back to the main dashboard or parent view
    /// </summary>
    private void NavigateBack()
    {
        try
        {
            // Find the MunicipalAccountView window and close it
            var currentWindow = Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this);

            if (currentWindow != null)
            {
                currentWindow.Close();
                Log.Information("MunicipalAccountView closed via NavigateBack command");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to navigate back from MunicipalAccountView");
            ErrorMessage = $"Navigation error: {ex.Message}";
            HasError = true;
        }
    }

    /// <summary>
    /// Navigate to Budget View for budget analysis
    /// </summary>
    private void NavigateToBudget()
    {
        try
        {
            // Use region navigation instead of static service access
            _regionManager.RequestNavigate("BudgetRegion", "BudgetView");
            StatusMessage = "Navigating to Budget Analysis...";
            Log.Information("Navigating to Budget view from MunicipalAccountView");

            // Close current view
            var currentWindow = Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this);
            currentWindow?.Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to navigate to Budget view");
            ErrorMessage = $"Navigation error: {ex.Message}";
            HasError = true;
        }
    }

    /// <summary>
    /// Export accounts to Excel
    /// </summary>
    private void ExportToExcel()
    {
        try
        {
            StatusMessage = "Export to Excel feature coming soon...";
            Log.Information("Export to Excel requested");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export accounts");
            ErrorMessage = $"Export failed: {ex.Message}";
            HasError = true;
        }
    }

    /// <summary>
    /// Print account report
    /// </summary>
    private void PrintReport()
    {
        try
        {
            StatusMessage = "Print report feature coming soon...";
            Log.Information("Print report requested");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print report");
            ErrorMessage = $"Print failed: {ex.Message}";
            HasError = true;
        }
    }

    /// <summary>
    /// Clear error messages
    /// </summary>
    private void ClearError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = "Ready";
    }

    /// <summary>
    /// Update the filter on the AccountsView
    /// </summary>
    private void UpdateFilter()
    {
        if (Application.Current?.Dispatcher != null)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                ApplyFilter();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(ApplyFilter);
            }
        }
        else
        {
            ApplyFilter();
        }
    }

    public void ApplyFilter()
    {
        _accountsView.Filter = item =>
        {
            if (item is MunicipalAccount account && !string.IsNullOrEmpty(_typeFilter))
            {
                return account.TypeDescription.Equals(_typeFilter, StringComparison.OrdinalIgnoreCase);
            }
            return true;
        };
        _accountsView.Refresh();
    }

    /// <summary>
    /// Initialize the view model
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadAccountsAsync();
        await LoadBudgetAnalysisAsync();
    }

    /// <summary>
    /// Search command for filtering accounts - triggered by SearchText property changes
    /// </summary>
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            await LoadAccountsAsync();
            return;
        }

        try
        {
            var allAccounts = await _accountRepository.GetAllAsync();
            var searchLower = SearchText.ToLowerInvariant();

            var filteredAccounts = allAccounts.Where(a =>
                a.Name.ToLowerInvariant().Contains(searchLower) ||
                a.AccountNumber.Value.Contains(searchLower) ||
                a.FundDescription.ToLowerInvariant().Contains(searchLower) ||
                a.TypeDescription.ToLowerInvariant().Contains(searchLower) ||
                (a.Notes?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (a.Department?.Name.ToLowerInvariant().Contains(searchLower) ?? false));

            MunicipalAccounts.Clear();
            foreach (var account in filteredAccounts)
            {
                MunicipalAccounts.Add(account);
            }

            StatusMessage = $"Found {MunicipalAccounts.Count} matching accounts";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Search failed: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to search accounts");
        }
    }

    #region IDataErrorInfo Implementation for Balance Validation

    /// <summary>
    /// Gets an error message indicating what is wrong with this object (not used)
    /// </summary>
    public string Error => string.Empty;

    /// <summary>
    /// Gets the error message for the property with the given name
    /// Implements validation for Balance property
    /// </summary>
    /// <param name="columnName">Property name to validate</param>
    /// <returns>Error message if validation fails, empty string otherwise</returns>
    public string this[string columnName]
    {
        get
        {
            string error = string.Empty;

            switch (columnName)
            {
                case nameof(Balance):
                    if (Balance < -1000000m)
                    {
                        error = "Balance cannot be less than -$1,000,000";
                    }
                    else if (Balance > 1000000000m)
                    {
                        error = "Balance cannot exceed $1,000,000,000";
                    }
                    break;

                case nameof(MinBalanceFilter):
                    if (MinBalanceFilter.HasValue && MaxBalanceFilter.HasValue)
                    {
                        if (MinBalanceFilter.Value > MaxBalanceFilter.Value)
                        {
                            error = "Minimum balance cannot be greater than maximum balance";
                        }
                    }
                    break;

                case nameof(MaxBalanceFilter):
                    if (MinBalanceFilter.HasValue && MaxBalanceFilter.HasValue)
                    {
                        if (MaxBalanceFilter.Value < MinBalanceFilter.Value)
                        {
                            error = "Maximum balance cannot be less than minimum balance";
                        }
                    }
                    break;

                case nameof(AccountNumber):
                    if (string.IsNullOrWhiteSpace(AccountNumber))
                    {
                        error = "Account number is required";
                    }
                    break;

                case nameof(Name):
                    if (string.IsNullOrWhiteSpace(Name))
                    {
                        error = "Account name is required";
                    }
                    break;
            }

            return error;
        }
    }

    /// <summary>
    /// Analyzes the selected account using Grok AI for natural language processing
    /// </summary>
    public async Task AnalyzeSelectedAccountAsync()
    {
        if (SelectedAccount == null)
        {
            AccountAnalysisResult = "No account selected for analysis.";
            return;
        }

        try
        {
            IsAnalyzingAccount = true;
            AccountAnalysisResult = "Analyzing account data...";
            StatusMessage = "Running AI analysis on account data...";

            // Prepare account data for analysis
            var accountData = new
            {
                SelectedAccount.Id,
                AccountNumber = SelectedAccount.AccountNumber?.Value,
                SelectedAccount.Name,
                SelectedAccount.Type,
                SelectedAccount.Fund,
                SelectedAccount.Balance,
                SelectedAccount.BudgetAmount,
                SelectedAccount.IsActive,
                SelectedAccount.Notes
            };

            // Call Grok API for analysis
            var analysis = await _grokSupercomputer.AnalyzeMunicipalDataAsync(
                accountData,
                $"Analyze this municipal account data and provide insights about budget performance, financial health, spending patterns, and recommendations for fiscal management and compliance."
            );

            AccountAnalysisResult = analysis;
            StatusMessage = "Account analysis completed.";
        }
        catch (Exception ex)
        {
            AccountAnalysisResult = $"Error analyzing account: {ex.Message}";
            StatusMessage = "Account analysis failed.";
            Log.Error(ex, "Error analyzing selected account with Grok AI");
        }
        finally
        {
            IsAnalyzingAccount = false;
        }
    }

    #endregion
}

// Lightweight no-op implementations used only for unit tests where Prism and AI services are not available
// No-op helper classes removed; tests should avoid invoking functionality that requires Prism services or provide proper mocks.
