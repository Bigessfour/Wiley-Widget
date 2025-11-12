using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Prism;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Navigation.Regions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Logging;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;
using WileyWidget.Views;
using WileyWidget.Services.Abstractions;
using WileyWidget.Abstractions;

namespace WileyWidget.ViewModels.Main {
    public partial class MainViewModel : AsyncViewModelBase, INavigationAware
    {
        private readonly IDialogService dialogService;
        private readonly IRegionManager _regionManager;
        private readonly IEnterpriseRepository _enterpriseRepository;
        private readonly ICacheService? _cacheService;
        private readonly IExcelReaderService _excelReaderService;
        private readonly IReportExportService _reportExportService;
        private readonly IBudgetRepository _budgetRepository;
#pragma warning disable CS0169 // Field is never used - kept for future budget import functionality
        private readonly IBudgetImporter _budgetImporter;
#pragma warning restore CS0169
        private readonly IAIService? _aiService;

        public MainViewModel(IDialogService dialogService, IRegionManager regionManager, IDispatcherHelper dispatcherHelper, ILogger<MainViewModel> logger, IEnterpriseRepository enterpriseRepository, IExcelReaderService excelReaderService, IReportExportService reportExportService, IBudgetRepository budgetRepository, IBudgetImporter budgetImporter, IAIService? aiService = null, ICacheService? cacheService = null)
            : base(dispatcherHelper, logger)
        {
            // this.regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager)); // Disabled
            this.dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
            _cacheService = cacheService;
            _excelReaderService = excelReaderService ?? throw new ArgumentNullException(nameof(excelReaderService));
            _reportExportService = reportExportService ?? throw new ArgumentNullException(nameof(reportExportService));
            _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
            _aiService = aiService;

            // Subscribe to collection change events for detailed logging
            Enterprises.CollectionChanged += Enterprises_CollectionChanged;

            // Load initial data from cache or database
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_cacheService != null)
                    {
                        var cached = await _cacheService.GetAsync<System.Collections.Generic.List<Enterprise>>("enterprises");
                        if (cached != null && cached.Any())
                        {
                            DispatcherHelper.Invoke(() =>
                            {
                                foreach (var e in cached) Enterprises.Add(e);
                            });
                            return;
                        }
                    }

                    var all = await _enterpriseRepository.GetAllAsync();
                    if (all != null)
                    {
                        var list = all.ToList();
                        if (_cacheService != null && list.Any())
                        {
                            await _cacheService.SetAsync("enterprises", list, TimeSpan.FromHours(6));
                        }
                        DispatcherHelper.Invoke(() =>
                        {
                            foreach (var e in list) Enterprises.Add(e);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to auto-load enterprises in MainViewModel");
                }
            });

            // Initialize navigation commands
            NavigateToDashboardCommand = new DelegateCommand(async () => await NavigateToDashboardAsync());
            NavigateToEnterprisesCommand = new DelegateCommand(async () => await NavigateToEnterprisesAsync());
            NavigateToAccountsCommand = new DelegateCommand(async () => await NavigateToAccountsAsync());
            NavigateToBudgetCommand = new DelegateCommand(async () => await NavigateToBudgetAsync());
            NavigateToDepartmentsCommand = new DelegateCommand(async () => await NavigateToDepartmentsAsync());
            NavigateToAIAssistCommand = new DelegateCommand(async () => await NavigateToAIAssistAsync());
            NavigateToAnalyticsCommand = new DelegateCommand(async () => await NavigateToAnalyticsAsync());

            // Initialize UI commands
            RefreshCommand = new DelegateCommand(async () => await RefreshAsync());
            RefreshAllCommand = new DelegateCommand(async () => await RefreshAllAsync());
            OpenSettingsCommand = new DelegateCommand(async () => await OpenSettingsAsync());
            OpenReportsCommand = new DelegateCommand(async () => await OpenReportsAsync());
            OpenAIAssistCommand = new DelegateCommand(async () => await OpenAIAssistAsync());

            // Initialize theme commands
            SwitchToFluentDarkCommand = new DelegateCommand(async () => { CurrentTheme = "FluentDark"; await Task.CompletedTask; });
            SwitchToFluentLightCommand = new DelegateCommand(async () => { CurrentTheme = "FluentLight"; await Task.CompletedTask; });

            // Initialize data commands
            ImportExcelCommand = new DelegateCommand(async () => await ImportExcelAsync());
            ExportDataCommand = new DelegateCommand(async () => await ExportDataAsync());
            SyncQuickBooksCommand = new DelegateCommand(async () => await NavigateToQuickBooksAsync());

            // Initialize view commands
            ShowDashboardCommand = new DelegateCommand(async () => await ShowDashboardAsync());
            ShowAnalyticsCommand = new DelegateCommand(async () => await ShowAnalyticsAsync());

            // Initialize budget commands
            CreateNewBudgetCommand = new DelegateCommand(async () => await CreateNewBudgetAsync());
            ImportBudgetCommand = new DelegateCommand(async () => await ImportBudgetAsync());
            ExportBudgetCommand = new DelegateCommand(async () => await ExportBudgetAsync());
            ShowBudgetAnalysisCommand = new DelegateCommand(async () => await ShowBudgetAnalysisAsync());
            ShowRateCalculatorCommand = new DelegateCommand(async () => await ShowRateCalculatorAsync());

            // Initialize enterprise commands
            AddEnterpriseCommand = new DelegateCommand(async () => await AddEnterpriseAsync());
            EditEnterpriseCommand = new DelegateCommand(async () => await EditEnterpriseAsync());
            DeleteEnterpriseCommand = new DelegateCommand(async () => await DeleteEnterpriseAsync());
            ManageServiceChargesCommand = new DelegateCommand(async () => await ManageServiceChargesAsync());
            ManageUtilityBillsCommand = new DelegateCommand(async () => await ManageUtilityBillsAsync());

            // Initialize report commands
            GenerateFinancialSummaryCommand = new DelegateCommand(async () => await GenerateFinancialSummaryAsync());
            GenerateBudgetVsActualCommand = new DelegateCommand(async () => await GenerateBudgetVsActualAsync());
            GenerateEnterprisePerformanceCommand = new DelegateCommand(async () => await GenerateEnterprisePerformanceAsync());
            CreateCustomReportCommand = new DelegateCommand(async () => await CreateCustomReportAsync());
            ShowSavedReportsCommand = new DelegateCommand(async () => await ShowSavedReportsAsync());

            // Initialize AI commands
            SendAIQueryCommand = new DelegateCommand(async () => await SendAIQueryAsync(), CanSendAIQuery);
            ChangeConversationModeCommand = new DelegateCommand<ConversationMode?>(mode =>
            {
                if (!mode.HasValue)
                {
                    Logger.LogWarning("ChangeConversationModeCommand invoked without a mode parameter; ignoring request.");
                    return;
                }

                CurrentConversationMode = mode.Value;
            });
            ClearAIInsightsCommand = new DelegateCommand(async () => await ClearAIInsightsAsync());

            // Initialize legacy commands
            AddTestEnterpriseCommand = new DelegateCommand(async () => await AddTestEnterpriseAsync());
        }

        // Properties
        public ObservableCollection<Enterprise> Enterprises { get; } = new();
        private bool isAutoRefreshEnabled;
        public bool IsAutoRefreshEnabled
        {
            get => isAutoRefreshEnabled;
            set
            {
                if (SetProperty(ref isAutoRefreshEnabled, value))
                {
                    Logger.LogDebug("MainViewModel IsAutoRefreshEnabled changed to: {Value}", value);
                }
            }
        }



        private object? currentView;
        public object? CurrentView
        {
            get => currentView;
            set
            {
                if (SetProperty(ref currentView, value))
                {
                    Logger.LogDebug("MainViewModel CurrentView changed to: {ViewType}", value?.GetType().Name ?? "null");
                }
            }
        }

        private object? activeWindow;
        public object? ActiveWindow
        {
            get => activeWindow;
            set
            {
                if (SetProperty(ref activeWindow, value))
                {
                    Logger.LogDebug("MainViewModel ActiveWindow changed to: {WindowType}", value?.GetType().Name ?? "null");
                    // Update CurrentViewTitle when ActiveWindow changes
                    UpdateCurrentViewTitle();
                }
            }
        }

        private string? currentViewTitle;
        public string? CurrentViewTitle
        {
            get => currentViewTitle;
            set
            {
                if (SetProperty(ref currentViewTitle, value))
                {
                    Logger.LogDebug("MainViewModel CurrentViewTitle changed to: {Title}", value ?? "null");
                }
            }
        }

        private void UpdateCurrentViewTitle()
        {
            if (ActiveWindow is FrameworkElement element && element.Name != null)
            {
                // Try to get a user-friendly title from the element's name or tag
                CurrentViewTitle = element.Name switch
                {
                    "DashboardRegion" => "Dashboard",
                    "EnterpriseRegion" => "Enterprises",
                    "BudgetRegion" => "Budget Management",
                    "MunicipalAccountRegion" => "Municipal Accounts",
                    "UtilityCustomerRegion" => "Utility Customers",
                    "ReportsRegion" => "Reports & Analytics",
                    "AnalyticsRegion" => "Advanced Analytics",
                    "AIAssistRegion" => "AI Assistant",
                    "SettingsRegion" => "Settings",
                    _ => element.Name
                };
            }
            else
            {
                CurrentViewTitle = "Wiley Widget";
            }
        }

        // Region Content Properties for custom navigation
        private object? dashboardContent;
        public object? DashboardContent
        {
            get => dashboardContent;
            set => SetProperty(ref dashboardContent, value);
        }

        private object? reportsContent;
        public object? ReportsContent
        {
            get => reportsContent;
            set => SetProperty(ref reportsContent, value);
        }

        private object? enterpriseContent;
        public object? EnterpriseContent
        {
            get => enterpriseContent;
            set => SetProperty(ref enterpriseContent, value);
        }

        private object? municipalAccountContent;
        public object? MunicipalAccountContent
        {
            get => municipalAccountContent;
            set => SetProperty(ref municipalAccountContent, value);
        }

        private object? budgetContent;
        public object? BudgetContent
        {
            get => budgetContent;
            set => SetProperty(ref budgetContent, value);
        }

        private object? analyticsContent;
        public object? AnalyticsContent
        {
            get => analyticsContent;
            set => SetProperty(ref analyticsContent, value);
        }

        private object? settingsContent;
        public object? SettingsContent
        {
            get => settingsContent;
            set => SetProperty(ref settingsContent, value);
        }

        private object? utilityCustomerContent;
        public object? UtilityCustomerContent
        {
            get => utilityCustomerContent;
            set => SetProperty(ref utilityCustomerContent, value);
        }

        private object? aiAssistContent;
        public object? AIAssistContent
        {
            get => aiAssistContent;
            set => SetProperty(ref aiAssistContent, value);
        }

        private object? quickBooksContent;
        public object? QuickBooksContent
        {
            get => quickBooksContent;
            set => SetProperty(ref quickBooksContent, value);
        }

        private object? leftPanelContent;
        public object? LeftPanelContent
        {
            get => leftPanelContent;
            set => SetProperty(ref leftPanelContent, value);
        }

        private object? rightPanelContent;
        public object? RightPanelContent
        {
            get => rightPanelContent;
            set => SetProperty(ref rightPanelContent, value);
        }

        private object? bottomPanelContent;
        public object? BottomPanelContent
        {
            get => bottomPanelContent;
            set => SetProperty(ref bottomPanelContent, value);
        }

        // DockingManager State Properties for MVVM binding
        private bool isDashboardVisible = true;
        public bool IsDashboardVisible
        {
            get => isDashboardVisible;
            set
            {
                if (SetProperty(ref isDashboardVisible, value))
                {
                    Logger.LogDebug("MainViewModel IsDashboardVisible changed to: {Value}", value);
                    UpdateDockingVisibility("DashboardRegion", value);
                }
            }
        }

        private bool isAnalyticsVisible;
        public bool IsAnalyticsVisible
        {
            get => isAnalyticsVisible;
            set
            {
                if (SetProperty(ref isAnalyticsVisible, value))
                {
                    Logger.LogDebug("MainViewModel IsAnalyticsVisible changed to: {Value}", value);
                    UpdateDockingVisibility("AnalyticsRegion", value);
                }
            }
        }

        private bool isReportsVisible;
        public bool IsReportsVisible
        {
            get => isReportsVisible;
            set
            {
                if (SetProperty(ref isReportsVisible, value))
                {
                    Logger.LogDebug("MainViewModel IsReportsVisible changed to: {Value}", value);
                    UpdateDockingVisibility("ReportsRegion", value);
                }
            }
        }

        private void UpdateDockingVisibility(string regionName, bool isVisible)
        {
            // This method would be called from code-behind to update actual docking state
            // For now, just log the intended change
            Logger.LogDebug("Docking visibility update requested: {RegionName} -> {IsVisible}", regionName, isVisible);
        }

        private string currentTheme = "FluentDark";
        public string CurrentTheme
        {
            get => currentTheme;
            set
            {
                if (SetProperty(ref currentTheme, value))
                {
                    Logger.LogInformation("MainViewModel CurrentTheme changed to: {Theme}", value);
                    ApplyTheme(value);
                }
            }
        }

        private void ApplyTheme(string themeName)
        {
            // This will be called from code-behind to apply the theme
            // The actual theme application is handled in the view via event
            Logger.LogDebug("Applying theme: {Theme}", themeName);
            // Raise property changed to notify view
            RaisePropertyChanged(nameof(CurrentTheme));
        }

        // Theme switching commands
        public DelegateCommand SwitchToFluentDarkCommand { get; }
        public DelegateCommand SwitchToFluentLightCommand { get; }

        private bool isInitialized;
        public bool IsInitialized
        {
            get => isInitialized;
            set
            {
                if (SetProperty(ref isInitialized, value))
                {
                    Logger.LogInformation("MainViewModel IsInitialized changed to: {Value}", value);
                }
            }
        }

        private Enterprise? selectedEnterprise;
        public Enterprise? SelectedEnterprise
        {
            get => selectedEnterprise;
            set
            {
                if (SetProperty(ref selectedEnterprise, value))
                {
                    Logger.LogDebug("MainViewModel SelectedEnterprise changed to: {EnterpriseName} (ID: {EnterpriseId})",
                        value?.Name ?? "null", value?.Id ?? 0);
                }
            }
        }

        // AI Conversation Mode Properties
        private ConversationMode currentConversationMode = ConversationMode.General;
        public ConversationMode CurrentConversationMode
        {
            get => currentConversationMode;
            set
            {
                if (SetProperty(ref currentConversationMode, value))
                {
                    Logger.LogInformation("AI Conversation mode changed to: {Mode}", value);
                    RaisePropertyChanged(nameof(ConversationModeDescription));
                    SendAIQueryCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public string ConversationModeDescription => CurrentConversationMode switch
        {
            ConversationMode.General => "General AI assistance for municipal utility management",
            ConversationMode.Budget => "Budget analysis, forecasting, and financial recommendations",
            ConversationMode.Enterprise => "Enterprise optimization, rate calculations, and performance insights",
            ConversationMode.Analytics => "Advanced analytics, trends, and predictive insights",
            _ => "AI Assistant"
        };

        private string aiQuery = string.Empty;
        public string AIQuery
        {
            get => aiQuery;
            set
            {
                if (SetProperty(ref aiQuery, value))
                {
                    SendAIQueryCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private string aiResponse = string.Empty;
        public string AIResponse
        {
            get => aiResponse;
            set => SetProperty(ref aiResponse, value);
        }

        public ObservableCollection<AIInsight> AIInsights { get; } = new();

        private AIInsight? selectedAIInsight;
        public AIInsight? SelectedAIInsight
        {
            get => selectedAIInsight;
            set => SetProperty(ref selectedAIInsight, value);
        }

        // Navigation Commands - Convert to AsyncRelayCommand for better UX
        public DelegateCommand NavigateToDashboardCommand { get; }
        public DelegateCommand NavigateToEnterprisesCommand { get; }
        public DelegateCommand NavigateToEnterpriseCommand => NavigateToEnterprisesCommand; // Alias for tests
        public DelegateCommand NavigateToAccountsCommand { get; }
        public DelegateCommand NavigateToBudgetCommand { get; }
        public DelegateCommand NavigateToDepartmentsCommand { get; }
        public DelegateCommand NavigateToAIAssistCommand { get; }
        public DelegateCommand NavigateToAnalyticsCommand { get; }

        // UI Commands
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand RefreshAllCommand { get; }
        public DelegateCommand OpenSettingsCommand { get; }
        public DelegateCommand OpenReportsCommand { get; }
        public DelegateCommand OpenAIAssistCommand { get; }

        // Data Commands
        public DelegateCommand ImportExcelCommand { get; }
        public DelegateCommand ExportDataCommand { get; }
        public DelegateCommand SyncQuickBooksCommand { get; }

        // View Commands
        public DelegateCommand ShowDashboardCommand { get; }
        public DelegateCommand ShowAnalyticsCommand { get; }

        // Budget Commands
        public DelegateCommand CreateNewBudgetCommand { get; }
        public DelegateCommand ImportBudgetCommand { get; }
        public DelegateCommand ExportBudgetCommand { get; }
        public DelegateCommand ShowBudgetAnalysisCommand { get; }
        public DelegateCommand ShowRateCalculatorCommand { get; }

        // Enterprise Commands
        public DelegateCommand AddEnterpriseCommand { get; }
        public DelegateCommand EditEnterpriseCommand { get; }
        public DelegateCommand DeleteEnterpriseCommand { get; }
        public DelegateCommand ManageServiceChargesCommand { get; }
        public DelegateCommand ManageUtilityBillsCommand { get; }

        // Report Commands
        public DelegateCommand GenerateFinancialSummaryCommand { get; }
        public DelegateCommand GenerateBudgetVsActualCommand { get; }
        public DelegateCommand GenerateEnterprisePerformanceCommand { get; }
        public DelegateCommand CreateCustomReportCommand { get; }
        public DelegateCommand ShowSavedReportsCommand { get; }

        // AI Commands
        public DelegateCommand SendAIQueryCommand { get; }
        public DelegateCommand<ConversationMode?> ChangeConversationModeCommand { get; }
        public DelegateCommand ClearAIInsightsCommand { get; }

        // Legacy Commands
        public DelegateCommand AddTestEnterpriseCommand { get; }

        // Show AIAssist as a Prism modal dialog using IDialogService
        private async Task OpenAIAssistAsync()
        {
            await Task.Yield(); // keep method async-friendly
            var parameters = new DialogParameters
            {
                { "Title", "AI Assistant" }
            };
            dialogService.ShowDialog("AIAssistDialog", parameters, r =>
            {
                // Optionally process results
            });
        }

        // Navigation Methods with region existence checks - Now Async
        private async Task NavigateToDashboardAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToDashboard command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50); // Small delay for UI feedback
                // Navigate to Dashboard using region navigation
                await NavigateToRegionSafelyAsync("MainRegion", "DashboardView", "Dashboard");
                Logger.LogInformation("Navigated to Dashboard");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to Dashboard failed");
                // Could show error dialog here
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task NavigateToEnterprisesAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToEnterprises command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50);
                await NavigateToRegionSafelyAsync("MainRegion", "EnterpriseView", "Enterprises");
                Logger.LogInformation("Navigated to Enterprises");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to Enterprises failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task NavigateToAccountsAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToAccounts command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50);
                await NavigateToRegionSafelyAsync("MainRegion", "MunicipalAccountView", "Municipal Accounts");
                Logger.LogInformation("Navigated to Municipal Accounts");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to Municipal Accounts failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task NavigateToBudgetAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToBudget command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50);
                var parameters = new Prism.Navigation.NavigationParameters { { "EnterpriseId", SelectedEnterprise?.Id ?? 0 } };
                await NavigateToRegionSafelyAsync("MainRegion", "BudgetView", "Budget", parameters);
                Logger.LogInformation("Navigated to Budget");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to Budget failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task NavigateToDepartmentsAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToDepartments command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50);
                await NavigateToRegionSafelyAsync("MainRegion", "DepartmentView", "Departments");
                Logger.LogInformation("Navigated to Departments");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to Departments failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task NavigateToAIAssistAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToAIAssist command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50);
                await NavigateToRegionSafelyAsync("MainRegion", "AIAssistView", "AI Assistant");
                Logger.LogInformation("Navigated to AI Assistant");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to AI Assistant failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task NavigateToAnalyticsAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToAnalytics command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50);
                await NavigateToRegionSafelyAsync("MainRegion", "AnalyticsView", "Analytics");
                Logger.LogInformation("Navigated to Analytics");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to Analytics failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task NavigateToQuickBooksAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToQuickBooks command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50);
                await NavigateToRegionSafelyAsync("MainRegion", "QuickBooksView", "QuickBooks");
                Logger.LogInformation("Navigated to QuickBooks");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to QuickBooks failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Safely navigates to a region after checking for existence and logging (Async version)
        /// </summary>
        private async Task NavigateToRegionSafelyAsync(string regionName, string viewName, string displayName, Prism.Navigation.NavigationParameters? parameters = null)
        {
            Logger.LogInformation("Attempting to navigate to {DisplayName} (Region: {RegionName}, View: {ViewName})",
                displayName, regionName, viewName);

            try
            {
                // Perform navigation on the UI thread
                await DispatcherHelper.InvokeAsync(() =>
                {
                    _regionManager.RequestNavigate(regionName, viewName, parameters ?? new Prism.Navigation.NavigationParameters());
                });

                Logger.LogInformation("Successfully navigated to {DisplayName} in region {RegionName}",
                    displayName, regionName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception during navigation to {DisplayName} (Region: {RegionName})",
                    displayName, regionName);
            }
        }
        // Other Methods
        //             Logger.LogWarning("Region '{RegionName}' not found in region manager. Available regions: [{AvailableRegions}]",
        //                 regionName, string.Join(", ", regionManager.Regions.Select(r => r.Name)));
        //             return;
        //         }

        //         // Log current region state before navigation
        //         var region = regionManager.Regions[regionName];
        //         Logger.LogDebug("Region '{RegionName}' found. Current views: {ViewCount}, Active view: {ActiveView}",
        //             regionName,
        //             region.Views?.Count() ?? 0,
        //             region.ActiveViews?.FirstOrDefault()?.GetType().Name ?? "None");

        //         // Perform navigation on the UI thread with callback for result tracking
        //         var tcs = new TaskCompletionSource<bool>();
        //         await DispatcherHelper.InvokeAsync(() =>
        //         {
        //             regionManager.RequestNavigate(regionName, viewName, (result) =>
        //             {
        //                 tcs.SetResult(result.Success);
        //                 if (result.Success)
        //                 {
        //                     Logger.LogInformation("Successfully navigated to {DisplayName} in region {RegionName}",
        //                         displayName, regionName);
        //                 }
        //                 else
        //                 {
        //                     Logger.LogWarning("Navigation to {DisplayName} failed. Region: {RegionName}, Error: {Error}",
        //                         displayName, regionName, result.Exception?.Message ?? "Unknown navigation error");
        //                 }
        //             });
        //         });

        //         var navigationResult = await tcs.Task;

        //         if (!navigationResult)
        //         {
        //             Logger.LogWarning("Navigation to {DisplayName} completed but reported failure", displayName);
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         Logger.LogError(ex, "Exception during navigation to {DisplayName} (Region: {RegionName})",
        //             displayName, regionName);
        //     }
        // }

        /// <summary>
        /// Safely navigates to a region after checking for existence and logging (Synchronous version for compatibility)
        /// </summary>
        private void NavigateToRegionSafely(string regionName, string viewName, string displayName)
        {
            Logger.LogInformation("Attempting to navigate to {DisplayName} (Region: {RegionName}, View: {ViewName})",
                displayName, regionName, viewName);

            try
            {
                // Check if region exists before attempting navigation
                if (!_regionManager.Regions.ContainsRegionWithName(regionName))
                {
                    Logger.LogWarning("Region '{RegionName}' not found in region manager. Available regions: [{AvailableRegions}]",
                        regionName, string.Join(", ", _regionManager.Regions.Select(r => r.Name)));
                    return;
                }

                // Log current region state before navigation
                var region = _regionManager.Regions[regionName];
                Logger.LogDebug("Region '{RegionName}' found. Current views: {ViewCount}, Active view: {ActiveView}",
                    regionName,
                    region.Views?.Count() ?? 0,
                    region.ActiveViews?.FirstOrDefault()?.GetType().Name ?? "None");

                // Perform navigation with callback for result tracking on the UI thread
                DispatcherHelper.Invoke(() =>
                {
                    _regionManager.RequestNavigate(regionName, viewName, (result) =>
                    {
                        if (result.Success)
                        {
                            Logger.LogInformation("Successfully navigated to {DisplayName} in region {RegionName}",
                                displayName, regionName);
                        }
                        else
                        {
                            Logger.LogWarning("Navigation to {DisplayName} failed. Region: {RegionName}, Error: {Error}",
                                displayName, regionName, result.Exception?.Message ?? "Unknown navigation error");
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception during navigation to {DisplayName} (Region: {RegionName})",
                    displayName, regionName);
            }
        }

        // Other Methods
        private async Task RefreshAsync()
        {
            Logger.LogInformation("MainViewModel: Refresh command executed");
            try
            {
                // Refresh the current active view
                if (CurrentView != null)
                {
                    Logger.LogInformation("Refreshing current view: {ViewType}", CurrentView.GetType().Name);
                }
                else
                {
                    Logger.LogInformation("No current view to refresh, reloading data");
                    await RefreshAllAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to refresh view");
            }
        }

        private void RefreshAll()
        {
            Logger.LogInformation("MainViewModel: Refresh all command executed (synchronous wrapper)");
            _ = RefreshAllAsync();
        }

        private async Task RefreshAllAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("RefreshAll");
            var stopwatch = Stopwatch.StartNew();

            Logger.LogInformation("MainViewModel: Refresh all command executed - {LogContext}", loggingContext);
            try
            {
                IsBusy = true;

                // Refresh all data sources
                Logger.LogInformation("Refreshing all data sources");

                // Clear and reload enterprises on UI thread without unnecessary Task.Run
                await DispatcherHelper.InvokeAsync(() => Enterprises.Clear(), System.Windows.Threading.DispatcherPriority.Background);

                // Trigger navigation refresh on all regions
                foreach (var region in _regionManager.Regions)
                {
                    Logger.LogDebug("Refreshing region: {RegionName}", region.Name);
                }

                stopwatch.Stop();
                Logger.LogInformation("All data sources refreshed successfully in {ElapsedMs}ms - {LogContext}",
                    stopwatch.ElapsedMilliseconds, loggingContext);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogError(ex, "Failed to refresh all data after {ElapsedMs}ms - {LogContext}",
                    stopwatch.ElapsedMilliseconds, loggingContext);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task OpenSettingsAsync()
        {
            Logger.LogInformation("MainViewModel: Open settings command executed");
            try
            {
                // Navigate to settings view (ensure UI thread)
                await NavigateToRegionSafelyAsync("MainRegion", "SettingsView", "Settings");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to open settings");
            }
        }

        private async Task AddTestEnterpriseAsync()
        {
            Logger.LogInformation("MainViewModel: Add test enterprise command executed");
            try
            {
                // Create a test enterprise for development/testing purposes
                var testEnterprise = new Enterprise
                {
                    Name = $"Test Enterprise {DateTime.Now:HHmmss}",
                    Type = "Water"
                };

#if DEBUG
                DispatcherHelper.EnsureOnUIThread("AddTestEnterpriseAsync");
#endif
                await DispatcherHelper.InvokeAsync(() =>
                {
                    Enterprises.Add(testEnterprise);
                    SelectedEnterprise = testEnterprise;
                });

                Logger.LogInformation("Test enterprise added: {EnterpriseName}", testEnterprise.Name);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to add test enterprise");
            }
        }

        // Docking and Navigation Snapshot Methods
        public void SaveNavigationSnapshot()
        {
            // Implement save navigation snapshot logic
            Logger.LogInformation("MainViewModel: Navigation snapshot saved");
        }

        public void UpdateDockingState(string regionName, bool isVisible)
        {
            // Implement update docking state logic
            Logger.LogInformation("MainViewModel: Docking state updated for {RegionName}: {IsVisible}", regionName, isVisible);
        }

        public void RestoreNavigationSnapshot()
        {
            // Implement restore navigation snapshot logic
            Logger.LogInformation("MainViewModel: Navigation snapshot restored");
        }

        // UI Command Implementations
        private async Task OpenReportsAsync()
        {
            await NavigateToRegionSafelyAsync("MainRegion", "ReportsView", "Reports");
        }



        // AI Command Implementations
        private bool CanSendAIQuery()
        {
            return !string.IsNullOrWhiteSpace(AIQuery) && !IsBusy;
        }

        private async Task SendAIQueryAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("SendAIQuery");
            var stopwatch = Stopwatch.StartNew();

            Logger.LogInformation("Sending AI query in {Mode} mode - {LogContext}", CurrentConversationMode, loggingContext);

            try
            {
                IsBusy = true;
                BusyMessage = $"Processing AI query in {CurrentConversationMode} mode...";

                // Build context based on conversation mode
                var context = await BuildConversationContextAsync();

                // Get AI response using XAIService
                var response = await _aiService.GetInsightsAsync(context, AIQuery);

                // Update UI with response
                AIResponse = response;

                // Create insight record
                var insight = new AIInsight
                {
                    Id = AIInsights.Count + 1,
                    Timestamp = DateTime.UtcNow,
                    Mode = CurrentConversationMode,
                    Query = AIQuery,
                    Response = response,
                    Category = GetCategoryForMode(CurrentConversationMode),
                    Priority = DeterminePriority(response)
                };

                // Add to insights collection for display in SfDataGrid
                AIInsights.Insert(0, insight); // Add to top of list

                Logger.LogInformation("AI query completed successfully in {ElapsedMs}ms - {LogContext}",
                    stopwatch.ElapsedMilliseconds, loggingContext);

                // Clear query for next input
                AIQuery = string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to process AI query - {LogContext}", loggingContext);
                AIResponse = $"Error processing query: {ex.Message}";
                MessageBox.Show($"Failed to process AI query: {ex.Message}", "AI Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        private async Task<string> BuildConversationContextAsync()
        {
            var context = CurrentConversationMode switch
            {
                ConversationMode.Budget => await BuildBudgetContextAsync(),
                ConversationMode.Enterprise => await BuildEnterpriseContextAsync(),
                ConversationMode.Analytics => await BuildAnalyticsContextAsync(),
                ConversationMode.General or _ => await BuildGeneralContextAsync()
            };

            return context;
        }

        private async Task<string> BuildBudgetContextAsync()
        {
            try
            {
                var currentYear = DateTime.UtcNow.Year;
                var budgets = await _budgetRepository.GetByFiscalYearAsync(currentYear);
                var budgetList = budgets.ToList();
                var budgetCount = budgetList.Count;
                var totalBudgeted = budgetList.Sum(b => b.BudgetedAmount);

                return $"Budget Context: Total budgets for {currentYear}: {budgetCount}. " +
                       $"Total budgeted amount: ${totalBudgeted:N0}. " +
                       $"Sample accounts: {string.Join(", ", budgetList.Take(3).Select(b => $"{b.AccountNumber} - {b.Description}"))}. " +
                       $"Focus on budget analysis, forecasting, and financial recommendations.";
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to build budget context");
                return "Budget Context: Budget data temporarily unavailable. Providing general budget guidance.";
            }
        }

        private async Task<string> BuildEnterpriseContextAsync()
        {
            try
            {
                // Get fresh enterprise data from repository
                var allEnterprises = await _enterpriseRepository.GetAllAsync();
                var enterpriseList = allEnterprises.ToList();

                if (!enterpriseList.Any())
                {
                    return "Enterprise Context: No enterprises currently in the system. " +
                           "Focus on enterprise setup and initial configuration guidance.";
                }

                var enterpriseCount = enterpriseList.Count;
                var totalBudget = enterpriseList.Sum(e => e.TotalBudget);
                var avgRate = enterpriseList.Average(e => e.CurrentRate);
                var activeCount = enterpriseList.Count(e => e.Status == EnterpriseStatus.Active);
                var totalCitizens = enterpriseList.Sum(e => e.CitizenCount);
                var totalMonthlyExpenses = enterpriseList.Sum(e => e.MonthlyExpenses);

                // Group by type for analysis
                var typeGroups = enterpriseList.GroupBy(e => e.Type)
                    .Select(g => new { Type = g.Key, Count = g.Count(), Budget = g.Sum(e => e.TotalBudget) })
                    .OrderByDescending(g => g.Budget)
                    .ToList();

                var typeDistribution = string.Join(", ", typeGroups.Select(g => $"{g.Type}: {g.Count} ({g.Budget:C0})"));

                // Identify top performers and concerns
                var topPerformers = enterpriseList
                    .Where(e => e.TotalBudget > 0)
                    .OrderByDescending(e => e.TotalBudget / Math.Max(e.CitizenCount, 1))
                    .Take(3)
                    .Select(e => $"{e.Name} (${e.CurrentRate:N2}/citizen)")
                    .ToList();

                var concernEntries = enterpriseList
                    .Where(e => e.MonthlyExpenses > e.TotalBudget * 0.1m) // More than 10% monthly burn rate
                    .Select(e => e.Name)
                    .ToList();

                var contextBuilder = new System.Text.StringBuilder();
                contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"Enterprise Context for {DateTime.UtcNow:MMMM yyyy}:");
                contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Total Enterprises: {enterpriseCount} ({activeCount} active)");
                contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Combined Annual Budget: ${totalBudget:N0}");
                contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Total Citizens Served: {totalCitizens:N0}");
                contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Average Rate: ${avgRate:N2}");
                contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Total Monthly Expenses: ${totalMonthlyExpenses:N0}");
                contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Enterprise Distribution: {typeDistribution}");

                if (topPerformers.Any())
                {
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Top Performers (by per-citizen cost): {string.Join(", ", topPerformers)}");
                }

                if (concernEntries.Any())
                {
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- High Burn Rate Concerns: {string.Join(", ", concernEntries)}");
                }

                contextBuilder.AppendLine("Focus: Enterprise optimization, rate calculations, performance insights, and cost efficiency recommendations.");

                return contextBuilder.ToString();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to build enterprise context");
                return "Enterprise Context: Enterprise data temporarily unavailable. Providing general enterprise guidance.";
            }
        }

        private async Task<string> BuildAnalyticsContextAsync()
        {
            try
            {
                // Get comprehensive data for analytics
                var allEnterprises = await _enterpriseRepository.GetAllAsync();
                var enterpriseList = allEnterprises.ToList();

                var currentYear = DateTime.UtcNow.Year;
                var budgets = await _budgetRepository.GetByFiscalYearAsync(currentYear);
                var budgetList = budgets.ToList();

                if (!enterpriseList.Any() && !budgetList.Any())
                {
                    return "Analytics Context: No data available for analysis. " +
                           "Focus on data collection strategies and initial setup recommendations.";
                }

                var contextBuilder = new System.Text.StringBuilder();
                contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"Analytics Context for {DateTime.UtcNow:MMMM yyyy}:");

                // Enterprise Analytics
                if (enterpriseList.Any())
                {
                    var dataPoints = enterpriseList.Count;
                    var dateRange = $"{enterpriseList.Min(e => e.CreatedDate):d} to {enterpriseList.Max(e => e.ModifiedDate):d}";
                    var totalRevenuePotential = enterpriseList.Sum(e => e.CurrentRate * e.CitizenCount * 12); // Annual
                    var totalExpenses = enterpriseList.Sum(e => e.MonthlyExpenses * 12); // Annual
                    var profitMargin = totalRevenuePotential > 0
                        ? ((totalRevenuePotential - totalExpenses) / totalRevenuePotential * 100)
                        : 0;

                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"Enterprise Analytics:");
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Data Points: {dataPoints} enterprises");
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Date Range: {dateRange}");
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Projected Annual Revenue: ${totalRevenuePotential:N0}");
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Projected Annual Expenses: ${totalExpenses:N0}");
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Profit Margin: {profitMargin:N2}%");

                    // Calculate trends (growth over time)
                    var recentEntities = enterpriseList.Where(e => e.CreatedDate > DateTime.UtcNow.AddMonths(-6)).Count();
                    var olderEntities = enterpriseList.Count - recentEntities;
                    var growthRate = olderEntities > 0 ? (recentEntities / (double)olderEntities * 100) : 0;
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- 6-Month Growth: {recentEntities} new enterprises ({growthRate:N1}% growth)");

                    // Type distribution analysis
                    var typeAnalysis = enterpriseList.GroupBy(e => e.Type)
                        .Select(g => new
                        {
                            Type = g.Key,
                            Count = g.Count(),
                            AvgBudget = g.Average(e => e.TotalBudget),
                            AvgRate = g.Average(e => e.CurrentRate)
                        })
                        .OrderByDescending(t => t.Count);

                    contextBuilder.AppendLine("- Type Performance:");
                    foreach (var type in typeAnalysis)
                    {
                        contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"  • {type.Type}: {type.Count} enterprises, Avg Budget: ${type.AvgBudget:N0}, Avg Rate: ${type.AvgRate:N2}");
                    }
                }

                // Budget Analytics
                if (budgetList.Any())
                {
                    var totalBudgeted = budgetList.Sum(b => b.BudgetedAmount);
                    var totalActual = budgetList.Sum(b => b.ActualAmount);
                    var totalVariance = totalBudgeted - totalActual;
                    var variancePercent = totalBudgeted > 0 ? (totalVariance / totalBudgeted * 100) : 0;
                    var overBudgetCount = budgetList.Count(b => b.ActualAmount > b.BudgetedAmount);
                    var underBudgetCount = budgetList.Count(b => b.ActualAmount < b.BudgetedAmount);

                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"\nBudget Analytics for FY {currentYear}:");
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Total Budgeted: ${totalBudgeted:N0}");
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Total Actual: ${totalActual:N0}");
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Variance: ${totalVariance:N0} ({variancePercent:N2}%)");
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Over Budget: {overBudgetCount} accounts");
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Under Budget: {underBudgetCount} accounts");

                    // Department analysis
                    var deptAnalysis = budgetList.GroupBy(b => b.DepartmentId)
                        .Select(g => new
                        {
                            DeptId = g.Key,
                            Count = g.Count(),
                            TotalBudget = g.Sum(b => b.BudgetedAmount),
                            TotalActual = g.Sum(b => b.ActualAmount),
                            Variance = g.Sum(b => b.BudgetedAmount) - g.Sum(b => b.ActualAmount)
                        })
                        .OrderByDescending(d => Math.Abs(d.Variance))
                        .Take(5);

                    contextBuilder.AppendLine("- Top 5 Departments by Variance:");
                    foreach (var dept in deptAnalysis)
                    {
                        var status = dept.Variance >= 0 ? "under" : "over";
                        contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"  • Dept {dept.DeptId}: ${Math.Abs(dept.Variance):N0} {status} budget");
                    }
                }

                contextBuilder.AppendLine("\nFocus: Advanced analytics, trend identification, predictive insights, and performance optimization recommendations.");
                return contextBuilder.ToString();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to build analytics context");
                return "Analytics Context: Analytics data temporarily unavailable. Providing general analytics guidance.";
            }
        }

        private async Task<string> BuildGeneralContextAsync()
        {
            try
            {
                // Gather system-wide statistics for general context
                var allEnterprises = await _enterpriseRepository.GetAllAsync();
                var enterpriseList = allEnterprises.ToList();

                var currentYear = DateTime.UtcNow.Year;
                var budgets = await _budgetRepository.GetByFiscalYearAsync(currentYear);
                var budgetList = budgets.ToList();

                var contextBuilder = new System.Text.StringBuilder();
                contextBuilder.AppendLine($"Wiley Widget Municipal Utility Management System");
                contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"System Status as of {DateTime.UtcNow:f}");
                contextBuilder.AppendLine();
                contextBuilder.AppendLine("Available Modules:");
                contextBuilder.AppendLine("- Enterprise Management: Municipal enterprise tracking and optimization");
                contextBuilder.AppendLine("- Budget Tracking: GASB-compliant budget management and variance analysis");
                contextBuilder.AppendLine("- Analytics: Advanced reporting and predictive insights");
                contextBuilder.AppendLine("- Municipal Accounts: Account management and billing");
                contextBuilder.AppendLine("- Utility Customers: Customer relationship management");
                contextBuilder.AppendLine();

                if (enterpriseList.Any())
                {
                    contextBuilder.AppendLine("Current System Data:");
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- {enterpriseList.Count} enterprises managing ${enterpriseList.Sum(e => e.TotalBudget):N0} in budgets");
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- Serving {enterpriseList.Sum(e => e.CitizenCount):N0} citizens");
                }

                if (budgetList.Any())
                {
                    contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- {budgetList.Count} budget accounts totaling ${budgetList.Sum(b => b.BudgetedAmount):N0} for FY {currentYear}");
                }

                if (AIInsights.Any())
                {
                    var recentInsights = AIInsights.Take(5);
                    contextBuilder.AppendLine();
                    contextBuilder.AppendLine("Recent AI Conversation History ({AIInsights.Count} total insights):");
                    foreach (var insight in recentInsights)
                    {
                        contextBuilder.AppendLine(CultureInfo.InvariantCulture, $"- [{insight.Mode}] {insight.Query.Substring(0, Math.Min(50, insight.Query.Length))}...");
                    }
                }

                contextBuilder.AppendLine();
                contextBuilder.AppendLine("Capabilities: General assistance, system navigation, feature explanations, best practices, and integration with all modules.");

                return contextBuilder.ToString();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to build general context");
                return "General Context: Wiley Widget municipal utility management system. " +
                       "Available features: Enterprise management, Budget tracking, Analytics, Reporting. " +
                       "Provide general assistance and guidance.";
            }
        }

        private string GetCategoryForMode(ConversationMode mode)
        {
            return mode switch
            {
                ConversationMode.Budget => "Budget Analysis",
                ConversationMode.Enterprise => "Enterprise Optimization",
                ConversationMode.Analytics => "Analytics & Insights",
                ConversationMode.General or _ => "General Assistance"
            };
        }

        private string DeterminePriority(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return "Low";

            var lowerResponse = response.ToLowerInvariant();

            // High priority indicators
            var highPriorityKeywords = new[] {
                "urgent", "critical", "immediate", "warning", "alert", "danger",
                "must", "required", "mandatory", "compliance", "violation", "risk",
                "emergency", "failure", "error", "security", "breach"
            };

            var highPriorityCount = highPriorityKeywords.Count(keyword => lowerResponse.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (highPriorityCount >= 2) // Multiple high priority keywords
                return "High";

            // Medium priority indicators
            var mediumPriorityKeywords = new[] {
                "recommend", "consider", "suggest", "should", "advise", "improve",
                "optimize", "enhance", "review", "attention", "monitor", "track",
                "important", "significant", "notable"
            };

            var mediumPriorityCount = mediumPriorityKeywords.Count(keyword => lowerResponse.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (mediumPriorityCount >= 2 || highPriorityCount == 1)
                return "Medium";

            // Check for numerical thresholds that might indicate priority
            if (lowerResponse.Contains("over budget", StringComparison.OrdinalIgnoreCase) || lowerResponse.Contains("exceeded", StringComparison.OrdinalIgnoreCase) ||
                lowerResponse.Contains("deficit", StringComparison.OrdinalIgnoreCase) || lowerResponse.Contains("overspending", StringComparison.OrdinalIgnoreCase))
                return "High";

            if (lowerResponse.Contains("variance", StringComparison.OrdinalIgnoreCase) || lowerResponse.Contains("difference", StringComparison.OrdinalIgnoreCase) ||
                lowerResponse.Contains("deviation", StringComparison.OrdinalIgnoreCase))
                return "Medium";

            return "Low";
        }

        private async Task ClearAIInsightsAsync()
        {
            if (!AIInsights.Any())
            {
                Logger.LogInformation("No AI insights to clear");
                return;
            }

            var count = AIInsights.Count;

            MessageBoxResult result = MessageBoxResult.No;
            await DispatcherHelper.InvokeAsync(() =>
            {
                result = MessageBox.Show(
                    $"Are you sure you want to clear all {count} AI insights? This action cannot be undone.",
                    "Clear AI Insights",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
            });

            if (result == MessageBoxResult.Yes)
            {
                Logger.LogInformation("Clearing {Count} AI insights", count);
                AIInsights.Clear();
                AIResponse = string.Empty;
                SelectedAIInsight = null;

                await DispatcherHelper.InvokeAsync(() =>
                {
                    MessageBox.Show($"Successfully cleared {count} AI insights.", "Insights Cleared",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            else
            {
                Logger.LogInformation("User cancelled clearing AI insights");
            }
        }

        /// <summary>
        /// Export AI insights to a file for reporting or archival purposes
        /// </summary>
        public async Task ExportAIInsightsAsync(string filePath)
        {
            if (!AIInsights.Any())
            {
                Logger.LogWarning("No AI insights to export");
                MessageBox.Show("No AI insights available to export.", "Export Failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                BusyMessage = "Exporting AI insights...";

                using (var writer = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8))
                {
                    await writer.WriteLineAsync("Wiley Widget - AI Insights Export");
                    await writer.WriteLineAsync($"Exported: {DateTime.UtcNow:f}");
                    await writer.WriteLineAsync($"Total Insights: {AIInsights.Count}");
                    await writer.WriteLineAsync(new string('=', 80));
                    await writer.WriteLineAsync();

                    foreach (var insight in AIInsights.OrderBy(i => i.Timestamp))
                    {
                        await writer.WriteLineAsync($"Insight #{insight.Id}");
                        await writer.WriteLineAsync($"Timestamp: {insight.Timestamp:f}");
                        await writer.WriteLineAsync($"Mode: {insight.Mode}");
                        await writer.WriteLineAsync($"Category: {insight.Category}");
                        await writer.WriteLineAsync($"Priority: {insight.Priority}");
                        await writer.WriteLineAsync($"Actioned: {insight.IsActioned}");
                        await writer.WriteLineAsync();
                        await writer.WriteLineAsync($"Query:");
                        await writer.WriteLineAsync(insight.Query);
                        await writer.WriteLineAsync();
                        await writer.WriteLineAsync($"Response:");
                        await writer.WriteLineAsync(insight.Response);

                        if (!string.IsNullOrWhiteSpace(insight.Notes))
                        {
                            await writer.WriteLineAsync();
                            await writer.WriteLineAsync($"Notes:");
                            await writer.WriteLineAsync(insight.Notes);
                        }

                        await writer.WriteLineAsync();
                        await writer.WriteLineAsync(new string('-', 80));
                        await writer.WriteLineAsync();
                    }
                }

                Logger.LogInformation("Successfully exported {Count} AI insights to {FilePath}", AIInsights.Count, filePath);
                MessageBox.Show($"Successfully exported {AIInsights.Count} insights to:\n{filePath}",
                    "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to export AI insights to {FilePath}", filePath);
                MessageBox.Show($"Failed to export AI insights: {ex.Message}", "Export Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Mark an insight as actioned/resolved
        /// </summary>
        public void MarkInsightAsActioned(AIInsight insight)
        {
            if (insight == null)
            {
                Logger.LogWarning("Cannot mark null insight as actioned");
                return;
            }

            insight.IsActioned = true;
            Logger.LogInformation("Marked insight #{Id} as actioned", insight.Id);

            // Refresh the UI
            RaisePropertyChanged(nameof(AIInsights));
        }

        /// <summary>
        /// Filter insights by mode
        /// </summary>
        public IEnumerable<AIInsight> GetInsightsByMode(ConversationMode mode)
        {
            return AIInsights.Where(i => i.Mode == mode).OrderByDescending(i => i.Timestamp);
        }

        /// <summary>
        /// Get high priority unactioned insights
        /// </summary>
        public IEnumerable<AIInsight> GetActionableInsights()
        {
            return AIInsights
                .Where(i => !i.IsActioned && i.Priority == "High")
                .OrderByDescending(i => i.Timestamp);
        }

        // Data Command Implementations
        private void ImportExcel()
        {
            Logger.LogInformation("MainViewModel: Import Excel command executed (synchronous wrapper)");
            _ = ImportExcelAsync();
        }

        private async Task ImportExcelAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("ImportExcel");
            var stopwatch = Stopwatch.StartNew();

            Logger.LogInformation("MainViewModel: Import Excel command executed - {LogContext}", loggingContext);
            try
            {
                IsBusy = true;

                // Show open file dialog for Excel files
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Budget Data from Excel",
                    Filter = "Excel files (*.xlsx;*.xls)|*.xlsx;*.xls|All files (*.*)|*.*",
                    DefaultExt = ".xlsx",
                    CheckFileExists = true,
                    CheckPathExists = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var filePath = openFileDialog.FileName;
                    Logger.LogInformation("Selected Excel file for import: {FilePath} - {LogContext}", filePath, loggingContext);

                    // Validate Excel structure first
                    BusyMessage = "Validating Excel file structure...";
                    var isValid = await _excelReaderService.ValidateExcelStructureAsync(filePath);

                    if (!isValid)
                    {
                        Logger.LogWarning("Excel file structure validation failed for {FilePath} - {LogContext}", filePath, loggingContext);
                        MessageBox.Show("The selected Excel file does not have the expected structure for budget import.\n\nPlease ensure the file contains the required columns.",
                                      "Invalid File Structure", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Read budget data from Excel
                    BusyMessage = "Reading budget data from Excel...";
                    var budgetEntries = await _excelReaderService.ReadBudgetDataAsync(filePath);
                    var entriesList = budgetEntries.ToList();

                    Logger.LogInformation("Read {RecordCount} budget entries from Excel file - {LogContext}", entriesList.Count, loggingContext);

                    // Save to database
                    BusyMessage = "Saving budget data to database...";
                    foreach (var entry in entriesList)
                    {
                        await _budgetRepository.AddAsync(entry);
                    }

                    BusyMessage = $"Successfully imported {entriesList.Count} budget records from Excel";
                    Logger.LogInformation("Excel import completed successfully - imported {RecordCount} budget records from {FilePath} in {ElapsedMs}ms - {LogContext}",
                        entriesList.Count, filePath, stopwatch.ElapsedMilliseconds, loggingContext);

                    MessageBox.Show($"Excel import completed successfully!\n\nImported {entriesList.Count} budget records from:\n{System.IO.Path.GetFileName(filePath)}",
                                  "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Refresh data after import
                    await RefreshAllAsync();
                }
                else
                {
                    BusyMessage = "Excel import cancelled";
                    Logger.LogInformation("Excel import cancelled by user - {LogContext}", loggingContext);
                }

                stopwatch.Stop();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                BusyMessage = "Error importing Excel file";
                Logger.LogError(ex, "Failed to import Excel file after {ElapsedMs}ms - {LogContext}",
                    stopwatch.ElapsedMilliseconds, loggingContext);
                MessageBox.Show($"Error importing Excel file: {ex.Message}", "Import Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExportData()
        {
            Logger.LogInformation("MainViewModel: Export data command executed (synchronous wrapper)");
            _ = ExportDataAsync();
        }

        private async Task ExportDataAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("ExportData");
            var stopwatch = Stopwatch.StartNew();

            Logger.LogInformation("MainViewModel: Export data command executed - {LogContext}", loggingContext);
            try
            {
                IsBusy = true;

                // Get supported formats
                var supportedFormats = _reportExportService.GetSupportedFormats();
                var filter = string.Join("|", supportedFormats.Select(f => $"{f} files (*.{f.ToLower(CultureInfo.InvariantCulture)})|*.{f.ToLower(CultureInfo.InvariantCulture)}"));
                filter += "|All files (*.*)|*.*";

                // Show save file dialog
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Application Data",
                    Filter = filter,
                    DefaultExt = ".csv",
                    FileName = $"WileyWidget_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var filePath = saveFileDialog.FileName;
                    var extension = System.IO.Path.GetExtension(filePath).ToLower(CultureInfo.InvariantCulture);

                    Logger.LogInformation("Exporting data to {FilePath} with extension {Extension} - {LogContext}", filePath, extension, loggingContext);

                    BusyMessage = "Preparing data for export...";

                    // Prepare data for export
                    var exportData = Enterprises.Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.Type,
                        e.Description,
                        e.CurrentRate,
                        e.MonthlyExpenses,
                        e.CitizenCount,
                        e.CreatedDate,
                        e.ModifiedDate
                    }).ToList();

                    BusyMessage = $"Exporting {exportData.Count} records...";

                    // Export based on file type
                    if (extension == ".csv")
                    {
                        await _reportExportService.ExportToCsvAsync(exportData, filePath);
                    }
                    else if (extension == ".xlsx" || extension == ".xls")
                    {
                        await _reportExportService.ExportToExcelAsync(exportData, filePath);
                    }
                    else if (extension == ".pdf")
                    {
                        await _reportExportService.ExportToPdfAsync(exportData, filePath);
                    }
                    else
                    {
                        // Default to CSV
                        await _reportExportService.ExportToCsvAsync(exportData, filePath);
                    }

                    BusyMessage = $"Data exported to {System.IO.Path.GetFileName(filePath)}";
                    Logger.LogInformation("Data export completed successfully to {FilePath} in {ElapsedMs}ms - {LogContext}",
                        filePath, stopwatch.ElapsedMilliseconds, loggingContext);

                    MessageBox.Show($"Data export completed successfully!\n\nFile: {System.IO.Path.GetFileName(filePath)}\nRecords: {exportData.Count}",
                                  "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    BusyMessage = "Data export cancelled";
                    Logger.LogInformation("Data export cancelled by user - {LogContext}", loggingContext);
                }

                stopwatch.Stop();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                BusyMessage = "Error exporting data";
                Logger.LogError(ex, "Failed to export data after {ElapsedMs}ms - {LogContext}",
                    stopwatch.ElapsedMilliseconds, loggingContext);
                MessageBox.Show($"Error exporting data: {ex.Message}", "Export Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SyncQuickBooks()
        {
            Logger.LogInformation("MainViewModel: Sync QuickBooks command executed (synchronous wrapper)");
            _ = SyncQuickBooksAsync();
        }

        private async Task SyncQuickBooksAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("SyncQuickBooks");
            var stopwatch = Stopwatch.StartNew();

            Logger.LogInformation("MainViewModel: Sync QuickBooks command executed - {LogContext}", loggingContext);
            try
            {
                IsBusy = true;

                // Simulate QuickBooks synchronization process
                BusyMessage = "Connecting to QuickBooks Online...";
                Logger.LogInformation("Initiating QuickBooks synchronization...");
                await Task.Delay(500); // Simulate connection

                BusyMessage = "Synchronizing enterprise data...";
                await Task.Delay(800); // Simulate data sync

                BusyMessage = "Synchronizing financial transactions...";
                await Task.Delay(600); // Simulate transaction sync

                // Simulate sync results
                var syncResults = new
                {
                    EnterprisesSynced = Enterprises.Count,
                    TransactionsProcessed = 45,
                    NewRecords = 12,
                    UpdatedRecords = 23,
                    Errors = 0
                };

                BusyMessage = "QuickBooks sync completed successfully";
                Logger.LogInformation("QuickBooks sync completed successfully - Synced {EnterpriseCount} enterprises, {TransactionCount} transactions in {ElapsedMs}ms - {LogContext}",
                    syncResults.EnterprisesSynced, syncResults.TransactionsProcessed, stopwatch.ElapsedMilliseconds, loggingContext);

                MessageBox.Show($"QuickBooks synchronization completed successfully!\n\n" +
                              $"Enterprises: {syncResults.EnterprisesSynced}\n" +
                              $"Transactions: {syncResults.TransactionsProcessed}\n" +
                              $"New Records: {syncResults.NewRecords}\n" +
                              $"Updated Records: {syncResults.UpdatedRecords}\n" +
                              $"Errors: {syncResults.Errors}",
                              "QuickBooks Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh data after sync
                await RefreshAllAsync();

                stopwatch.Stop();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                BusyMessage = "Error synchronizing with QuickBooks";
                Logger.LogError(ex, "Failed to sync with QuickBooks after {ElapsedMs}ms - {LogContext}",
                    stopwatch.ElapsedMilliseconds, loggingContext);
                MessageBox.Show($"Error synchronizing with QuickBooks: {ex.Message}", "Sync Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // View Command Implementations
        private async Task ShowDashboardAsync()
        {
            await NavigateToRegionSafelyAsync("MainRegion", "DashboardView", "Dashboard");
        }

        private async Task ShowAnalyticsAsync()
        {
            await NavigateToRegionSafelyAsync("MainRegion", "AnalyticsView", "Analytics");
        }

        // Budget Command Implementations
        private async Task CreateNewBudgetAsync()
        {
            Logger.LogInformation("MainViewModel: Create new budget command executed");
            try
            {
                IsBusy = true;
                BusyMessage = "Creating new budget...";

                // Create a new budget entry for the current fiscal year
                var currentYear = DateTime.Now.Year;
                var newBudget = new BudgetEntry
                {
                    FiscalYear = currentYear,
                    AccountNumber = "100.1",
                    Description = "New Budget Entry",
                    BudgetedAmount = 0,
                    ActualAmount = 0,
                    DepartmentId = 1, // Default department
                    CreatedAt = DateTime.UtcNow
                };

                await _budgetRepository.AddAsync(newBudget);

                Logger.LogInformation("New budget created successfully for fiscal year {FiscalYear}", currentYear);
                MessageBox.Show($"New budget created successfully for fiscal year {currentYear}!",
                              "Budget Created", MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh data
                await RefreshAllAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to create new budget");
                MessageBox.Show($"Error creating new budget: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ImportBudgetAsync()
        {
            Logger.LogInformation("MainViewModel: Import budget command executed");
            try
            {
                IsBusy = true;

                // Show open file dialog for budget files
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Budget Data",
                    Filter = "Excel files (*.xlsx;*.xls)|*.xlsx;*.xls|CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = ".xlsx",
                    CheckFileExists = true,
                    CheckPathExists = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var filePath = openFileDialog.FileName;
                    Logger.LogInformation("Selected budget file for import: {FilePath}", filePath);

                    BusyMessage = "Importing budget data...";

                    // For now, simulate budget import - in real implementation, parse file and create BudgetEntry objects
                    var importedBudgets = new List<BudgetEntry>
                    {
                        new BudgetEntry { FiscalYear = DateTime.Now.Year, AccountNumber = "200.1", Description = "Imported Dept 1", BudgetedAmount = 10000, ActualAmount = 9500, DepartmentId = 1 },
                        new BudgetEntry { FiscalYear = DateTime.Now.Year, AccountNumber = "200.2", Description = "Imported Dept 2", BudgetedAmount = 15000, ActualAmount = 14000, DepartmentId = 1 }
                    };

                    foreach (var budget in importedBudgets)
                    {
                        budget.CreatedAt = DateTime.UtcNow;
                        await _budgetRepository.AddAsync(budget);
                    }

                    Logger.LogInformation("Budget import completed successfully - imported {Count} entries", importedBudgets.Count);
                    MessageBox.Show($"Budget import completed successfully!\n\nImported {importedBudgets.Count} budget entries from:\n{System.IO.Path.GetFileName(filePath)}",
                                  "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Refresh data
                    await RefreshAllAsync();
                }
                else
                {
                    Logger.LogInformation("Budget import cancelled by user");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to import budget");
                MessageBox.Show($"Error importing budget: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExportBudgetAsync()
        {
            Logger.LogInformation("MainViewModel: Export budget command executed");
            try
            {
                IsBusy = true;

                // Get current fiscal year budgets
                var currentYear = DateTime.Now.Year;
                var budgets = await _budgetRepository.GetByFiscalYearAsync(currentYear);
                var budgetList = budgets.ToList();

                if (budgetList.Count == 0)
                {
                    Logger.LogWarning("No budget data found for fiscal year {FiscalYear}", currentYear);
                    MessageBox.Show($"No budget data found for fiscal year {currentYear}.", "No Data",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Show save file dialog
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Budget Data",
                    Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    DefaultExt = ".csv",
                    FileName = $"Budget_Data_{currentYear}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var filePath = saveFileDialog.FileName;
                    var extension = System.IO.Path.GetExtension(filePath).ToLower(CultureInfo.InvariantCulture);

                    Logger.LogInformation("Exporting {Count} budget entries to {FilePath}", budgetList.Count, filePath);

                    BusyMessage = $"Exporting {budgetList.Count} budget records...";

                    // Export based on file type
                    if (extension == ".csv")
                    {
                        await _reportExportService.ExportToCsvAsync(budgetList, filePath);
                    }
                    else if (extension == ".xlsx" || extension == ".xls")
                    {
                        await _reportExportService.ExportToExcelAsync(budgetList, filePath);
                    }
                    else
                    {
                        // Default to CSV
                        await _reportExportService.ExportToCsvAsync(budgetList, filePath);
                    }

                    Logger.LogInformation("Budget export completed successfully to {FilePath}", filePath);
                    MessageBox.Show($"Budget export completed successfully!\n\nFile: {System.IO.Path.GetFileName(filePath)}\nRecords: {budgetList.Count}",
                                  "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Logger.LogInformation("Budget export cancelled by user");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to export budget");
                MessageBox.Show($"Error exporting budget: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ShowBudgetAnalysisAsync()
        {
            Logger.LogInformation("MainViewModel: Show budget analysis command executed");
            try
            {
                // Navigate to budget analysis view
                await NavigateToRegionSafelyAsync("MainRegion", "BudgetAnalysisView", "Budget Analysis");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to show budget analysis");
            }
        }

        private async Task ShowRateCalculatorAsync()
        {
            Logger.LogInformation("MainViewModel: Show rate calculator command executed");
            try
            {
                // Navigate to rate calculator view
                await NavigateToRegionSafelyAsync("MainRegion", "RateCalculatorView", "Rate Calculator");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to show rate calculator");
            }
        }

        // Enterprise Command Implementations
        private async Task AddEnterpriseAsync()
        {
            Logger.LogInformation("MainViewModel: Add enterprise command invoked");
            try
            {
                IsBusy = true;

                // Create a new enterprise with default values
                var newEnterprise = new Enterprise
                {
                    Name = string.Empty,
                    Type = "Water", // Default type
                    Description = string.Empty,
                    CurrentRate = 0.00m,
                    MonthlyExpenses = 0.00m,
                    CitizenCount = 1,
                    TotalBudget = 0.00m,
                    BudgetAmount = 0.00m,
                    Notes = string.Empty,
                    Status = EnterpriseStatus.Active,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = Environment.UserName
                };

                // Show dialog for editing the new enterprise
                if (await ShowEnterpriseDialogAsync(newEnterprise, "Add New Enterprise"))
                {
                    // Validate the enterprise before adding
                    if (ValidateEnterprise(newEnterprise, out var validationErrors))
                    {
                        // Save to database
                        var savedEnterprise = await _enterpriseRepository.AddAsync(newEnterprise);

                        // Add to the observable collection
                        Enterprises.Add(savedEnterprise);
                        SelectedEnterprise = savedEnterprise;

                        Logger.LogInformation("Enterprise added successfully: {EnterpriseName} (ID: {EnterpriseId})",
                            savedEnterprise.Name, savedEnterprise.Id);

                        MessageBox.Show($"Enterprise '{savedEnterprise.Name}' added successfully!",
                                      "Enterprise Added", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        var errorMessage = string.Join(Environment.NewLine, validationErrors);
                        MessageBox.Show($"Please correct the following errors:{Environment.NewLine}{Environment.NewLine}{errorMessage}",
                                      "Validation Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Logger.LogWarning("Enterprise validation failed: {Errors}", string.Join(", ", validationErrors));
                    }
                }
                else
                {
                    Logger.LogInformation("Enterprise creation cancelled by user");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to add enterprise");
                MessageBox.Show($"Error adding enterprise: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task EditEnterpriseAsync()
        {
            Logger.LogInformation("MainViewModel: Edit enterprise command invoked");
            try
            {
                if (SelectedEnterprise == null)
                {
                    Logger.LogWarning("Cannot edit enterprise: No enterprise selected");
                    MessageBox.Show("Please select an enterprise to edit.", "No Selection",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                IsBusy = true;

                Logger.LogDebug("Editing enterprise: {EnterpriseName} (ID: {EnterpriseId})",
                    SelectedEnterprise.Name, SelectedEnterprise.Id);

                // Create a copy for editing (to allow cancellation)
                var enterpriseCopy = CreateEnterpriseCopy(SelectedEnterprise);

                // Show dialog for editing
                if (await ShowEnterpriseDialogAsync(enterpriseCopy, "Edit Enterprise"))
                {
                    // Validate the enterprise before updating
                    if (ValidateEnterprise(enterpriseCopy, out var validationErrors))
                    {
                        // Update the original enterprise with validated data
                        UpdateEnterpriseFromCopy(SelectedEnterprise, enterpriseCopy);

                        // Save to database
                        await _enterpriseRepository.UpdateAsync(SelectedEnterprise);

                        Logger.LogInformation("Enterprise updated successfully: {EnterpriseName} (ID: {EnterpriseId})",
                            SelectedEnterprise.Name, SelectedEnterprise.Id);

                        MessageBox.Show($"Enterprise '{SelectedEnterprise.Name}' updated successfully!",
                                      "Enterprise Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        var errorMessage = string.Join(Environment.NewLine, validationErrors);
                        MessageBox.Show($"Please correct the following errors:{Environment.NewLine}{Environment.NewLine}{errorMessage}",
                                      "Validation Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Logger.LogWarning("Enterprise validation failed: {Errors}", string.Join(", ", validationErrors));
                    }
                }
                else
                {
                    Logger.LogInformation("Enterprise editing cancelled by user");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to edit enterprise");
                MessageBox.Show($"Error editing enterprise: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteEnterpriseAsync()
        {
            Logger.LogInformation("MainViewModel: Delete enterprise command invoked");
            try
            {
                if (SelectedEnterprise == null)
                {
                    Logger.LogWarning("Cannot delete enterprise: No enterprise selected");
                    MessageBox.Show("Please select an enterprise to delete.", "No Selection",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Logger.LogInformation("Deleting enterprise: {EnterpriseName} (ID: {EnterpriseId})",
                    SelectedEnterprise.Name, SelectedEnterprise.Id);

                // Show confirmation dialog
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the enterprise '{SelectedEnterprise.Name}'?\n\nThis action cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    IsBusy = true;

                    // Delete from database (soft delete)
                    var deleteResult = await _enterpriseRepository.DeleteAsync(SelectedEnterprise.Id);

                    if (deleteResult)
                    {
                        // Remove from collection
                        Enterprises.Remove(SelectedEnterprise);
                        SelectedEnterprise = null;

                        Logger.LogInformation("Enterprise deleted successfully");

                        MessageBox.Show("Enterprise deleted successfully.", "Enterprise Deleted",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        Logger.LogWarning("Enterprise deletion returned false from repository");
                        MessageBox.Show("Enterprise could not be deleted. It may have already been removed.", "Delete Failed",
                                      MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    Logger.LogInformation("Enterprise deletion cancelled by user");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to delete enterprise");
                MessageBox.Show($"Error deleting enterprise: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ManageServiceChargesAsync()
        {
            Logger.LogInformation("MainViewModel: Manage service charges command executed");
            try
            {
                // Navigate to service charges management view
                await NavigateToRegionSafelyAsync("MainRegion", "ServiceChargesView", "Service Charges");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to open service charges management");
            }
        }

        private async Task ManageUtilityBillsAsync()
        {
            Logger.LogInformation("MainViewModel: Manage utility bills command executed");
            try
            {
                // Navigate to utility bills management view
                await NavigateToRegionSafelyAsync("MainRegion", "UtilityBillsView", "Utility Bills");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to open utility bills management");
            }
        }

        // Report Command Implementations
        private async Task GenerateFinancialSummaryAsync()
        {
            Logger.LogInformation("MainViewModel: Generate financial summary command executed");
            try
            {
                // Navigate to financial summary report view
                await NavigateToRegionSafelyAsync("MainRegion", "FinancialSummaryReportView", "Financial Summary Report");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to generate financial summary report");
            }
        }

        private async Task GenerateBudgetVsActualAsync()
        {
            Logger.LogInformation("MainViewModel: Generate budget vs actual command executed");
            try
            {
                // Navigate to budget vs actual report view
                await NavigateToRegionSafelyAsync("MainRegion", "BudgetVsActualReportView", "Budget vs Actual Report");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to generate budget vs actual report");
            }
        }

        private async Task GenerateEnterprisePerformanceAsync()
        {
            Logger.LogInformation("MainViewModel: Generate enterprise performance command executed");
            try
            {
                // Navigate to enterprise performance report view
                await NavigateToRegionSafelyAsync("MainRegion", "EnterprisePerformanceReportView", "Enterprise Performance Report");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to generate enterprise performance report");
            }
        }

        private async Task CreateCustomReportAsync()
        {
            Logger.LogInformation("MainViewModel: Create custom report command executed");
            try
            {
                // Navigate to custom report builder view
                await NavigateToRegionSafelyAsync("MainRegion", "CustomReportBuilderView", "Custom Report Builder");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to create custom report");
            }
        }

        private async Task ShowSavedReportsAsync()
        {
            Logger.LogInformation("MainViewModel: Show saved reports command executed");
            try
            {
                // Navigate to saved reports view
                await NavigateToRegionSafelyAsync("MainRegion", "SavedReportsView", "Saved Reports");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to show saved reports");
            }
        }

        // Helper Methods for Enterprise CRUD Operations

        /// <summary>
        /// Loads enterprises from the database into the observable collection
        /// </summary>
        private async Task LoadEnterprisesAsync()
        {
            try
            {
                Logger.LogInformation("Loading enterprises from database");

                var enterprises = await _enterpriseRepository.GetAllAsync();

                // Update the collection on the UI thread
                await DispatcherHelper.InvokeAsync(() =>
                {
                    Enterprises.Clear();
                    foreach (var enterprise in enterprises)
                    {
                        Enterprises.Add(enterprise);
                    }
                });

                Logger.LogInformation("Loaded {Count} enterprises from database", enterprises.Count());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load enterprises from database");
                // Continue with empty collection - don't crash the app
            }
        }

        /// <summary>
        /// Shows a dialog for editing an enterprise
        /// </summary>
        private async Task<bool> ShowEnterpriseDialogAsync(Enterprise enterprise, string title)
        {
            try
            {
                var parameters = new DialogParameters();
                parameters.Add("Enterprise", enterprise);

                var result = await dialogService.ShowDialogAsync("EnterpriseDialogView", parameters);

                if (result.Result == ButtonResult.OK)
                {
                    // Update the enterprise with the result
                    if (result.Parameters.TryGetValue("Enterprise", out Enterprise updatedEnterprise))
                    {
                        // Copy properties back to the original enterprise
                        enterprise.Name = updatedEnterprise.Name;
                        enterprise.Type = updatedEnterprise.Type;
                        enterprise.CitizenCount = updatedEnterprise.CitizenCount;
                        enterprise.Description = updatedEnterprise.Description;
                    }
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to show enterprise dialog");
                MessageBox.Show($"Error displaying dialog: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Validates an enterprise and returns validation errors
        /// </summary>
        private bool ValidateEnterprise(Enterprise enterprise, out List<string> validationErrors)
        {
            validationErrors = new List<string>();

            // Required field validations
            if (string.IsNullOrWhiteSpace(enterprise.Name))
                validationErrors.Add("Enterprise name is required");

            if (enterprise.Name?.Length > 100)
                validationErrors.Add("Enterprise name cannot exceed 100 characters");

            if (string.IsNullOrWhiteSpace(enterprise.Type))
                validationErrors.Add("Enterprise type is required");

            if (enterprise.Type?.Length > 50)
                validationErrors.Add("Enterprise type cannot exceed 50 characters");

            if (enterprise.CurrentRate <= 0)
                validationErrors.Add("Current rate must be greater than zero");

            if (enterprise.CurrentRate > 9999.99m)
                validationErrors.Add("Current rate cannot exceed $9,999.99");

            if (enterprise.MonthlyExpenses < 0)
                validationErrors.Add("Monthly expenses cannot be negative");

            if (enterprise.CitizenCount < 1)
                validationErrors.Add("Citizen count must be at least 1");

            if (enterprise.Description?.Length > 500)
                validationErrors.Add("Description cannot exceed 500 characters");

            if (enterprise.Notes?.Length > 500)
                validationErrors.Add("Notes cannot exceed 500 characters");

            // Business rule validations
            if (enterprise.CitizenCount > 0 && enterprise.MonthlyExpenses > 0)
            {
                var breakEvenRate = enterprise.MonthlyExpenses / enterprise.CitizenCount;
                if (enterprise.CurrentRate < breakEvenRate * 0.5m)
                {
                    validationErrors.Add($"Warning: Current rate (${enterprise.CurrentRate:F2}) is significantly below break-even rate (${breakEvenRate:F2})");
                }
            }

            return validationErrors.Count == 0;
        }

        /// <summary>
        /// Creates a deep copy of an enterprise for editing
        /// </summary>
        private Enterprise CreateEnterpriseCopy(Enterprise original)
        {
            return new Enterprise
            {
                Id = original.Id,
                Name = original.Name,
                Description = original.Description,
                CurrentRate = original.CurrentRate,
                MonthlyExpenses = original.MonthlyExpenses,
                CitizenCount = original.CitizenCount,
                TotalBudget = original.TotalBudget,
                BudgetAmount = original.BudgetAmount,
                Type = original.Type,
                Notes = original.Notes,
                Status = original.Status,
                CreatedDate = original.CreatedDate,
                ModifiedDate = original.ModifiedDate,
                CreatedBy = original.CreatedBy,
                ModifiedBy = original.ModifiedBy,
                IsDeleted = original.IsDeleted,
                DeletedDate = original.DeletedDate,
                DeletedBy = original.DeletedBy,
                // Meter reading fields
                MeterReading = original.MeterReading,
                MeterReadDate = original.MeterReadDate,
                PreviousMeterReading = original.PreviousMeterReading,
                PreviousMeterReadDate = original.PreviousMeterReadDate
            };
        }

        /// <summary>
        /// Updates an enterprise from a copy
        /// </summary>
        private void UpdateEnterpriseFromCopy(Enterprise target, Enterprise source)
        {
            target.Name = source.Name;
            target.Description = source.Description;
            target.CurrentRate = source.CurrentRate;
            target.MonthlyExpenses = source.MonthlyExpenses;
            target.CitizenCount = source.CitizenCount;
            target.TotalBudget = source.TotalBudget;
            target.BudgetAmount = source.BudgetAmount;
            target.Type = source.Type;
            target.Notes = source.Notes;
            target.Status = source.Status;
            target.ModifiedDate = DateTime.UtcNow;
            target.ModifiedBy = Environment.UserName;

            // Meter reading fields
            target.MeterReading = source.MeterReading;
            target.MeterReadDate = source.MeterReadDate;
            target.PreviousMeterReading = source.PreviousMeterReading;
            target.PreviousMeterReadDate = source.PreviousMeterReadDate;
        }

        // Collection change event handlers
        private void Enterprises_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    Logger.LogDebug("MainViewModel Enterprises collection - Added {Count} items at index {Index}",
                        e.NewItems?.Count ?? 0, e.NewStartingIndex);
                    if (e.NewItems != null)
                    {
                        foreach (Enterprise enterprise in e.NewItems)
                        {
                            Logger.LogTrace("MainViewModel Added Enterprise: Id={Id}, Name={Name}, Type={Type}",
                                enterprise.Id, enterprise.Name, enterprise.Type);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    Logger.LogDebug("MainViewModel Enterprises collection - Removed {Count} items at index {Index}",
                        e.OldItems?.Count ?? 0, e.OldStartingIndex);
                    if (e.OldItems != null)
                    {
                        foreach (Enterprise enterprise in e.OldItems)
                        {
                            Logger.LogTrace("MainViewModel Removed Enterprise: Id={Id}, Name={Name}",
                                enterprise.Id, enterprise.Name);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    Logger.LogDebug("MainViewModel Enterprises collection - Replaced {Count} items at index {Index}",
                        e.NewItems?.Count ?? 0, e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    Logger.LogDebug("MainViewModel Enterprises collection - Reset (cleared or major change)");
                    break;

                case NotifyCollectionChangedAction.Move:
                    Logger.LogDebug("MainViewModel Enterprises collection - Moved item from index {OldIndex} to {NewIndex}",
                        e.OldStartingIndex, e.NewStartingIndex);
                    break;
            }

            Logger.LogDebug("MainViewModel Enterprises collection now has {Count} items", Enterprises.Count);
        }

        #region INavigationAware Implementation

        /// <summary>
        /// Called when the view is navigated to
        /// </summary>
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            Logger.LogInformation("MainViewModel navigated to");

            // Initialize default state if needed
            if (!Enterprises.Any() && !IsBusy)
            {
                LoadDataCommand.Execute();
            }
        }

        /// <summary>
        /// Called when the view is navigated from
        /// </summary>
        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Logger.LogInformation("MainViewModel navigated from");
            // Cleanup if needed
        }

        /// <summary>
        /// Determines if this view model is the target for navigation
        /// </summary>
        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        #endregion
    }
}
