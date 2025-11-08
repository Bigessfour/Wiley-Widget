using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Messages;

namespace WileyWidget.ViewModels.Main;

/// <summary>
/// View model for budget analysis and reporting
/// Provides comprehensive budget insights and financial analysis
/// Implements messaging, busy states, input validation, and IDataErrorInfo
/// </summary>
public partial class BudgetViewModel : BindableBase, IDisposable, IDataErrorInfo, INavigationAware
{
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IMunicipalAccountRepository? _municipalAccountRepository;
    private readonly IEventAggregator? _eventAggregator;
    private readonly ICacheService? _cacheService;
    private readonly IDispatcherHelper? _dispatcherHelper;
    // NOTE: ThemeManager removed - SfSkinManager.ApplicationTheme handles theming globally
    private readonly DispatcherTimer _refreshTimer;
    private bool _disposed;
    private Task? _cacheLoadingTask;

    /// <summary>
    /// Collection of budget details for each enterprise
    /// </summary>
    public ObservableCollection<BudgetDetailItem> BudgetDetails { get; } = new();

    /// <summary>
    /// Total revenue across all enterprises
    /// </summary>
    private decimal _totalRevenue;
    public decimal TotalRevenue
    {
        get => _totalRevenue;
        set => SetProperty(ref _totalRevenue, value);
    }

    /// <summary>
    /// Total expenses across all enterprises
    /// </summary>
    private decimal _totalExpenses;
    public decimal TotalExpenses
    {
        get => _totalExpenses;
        set => SetProperty(ref _totalExpenses, value);
    }

    /// <summary>
    /// Net balance (revenue - expenses)
    /// </summary>
    private decimal _netBalance;
    public decimal NetBalance
    {
        get => _netBalance;
        set => SetProperty(ref _netBalance, value);
    }

    /// <summary>
    /// Total citizens served across all enterprises
    /// </summary>
    private int _totalCitizens;
    public int TotalCitizens
    {
        get => _totalCitizens;
        set => SetProperty(ref _totalCitizens, value);
    }

    /// <summary>
    /// Break-even analysis text
    /// </summary>
    private string _breakEvenAnalysisText = "Click 'Break-even Analysis' to generate insights";
    public string BreakEvenAnalysisText
    {
        get => _breakEvenAnalysisText;
        set => SetProperty(ref _breakEvenAnalysisText, value);
    }

    /// <summary>
    /// Trend analysis text
    /// </summary>
    private string _trendAnalysisText = "Click 'Trend Analysis' to view budget trends";
    public string TrendAnalysisText
    {
        get => _trendAnalysisText;
        set => SetProperty(ref _trendAnalysisText, value);
    }

    /// <summary>
    /// Recommendations text
    /// </summary>
    private string _recommendationsText = "Click 'Refresh' to load budget data and generate recommendations";
    public string RecommendationsText
    {
        get => _recommendationsText;
        set => SetProperty(ref _recommendationsText, value);
    }

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    private string _lastUpdated = "Never";
    public string LastUpdated
    {
        get => _lastUpdated;
        set => SetProperty(ref _lastUpdated, value);
    }

    /// <summary>
    /// Analysis status
    /// </summary>
    private string _analysisStatus = "Ready";
    public string AnalysisStatus
    {
        get => _analysisStatus;
        set => SetProperty(ref _analysisStatus, value);
    }

    /// <summary>
    /// Whether there's an error
    /// </summary>
    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    /// <summary>
    /// Error message if any
    /// </summary>
    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// Busy state for long-running operations
    /// </summary>
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                // Raise CanExecuteChanged for all commands that depend on IsBusy
                RefreshBudgetDataCommand?.RaiseCanExecuteChanged();
                ToggleFiscalYearCommand?.RaiseCanExecuteChanged();
                SaveConfirmationCommand?.RaiseCanExecuteChanged();
                NavigateToMunicipalAccountCommand?.RaiseCanExecuteChanged();
                ImportBudgetCommand?.RaiseCanExecuteChanged();
                ExportBudgetCommand?.RaiseCanExecuteChanged();
                AddAccountCommand?.RaiseCanExecuteChanged();
                DeleteAccountCommand?.RaiseCanExecuteChanged();
                UpdateAnalysisCommandStates();
            }
        }
    }

    /// <summary>
    /// Progress value for import/export operations (0-100)
    /// </summary>
    private double _progressValue;
    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    /// <summary>
    /// Maximum progress value
    /// </summary>
    private double _progressMaximum = 100;
    public double ProgressMaximum
    {
        get => _progressMaximum;
        set => SetProperty(ref _progressMaximum, value);
    }

    /// <summary>
    /// Progress text for status updates
    /// </summary>
    private string _progressText = string.Empty;
    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    /// <summary>
    /// Budget items collection
    /// </summary>
    public ObservableCollection<BudgetDetailItem> BudgetItems { get; } = new();

    /// <summary>
    /// Budget performance data
    /// </summary>
    public ObservableCollection<BudgetPerformanceData> BudgetPerformanceData { get; } = new();

    /// <summary>
    /// Budget variance value
    /// </summary>
    private decimal _budgetVariance;
    public decimal BudgetVariance
    {
        get => _budgetVariance;
        set => SetProperty(ref _budgetVariance, value);
    }

    /// <summary>
    /// Loading state
    /// </summary>
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshBudgetDataCommand?.RaiseCanExecuteChanged();
                ToggleFiscalYearCommand?.RaiseCanExecuteChanged();
                SaveConfirmationCommand?.RaiseCanExecuteChanged();
                ImportBudgetCommand?.RaiseCanExecuteChanged();
                ExportBudgetCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Net income
    /// </summary>
    private decimal _netIncome;
    public decimal NetIncome
    {
        get => _netIncome;
        set => SetProperty(ref _netIncome, value);
    }

    /// <summary>
    /// Projected rate data
    /// </summary>
    public ObservableCollection<ProjectedRateData> ProjectedRateData { get; } = new();

    /// <summary>
    /// Rate trend data
    /// </summary>
    public ObservableCollection<RateTrendData> RateTrendData { get; } = new();

    /// <summary>
    /// Hierarchical collection of budget accounts with parent-child relationships
    /// </summary>
    public ObservableCollection<BudgetAccount> BudgetAccounts { get; } = new();

    /// <summary>
    /// Collection of available fund types for dropdown editors
    /// </summary>
    public ObservableCollection<BudgetFundType> FundTypes { get; } = BudgetFundType.GetStandardFundTypes();

    /// <summary>
    /// Collection of fiscal years for selection
    /// </summary>
    public ObservableCollection<string> FiscalYears { get; } = new()
    {
        "FY 2023", "FY 2024", "FY 2025", "FY 2026"
    };

    /// <summary>
    /// Currently selected fiscal year
    /// </summary>
    private string _selectedFiscalYear = "FY 2025";
    public string SelectedFiscalYear
    {
        get => _selectedFiscalYear;
        set
        {
            if (SetProperty(ref _selectedFiscalYear, value))
            {
                ToggleFiscalYearCommand?.RaiseCanExecuteChanged();
                OnSelectedFiscalYearChanged(value);
            }
        }
    }

    /// <summary>
    /// Total budgeted amount across all accounts
    /// </summary>
    private decimal _totalBudgetAmount;
    public decimal TotalBudget
    {
        get => _totalBudgetAmount;
        set => SetProperty(ref _totalBudgetAmount, value);
    }

    /// <summary>
    /// Total actual expenses across all accounts
    /// </summary>
    private decimal _totalActualAmount;
    public decimal TotalActual
    {
        get => _totalActualAmount;
        set => SetProperty(ref _totalActualAmount, value);
    }

    /// <summary>
    /// Total variance (Budget - Actual)
    /// </summary>
    private decimal _totalVarianceAmount;
    public decimal TotalVariance
    {
        get => _totalVarianceAmount;
        set => SetProperty(ref _totalVarianceAmount, value);
    }

    /// <summary>
    /// Budget distribution data for pie chart
    /// </summary>
    public ObservableCollection<BudgetDistributionData> BudgetDistributionData { get; } = new();

    /// <summary>
    /// Budget comparison data for bar chart
    /// </summary>
    public ObservableCollection<BudgetComparisonData> BudgetComparisonData { get; } = new();

    /// <summary>
    /// Foreground color (for UI binding)
    /// </summary>
    private string _foreground = "#000000";
    public string Foreground
    {
        get => _foreground;
        set => SetProperty(ref _foreground, value);
    }

    /// <summary>
    /// Whether budget is over budget
    /// </summary>
    private bool _isOverBudget;
    public bool IsOverBudget
    {
        get => _isOverBudget;
        set => SetProperty(ref _isOverBudget, value);
    }

    /// <summary>
    /// Percentage value
    /// </summary>
    private decimal _percentage;
    public decimal Percentage
    {
        get => _percentage;
        set => SetProperty(ref _percentage, value);
    }

    /// <summary>
    /// Collection of budgets
    /// </summary>
    public ObservableCollection<OverallBudget> Budgets { get; } = new();

    /// <summary>
    /// Selected budget
    /// </summary>
    private OverallBudget? _selectedBudget;
    public OverallBudget? SelectedBudget
    {
        get => _selectedBudget;
        set => SetProperty(ref _selectedBudget, value);
    }

    /// <summary>
    /// Collection of budget periods
    /// </summary>
    public ObservableCollection<BudgetPeriod> BudgetPeriods { get; } = new();

    /// <summary>
    /// Collection of budget entries
    /// </summary>
    public ObservableCollection<BudgetEntry> BudgetEntries { get; } = new();

    /// <summary>
    /// Budget analysis data
    /// </summary>
    private BudgetAnalysisResult? _analysisData;
    public BudgetAnalysisResult? AnalysisData
    {
        get => _analysisData;
        set => SetProperty(ref _analysisData, value);
    }

    /// <summary>
    /// Trend data for budget analysis
    /// </summary>
    public ObservableCollection<WileyWidget.Models.BudgetTrendItem> TrendData { get; } = new();

    /// <summary>
    /// Self-reference for DataContext binding
    /// </summary>
    public BudgetViewModel ViewModel => this;

    // Prism command properties
    public DelegateCommand RefreshBudgetDataCommand { get; private set; }
    public DelegateCommand BreakEvenAnalysisCommand { get; private set; }
    public DelegateCommand TrendAnalysisCommand { get; private set; }
    public DelegateCommand ExportReportCommand { get; private set; }
    public DelegateCommand ToggleFiscalYearCommand { get; private set; }
    public DelegateCommand SaveConfirmationCommand { get; private set; }
    public DelegateCommand NavigateToMunicipalAccountCommand { get; private set; }
    public DelegateCommand ImportBudgetCommand { get; private set; }
    public DelegateCommand ExportBudgetCommand { get; private set; }
    public DelegateCommand AddAccountCommand { get; private set; }
    public DelegateCommand DeleteAccountCommand { get; private set; }

    /// <summary>
    /// Standardized load command to align with other ViewModels; proxies to RefreshBudgetDataCommand.
    /// </summary>
    public DelegateCommand LoadDataCommand => RefreshBudgetDataCommand;

    /// <summary>
    /// Constructor with dependency injection
    /// Subscribes to enterprise change messages for automatic refresh
    /// </summary>
    public BudgetViewModel(IEnterpriseRepository enterpriseRepository, IBudgetRepository budgetRepository, IEventAggregator eventAggregator, ICacheService? cacheService = null, IDispatcherHelper? dispatcherHelper = null, IMunicipalAccountRepository? municipalAccountRepository = null)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _budgetRepository = budgetRepository;
        _municipalAccountRepository = municipalAccountRepository;
        _eventAggregator = eventAggregator;
        _cacheService = cacheService;
        _dispatcherHelper = dispatcherHelper;
        // NOTE: ThemeManager removed - SfSkinManager.ApplicationTheme handles all theming globally
        Log.Debug("BudgetViewModel initialized");

        // Initialize live update timer (refresh every 5 minutes)
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        _refreshTimer.Tick += async (s, e) => await RefreshBudgetsAsync();

        // Subscribe to enterprise change messages
        _eventAggregator?.GetEvent<EnterpriseChangedMessage>().Subscribe(OnEnterpriseChanged);

        BudgetDetails.CollectionChanged += OnBudgetDetailsChanged;
        BudgetAccounts.CollectionChanged += OnBudgetAccountsChanged;

        InitializeCommands();

        // Auto-load enterprises into cache for faster access (non-blocking, tracked)
        // Kick off the fetch immediately to ensure invocation happens deterministically for tests
        var preloadEnterprisesTask = _enterpriseRepository.GetAllAsync();
        _cacheLoadingTask = Task.Run(async () =>
        {
            try
            {
                var enterprises = await preloadEnterprisesTask.ConfigureAwait(false);
                if (_cacheService != null)
                {
                    await _cacheService.SetAsync("enterprises", enterprises, TimeSpan.FromHours(6)).ConfigureAwait(false);
                }
                Log.Debug("Auto-loaded {Count} enterprises into cache", enterprises?.Count() ?? 0);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to auto-load enterprises into cache");
            }
        });
    }

    // Prism navigation lifecycle
    public async void OnNavigatedTo(NavigationContext navigationContext)
    {
        // Load or refresh data when navigated to
        try
        {
            await RefreshBudgetDataAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading budget data during navigation");
        }
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        // Stop live updates and release resources when navigating away
        try
        {
            StopLiveUpdates();
            _refreshTimer?.Stop();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error stopping live updates in BudgetViewModel.OnNavigatedFrom");
        }
    }

    private void OnEnterpriseChanged(EnterpriseChangedMessage message)
    {
        // Automatically refresh budget data when enterprises change
        Log.Information("Received EnterpriseChangedMessage: {EnterpriseName} ({ChangeType})",
            message.EnterpriseName, message.ChangeType);

        // Call async method and ensure exceptions are logged
        Task.Run(async () =>
        {
            try
            {
                await RefreshBudgetDataAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error refreshing budget data after enterprise change");
            }
        });
    }

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        Log.Debug("BudgetViewModel detected theme change from {OldTheme} to {NewTheme}", e.OldTheme, e.NewTheme);
    }

    /// <summary>
    /// Starts the live update timer
    /// </summary>
    public void StartLiveUpdates()
    {
        if (!_refreshTimer.IsEnabled)
        {
            _refreshTimer.Start();
            Log.Information("Started budget live updates with 5-minute interval");
        }
    }

    /// <summary>
    /// Stops the live update timer
    /// </summary>
    public void StopLiveUpdates()
    {
        if (_refreshTimer.IsEnabled)
        {
            _refreshTimer.Stop();
            Log.Information("Stopped budget live updates");
        }
    }

    private void InitializeCommands()
    {
        RefreshBudgetDataCommand = new DelegateCommand(async () => await RefreshBudgetDataAsync(), () => !IsBusy && !IsLoading);
        BreakEvenAnalysisCommand = new DelegateCommand(BreakEvenAnalysis, CanRunAnalysis);
        TrendAnalysisCommand = new DelegateCommand(TrendAnalysis, CanRunAnalysis);
        ExportReportCommand = new DelegateCommand(ExportReport, CanRunAnalysis);
        ToggleFiscalYearCommand = new DelegateCommand(async () => await ToggleFiscalYearAsync(), CanToggleFiscalYear);
        SaveConfirmationCommand = new DelegateCommand(async () => await SaveConfirmationAsync(), CanSaveBudget);
        NavigateToMunicipalAccountCommand = new DelegateCommand(NavigateToMunicipalAccount, () => !IsBusy);
        ImportBudgetCommand = new DelegateCommand(async () => await ImportBudgetAsync(), () => !IsBusy && !IsLoading);
        ExportBudgetCommand = new DelegateCommand(async () => await ExportBudgetAsync(), () => !IsBusy && BudgetAccounts.Any());
        AddAccountCommand = new DelegateCommand(AddAccount, () => !IsBusy);
        DeleteAccountCommand = new DelegateCommand(DeleteAccount, () => !IsBusy);

        UpdateAnalysisCommandStates();
        UpdateBudgetCommandStates();
    }

    private void OnBudgetDetailsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => UpdateAnalysisCommandStates();

    private void OnBudgetAccountsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => UpdateBudgetCommandStates();

    private void UpdateAnalysisCommandStates()
    {
        BreakEvenAnalysisCommand?.RaiseCanExecuteChanged();
        TrendAnalysisCommand?.RaiseCanExecuteChanged();
        ExportReportCommand?.RaiseCanExecuteChanged();
    }

    private void UpdateBudgetCommandStates()
    {
        SaveConfirmationCommand?.RaiseCanExecuteChanged();
        ExportBudgetCommand?.RaiseCanExecuteChanged();
    }

    private bool CanRunAnalysis() => !IsBusy && BudgetDetails.Any();

    private bool CanToggleFiscalYear() => !IsBusy && !IsLoading && FiscalYears.Count > 0;

    private bool CanSaveBudget() => !IsBusy && BudgetAccounts.Any();

    /// <summary>
    /// Async load budgets for selected fiscal year using Task.Run
    /// </summary>
    private async Task LoadBudgetsAsync()
    {
        try
        {
            IsBusy = true;
            ProgressText = $"Loading budgets for {SelectedFiscalYear}...";

            // Extract year from "FY 2025" format
            var yearStr = SelectedFiscalYear.Replace("FY", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (!int.TryParse(yearStr, out var fiscalYear))
            {
                throw new InvalidOperationException($"Invalid fiscal year format: {SelectedFiscalYear}");
            }

            // Use Task.Run for async data loading to avoid UI thread blocking
            var budgets = await _budgetRepository.GetBudgetHierarchyAsync(fiscalYear);

            // Update UI on dispatcher thread
            Application.Current?.Dispatcher.Invoke(() =>
            {
                BudgetAccounts.Clear();
                foreach (var budget in budgets)
                {
                    // Convert BudgetEntry to BudgetAccount
                    var account = new BudgetAccount
                    {
                        Id = budget.Id,
                        AccountNumber = budget.AccountNumber,
                        Description = budget.Description,
                        FundType = budget.FundType.ToString(),
                        BudgetAmount = budget.BudgetedAmount,
                        ActualAmount = budget.ActualAmount,
                        ParentId = budget.ParentId ?? -1
                    };
                    BudgetAccounts.Add(account);
                }

                UpdateTotalsAndCharts();
            });

            ProgressText = $"Loaded {BudgetAccounts.Count} budget accounts";
            Log.Information("Successfully loaded {Count} budget accounts for {Year}", BudgetAccounts.Count, fiscalYear);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load budgets: {ex.Message}";
            HasError = true;
            AnalysisStatus = $"Error: {ex.Message}";
            Log.Error(ex, "Failed to load budgets for {Year}", SelectedFiscalYear);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Refreshes budget data (called by timer)
    /// </summary>
    private async Task RefreshBudgetsAsync()
    {
        await LoadBudgetsAsync();
    }

    /// <summary>
    /// Toggle fiscal year command
    /// </summary>
    private async Task ToggleFiscalYearAsync()
    {
        // Find next year in the list
        var currentIndex = FiscalYears.IndexOf(SelectedFiscalYear);
        var nextIndex = (currentIndex + 1) % FiscalYears.Count;
        SelectedFiscalYear = FiscalYears[nextIndex];

        await LoadBudgetsAsync();
        Log.Information("Toggled fiscal year to {Year}", SelectedFiscalYear);
    }

    /// <summary>
    /// Save confirmation command with MessageBox
    /// </summary>
    private async Task SaveConfirmationAsync()
    {
        if (BudgetAccounts.Any(a => a.IsOverBudget))
        {
            var result = MessageBox.Show(
                "Some accounts are over budget. Are you sure you want to save?",
                "Budget Overrun Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                Log.Information("User canceled save due to budget overruns");
                return;
            }
        }

        try
        {
            IsBusy = true;
            ProgressText = "Saving budget changes...";

            // Save logic here (update repository). Await repository async methods directly
            foreach (var account in BudgetAccounts)
            {
                // Convert back to BudgetEntry and update
                var entry = new BudgetEntry
                {
                    Id = account.Id,
                    AccountNumber = account.AccountNumber,
                    Description = account.Description,
                    FundType = Enum.TryParse<FundType>(account.FundType, out var fundType)
                        ? fundType
                        : FundType.GeneralFund,
                    BudgetedAmount = account.BudgetAmount,
                    ActualAmount = account.ActualAmount,
                    ParentId = account.ParentId == -1 ? null : account.ParentId
                };

                await _budgetRepository.UpdateAsync(entry);
            }

            MessageBox.Show("Budget saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            ProgressText = "Budget saved successfully";
            Log.Information("Budget saved successfully");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save budget: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            ErrorMessage = $"Failed to save: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to save budget");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Navigate to Municipal Account View
    /// </summary>
    private void NavigateToMunicipalAccount()
    {
        // Send navigation message to MainViewModel
        _eventAggregator?.GetEvent<NavigationMessage>().Publish(new NavigationMessage
        {
            TargetView = "MunicipalAccountView"
        });
        Log.Information("Navigating to MunicipalAccountView");
    }

    /// <summary>
    /// Updates totals and chart data
    /// </summary>
    private void UpdateTotalsAndCharts()
    {
        TotalBudget = BudgetAccounts.Sum(a => a.BudgetAmount);
        TotalActual = BudgetAccounts.Sum(a => a.ActualAmount);
        TotalVariance = TotalBudget - TotalActual;

        // Update distribution data
        BudgetDistributionData.Clear();
        var fundGroups = BudgetAccounts.GroupBy(a => a.FundType);
        foreach (var group in fundGroups)
        {
            var amount = group.Sum(a => a.BudgetAmount);
            BudgetDistributionData.Add(new BudgetDistributionData
            {
                FundType = group.Key,
                Amount = amount,
                Percentage = TotalBudget > 0 ? (double)(amount / TotalBudget) : 0
            });
        }

        // Update comparison data
        BudgetComparisonData.Clear();
        foreach (var group in fundGroups)
        {
            BudgetComparisonData.Add(new BudgetComparisonData
            {
                Category = group.Key,
                BudgetAmount = group.Sum(a => a.BudgetAmount),
                ActualAmount = group.Sum(a => a.ActualAmount)
            });
        }
    }

    #region IDataErrorInfo Implementation

    /// <summary>
    /// Gets the error message for the entire object
    /// </summary>
    public string Error
    {
        get
        {
            if (BudgetAccounts.Any(a => a.IsOverBudget))
            {
                return "One or more accounts are over budget";
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the error message for a specific property
    /// </summary>
    public string this[string columnName]
    {
        get
        {
            switch (columnName)
            {
                case nameof(TotalBudget):
                    if (TotalBudget <= 0)
                        return "Total budget must be greater than zero";
                    break;
                case nameof(TotalActual):
                    if (TotalActual > TotalBudget)
                        return "Total actual expenses exceed total budget";
                    break;
            }
            return string.Empty;
        }
    }

    #endregion

    // NOTE: Overload constructors removed - ThemeManager parameter no longer needed
    // Use primary constructor with 3 parameters only

    /// <summary>
    /// Constructor with dependency injection (original signature for backward compatibility)
    /// </summary>
    public BudgetViewModel(IEnterpriseRepository enterpriseRepository)
        : this(enterpriseRepository, null!, null!, null)
    {
        // Fallback constructor - budget repository and event aggregator will be null
        // This maintains compatibility with existing tests
        Log.Warning("BudgetViewModel created without IBudgetRepository and IEventAggregator - some features will be unavailable");
    }

    /// <summary>
    /// Refreshes all budget data from the database
    /// Includes busy state management and error handling
    /// </summary>
    public async Task RefreshBudgetDataAsync()
    {
        if (IsBusy) return; // Prevent concurrent refreshes

        try
        {
            IsBusy = true;
            ProgressText = "Loading budget data...";
            AnalysisStatus = "Loading...";
            HasError = false;
            ErrorMessage = string.Empty;

            // Extract fiscal year from SelectedFiscalYear (e.g., "FY 2025" -> 2025)
            var yearStr = SelectedFiscalYear.Replace("FY", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (!int.TryParse(yearStr, out var fiscalYear))
            {
                fiscalYear = DateTime.Now.Year; // Default to current year if parsing fails
            }

            // Load budget entries for the fiscal year - MUST be called for tests
            var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(fiscalYear);

            // Populate new collections
            Budgets.Clear();
            // Note: OverallBudget loading would need to be implemented based on your data model

            BudgetPeriods.Clear();
            BudgetPeriods.Add(new BudgetPeriod { Year = fiscalYear, Name = $"FY {fiscalYear}", Status = BudgetStatus.Adopted });

            BudgetEntries.Clear();
            foreach (var entry in budgetEntries)
            {
                BudgetEntries.Add(entry);
            }

            // Generate trend data
            TrendData.Clear();
            if (budgetEntries != null && budgetEntries.Any())
            {
                var trendItem = new WileyWidget.Models.BudgetTrendItem
                {
                    Period = $"FY {fiscalYear}",
                    Amount = budgetEntries.Sum(b => b.BudgetedAmount),
                    ProjectedAmount = budgetEntries.Sum(b => b.ActualAmount),
                    Category = "Budget vs Actual"
                };
                TrendData.Add(trendItem);
            }

            var enterprises = _cacheService != null
                ? await _cacheService.GetAsync<List<Enterprise>>("enterprises") ?? await _enterpriseRepository.GetAllAsync()
                : await _enterpriseRepository.GetAllAsync();

            // Cache the enterprises if not already cached
            if (_cacheService != null && enterprises != null)
            {
                await _cacheService.SetAsync("enterprises", enterprises, TimeSpan.FromHours(6));
            }

            // Marshal UI updates to the UI thread using dispatcher helper
            if (_dispatcherHelper != null)
            {
                await _dispatcherHelper.InvokeAsync(() =>
                {
                    BudgetDetails.Clear();

                    foreach (var enterprise in enterprises)
                    {
                        var budgetDetail = new BudgetDetailItem
                        {
                            EnterpriseName = enterprise.Name,
                            CitizenCount = enterprise.CitizenCount,
                            CurrentRate = enterprise.CurrentRate,
                            MonthlyRevenue = enterprise.MonthlyRevenue,
                            MonthlyExpenses = enterprise.MonthlyExpenses,
                            MonthlyBalance = enterprise.MonthlyBalance,
                            BreakEvenRate = enterprise.BreakEvenRate,
                            Status = enterprise.MonthlyBalance >= 0 ? "Surplus" : "Deficit"
                        };

                        BudgetDetails.Add(budgetDetail);
                    }

                    // Calculate totals
                    TotalRevenue = BudgetDetails.Sum(b => b.MonthlyRevenue);
                    TotalExpenses = BudgetDetails.Sum(b => b.MonthlyExpenses);
                    NetBalance = TotalRevenue - TotalExpenses;
                    TotalCitizens = BudgetDetails.Sum(b => b.CitizenCount);

                    LastUpdated = DateTime.Now.ToString("g", CultureInfo.InvariantCulture);
                    ProgressText = "Data loaded successfully";
                    AnalysisStatus = "Data loaded successfully";

                    // Generate initial recommendations
                    GenerateRecommendations();
                });
            }
            else
            {
                // Fallback if no dispatcher helper provided (for backward compatibility)
                BudgetDetails.Clear();

                foreach (var enterprise in enterprises)
                {
                    var budgetDetail = new BudgetDetailItem
                    {
                        EnterpriseName = enterprise.Name,
                        CitizenCount = enterprise.CitizenCount,
                        CurrentRate = enterprise.CurrentRate,
                        MonthlyRevenue = enterprise.MonthlyRevenue,
                        MonthlyExpenses = enterprise.MonthlyExpenses,
                        MonthlyBalance = enterprise.MonthlyBalance,
                        BreakEvenRate = enterprise.BreakEvenRate,
                        Status = enterprise.MonthlyBalance >= 0 ? "Surplus" : "Deficit"
                    };

                    BudgetDetails.Add(budgetDetail);
                }

                // Calculate totals
                TotalRevenue = BudgetDetails.Sum(b => b.MonthlyRevenue);
                TotalExpenses = BudgetDetails.Sum(b => b.MonthlyExpenses);
                NetBalance = TotalRevenue - TotalExpenses;
                TotalCitizens = BudgetDetails.Sum(b => b.CitizenCount);

                LastUpdated = DateTime.Now.ToString("g", CultureInfo.InvariantCulture);
                ProgressText = "Data loaded successfully";
                AnalysisStatus = "Data loaded successfully";

                // Generate initial recommendations
                GenerateRecommendations();
            }

            Log.Information("Successfully refreshed budget data for {Count} enterprises and {BudgetCount} budget entries",
                enterprises.Count(), budgetEntries.Count());

            // Send refresh complete message
            _eventAggregator?.GetEvent<BudgetUpdatedMessage>().Publish(new BudgetUpdatedMessage
            {
                Context = "BudgetViewModel.RefreshBudgetDataAsync"
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to refresh budget data: {ex.Message}";
            HasError = true;
            // Tests expect the status to begin with "Error:" and include the exception message
            AnalysisStatus = $"Error: {ex.Message}";
            ProgressText = "Error loading data";
            Log.Error(ex, "Failed to refresh budget data");
        }
        finally
        {
            IsBusy = false;
        }
    }    /// <summary>
    /// Performs break-even analysis
    /// </summary>
    private void BreakEvenAnalysis()
    {
        if (!BudgetDetails.Any())
        {
            BreakEvenAnalysisText = "No budget data available. Please refresh data first.";
            return;
        }

        var analysis = new System.Text.StringBuilder();
        analysis.AppendLine("BREAK-EVEN ANALYSIS");
        analysis.AppendLine("===================");
        analysis.AppendLine();

        foreach (var detail in BudgetDetails.OrderByDescending(b => b.MonthlyBalance))
        {
            analysis.AppendLine(CultureInfo.InvariantCulture, $"Enterprise: {detail.EnterpriseName}");
            analysis.AppendLine(CultureInfo.InvariantCulture, $"  Current Rate: ${detail.CurrentRate:F2}");
            analysis.AppendLine(CultureInfo.InvariantCulture, $"  Break-even Rate: ${detail.BreakEvenRate:F2}");
            analysis.AppendLine(CultureInfo.InvariantCulture, $"  Current Balance: ${detail.MonthlyBalance:F2}");

            if (detail.CurrentRate > detail.BreakEvenRate)
            {
                analysis.AppendLine(CultureInfo.InvariantCulture, $"  Status: PROFITABLE (Rate exceeds break-even by ${(detail.CurrentRate - detail.BreakEvenRate):F2})");
            }
            else if (detail.CurrentRate < detail.BreakEvenRate)
            {
                analysis.AppendLine(CultureInfo.InvariantCulture, $"  Status: LOSS (Need ${(detail.BreakEvenRate - detail.CurrentRate):F2} increase to break-even)");
            }
            else
            {
                analysis.AppendLine("  Status: AT BREAK-EVEN");
            }
            analysis.AppendLine();
        }

        BreakEvenAnalysisText = analysis.ToString();
    }

    /// <summary>
    /// Performs trend analysis
    /// </summary>
    private void TrendAnalysis()
    {
        if (!BudgetDetails.Any())
        {
            TrendAnalysisText = "No budget data available. Please refresh data first.";
            return;
        }

        var analysis = new System.Text.StringBuilder();
        analysis.AppendLine("BUDGET TREND ANALYSIS");
        analysis.AppendLine("====================");
        analysis.AppendLine();

        var profitableEnterprises = BudgetDetails.Count(b => b.MonthlyBalance > 0);
        var deficitEnterprises = BudgetDetails.Count(b => b.MonthlyBalance < 0);
        var breakEvenEnterprises = BudgetDetails.Count(b => b.MonthlyBalance == 0);

        analysis.AppendLine($"Portfolio Overview:");
        analysis.AppendLine(CultureInfo.InvariantCulture, $"  Profitable Enterprises: {profitableEnterprises}");
        analysis.AppendLine(CultureInfo.InvariantCulture, $"  Deficit Enterprises: {deficitEnterprises}");
        analysis.AppendLine(CultureInfo.InvariantCulture, $"  Break-even Enterprises: {breakEvenEnterprises}");
        analysis.AppendLine();

        analysis.AppendLine($"Revenue Distribution:");
        var avgRevenue = BudgetDetails.Average(b => b.MonthlyRevenue);
        var maxRevenue = BudgetDetails.Max(b => b.MonthlyRevenue);
        var minRevenue = BudgetDetails.Min(b => b.MonthlyRevenue);

        analysis.AppendLine(CultureInfo.InvariantCulture, $"  Average Revenue: ${avgRevenue:F2}");
        analysis.AppendLine(CultureInfo.InvariantCulture, $"  Highest Revenue: ${maxRevenue:F2}");
        analysis.AppendLine(CultureInfo.InvariantCulture, $"  Lowest Revenue: ${minRevenue:F2}");
        analysis.AppendLine();

        analysis.AppendLine($"Expense Analysis:");
        var avgExpense = BudgetDetails.Average(b => b.MonthlyExpenses);
        var maxExpense = BudgetDetails.Max(b => b.MonthlyExpenses);
        var minExpense = BudgetDetails.Min(b => b.MonthlyExpenses);

        analysis.AppendLine(CultureInfo.InvariantCulture, $"  Average Expenses: ${avgExpense:F2}");
        analysis.AppendLine(CultureInfo.InvariantCulture, $"  Highest Expenses: ${maxExpense:F2}");
        analysis.AppendLine(CultureInfo.InvariantCulture, $"  Lowest Expenses: ${minExpense:F2}");

        TrendAnalysisText = analysis.ToString();
    }

    /// <summary>
    /// Export budget report to file
    /// </summary>
    private void ExportReport()
    {
        // Simple implementation - in real app this would export to Excel/CSV
        Log.Information("ExportReport command executed");
        MessageBox.Show("Budget report export functionality would be implemented here.",
                       "Export Report",
                       MessageBoxButton.OK,
                       MessageBoxImage.Information);
    }

    /// <summary>
    /// Generates budget recommendations
    /// </summary>
    private void GenerateRecommendations()
    {
        if (!BudgetDetails.Any())
        {
            RecommendationsText = "No budget data available for recommendations.";
            return;
        }

        var recommendations = new System.Text.StringBuilder();
        recommendations.AppendLine("BUDGET RECOMMENDATIONS");
        recommendations.AppendLine("=====================");
        recommendations.AppendLine();

        // Check overall portfolio health
        if (NetBalance < 0)
        {
            recommendations.AppendLine("⚠️  CRITICAL: Overall portfolio is operating at a loss");
            recommendations.AppendLine("   Consider rate increases or expense reductions");
            recommendations.AppendLine();
        }

        // Identify deficit enterprises
        var deficitEnterprises = BudgetDetails.Where(b => b.MonthlyBalance < 0).ToList();
        if (deficitEnterprises.Any())
        {
            recommendations.AppendLine("Enterprises requiring attention:");
            foreach (var enterprise in deficitEnterprises.OrderBy(b => b.MonthlyBalance))
            {
                recommendations.AppendLine(CultureInfo.InvariantCulture, $"  • {enterprise.EnterpriseName}: Loss of ${Math.Abs(enterprise.MonthlyBalance):F2}");
                recommendations.AppendLine(CultureInfo.InvariantCulture, $"    Suggested rate increase: ${(enterprise.BreakEvenRate - enterprise.CurrentRate):F2}");
            }
            recommendations.AppendLine();
        }

        // Identify high performers
        var highPerformers = BudgetDetails.Where(b => b.MonthlyBalance > 100).ToList();
        if (highPerformers.Any())
        {
            recommendations.AppendLine("High-performing enterprises:");
            foreach (var enterprise in highPerformers.OrderByDescending(b => b.MonthlyBalance))
            {
                recommendations.AppendLine(CultureInfo.InvariantCulture, $"  • {enterprise.EnterpriseName}: Profit of ${enterprise.MonthlyBalance:F2}");
            }
            recommendations.AppendLine();
        }

        // General recommendations
        recommendations.AppendLine("General Recommendations:");
        recommendations.AppendLine("  • Monitor enterprises with low citizen counts for potential consolidation");
        recommendations.AppendLine("  • Consider seasonal rate adjustments for utilities");
        recommendations.AppendLine("  • Review expense patterns quarterly for optimization opportunities");

        RecommendationsText = recommendations.ToString();
    }

    /// <summary>
    /// Clears any error state
    /// </summary>
    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
        AnalysisStatus = "Ready";
        Log.Information("Error cleared by user");
    }

    /// <summary>
    /// Dispose pattern implementation
    /// Unsubscribes from messenger to prevent memory leaks
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Wait for cache loading to complete (with timeout)
                if (_cacheLoadingTask != null && !_cacheLoadingTask.IsCompleted)
                {
                    try
                    {
                        _cacheLoadingTask.Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (AggregateException ex)
                    {
                        Log.Warning(ex, "Cache loading task failed during dispose");
                    }
                }

                // Unregister from messenger to prevent memory leaks
                BudgetDetails.CollectionChanged -= OnBudgetDetailsChanged;
                BudgetAccounts.CollectionChanged -= OnBudgetAccountsChanged;
                // Prism EventAggregator subscriptions are automatically cleaned up
                // NOTE: ThemeManager event unsubscription removed
                Log.Debug("BudgetViewModel disposed");
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Data model for budget detail items
/// </summary>
public class BudgetDetailItem
{
    public string EnterpriseName { get; set; } = string.Empty;
    public int CitizenCount { get; set; }
    public decimal CurrentRate { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal MonthlyBalance { get; set; }
    public decimal BreakEvenRate { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Data model for budget performance data
/// </summary>
public class BudgetPerformanceData
{
    public string Category { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

/// <summary>
/// Data model for projected rate data
/// </summary>
public class ProjectedRateData
{
    public string Period { get; set; } = string.Empty;
    public decimal Rate { get; set; }
}

/// <summary>
/// Data model for rate trend data
/// </summary>
public class RateTrendData
{
    public string Period { get; set; } = string.Empty;
    public decimal Trend { get; set; }
}
