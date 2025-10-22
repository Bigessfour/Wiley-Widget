using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Shell;
using WileyWidget.ViewModels.Base;

#nullable enable

namespace WileyWidget.ViewModels;

/// <summary>
/// ShellViewModel coordinates high-level navigation for the PolishHost shell.
/// Navigation is now snapshot-driven, enabling rich history metadata, telemetry,
/// and clean separation from the WPF window code-behind.
/// </summary>
public partial class ShellViewModel : AsyncViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly Stack<NavigationSnapshot> _backStack = new();
    private readonly Stack<NavigationSnapshot> _forwardStack = new();
    private bool _suppressSelectionChange;

    [ActivatorUtilitiesConstructor]
    public ShellViewModel(
        SettingsService settingsService,
        DashboardViewModel dashboardViewModel,
        EnterpriseViewModel enterpriseViewModel,
        BudgetViewModel budgetViewModel,
        UtilityCustomerViewModel utilityCustomerViewModel,
        AIAssistViewModel aiAssistViewModel,
        ToolsViewModel toolsViewModel,
        SettingsViewModel settingsViewModel,
        IDispatcherHelper dispatcherHelper,
        ILogger<ShellViewModel> logger)
        : this(
            settingsService,
            BuildDefaultNavigation(
                dashboardViewModel,
                enterpriseViewModel,
                budgetViewModel,
                utilityCustomerViewModel,
                aiAssistViewModel,
                toolsViewModel,
                settingsViewModel),
            dispatcherHelper,
        logger)
    {
    }

    private ShellViewModel(
        SettingsService settingsService,
        IEnumerable<NavigationItem> navigationItems,
        IDispatcherHelper dispatcherHelper,
        ILogger<ShellViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        var items = navigationItems?.ToList() ?? throw new ArgumentNullException(nameof(navigationItems));
        NavigationItems = new ObservableCollection<NavigationItem>(items);

        if (NavigationItems.Count == 0)
        {
            Logger.LogWarning("Shell navigation initialized with no items. The shell will load without content.");
        }

        Theme = _settingsService.Current.Theme ?? "FluentDark";
        LoadInitialContent();
        InitializeCommands();
    }

    /// <summary>
    /// Navigation entries displayed in the shell sidebar.
    /// </summary>
    public ObservableCollection<NavigationItem> NavigationItems { get; }

    /// <summary>
    /// The active view model currently hosted by the shell.
    /// </summary>
    public object? ActiveViewModel => CurrentSnapshot?.ViewModel;

    private NavigationSnapshot? _currentSnapshot;
    public NavigationSnapshot? CurrentSnapshot
    {
        get => _currentSnapshot;
        set
        {
            if (_currentSnapshot != value)
            {
                _currentSnapshot = value;
                RaisePropertyChanged();
                RefreshCurrentViewCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    private string _currentViewName = "Dashboard";
    public string CurrentViewName
    {
        get => _currentViewName;
        set
        {
            if (_currentViewName != value)
            {
                _currentViewName = value;
                RaisePropertyChanged();
            }
        }
    }

    private string _currentBreadcrumb = string.Empty;
    public string CurrentBreadcrumb
    {
        get => _currentBreadcrumb;
        set
        {
            if (_currentBreadcrumb != value)
            {
                _currentBreadcrumb = value;
                RaisePropertyChanged();
            }
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                RaisePropertyChanged();
            }
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                RaisePropertyChanged();
            }
        }
    }

    private string _theme = "FluentDark";
    public string Theme
    {
        get => _theme;
        set
        {
            if (_theme != value)
            {
                _theme = value;
                RaisePropertyChanged();
            }
        }
    }

    private NavigationItem? _selectedNavigationItem;
    public NavigationItem? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (_selectedNavigationItem != value)
            {
                _selectedNavigationItem = value;
                RaisePropertyChanged();

                if (!_suppressSelectionChange && value is not null)
                {
                    NavigateToItem(value, NavigationTrigger.Selection, recordHistory: true);
                }
            }
        }
    }

    private int _navigationIndex;
    public int NavigationIndex
    {
        get => _navigationIndex;
        set
        {
            if (_navigationIndex != value)
            {
                _navigationIndex = value;
                RaisePropertyChanged();
            }
        }
    }

    private bool _canNavigateBack;
    public bool CanNavigateBack
    {
        get => _canNavigateBack;
        set
        {
            if (_canNavigateBack != value)
            {
                _canNavigateBack = value;
                RaisePropertyChanged();
                NavigateBackCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _canNavigateForward;
    public bool CanNavigateForward
    {
        get => _canNavigateForward;
        set
        {
            if (_canNavigateForward != value)
            {
                _canNavigateForward = value;
                RaisePropertyChanged();
                NavigateForwardCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public DelegateCommand NavigateBackCommand { get; private set; } = null!;
    public DelegateCommand NavigateForwardCommand { get; private set; } = null!;
    public DelegateCommand ClearNavigationHistoryCommand { get; private set; } = null!;
    public DelegateCommand RefreshCurrentViewCommand { get; private set; } = null!;
    public DelegateCommand NavigateToDashboardCommand { get; private set; } = null!;
    public DelegateCommand NavigateToEnterprisesCommand { get; private set; } = null!;
    public DelegateCommand NavigateToBudgetCommand { get; private set; } = null!;
    public DelegateCommand NavigateToCustomersCommand { get; private set; } = null!;
    public DelegateCommand NavigateToAIAssistantCommand { get; private set; } = null!;
    public DelegateCommand NavigateToToolsCommand { get; private set; } = null!;
    public DelegateCommand NavigateToSettingsCommand { get; private set; } = null!;

    private void LoadInitialContent()
    {
        var initialItem = NavigationItems.FirstOrDefault();
        if (initialItem is null)
        {
            Logger.LogWarning("Shell navigation list is empty; nothing to display.");
            return;
        }

        ApplySnapshot(initialItem.CreateSnapshot(), NavigationTrigger.Startup, recordHistory: false);
    }

    private void InitializeCommands()
    {
        NavigateBackCommand = new DelegateCommand(ExecuteNavigateBack, () => CanNavigateBack);
        NavigateForwardCommand = new DelegateCommand(ExecuteNavigateForward, () => CanNavigateForward);
        ClearNavigationHistoryCommand = new DelegateCommand(ExecuteClearNavigationHistory, () => _backStack.Count > 0 || _forwardStack.Count > 0);
        RefreshCurrentViewCommand = new DelegateCommand(async () => await ExecuteRefreshCurrentView(), () => ActiveViewModel is not null);
        NavigateToDashboardCommand = new DelegateCommand(ExecuteNavigateToDashboard);
        NavigateToEnterprisesCommand = new DelegateCommand(ExecuteNavigateToEnterprises);
        NavigateToBudgetCommand = new DelegateCommand(ExecuteNavigateToBudget);
        NavigateToCustomersCommand = new DelegateCommand(ExecuteNavigateToCustomers);
        NavigateToAIAssistantCommand = new DelegateCommand(ExecuteNavigateToAIAssistant);
        NavigateToToolsCommand = new DelegateCommand(ExecuteNavigateToTools);
        NavigateToSettingsCommand = new DelegateCommand(ExecuteNavigateToSettings);
    }

    private static IEnumerable<NavigationItem> BuildDefaultNavigation(
        DashboardViewModel dashboardViewModel,
        EnterpriseViewModel enterpriseViewModel,
        BudgetViewModel budgetViewModel,
        UtilityCustomerViewModel utilityCustomerViewModel,
        AIAssistViewModel aiAssistViewModel,
        ToolsViewModel toolsViewModel,
        SettingsViewModel settingsViewModel)
    {
        yield return CreateNavigationItem("dashboard", "Dashboard", "📊", "Overview and KPIs", dashboardViewModel);
        yield return CreateNavigationItem("enterprises", "Enterprises", "🏢", "Municipal enterprise management", enterpriseViewModel);
        yield return CreateNavigationItem("budget", "Budget", "💰", "Budget planning and analysis", budgetViewModel);
        yield return CreateNavigationItem("customers", "Customers", "👥", "Utility customer management", utilityCustomerViewModel);
        yield return CreateNavigationItem("ai-assistant", "AI Assistant", "🤖", "AI-powered assistance", aiAssistViewModel);
        yield return CreateNavigationItem("tools", "Tools", "🔧", "Administrative tools", toolsViewModel);
        yield return CreateNavigationItem("settings", "Settings", "⚙️", "Application settings", settingsViewModel);
    }

    private static NavigationItem CreateNavigationItem(
        string route,
        string name,
        string icon,
    string description,
    object viewModel)
    {
        return new NavigationItem(route, name, icon, description, viewModel);
    }

    private void NavigateToItem(
        NavigationItem navigationItem,
        NavigationTrigger trigger,
        bool recordHistory)
    {
        var snapshot = navigationItem.CreateSnapshot();
        ApplySnapshot(snapshot, trigger, recordHistory);
    }

    private void ApplySnapshot(
        NavigationSnapshot snapshot,
        NavigationTrigger trigger,
        bool recordHistory)
    {
        if (recordHistory && CurrentSnapshot is { } current)
        {
            _backStack.Push(current);
        }

        if (recordHistory)
        {
            _forwardStack.Clear();
        }

        CurrentSnapshot = snapshot;
        CurrentViewName = snapshot.DisplayName;
        CurrentBreadcrumb = snapshot.Breadcrumb.Count == 0
            ? snapshot.DisplayName
            : string.Join(" › ", snapshot.Breadcrumb);

        NavigationIndex++;

        NavigationItem? selected = null;
        foreach (var item in NavigationItems)
        {
            var isMatch = string.Equals(item.Route, snapshot.Route, StringComparison.OrdinalIgnoreCase);
            item.IsSelected = isMatch;
            if (isMatch)
            {
                selected = item;
            }
        }

        try
        {
            _suppressSelectionChange = true;
            SelectedNavigationItem = selected;
        }
        finally
        {
            _suppressSelectionChange = false;
        }

        CanNavigateBack = _backStack.Count > 0;
        CanNavigateForward = _forwardStack.Count > 0;

        Logger.LogDebug("Shell navigation -> {Route} ({Trigger})", snapshot.Route, trigger);
        TrackNavigationTelemetry(snapshot, trigger);
    }

    private void ExecuteNavigateBack()
    {
        if (_backStack.Count == 0)
        {
            return;
        }

        if (CurrentSnapshot is { } current)
        {
            _forwardStack.Push(current);
        }

        var previous = _backStack.Pop();
        ApplySnapshot(previous, NavigationTrigger.Back, recordHistory: false);
        CanNavigateBack = _backStack.Count > 0;
        CanNavigateForward = _forwardStack.Count > 0;
    }

    private void ExecuteNavigateForward()
    {
        if (_forwardStack.Count == 0)
        {
            return;
        }

        if (CurrentSnapshot is { } current)
        {
            _backStack.Push(current);
        }

        var next = _forwardStack.Pop();
        ApplySnapshot(next, NavigationTrigger.Forward, recordHistory: false);
        CanNavigateBack = _backStack.Count > 0;
        CanNavigateForward = _forwardStack.Count > 0;
    }

    private void ExecuteClearNavigationHistory()
    {
        _backStack.Clear();
        _forwardStack.Clear();
        NavigationIndex = 0;
        CanNavigateBack = false;
        CanNavigateForward = false;
        ClearNavigationHistoryCommand.RaiseCanExecuteChanged();

        Logger.LogDebug("Shell navigation history cleared.");
    }

    private async Task ExecuteRefreshCurrentView()
    {
        if (ActiveViewModel is null)
        {
            return;
        }

        try
        {
            StatusMessage = $"Refreshing {CurrentViewName}...";
            IsLoading = true;

            await Task.Delay(100).ConfigureAwait(false);

            StatusMessage = $"{CurrentViewName} refreshed";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to refresh current view {ViewName}", CurrentViewName);
            StatusMessage = $"Failed to refresh {CurrentViewName}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ExecuteNavigateToDashboard() => NavigateToRoute("dashboard");

    private void ExecuteNavigateToEnterprises() => NavigateToRoute("enterprises");

    private void ExecuteNavigateToBudget() => NavigateToRoute("budget");

    private void ExecuteNavigateToCustomers() => NavigateToRoute("customers");

    private void ExecuteNavigateToAIAssistant() => NavigateToRoute("ai-assistant");

    private void ExecuteNavigateToTools() => NavigateToRoute("tools");

    private void ExecuteNavigateToSettings() => NavigateToRoute("settings");

    public void NavigateToRoute(string route, NavigationTrigger trigger = NavigationTrigger.Command, bool recordHistory = true)
    {
        var target = NavigationItems.FirstOrDefault(
            item => string.Equals(item.Route, route, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            Logger.LogWarning("Navigation route '{Route}' was not found.", route);
            return;
        }

        NavigateToItem(target, trigger, recordHistory);
    }

    private void TrackNavigationTelemetry(NavigationSnapshot snapshot, NavigationTrigger trigger)
    {
        var properties = new Dictionary<string, object>
        {
            ["Route"] = snapshot.Route,
            ["DisplayName"] = snapshot.DisplayName,
            ["Icon"] = snapshot.Icon,
            ["Trigger"] = trigger.ToString(),
            ["Breadcrumb"] = string.Join(">", snapshot.Breadcrumb),
            ["BackStackDepth"] = _backStack.Count.ToString(),
            ["ForwardStackDepth"] = _forwardStack.Count.ToString()
        };

        ErrorReportingService.Instance.TrackEvent("ShellNavigation", properties);
    }
}
