// Removed Prism.Navigation; WPF uses Prism.Regions for region navigation
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Syncfusion.SfSkinManager;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Abstractions;
using WileyWidget.Services.Logging;
using WileyWidget.ViewModels.Messages;
using PrismDelegateCommand = Prism.Commands.DelegateCommand;

namespace WileyWidget.ViewModels.Main {
    public class DashboardViewModel : BindableBase, IDataErrorInfo, IDisposable, INavigationAware
    {
        private readonly ILogger<DashboardViewModel> _logger;
        private readonly IEnterpriseRepository _enterpriseRepository;
        private readonly IWhatIfScenarioEngine _whatIfScenarioEngine;
        private readonly IUtilityCustomerRepository _utilityCustomerRepository;
        private readonly IMunicipalAccountRepository _municipalAccountRepository;
        private readonly FiscalYearSettings _fiscalYearSettings;
        private readonly IEventAggregator _eventAggregator;
        private readonly IRegionManager _regionManager;

        // Use Lazy<T> for optional/circular dependency services
        private readonly Lazy<ICacheService>? _lazyCacheService;
        private readonly Lazy<ISettingsService>? _lazySettingsService;
        private readonly Lazy<IQuickBooksService>? _lazyQuickBooksService;
        private readonly Lazy<IChargeCalculatorService>? _lazyChargeCalculatorService;

        // Convenience properties for lazy services
        private ICacheService? CacheService => _lazyCacheService?.Value;
        private ISettingsService? SettingsService => _lazySettingsService?.Value;
        private IQuickBooksService? QuickBooksService => _lazyQuickBooksService?.Value;
        private IChargeCalculatorService? ChargeCalculatorService => _lazyChargeCalculatorService?.Value;

        private DispatcherTimer _refreshTimer;
        private Task? _cacheLoadingTask;
        private Task? _initialLoadTask;

        // KPI Properties
        private int _totalEnterprises;
        public int TotalEnterprises
        {
            get => _totalEnterprises;
            set => SetProperty(ref _totalEnterprises, value);
        }

        private decimal _totalBudget;
        public decimal TotalBudget
        {
            get => _totalBudget;
            set => SetProperty(ref _totalBudget, value);
        }

        private int _activeProjects;
        public int ActiveProjects
        {
            get => _activeProjects;
            set => SetProperty(ref _activeProjects, value);
        }

        private string _systemHealthStatus = "Good";
        public string SystemHealthStatus
        {
            get => _systemHealthStatus;
            set => SetProperty(ref _systemHealthStatus, value);
        }

        private Brush _systemHealthColor = Brushes.Green;
        public Brush SystemHealthColor
        {
            get => _systemHealthColor;
            set => SetProperty(ref _systemHealthColor, value);
        }

        private int _healthScore = 95;
        public int HealthScore
        {
            get => _healthScore;
            set => SetProperty(ref _healthScore, value);
        }

        // Change indicators
        private string _enterprisesChangeText = "+2 from last month";
        public string EnterprisesChangeText
        {
            get => _enterprisesChangeText;
            set => SetProperty(ref _enterprisesChangeText, value);
        }

        private Brush _enterprisesChangeColor = Brushes.Green;
        public Brush EnterprisesChangeColor
        {
            get => _enterprisesChangeColor;
            set => SetProperty(ref _enterprisesChangeColor, value);
        }

        private string _budgetChangeText = "+$15K from last month";
        public string BudgetChangeText
        {
            get => _budgetChangeText;
            set => SetProperty(ref _budgetChangeText, value);
        }

        private Brush _budgetChangeColor = Brushes.Green;
        public Brush BudgetChangeColor
        {
            get => _budgetChangeColor;
            set => SetProperty(ref _budgetChangeColor, value);
        }

        private string _projectsChangeText = "+1 from last week";
        public string ProjectsChangeText
        {
            get => _projectsChangeText;
            set => SetProperty(ref _projectsChangeText, value);
        }

        private Brush _projectsChangeColor = Brushes.Green;
        public Brush ProjectsChangeColor
        {
            get => _projectsChangeColor;
            set => SetProperty(ref _projectsChangeColor, value);
        }

        // Auto-refresh settings
        private bool _autoRefreshEnabled = true;
        public bool AutoRefreshEnabled
        {
            get => _autoRefreshEnabled;
            set => SetProperty(ref _autoRefreshEnabled, value);
        }

        private int _refreshIntervalMinutes = 5;
        public int RefreshIntervalMinutes
        {
            get => _refreshIntervalMinutes;
            set => SetProperty(ref _refreshIntervalMinutes, value);
        }

        // Status
        private string _dashboardStatus = "Loading...";
        public string DashboardStatus
        {
            get => _dashboardStatus;
            set => SetProperty(ref _dashboardStatus, value);
        }

        private string _lastUpdated = "Never";
        public string LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value);
        }

        private string _nextRefreshTime = "Calculating...";
        public string NextRefreshTime
        {
            get => _nextRefreshTime;
            set => SetProperty(ref _nextRefreshTime, value);
        }

        // Chart data
        private ObservableCollection<BudgetTrendItem> _budgetTrendData = new();
        public ObservableCollection<BudgetTrendItem> BudgetTrendData
        {
            get => _budgetTrendData;
            set => SetProperty(ref _budgetTrendData, value);
        }

        private ObservableCollection<BudgetTrendItem> _chartData = new();
        public ObservableCollection<BudgetTrendItem> ChartData
        {
            get => _chartData;
            set => SetProperty(ref _chartData, value);
        }

        private ObservableCollection<BudgetTrendItem> _expenseData = new();
        public ObservableCollection<BudgetTrendItem> ExpenseData
        {
            get => _expenseData;
            set => SetProperty(ref _expenseData, value);
        }

        private ObservableCollection<BudgetTrendItem> _revenueData = new();
        public ObservableCollection<BudgetTrendItem> RevenueData
        {
            get => _revenueData;
            set => SetProperty(ref _revenueData, value);
        }

        private ObservableCollection<BudgetTrendItem> _historicalData = new();
        public ObservableCollection<BudgetTrendItem> HistoricalData
        {
            get => _historicalData;
            set => SetProperty(ref _historicalData, value);
        }

        private ObservableCollection<RateTrendItem> _rateTrendData = new();
        public ObservableCollection<RateTrendItem> RateTrendData
        {
            get => _rateTrendData;
            set => SetProperty(ref _rateTrendData, value);
        }

        private ObservableCollection<EnterpriseTypeItem> _enterpriseTypeData = new();
        public ObservableCollection<EnterpriseTypeItem> EnterpriseTypeData
        {
            get => _enterpriseTypeData;
            set => SetProperty(ref _enterpriseTypeData, value);
        }

        // Activity and alerts
        private ObservableCollection<ActivityItem> _recentActivities = new();
        public ObservableCollection<ActivityItem> RecentActivities
        {
            get => _recentActivities;
            set => SetProperty(ref _recentActivities, value);
        }

        private ObservableCollection<AlertItem> _systemAlerts = new();
        public ObservableCollection<AlertItem> SystemAlerts
        {
            get => _systemAlerts;
            set => SetProperty(ref _systemAlerts, value);
        }

        // Budget insights for dashboard analysis
        private ObservableCollection<BudgetInsight> _budgetInsights = new();
        public ObservableCollection<BudgetInsight> BudgetInsights
        {
            get => _budgetInsights;
            set => SetProperty(ref _budgetInsights, value);
        }

        // Recent budget changes tracking
        private ObservableCollection<BudgetChange> _recentBudgetChanges = new();
        public ObservableCollection<BudgetChange> RecentBudgetChanges
        {
            get => _recentBudgetChanges;
            set => SetProperty(ref _recentBudgetChanges, value);
        }

        // Quick actions for dashboard shortcuts
        private ObservableCollection<QuickAction> _quickActions = new();
        public ObservableCollection<QuickAction> QuickActions
        {
            get => _quickActions;
            set => SetProperty(ref _quickActions, value);
        }

        // Panel items for tile view layouts
        private ObservableCollection<PanelItem> _panelItems = new();
        public ObservableCollection<PanelItem> PanelItems
        {
            get => _panelItems;
            set => SetProperty(ref _panelItems, value);
        }

        // Budget metrics
        private int _budgetOverruns;
        public int BudgetOverruns
        {
            get => _budgetOverruns;
            set => SetProperty(ref _budgetOverruns, value);
        }

        private double _budgetProgress;
        public double BudgetProgress
        {
            get => _budgetProgress;
            set => SetProperty(ref _budgetProgress, value);
        }

        // Enterprise data for grids
        private ObservableCollection<Enterprise> _enterprises = new();
        public ObservableCollection<Enterprise> Enterprises
        {
            get => _enterprises;
            set => SetProperty(ref _enterprises, value);
        }

        // Loading and status properties
        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private int _systemHealthScore = 95;
        public int SystemHealthScore
        {
            get => _systemHealthScore;
            set => SetProperty(ref _systemHealthScore, value);
        }

        private int _budgetUtilizationScore = 78;
        public int BudgetUtilizationScore
        {
            get => _budgetUtilizationScore;
            set => SetProperty(ref _budgetUtilizationScore, value);
        }

        private decimal _suggestedRate;
        public decimal SuggestedRate
        {
            get => _suggestedRate;
            set => SetProperty(ref _suggestedRate, value);
        }

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Missing properties for view bindings
        private string _currentTheme = "FluentLight";
        public VisualStyles CurrentTheme
        {
            get => MapThemeToVisualStyle(_currentTheme);
            set
            {
                var themeString = MapVisualStyleToTheme(value);
                if (SetProperty(ref _currentTheme, themeString))
                {
                    // Theme changes are handled globally via SfSkinManager.ApplicationTheme
                }
            }
        }

        private VisualStyles MapThemeToVisualStyle(string themeName)
        {
            return themeName switch
            {
                "FluentLight" => VisualStyles.FluentLight,
                "FluentDark" => VisualStyles.FluentDark,
                _ => VisualStyles.FluentLight
            };
        }

        private string MapVisualStyleToTheme(VisualStyles style)
        {
            return style switch
            {
                VisualStyles.FluentLight => "FluentLight",
                VisualStyles.FluentDark => "FluentDark",
                _ => "FluentLight"
            };
        }

        private ObservableCollection<BudgetUtilizationData> _budgetUtilizationData = new();
        public ObservableCollection<BudgetUtilizationData> BudgetUtilizationData
        {
            get => _budgetUtilizationData;
            set => SetProperty(ref _budgetUtilizationData, value);
        }

        private decimal _progressPercentage;
        public decimal ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        private decimal _remainingBudget;
        public decimal RemainingBudget
        {
            get => _remainingBudget;
            set => SetProperty(ref _remainingBudget, value);
        }

        private decimal _spentAmount;
        public decimal SpentAmount
        {
            get => _spentAmount;
            set => SetProperty(ref _spentAmount, value);
        }

        // Growth scenario properties
        private decimal _payRaisePercentage;
        public decimal PayRaisePercentage
        {
            get => _payRaisePercentage;
            set => SetProperty(ref _payRaisePercentage, value);
        }

        private decimal _benefitsIncreaseAmount;
        public decimal BenefitsIncreaseAmount
        {
            get => _benefitsIncreaseAmount;
            set => SetProperty(ref _benefitsIncreaseAmount, value);
        }

        private decimal _equipmentPurchaseAmount;
        public decimal EquipmentPurchaseAmount
        {
            get => _equipmentPurchaseAmount;
            set => SetProperty(ref _equipmentPurchaseAmount, value);
        }

        private int _equipmentFinancingYears = 5;
        public int EquipmentFinancingYears
        {
            get => _equipmentFinancingYears;
            set => SetProperty(ref _equipmentFinancingYears, value);
        }

        private decimal _reservePercentage;
        public decimal ReservePercentage
        {
            get => _reservePercentage;
            set => SetProperty(ref _reservePercentage, value);
        }

        private ComprehensiveScenario _currentScenario;
        public ComprehensiveScenario CurrentScenario
        {
            get => _currentScenario;
            set => SetProperty(ref _currentScenario, value);
        }

        private bool _isScenarioRunning;
        public bool IsScenarioRunning
        {
            get => _isScenarioRunning;
            set => SetProperty(ref _isScenarioRunning, value);
        }

        private string _scenarioStatus;
        public string ScenarioStatus
        {
            get => _scenarioStatus;
            set => SetProperty(ref _scenarioStatus, value);
        }

        // Search and filtering properties
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        private ObservableCollection<Enterprise> _filteredEnterprises = new();
        public ObservableCollection<Enterprise> FilteredEnterprises
        {
            get => _filteredEnterprises;
            set => SetProperty(ref _filteredEnterprises, value);
        }

        // Error handling
        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        // IDataErrorInfo implementation for validation
        public string Error => string.Empty;

        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(TotalBudget):
                        if (TotalBudget < 0)
                            return "Total budget cannot be negative";
                        break;
                    case nameof(TotalEnterprises):
                        if (TotalEnterprises < 0)
                            return "Total enterprises cannot be negative";
                        break;
                    case nameof(ActiveProjects):
                        if (ActiveProjects < 0)
                            return "Active projects cannot be negative";
                        break;
                }
                return string.Empty;
            }
        }

        // Commands
        public DelegateCommand LoadDataCommand { get; private set; }
        public DelegateCommand RefreshDashboardCommand { get; private set; }
        public DelegateCommand ToggleAutoRefreshCommand { get; private set; }
        public DelegateCommand ExportDashboardCommand { get; private set; }
        public DelegateCommand OpenBudgetAnalysisCommand { get; private set; }
        public DelegateCommand OpenSettingsCommand { get; private set; }
        public DelegateCommand GenerateReportCommand { get; private set; }
        public DelegateCommand BackupDataCommand { get; private set; }
        public DelegateCommand SearchCommand { get; private set; }
        public DelegateCommand ClearSearchCommand { get; private set; }
        public DelegateCommand NavigateToAccountsCommand { get; private set; }
        public DelegateCommand NavigateBackCommand { get; private set; }
        public DelegateCommand NavigateForwardCommand { get; private set; }
        public DelegateCommand OpenEnterpriseManagementCommand { get; private set; }
        public DelegateCommand<object> RunGrowthScenarioCommand { get; private set; }

        // Event subscription tokens for proper cleanup
        private SubscriptionToken _refreshDataSubscription;
        private SubscriptionToken _enterpriseChangedSubscription;
        private SubscriptionToken _budgetUpdatedSubscription;
        private SubscriptionToken _accountsLoadedSubscription;

        public DashboardViewModel(
            ILogger<DashboardViewModel> logger,
            IEnterpriseRepository enterpriseRepository,
            IWhatIfScenarioEngine whatIfScenarioEngine,
            IUtilityCustomerRepository utilityCustomerRepository,
            IMunicipalAccountRepository municipalAccountRepository,
            FiscalYearSettings fiscalYearSettings,
            IEventAggregator eventAggregator,
            IRegionManager regionManager,
            Lazy<ICacheService>? lazyCacheService = null,
            Lazy<ISettingsService>? lazySettingsService = null,
            Lazy<IQuickBooksService>? lazyQuickBooksService = null,
            Lazy<IChargeCalculatorService>? lazyChargeCalculatorService = null,
            bool autoLoadData = true)
        {
            // REQUIRED dependencies (constructor injection enforces these)
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
            _whatIfScenarioEngine = whatIfScenarioEngine ?? throw new ArgumentNullException(nameof(whatIfScenarioEngine));
            _utilityCustomerRepository = utilityCustomerRepository ?? throw new ArgumentNullException(nameof(utilityCustomerRepository));
            _municipalAccountRepository = municipalAccountRepository ?? throw new ArgumentNullException(nameof(municipalAccountRepository));
            _fiscalYearSettings = fiscalYearSettings ?? throw new ArgumentNullException(nameof(fiscalYearSettings));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));

            // OPTIONAL dependencies (Lazy<T> prevents circular references and initialization order issues)
            _lazyCacheService = lazyCacheService;
            _lazySettingsService = lazySettingsService;
            _lazyQuickBooksService = lazyQuickBooksService;
            _lazyChargeCalculatorService = lazyChargeCalculatorService;

            _logger.LogInformation("DashboardViewModel constructor: Initializing with {RequiredDeps} required and {OptionalDeps} optional dependencies",
                8, (new object?[] { lazyCacheService, lazySettingsService, lazyQuickBooksService, lazyChargeCalculatorService }).Count(x => x != null));

            // Subscribe to collection change events for detailed logging
            Enterprises.CollectionChanged += Enterprises_CollectionChanged;
            FilteredEnterprises.CollectionChanged += FilteredEnterprises_CollectionChanged;

            // Subscribe to events
            _refreshDataSubscription = _eventAggregator.GetEvent<RefreshDataMessage>().Subscribe(OnRefreshDataRequested);
            _enterpriseChangedSubscription = _eventAggregator.GetEvent<EnterpriseChangedMessage>().Subscribe(OnEnterpriseChanged);
            _budgetUpdatedSubscription = _eventAggregator.GetEvent<BudgetUpdatedMessage>().Subscribe(OnBudgetUpdated, ThreadOption.PublisherThread);
            _accountsLoadedSubscription = _eventAggregator.GetEvent<AccountsLoadedEvent>().Subscribe(OnAccountsLoaded, ThreadOption.PublisherThread);

            // Initialize commands
            InitializeCommands();

            // Setup auto-refresh timer
            SetupAutoRefreshTimer();

            // Load initial data (tracked) - can be disabled for testing
            if (autoLoadData)
            {
                _initialLoadTask = LoadDashboardDataAsync();
            }

            // Preload enterprises into cache for E2E scenarios (cache-first, tracked) - only if autoLoadData is true
            if (autoLoadData)
            {
                _cacheLoadingTask = Task.Run(async () =>
                {
                    try
                    {
                        if (CacheService != null)
                        {
                            var cached = await CacheService.GetAsync<System.Collections.Generic.List<Enterprise>>("enterprises").ConfigureAwait(false);
                            if (cached != null && cached.Any())
                                return;
                        }

                        var all = await enterpriseRepository.GetAllAsync().ConfigureAwait(false);
                        var list = all?.ToList() ?? new System.Collections.Generic.List<Enterprise>();
                        if (CacheService != null && list.Any())
                            await CacheService.SetAsync("enterprises", list, TimeSpan.FromHours(6)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to preload enterprises in DashboardViewModel");
                    }
                });
            }
        }

        private void InitializeCommands()
        {
            LoadDataCommand = new DelegateCommand(async () => await LoadDashboardDataAsync(), () => !IsLoading);
            RefreshDashboardCommand = new DelegateCommand(async () => await ExecuteRefreshDashboardAsync(), () => !IsLoading);
            ToggleAutoRefreshCommand = new DelegateCommand(ExecuteToggleAutoRefresh);
            ExportDashboardCommand = new DelegateCommand(async () => await ExecuteExportDashboardAsync(), () => !IsLoading);
            OpenBudgetAnalysisCommand = new DelegateCommand(ExecuteOpenBudgetAnalysis);
            OpenSettingsCommand = new DelegateCommand(ExecuteOpenSettings);
            GenerateReportCommand = new DelegateCommand(async () => await ExecuteGenerateReportAsync(), () => !IsLoading);
            BackupDataCommand = new DelegateCommand(async () => await ExecuteBackupDataAsync(), () => !IsLoading);
            SearchCommand = new DelegateCommand(ExecuteSearch);
            ClearSearchCommand = new DelegateCommand(ExecuteClearSearch, () => !string.IsNullOrWhiteSpace(SearchText));
            NavigateToAccountsCommand = new DelegateCommand(ExecuteNavigateToAccounts);
            NavigateBackCommand = new DelegateCommand(ExecuteNavigateBack, () => CanNavigateBack);
            NavigateForwardCommand = new DelegateCommand(ExecuteNavigateForward, () => CanNavigateForward);
            OpenEnterpriseManagementCommand = new DelegateCommand(ExecuteOpenEnterpriseManagement);
            RunGrowthScenarioCommand = new DelegateCommand<object>(async (param) => await ExecuteRunGrowthScenarioAsync(Convert.ToInt32(param, CultureInfo.InvariantCulture)), (param) => !IsScenarioRunning);
        }

        private bool CanNavigateBack => _regionManager.Regions.ContainsRegionWithName("MainRegion") && _regionManager.Regions["MainRegion"].NavigationService.Journal.CanGoBack;
        private bool CanNavigateForward => _regionManager.Regions.ContainsRegionWithName("MainRegion") && _regionManager.Regions["MainRegion"].NavigationService.Journal.CanGoForward;

        private void SetupAutoRefreshTimer()
        {
            // Prevent multiple timer instances
            if (_refreshTimer != null)
            {
                _logger.LogWarning("DashboardView: Auto-refresh timer already exists, skipping setup");
                return;
            }

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Tick += async (s, e) =>
            {
                using var loggingContext = LoggingContext.BeginOperation("AutoRefresh_Tick");
                var callingThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                var uiThreadId = Dispatcher.CurrentDispatcher.Thread.ManagedThreadId;

                _logger.LogDebug("DashboardView AutoRefresh timer tick - ThreadId: {CallingThread} -> UI ThreadId: {UIThread} - {LogContext}",
                    callingThreadId, uiThreadId, loggingContext);

                if (AutoRefreshEnabled)
                {
                    _logger.LogDebug("DashboardView: Executing auto-refresh - {LogContext}", loggingContext);
                    await RefreshDashboardDataAsync();
                    _logger.LogDebug("DashboardView: Auto-refresh completed - {LogContext}", loggingContext);
                }
                else
                {
                    _logger.LogDebug("DashboardView: Auto-refresh skipped (disabled) - {LogContext}", loggingContext);
                }
            };
            _refreshTimer.Interval = TimeSpan.FromMinutes(RefreshIntervalMinutes);
            _refreshTimer.Start();

            _logger.LogInformation("DashboardView: Auto-refresh timer started with {IntervalMinutes} minute interval",
                RefreshIntervalMinutes);
        }

        public async Task LoadDashboardDataAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("LoadDashboard");
            var overallStopwatch = Stopwatch.StartNew();
            var hasErrors = false;

            try
            {
                DashboardStatus = "Loading dashboard data...";
                _logger.LogInformation("Starting dashboard data load - CorrelationId: {CorrelationId}",
                    loggingContext.CorrelationId);

                // Load all dashboard data in parallel
                await Task.WhenAll(
                    LoadKPIsAsync(),
                    LoadEnterprisesAsync(),
                    LoadChartDataAsync(),
                    LoadActivitiesAsync(),
                    LoadAlertsAsync(),
                    LoadBudgetInsightsAsync(),
                    LoadRecentBudgetChangesAsync(),
                    LoadQuickActionsAsync(),
                    LoadPanelItemsAsync(),
                    LoadScenarioInputsAsync()
                );

                overallStopwatch.Stop();
                DashboardStatus = "Dashboard loaded successfully";
                LastUpdated = DateTime.Now.ToString("g", CultureInfo.InvariantCulture);
                UpdateNextRefreshTime();

                _logger.LogInformation("Dashboard data loaded successfully in {ElapsedMs}ms - {TotalEnterprises} enterprises, ${TotalBudget} total budget - {LogContext}",
                    overallStopwatch.ElapsedMilliseconds, TotalEnterprises, TotalBudget, loggingContext);

                // Publish DataLoadedEvent to notify other ViewModels
                _eventAggregator.GetEvent<DataLoadedEvent>().Publish(new DataLoadedEvent
                {
                    ViewModelName = "DashboardViewModel",
                    ItemCount = TotalEnterprises
                });
            }
            catch (Exception ex)
            {
                overallStopwatch.Stop();
                hasErrors = true;
                DashboardStatus = "Error loading dashboard";
                _logger.LogError(ex, "CRITICAL: Dashboard data load failed after {ElapsedMs}ms - {Message} - {LogContext}",
                    overallStopwatch.ElapsedMilliseconds, ex.Message, loggingContext);

                // Don't show misleading success message
                // Surface error via status message and logging (avoid direct UI calls from ViewModel)
                StatusMessage = "Error loading dashboard";
                _logger.LogError(ex, "Dashboard error shown to user: {Message}", ex.Message);

                throw; // Propagate exception to prevent misleading success logs
            }
            finally
            {
                if (!hasErrors)
                {
                    _logger.LogDebug("Dashboard load completed without errors in {ElapsedMs}ms - {LogContext}",
                        overallStopwatch.ElapsedMilliseconds, loggingContext);
                }
            }
        }

        private async Task LoadKPIsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Get enterprise data - await directly, no Task.Run wrapper
                var enterprises = await _enterpriseRepository.GetAllAsync();

                TotalEnterprises = enterprises.Count();
                TotalBudget = enterprises.Sum(e => e.TotalBudget);

                // Calculate active projects (enterprises with recent activity)
                ActiveProjects = enterprises.Count(e =>
                    e.LastModified.HasValue &&
                    e.LastModified.Value > DateTime.Now.AddDays(-30));

                // Calculate system health based on various factors
                CalculateSystemHealth();

                // Calculate changes (simplified - in real app would compare with historical data)
                CalculateChanges();

                stopwatch.Stop();
                _logger.LogDebug("KPIs loaded successfully in {ElapsedMs}ms: {TotalEnterprises} enterprises, ${TotalBudget} budget",
                    stopwatch.ElapsedMilliseconds, TotalEnterprises, TotalBudget);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error loading KPIs after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                throw; // Propagate error to caller
            }
        }

        private async Task LoadEnterprisesAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                ErrorMessage = string.Empty;
                Enterprises.Clear();

                // Await directly - repository already uses async/await properly
                var enterprises = await _enterpriseRepository.GetAllAsync();

                foreach (var enterprise in enterprises)
                {
                    Enterprises.Add(enterprise);
                }

                // Initialize filtered collection with all enterprises
                FilteredEnterprises.Clear();
                foreach (var enterprise in Enterprises)
                {
                    FilteredEnterprises.Add(enterprise);
                }

                stopwatch.Stop();
                _logger.LogDebug("Loaded {Count} enterprises successfully in {ElapsedMs}ms",
                    enterprises.Count(), stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                ErrorMessage = $"Failed to load enterprises: {ex.Message}";
                _logger.LogError(ex, "Error loading enterprises after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                throw; // Propagate error to caller
            }
        }

        private void CalculateSystemHealth()
        {
            // Simple health calculation based on data availability and recent activity
            var healthFactors = new[]
            {
                TotalEnterprises > 0 ? 25 : 0,
                TotalBudget > 0 ? 25 : 0,
                ActiveProjects > 0 ? 25 : 0,
                true ? 25 : 0 // Database connectivity
            };

            HealthScore = healthFactors.Sum();
            SystemHealthScore = HealthScore;

            if (HealthScore >= 90)
            {
                SystemHealthStatus = "Excellent";
                SystemHealthColor = Brushes.Green;
            }
            else if (HealthScore >= 75)
            {
                SystemHealthStatus = "Good";
                SystemHealthColor = Brushes.Green;
            }
            else if (HealthScore >= 60)
            {
                SystemHealthStatus = "Fair";
                SystemHealthColor = Brushes.Orange;
            }
            else
            {
                SystemHealthStatus = "Poor";
                SystemHealthColor = Brushes.Red;
            }
        }

        private void CalculateChanges()
        {
            // Simplified change calculations - in real app would use historical data
            EnterprisesChangeText = TotalEnterprises > 10 ? "+2 from last month" : "New this month";
            EnterprisesChangeColor = Brushes.Green;

            BudgetChangeText = TotalBudget > 100000 ? "+$15K from last month" : "Growing";
            BudgetChangeColor = Brushes.Green;

            ProjectsChangeText = ActiveProjects > 5 ? "+1 from last week" : "Stable";
            ProjectsChangeColor = Brushes.Green;
        }

        private async Task LoadChartDataAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Load budget trend data (last 6 months)
                BudgetTrendData.Clear();
                HistoricalData.Clear();
                RateTrendData.Clear();
                ChartData.Clear();
                ExpenseData.Clear();
                RevenueData.Clear();

                for (int i = 5; i >= 0; i--)
                {
                    var date = DateTime.Now.AddMonths(-i);
                    var trendItem = new BudgetTrendItem
                    {
                        Period = date.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                        Amount = TotalBudget * (decimal)(0.8 + (i * 0.04)) // Simulated growth
                    };

                    BudgetTrendData.Add(trendItem);
                    HistoricalData.Add(trendItem); // Also populate HistoricalData for binding

                    // Populate ChartData (total budget)
                    ChartData.Add(new BudgetTrendItem
                    {
                        Period = date.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                        Amount = TotalBudget * (decimal)(0.8 + (i * 0.04))
                    });

                    // Populate ExpenseData (simulated expenses at ~70% of budget)
                    ExpenseData.Add(new BudgetTrendItem
                    {
                        Period = date.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                        Amount = TotalBudget * (decimal)(0.56 + (i * 0.028)) // ~70% of budget
                    });

                    // Populate RevenueData (simulated revenue at ~85% of budget)
                    RevenueData.Add(new BudgetTrendItem
                    {
                        Period = date.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                        Amount = TotalBudget * (decimal)(0.68 + (i * 0.034)) // ~85% of budget
                    });

                    // Add rate trend data using the suggested rate
                    var rateItem = new RateTrendItem
                    {
                        Period = date.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                        Rate = SuggestedRate
                    };
                    RateTrendData.Add(rateItem);
                }

                // Load enterprise type distribution - await directly
                EnterpriseTypeData.Clear();
                var enterprises = await _enterpriseRepository.GetAllAsync();
                var typeGroups = enterprises.GroupBy(e => e.Type ?? "Other");

                foreach (var group in typeGroups)
                {
                    EnterpriseTypeData.Add(new EnterpriseTypeItem
                    {
                        Type = group.Key,
                        Count = group.Count()
                    });
                }

                stopwatch.Stop();
                _logger.LogDebug("Chart data loaded successfully in {ElapsedMs}ms - {BudgetPoints} budget points, {TypeGroups} type groups",
                    stopwatch.ElapsedMilliseconds, BudgetTrendData.Count, EnterpriseTypeData.Count);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error loading chart data after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                throw; // Propagate error to caller
            }
        }

        private async Task LoadActivitiesAsync()
        {
            try
            {
                RecentActivities.Clear();

                // Add sample recent activities
                RecentActivities.Add(new ActivityItem
                {
                    Timestamp = DateTime.Now.AddMinutes(-5),
                    Description = "Enterprise budget updated",
                    Type = "Budget"
                });

                RecentActivities.Add(new ActivityItem
                {
                    Timestamp = DateTime.Now.AddMinutes(-15),
                    Description = "New enterprise added",
                    Type = "Enterprise"
                });

                RecentActivities.Add(new ActivityItem
                {
                    Timestamp = DateTime.Now.AddMinutes(-30),
                    Description = "Report generated",
                    Type = "Report"
                });

                RecentActivities.Add(new ActivityItem
                {
                    Timestamp = DateTime.Now.AddHours(-1),
                    Description = "Database backup completed",
                    Type = "System"
                });

                RecentActivities.Add(new ActivityItem
                {
                    Timestamp = DateTime.Now.AddHours(-2),
                    Description = "Settings updated",
                    Type = "Configuration"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading activities");
            }
            await Task.CompletedTask; // Suppress async warning for future async operations
        }

        private async Task LoadAlertsAsync()
        {
            try
            {
                SystemAlerts.Clear();

                // Add sample alerts based on system status
                if (TotalEnterprises == 0)
                {
                    SystemAlerts.Add(new AlertItem
                    {
                        Priority = "High",
                        Message = "No enterprises configured",
                        Timestamp = DateTime.Now,
                        PriorityColor = Brushes.Red
                    });
                }

                if (HealthScore < 75)
                {
                    SystemAlerts.Add(new AlertItem
                    {
                        Priority = "Medium",
                        Message = "System health below optimal",
                        Timestamp = DateTime.Now,
                        PriorityColor = Brushes.Orange
                    });
                }

                // Add informational alerts
                SystemAlerts.Add(new AlertItem
                {
                    Priority = "Low",
                    Message = "Database backup due soon",
                    Timestamp = DateTime.Now.AddHours(2),
                    PriorityColor = Brushes.Blue
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading alerts");
            }
            await Task.CompletedTask; // Suppress async warning for future async operations
        }

        private async Task LoadBudgetInsightsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                BudgetInsights.Clear();

                // Generate budget insights based on current data
                var enterprises = Enterprises.ToList();
                if (enterprises.Any())
                {
                    // Calculate average budget
                    var avgBudget = enterprises.Average(e => e.TotalBudget);
                    var totalBudget = enterprises.Sum(e => e.TotalBudget);

                    BudgetInsights.Add(new BudgetInsight
                    {
                        Category = "Overall Budget",
                        Insight = $"Total budget across all enterprises: {totalBudget:C0}",
                        Amount = totalBudget,
                        Trend = "Stable",
                        Severity = "Info",
                        PeriodStart = DateTime.Now.AddMonths(-1),
                        PeriodEnd = DateTime.Now
                    });

                    // Find over-budget scenarios
                    var overBudgetCount = enterprises.Count(e => e.TotalBudget > avgBudget * 1.2m);
                    if (overBudgetCount > 0)
                    {
                        BudgetInsights.Add(new BudgetInsight
                        {
                            Category = "Budget Variance",
                            Insight = $"{overBudgetCount} enterprise(s) exceed average budget by 20%+",
                            Amount = avgBudget,
                            PercentageChange = 20,
                            Trend = "Up",
                            Severity = "Warning",
                            PeriodStart = DateTime.Now.AddMonths(-1),
                            PeriodEnd = DateTime.Now
                        });
                    }
                }

                stopwatch.Stop();
                _logger.LogDebug("Budget insights loaded successfully in {ElapsedMs}ms - {Count} insights",
                    stopwatch.ElapsedMilliseconds, BudgetInsights.Count);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error loading budget insights after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            await Task.CompletedTask;
        }

        private async Task LoadRecentBudgetChangesAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                RecentBudgetChanges.Clear();

                // Generate sample budget changes (in production, load from audit log)
                var enterprises = Enterprises.Take(5).ToList();
                foreach (var enterprise in enterprises)
                {
                    RecentBudgetChanges.Add(new BudgetChange
                    {
                        ChangeDate = DateTime.Now.AddDays(-new Random().Next(1, 30)),
                        FundName = enterprise.Name,
                        AccountName = "Operating Budget",
                        PreviousAmount = enterprise.TotalBudget * 0.95m,
                        NewAmount = enterprise.TotalBudget,
                        ChangedBy = Environment.UserName,
                        Reason = "Annual budget adjustment",
                        ChangeType = "Increase"
                    });
                }

                stopwatch.Stop();
                _logger.LogDebug("Recent budget changes loaded successfully in {ElapsedMs}ms - {Count} changes",
                    stopwatch.ElapsedMilliseconds, RecentBudgetChanges.Count);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error loading recent budget changes after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            await Task.CompletedTask;
        }

        private async Task LoadQuickActionsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                QuickActions.Clear();

                // Define quick action shortcuts
                QuickActions.Add(new QuickAction
                {
                    Title = "Add Enterprise",
                    Description = "Create a new enterprise fund",
                    Icon = "AddCircle",
                    Command = OpenEnterpriseManagementCommand,
                    Category = "Management",
                    IsEnabled = !IsLoading
                });

                QuickActions.Add(new QuickAction
                {
                    Title = "View Budget Analysis",
                    Description = "Open detailed budget analysis",
                    Icon = "ChartLine",
                    Command = OpenBudgetAnalysisCommand,
                    Category = "Analysis",
                    IsEnabled = !IsLoading
                });

                QuickActions.Add(new QuickAction
                {
                    Title = "Generate Report",
                    Description = "Create a financial report",
                    Icon = "DocumentText",
                    Command = GenerateReportCommand,
                    Category = "Reporting",
                    IsEnabled = !IsLoading
                });

                QuickActions.Add(new QuickAction
                {
                    Title = "Refresh Data",
                    Description = "Reload dashboard data",
                    Icon = "Refresh",
                    Command = RefreshDashboardCommand,
                    Category = "Data",
                    IsEnabled = !IsLoading
                });

                stopwatch.Stop();
                _logger.LogDebug("Quick actions loaded successfully in {ElapsedMs}ms - {Count} actions",
                    stopwatch.ElapsedMilliseconds, QuickActions.Count);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error loading quick actions after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            await Task.CompletedTask;
        }

        private async Task LoadPanelItemsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                PanelItems.Clear();

                // Define dashboard panel items for tile-based layouts
                PanelItems.Add(new PanelItem
                {
                    Title = "KPIs",
                    Content = "Key Performance Indicators",
                    Icon = "Dashboard",
                    BackgroundColor = "#2196F3",
                    RowSpan = 1,
                    ColumnSpan = 2
                });

                PanelItems.Add(new PanelItem
                {
                    Title = "Budget Trends",
                    Content = "Historical budget analysis",
                    Icon = "ChartLine",
                    BackgroundColor = "#4CAF50",
                    RowSpan = 1,
                    ColumnSpan = 2
                });

                PanelItems.Add(new PanelItem
                {
                    Title = "Alerts",
                    Content = "System notifications",
                    Icon = "Warning",
                    BackgroundColor = "#FF9800",
                    RowSpan = 1,
                    ColumnSpan = 1
                });

                PanelItems.Add(new PanelItem
                {
                    Title = "Recent Activity",
                    Content = "Latest system activity",
                    Icon = "History",
                    BackgroundColor = "#9C27B0",
                    RowSpan = 1,
                    ColumnSpan = 1
                });

                stopwatch.Stop();
                _logger.LogDebug("Panel items loaded successfully in {ElapsedMs}ms - {Count} panels",
                    stopwatch.ElapsedMilliseconds, PanelItems.Count);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error loading panel items after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            await Task.CompletedTask;
        }

        private void UpdateNextRefreshTime()
        {
            if (AutoRefreshEnabled)
            {
                NextRefreshTime = DateTime.Now.AddMinutes(RefreshIntervalMinutes).ToString("HH:mm", CultureInfo.InvariantCulture);
            }
            else
            {
                NextRefreshTime = "Disabled";
            }
        }

        internal async Task RefreshDashboardDataAsync()
        {
            await LoadDashboardDataAsync();
        }

        private async Task ExecuteRefreshDashboardAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("RefreshDashboard");
            _logger.LogInformation("RefreshDashboard command invoked - IsLoading: {IsLoading} - {LogContext}",
                IsLoading, loggingContext);

            IsLoading = true;
            StatusMessage = "Refreshing dashboard...";

            try
            {
                await LoadDashboardDataAsync();
                StatusMessage = "Dashboard refreshed successfully";
                _logger.LogInformation("RefreshDashboard command completed successfully - {LogContext}", loggingContext);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error refreshing dashboard";
                _logger.LogError(ex, "RefreshDashboard command failed: {Message} - {LogContext}",
                    ex.Message, loggingContext);
                StatusMessage = "Error refreshing dashboard";
                _logger.LogError(ex, "Refresh dashboard error: {Message}", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExecuteToggleAutoRefresh()
        {
            AutoRefreshEnabled = !AutoRefreshEnabled;
            _logger.LogInformation("ToggleAutoRefresh command - New state: {AutoRefreshEnabled}, Interval: {RefreshIntervalMinutes} minutes",
                AutoRefreshEnabled, RefreshIntervalMinutes);
        }

        private async Task ExecuteExportDashboardAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("ExportDashboard");
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("ExportDashboard command invoked - {LogContext}", loggingContext);

            try
            {
                IsLoading = true;
                StatusMessage = "Exporting dashboard data...";

                // Create export data
                var exportData = new
                {
                    ExportDate = DateTime.Now,
                    DashboardData = new
                    {
                        TotalEnterprises = TotalEnterprises,
                        TotalBudget = TotalBudget,
                        SystemHealthScore = HealthScore,
                        SystemHealthStatus = SystemHealthStatus,
                        AutoRefreshEnabled = AutoRefreshEnabled,
                        RefreshIntervalMinutes = RefreshIntervalMinutes,
                        LastUpdated = LastUpdated
                    },
                    Enterprises = Enterprises.Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.Type,
                        e.Description
                    }).ToList(),
                    HistoricalData = HistoricalData.ToList(),
                    RateTrendData = RateTrendData.ToList(),
                    EnterpriseTypeData = EnterpriseTypeData.ToList()
                };

                // Serialize to JSON
                var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Show save file dialog
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Dashboard Data",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = ".json",
                    FileName = $"WileyWidget_Dashboard_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    await System.IO.File.WriteAllTextAsync(saveFileDialog.FileName, json);
                    StatusMessage = $"Dashboard exported to {System.IO.Path.GetFileName(saveFileDialog.FileName)}";
                    _logger.LogInformation("Dashboard data exported successfully to {FilePath} - {LogContext}",
                        saveFileDialog.FileName, loggingContext);

                    StatusMessage = $"Dashboard exported to {System.IO.Path.GetFileName(saveFileDialog.FileName)}";
                    _logger.LogInformation("Dashboard data exported successfully to {FilePath}", saveFileDialog.FileName);
                }
                else
                {
                    StatusMessage = "Dashboard export cancelled";
                    _logger.LogInformation("Dashboard export cancelled by user - {LogContext}", loggingContext);
                }

                stopwatch.Stop();
                _logger.LogInformation("ExportDashboard completed in {ElapsedMs}ms - {LogContext}",
                    stopwatch.ElapsedMilliseconds, loggingContext);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                StatusMessage = "Error exporting dashboard";
                _logger.LogError(ex, "ExportDashboard failed after {ElapsedMs}ms - {LogContext}",
                    stopwatch.ElapsedMilliseconds, loggingContext);
                StatusMessage = "Error exporting dashboard";
                _logger.LogError(ex, "Export dashboard failed: {Message}", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExecuteOpenBudgetAnalysis()
        {
            _logger.LogInformation("OpenBudgetAnalysis command invoked");
            // Navigate to budget analysis region with safety and fallback
            SafeRequestNavigate("MainRegion", "BudgetAnalysisView", fallbackTarget: "DashboardView");
        }

        private void ExecuteOpenSettings()
        {
            _logger.LogInformation("OpenSettings command invoked");
            // Navigate to settings region with safety and fallback
            SafeRequestNavigate("MainRegion", "SettingsView", fallbackTarget: "DashboardView");
        }

        private async Task ExecuteGenerateReportAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("GenerateReport");
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("GenerateReport command invoked - {LogContext}", loggingContext);

            try
            {
                IsLoading = true;
                StatusMessage = "Generating dashboard report...";

                // Create report data
                var reportData = new
                {
                    ReportTitle = "Wiley Widget Dashboard Report",
                    GeneratedDate = DateTime.Now,
                    ReportPeriod = $"{DateTime.Now.AddDays(-30):yyyy-MM-dd} to {DateTime.Now:yyyy-MM-dd}",
                    Summary = new
                    {
                        TotalEnterprises = TotalEnterprises,
                        TotalBudget = TotalBudget,
                        SystemHealthScore = HealthScore,
                        SystemHealthStatus = SystemHealthStatus,
                        EnterpriseChangeText = EnterprisesChangeText,
                        BudgetChangeText = BudgetChangeText
                    },
                    EnterpriseDetails = Enterprises.Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.Type,
                        e.Description
                    }).ToList(),
                    PerformanceMetrics = new
                    {
                        DataPoints = HistoricalData.Count,
                        RateTrends = RateTrendData.Count,
                        EnterpriseTypes = EnterpriseTypeData.Count
                    },
                    Recommendations = new[]
                    {
                    "Monitor system health score above 80%",
                    "Review budget utilization trends",
                    "Check enterprise performance metrics",
                    "Consider rate adjustments based on scenario analysis"
                }
                };

                // Generate HTML report
                var htmlReport = GenerateHtmlReport(reportData);

                // Show save file dialog
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Save Dashboard Report",
                    Filter = "HTML files (*.html)|*.html|All files (*.*)|*.*",
                    DefaultExt = ".html",
                    FileName = $"WileyWidget_Dashboard_Report_{DateTime.Now:yyyyMMdd_HHmmss}.html"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    await System.IO.File.WriteAllTextAsync(saveFileDialog.FileName, htmlReport);
                    StatusMessage = $"Report generated: {System.IO.Path.GetFileName(saveFileDialog.FileName)}";
                    _logger.LogInformation("Dashboard report generated successfully to {FilePath} - {LogContext}",
                        saveFileDialog.FileName, loggingContext);

                    // Ask if user wants to open the report
                    // Inform via status and log; opening the file is a UI operation left to the view or user
                    StatusMessage = $"Report generated: {System.IO.Path.GetFileName(saveFileDialog.FileName)}";
                    _logger.LogInformation("Report generated at {FilePath}", saveFileDialog.FileName);
                }
                else
                {
                    StatusMessage = "Report generation cancelled";
                    _logger.LogInformation("Report generation cancelled by user - {LogContext}", loggingContext);
                }

                stopwatch.Stop();
                _logger.LogInformation("GenerateReport completed in {ElapsedMs}ms - {LogContext}",
                    stopwatch.ElapsedMilliseconds, loggingContext);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                StatusMessage = "Error generating report";
                _logger.LogError(ex, "GenerateReport failed after {ElapsedMs}ms - {LogContext}",
                    stopwatch.ElapsedMilliseconds, loggingContext);
                StatusMessage = "Error generating report";
                _logger.LogError(ex, "Generate report failed: {Message}", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string GenerateHtmlReport(object reportData)
        {
            // Cast to dynamic for HTML generation
            dynamic data = reportData;
            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>{data.ReportTitle}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; }}
        .header {{ background: #2196F3; color: white; padding: 20px; border-radius: 8px; }}
        .section {{ margin: 20px 0; padding: 15px; border: 1px solid #ddd; border-radius: 5px; }}
        .summary {{ background: #f5f5f5; }}
        .metric {{ display: inline-block; margin: 10px; padding: 10px; background: white; border-radius: 5px; min-width: 150px; }}
        .recommendations {{ background: #e8f5e8; }}
        table {{ width: 100%; border-collapse: collapse; margin: 10px 0; }}
        th, td {{ padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }}
        th {{ background: #f2f2f2; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>{data.ReportTitle}</h1>
        <p>Generated: {data.GeneratedDate:yyyy-MM-dd HH:mm:ss}</p>
        <p>Report Period: {data.ReportPeriod}</p>
    </div>

    <div class='section summary'>
        <h2>Executive Summary</h2>
        <div class='metric'>
            <strong>Total Enterprises:</strong> {data.Summary.TotalEnterprises}
        </div>
        <div class='metric'>
            <strong>Total Budget:</strong> {data.Summary.TotalBudget:C0}
        </div>
        <div class='metric'>
            <strong>System Health:</strong> {data.Summary.SystemHealthScore}% ({data.Summary.SystemHealthStatus})
        </div>
        <p><strong>Enterprise Changes:</strong> {data.Summary.EnterpriseChangeText}</p>
        <p><strong>Budget Changes:</strong> {data.Summary.BudgetChangeText}</p>
    </div>

    <div class='section'>
        <h2>Enterprise Details</h2>
        <table>
            <thead>
                <tr>
                    <th>ID</th>
                    <th>Name</th>
                    <th>Type</th>
                    <th>Description</th>
                </tr>
            </thead>
            <tbody>
                {GenerateEnterpriseTableRows(data.EnterpriseDetails)}
            </tbody>
        </table>
    </div>

    <div class='section'>
        <h2>Performance Metrics</h2>
        <p>Data Points: {data.PerformanceMetrics.DataPoints}</p>
        <p>Rate Trends: {data.PerformanceMetrics.RateTrends}</p>
        <p>Enterprise Types: {data.PerformanceMetrics.EnterpriseTypes}</p>
    </div>

    <div class='section recommendations'>
        <h2>Recommendations</h2>
        <ul>
            {GenerateRecommendationsList(data.Recommendations)}
        </ul>
    </div>
</body>
</html>";
        }

        private string GenerateEnterpriseTableRows(dynamic enterpriseDetails)
        {
            var rows = new System.Collections.Generic.List<string>();
            foreach (dynamic enterprise in enterpriseDetails)
            {
                rows.Add($"<tr><td>{enterprise.Id}</td><td>{enterprise.Name}</td><td>{enterprise.Type}</td><td>{enterprise.Description}</td></tr>");
            }
            return string.Join("", rows);
        }

        private string GenerateRecommendationsList(dynamic recommendations)
        {
            var items = new System.Collections.Generic.List<string>();
            foreach (string recommendation in recommendations)
            {
                items.Add($"<li>{recommendation}</li>");
            }
            return string.Join("", items);
        }

        private async Task ExecuteBackupDataAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("BackupData");
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("BackupData command invoked - {LogContext}", loggingContext);

            try
            {
                IsLoading = true;
                StatusMessage = "Creating data backup...";

                // Create backup directory if it doesn't exist
                var backupDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "WileyWidget",
                    "Backups"
                );

                System.IO.Directory.CreateDirectory(backupDir);

                // Create backup filename with timestamp
                var backupFileName = $"WileyWidget_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var backupFilePath = System.IO.Path.Combine(backupDir, backupFileName);

                // Create backup data
                var backupData = new
                {
                    BackupDate = DateTime.Now,
                    Version = "1.0",
                    Application = "Wiley Widget",
                    DashboardData = new
                    {
                        TotalEnterprises = TotalEnterprises,
                        TotalBudget = TotalBudget,
                        SystemHealthScore = HealthScore,
                        SystemHealthStatus = SystemHealthStatus,
                        AutoRefreshEnabled = AutoRefreshEnabled,
                        RefreshIntervalMinutes = RefreshIntervalMinutes,
                        LastUpdated = LastUpdated
                    },
                    Enterprises = Enterprises.Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.Type,
                        e.Description,
                        e.CreatedDate,
                        e.ModifiedDate
                    }).ToList(),
                    HistoricalData = HistoricalData.ToList(),
                    RateTrendData = RateTrendData.ToList(),
                    EnterpriseTypeData = EnterpriseTypeData.ToList(),
                    Settings = new
                    {
                        FiscalYearSettings = _fiscalYearSettings,
                        ApplicationSettings = "Default"
                    }
                };

                // Serialize to JSON
                var json = System.Text.Json.JsonSerializer.Serialize(backupData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Write backup file
                await System.IO.File.WriteAllTextAsync(backupFilePath, json);

                // Create a compressed archive if possible
                var zipFilePath = System.IO.Path.Combine(backupDir, $"WileyWidget_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                try
                {
                    using (var archive = System.IO.Compression.ZipFile.Open(zipFilePath, System.IO.Compression.ZipArchiveMode.Create))
                    {
                        archive.CreateEntryFromFile(backupFilePath, backupFileName);
                    }

                    // Delete the uncompressed file
                    System.IO.File.Delete(backupFilePath);
                    backupFilePath = zipFilePath;
                    backupFileName = System.IO.Path.GetFileName(zipFilePath);
                }
                catch
                {
                    // If compression fails, keep the JSON file
                    _logger.LogWarning("Could not create compressed backup, keeping JSON file");
                }

                StatusMessage = $"Backup created: {backupFileName}";
                _logger.LogInformation("Data backup created successfully at {FilePath} - {LogContext}",
                    backupFilePath, loggingContext);

                StatusMessage = $"Backup created: {backupFileName}";
                _logger.LogInformation("Data backup created at {FilePath}", backupFilePath);

                stopwatch.Stop();
                _logger.LogInformation("BackupData completed in {ElapsedMs}ms - {LogContext}",
                    stopwatch.ElapsedMilliseconds, loggingContext);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                StatusMessage = "Error creating backup";
                _logger.LogError(ex, "BackupData failed after {ElapsedMs}ms - {LogContext}",
                    stopwatch.ElapsedMilliseconds, loggingContext);
                StatusMessage = "Error creating backup";
                _logger.LogError(ex, "Backup creation failed: {Message}", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExecuteSearch()
        {
            _logger.LogDebug("Search command invoked - SearchText: '{SearchText}'", SearchText);
            FilterEnterprises();
        }

        private void FilterEnterprises()
        {
            FilteredEnterprises.Clear();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // No search text, show all enterprises
                foreach (var enterprise in Enterprises)
                {
                    FilteredEnterprises.Add(enterprise);
                }
            }
            else
            {
                // Filter enterprises based on search text
                var filtered = Enterprises.Where(e =>
                    (e.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Type?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                );

                foreach (var enterprise in filtered)
                {
                    FilteredEnterprises.Add(enterprise);
                }
            }
        }

        private async Task LoadScenarioInputsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Initialize default values for growth scenario inputs
                PayRaisePercentage = 3.0m;     // 3% default pay raise
                BenefitsIncreaseAmount = 50m;  // $50/month benefits increase
                EquipmentPurchaseAmount = 0m;  // No equipment by default
                EquipmentFinancingYears = 5;   // 5-year financing
                ReservePercentage = 5.0m;      // 5% reserve increase

                // Calculate initial suggested rate
                var enterpriseId = await GetCurrentEnterpriseIdAsync();
                if (enterpriseId > 0)
                {
                    SuggestedRate = await CalculateSuggestedRateAsync(enterpriseId);
                }

                stopwatch.Stop();
                ScenarioStatus = "Scenario inputs loaded";
                _logger.LogDebug("Scenario inputs loaded in {ElapsedMs}ms - EnterpriseId: {EnterpriseId}, SuggestedRate: ${SuggestedRate}",
                    stopwatch.ElapsedMilliseconds, enterpriseId, SuggestedRate);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error loading scenario inputs after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                ScenarioStatus = "Error loading scenario inputs";
            }
        }

        private async Task<int> GetCurrentEnterpriseIdAsync()
        {
            try
            {
                // Get the first enterprise as current (you may want to implement proper selection logic)
                var enterprises = await _enterpriseRepository.GetAllAsync();
                return enterprises.FirstOrDefault()?.Id ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<decimal> CalculateSuggestedRateAsync(int enterpriseId)
        {
            try
            {
                // Get customer count
                var customerCount = await _utilityCustomerRepository.GetCountAsync();
                if (customerCount == 0) return 0;

                // Get enterprise to determine fund type
                var enterprise = await _enterpriseRepository.GetByIdAsync(enterpriseId);
                if (enterprise == null) return 0;

                // Map enterprise type to fund type (same logic as WhatIfScenarioEngine)
                var fundType = enterprise.Type switch
                {
                    "Water" => MunicipalFundType.Water,
                    "Sewer" => MunicipalFundType.Sewer,
                    "Trash" => MunicipalFundType.Trash,
                    _ => MunicipalFundType.Enterprise
                };

                // Get expense accounts for this fund
                var expenseAccounts = await _municipalAccountRepository.GetByFundAsync(fundType);

                // Calculate aggregated expenses (sum of balances from expense accounts)
                var aggregatedExpenses = expenseAccounts.Sum(account => account.Balance);

                // Add growth buffer (10% of expenses)
                var growthBuffer = aggregatedExpenses * 0.10m;

                // Calculate suggested rate
                var suggestedRate = (aggregatedExpenses + growthBuffer) / customerCount;

                return Math.Round(suggestedRate, 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating suggested rate");
                return 0;
            }
        }

        private async Task ExecuteRunGrowthScenarioAsync(int enterpriseId)
        {
            using var loggingContext = LoggingContext.BeginOperation("RunGrowthScenario");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                IsScenarioRunning = true;
                ScenarioStatus = "Running growth scenario analysis...";

                _logger.LogInformation("Starting growth scenario for EnterpriseId: {EnterpriseId} - {LogContext}",
                    enterpriseId, loggingContext);

                // Create scenario parameters from user inputs
                var parameters = new ScenarioParameters
                {
                    PayRaisePercentage = PayRaisePercentage / 100m, // Convert percentage to decimal
                    BenefitsIncreaseAmount = BenefitsIncreaseAmount,
                    EquipmentPurchaseAmount = EquipmentPurchaseAmount,
                    EquipmentFinancingYears = EquipmentFinancingYears,
                    ReservePercentage = ReservePercentage / 100m // Convert percentage to decimal
                };

                // Generate comprehensive scenario
                var scenario = await _whatIfScenarioEngine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters);

                // Store the scenario results
                CurrentScenario = scenario;

                // Recalculate suggested rate with new scenario data
                SuggestedRate = await CalculateSuggestedRateAsync(enterpriseId);

                stopwatch.Stop();
                ScenarioStatus = $"Scenario '{scenario.ScenarioName}' completed successfully";

                _logger.LogInformation("Growth scenario completed in {ElapsedMs}ms for enterprise {EnterpriseId} - New Rate: ${SuggestedRate} - {LogContext}",
                    stopwatch.ElapsedMilliseconds, enterpriseId, SuggestedRate, loggingContext);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                ScenarioStatus = $"Error running scenario: {ex.Message}";
                _logger.LogError(ex, "Error running growth scenario after {ElapsedMs}ms for enterprise {EnterpriseId} - {LogContext}",
                    stopwatch.ElapsedMilliseconds, enterpriseId, loggingContext);
            }
            finally
            {
                IsScenarioRunning = false;
            }
        }

        private void ExecuteClearSearch()
        {
            _logger.LogDebug("ClearSearch command invoked - Previous SearchText: '{SearchText}'", SearchText);
            SearchText = string.Empty;
        }

        // Navigation commands for testing journaling
        private void ExecuteNavigateToAccounts()
        {
            _logger.LogInformation("NavigateToAccounts command invoked - Navigating to MunicipalAccountView");
            SafeRequestNavigate("MainRegion", "MunicipalAccountView", fallbackTarget: "DashboardView");
        }

        private void ExecuteNavigateBack()
        {
            var region = _regionManager.Regions["DashboardRegion"];
            var canGoBack = region.NavigationService.Journal.CanGoBack;
            _logger.LogDebug("NavigateBack command invoked - CanGoBack: {CanGoBack}", canGoBack);

            if (canGoBack)
            {
                region.NavigationService.Journal.GoBack();
            }
        }

        private void ExecuteNavigateForward()
        {
            var region = _regionManager.Regions["DashboardRegion"];
            var canGoForward = region.NavigationService.Journal.CanGoForward;
            _logger.LogDebug("NavigateForward command invoked - CanGoForward: {CanGoForward}", canGoForward);

            if (canGoForward)
            {
                region.NavigationService.Journal.GoForward();
            }
        }

        private void ExecuteOpenEnterpriseManagement()
        {
            _logger.LogInformation("OpenEnterpriseManagement command invoked");
            try
            {
                // Navigate to enterprise management view
                // Use safe navigation to ensure failures are handled gracefully
                SafeRequestNavigate("MainRegion", "EnterpriseView", fallbackTarget: "DashboardView");
                _logger.LogInformation("Successfully navigated to Enterprise management view");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open Enterprise management view");
                StatusMessage = "Error opening Enterprise management";
                _logger.LogError(ex, "Navigation to enterprise management failed: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Safely requests navigation to a target within a region. If navigation fails (exception or unsuccessful result),
        /// logs the error and attempts to navigate to a fallback target to keep the UI stable.
        /// This aligns with Prism 8+ Region navigation best practices and avoids hard crashes in the ViewModel layer.
        /// </summary>
        /// <param name="regionName">The region to navigate within (e.g., "MainRegion").</param>
        /// <param name="target">The target view name/relative URI (e.g., "BudgetAnalysisView").</param>
        /// <param name="fallbackTarget">Fallback target if primary navigation fails (default: "DashboardView").</param>
        private void SafeRequestNavigate(string regionName, string target, string fallbackTarget = "DashboardView")
        {
            try
            {
                if (!_regionManager.Regions.ContainsRegionWithName(regionName))
                {
                    _logger.LogError("Region '{RegionName}' not found. Falling back to '{Fallback}'.", regionName, fallbackTarget);
                    TryFallback(regionName, fallbackTarget);
                    return;
                }

                var region = _regionManager.Regions[regionName];

                // Prefer using the region's RequestNavigate with a callback so we can inspect NavigationResult
                region.RequestNavigate(new Uri(target, UriKind.Relative), result =>
                {
                    if (result == null || result.Success == false || result.Exception != null)
                    {
                        var err = result?.Exception;
                        if (err != null)
                        {
                            _logger.LogError(err, "Navigation to '{Target}' failed in region '{RegionName}'. Falling back to '{Fallback}'.", target, regionName, fallbackTarget);
                        }
                        else
                        {
                            _logger.LogError("Navigation to '{Target}' failed in region '{RegionName}' with no error provided. Falling back to '{Fallback}'.", target, regionName, fallbackTarget);
                        }
                        TryFallback(regionName, fallbackTarget);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during navigation to '{Target}' in region '{RegionName}'. Falling back to '{Fallback}'.", target, regionName, fallbackTarget);
                TryFallback(regionName, fallbackTarget);
            }
        }

        private void TryFallback(string regionName, string fallbackTarget)
        {
            try
            {
                if (_regionManager.Regions.ContainsRegionWithName(regionName))
                {
                    var region = _regionManager.Regions[regionName];
                    region.RequestNavigate(new Uri(fallbackTarget, UriKind.Relative));
                }
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Fallback navigation to '{Fallback}' also failed in region '{RegionName}'.", fallbackTarget, regionName);
            }
        }

        // Event Handlers for EventAggregator
        private void OnRefreshDataRequested(RefreshDataMessage message)
        {
            _logger.LogInformation("Refresh data requested for view: {ViewName}", message.ViewName);

            if (string.IsNullOrEmpty(message.ViewName) || message.ViewName == "Dashboard")
            {
                _ = LoadDashboardDataAsync();
            }
        }

        private void OnEnterpriseChanged(EnterpriseChangedMessage message)
        {
            _logger.LogInformation("Enterprise changed: {EnterpriseName} ({ChangeType})",
                message.EnterpriseName, message.ChangeType);

            // Refresh dashboard data when enterprise changes
            _ = LoadDashboardDataAsync();
        }

        private void OnBudgetUpdated(BudgetUpdatedMessage message)
        {
            _logger.LogInformation("Budget updated: {Context}. Refreshing dashboard data.", message.Context);

            // Refresh dashboard data when budget is updated
            _ = LoadDashboardDataAsync();
        }

        private void OnAccountsLoaded(AccountsLoadedEvent message)
        {
            _logger.LogInformation("Accounts loaded: {Count} accounts {Type}. Refreshing dashboard data.",
                message.AccountCount, message.LoadType);

            // Refresh dashboard data when accounts are loaded/seeded
            _ = LoadDashboardDataAsync();
        }

        // Collection change event handlers
        private void Enterprises_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    _logger.LogDebug("Enterprises collection - Added {Count} items at index {Index}",
                        e.NewItems?.Count ?? 0, e.NewStartingIndex);
                    if (e.NewItems != null)
                    {
                        foreach (Enterprise enterprise in e.NewItems)
                        {
                            _logger.LogTrace("Added Enterprise: Id={Id}, Name={Name}, Type={Type}",
                                enterprise.Id, enterprise.Name, enterprise.Type);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    _logger.LogDebug("Enterprises collection - Removed {Count} items at index {Index}",
                        e.OldItems?.Count ?? 0, e.OldStartingIndex);
                    if (e.OldItems != null)
                    {
                        foreach (Enterprise enterprise in e.OldItems)
                        {
                            _logger.LogTrace("Removed Enterprise: Id={Id}, Name={Name}",
                                enterprise.Id, enterprise.Name);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    _logger.LogDebug("Enterprises collection - Replaced {Count} items at index {Index}",
                        e.NewItems?.Count ?? 0, e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    _logger.LogDebug("Enterprises collection - Reset (cleared or major change)");
                    break;

                case NotifyCollectionChangedAction.Move:
                    _logger.LogDebug("Enterprises collection - Moved item from index {OldIndex} to {NewIndex}",
                        e.OldStartingIndex, e.NewStartingIndex);
                    break;
            }

            _logger.LogDebug("Enterprises collection now has {Count} items", Enterprises.Count);
        }

        private void FilteredEnterprises_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    _logger.LogTrace("FilteredEnterprises - Added {Count} items", e.NewItems?.Count ?? 0);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    _logger.LogTrace("FilteredEnterprises - Removed {Count} items", e.OldItems?.Count ?? 0);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    _logger.LogTrace("FilteredEnterprises - Reset (filter applied)");
                    break;
            }

            _logger.LogTrace("FilteredEnterprises now has {Count} items", FilteredEnterprises.Count);
        }

        // Prism Navigation Implementation (production-ready)
        private CancellationTokenSource? _navCts;

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("DashboardViewModel navigated to with context: {Context}", navigationContext?.ToString() ?? "null");

            try
            {
                _navCts?.Cancel();
                _navCts?.Dispose();
            }
            catch { }

            _navCts = new CancellationTokenSource();
            var ct = _navCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    IsLoading = true;
                    DashboardStatus = "Loading dashboard...";

                    if (navigationContext?.Parameters != null)
                    {
                        var didTriggerLoad = false;
                        // Be lenient with parameter types (bool or string)
                        if (navigationContext.Parameters.ContainsKey("refresh"))
                        {
                            var refreshObj = navigationContext.Parameters["refresh"];
                            var shouldRefresh = (refreshObj as bool?) == true ||
                                                (refreshObj is string s && bool.TryParse(s, out var br) && br);

                            if (shouldRefresh)
                            {
                                await LoadDashboardDataAsync();
                                didTriggerLoad = true;
                            }
                        }

                        if (navigationContext.Parameters.ContainsKey("filter") &&
                            navigationContext.Parameters["filter"] is string f)
                        {
                            // Dashboard does not support ApplyFiltersAsync; set SearchText for consumer views
                            // SearchText property may be bound in view code-behind if needed
                            _logger.LogInformation("Dashboard filter parameter received (ignored): {Filter}", f);
                        }

                        // Fallback: if no recognized parameter triggered a load, load by default
                        if (!didTriggerLoad)
                        {
                            await LoadDashboardDataAsync();
                        }
                    }
                    else
                    {
                        await LoadDashboardDataAsync();
                    }

                    DashboardStatus = "Ready";
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Dashboard navigation load canceled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during Dashboard OnNavigatedTo");
                    DashboardStatus = "Load failed";
                }
                finally
                {
                    IsLoading = false;
                    // ensure commands update
                    LoadDataCommand?.RaiseCanExecuteChanged();
                }
            }, ct);
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("DashboardViewModel navigated from");
            try
            {
                if (_navCts != null && !_navCts.IsCancellationRequested)
                    _navCts.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cancel Dashboard navigation token");
            }

            // Persist transient dashboard state if requested
            try
            {
                if (navigationContext?.Parameters != null && navigationContext.Parameters.ContainsKey("persistState") &&
                    navigationContext.Parameters["persistState"] is bool p && p)
                {
                    _logger.LogInformation("Dashboard requested to persist state, but persistence is not implemented here");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while handling OnNavigatedFrom in DashboardViewModel");
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            // If caller wants a fresh dashboard instance when refresh=true, return false
            try
            {
                if (navigationContext?.Parameters != null && navigationContext.Parameters.TryGetValue("refresh", out object? refreshParam))
                {
                    if (refreshParam is bool r && r)
                        return false;
                }
            }
            catch { }

            return true;
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
                // Wait for initial load to complete (with timeout)
                if (_initialLoadTask != null && !_initialLoadTask.IsCompleted)
                {
                    try
                    {
                        _initialLoadTask.Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (AggregateException ex)
                    {
                        _logger.LogWarning(ex, "Initial load task failed during dispose");
                    }
                }

                // Wait for cache loading to complete (with timeout)
                if (_cacheLoadingTask != null && !_cacheLoadingTask.IsCompleted)
                {
                    try
                    {
                        _cacheLoadingTask.Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (AggregateException ex)
                    {
                        _logger.LogWarning(ex, "Cache loading task failed during dispose");
                    }
                }

                // Stop and dispose the auto-refresh timer
                if (_refreshTimer != null)
                {
                    try
                    {
                        _refreshTimer.Stop();
                    }
                    catch { }
                    _refreshTimer = null;
                }

                // Unsubscribe collection changed handlers to avoid memory leaks
                try
                {
                    Enterprises.CollectionChanged -= Enterprises_CollectionChanged;
                }
                catch { }

                try
                {
                    FilteredEnterprises.CollectionChanged -= FilteredEnterprises_CollectionChanged;
                }
                catch { }

                // Unsubscribe from EventAggregator events to prevent memory leaks
                try
                {
                    _refreshDataSubscription?.Dispose();
                    _enterpriseChangedSubscription?.Dispose();
                    _budgetUpdatedSubscription?.Dispose();
                    _accountsLoadedSubscription?.Dispose();
                }
                catch { }

                // Cancel and dispose navigation cancellation token source (fixes reported IDisposable leak)
                if (_navCts != null)
                {
                    try
                    {
                        if (!_navCts.IsCancellationRequested)
                            _navCts.Cancel();
                    }
                    catch { }

                    try
                    {
                        _navCts.Dispose();
                    }
                    catch { }

                    _navCts = null;
                }
            }
        }
    }

    // Data models for dashboard
    public class BudgetTrendItem
    {
        public string Period { get; set; }
        public decimal Amount { get; set; }
    }

    public class RateTrendItem
    {
        public string Period { get; set; }
        public decimal Rate { get; set; }
    }

    public class EnterpriseTypeItem
    {
        public string Type { get; set; }
        public int Count { get; set; }
    }

    public class ActivityItem
    {
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
    }

    public class AlertItem
    {
        public string Priority { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public Brush PriorityColor { get; set; }
    }

    public class BudgetUtilizationData
    {
        public string Category { get; set; }
        public decimal Budgeted { get; set; }
        public decimal Actual { get; set; }
        public decimal UtilizationPercent { get; set; }
    }
}
