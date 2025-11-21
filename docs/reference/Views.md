# Wiley Widget Views Documentation

**Last Updated**: October 14, 2025
**Status**: Consolidated view documentation

---

## Overview

This document consolidates all view-related documentation for the Wiley Widget WPF application. The application uses Prism.Container.DryIoc for navigation and dependency injection, with Syncfusion WPF controls for the UI.

---

## Architecture

### MVVM Pattern

- **Models**: Business entities in `WileyWidget.Models`
- **ViewModels**: Presentation logic in `src/ViewModels/`
- **Views**: XAML UI in `src/Views/`

### Navigation

- **Framework**: Prism.Container.DryIoc navigation (.NET 9 compatible)
- **Region Management**: Prism RegionManager
- **Main Region**: `MainRegion` in Shell

### Dependency Injection

- **Container**: DryIoc via Prism.Container.DryIoc extension
- **Registration**: `App.cs` `RegisterTypes()` method
- **View Registration**: `RegisterForNavigation<TView, TViewModel>()`

---

## Migration from Prism.DryIoc to Prism.Container.DryIoc

### Background

The application was migrated from `Prism.DryIoc` to `Prism.Container.DryIoc` to ensure compatibility with .NET 9 and follow Prism 9.0 best practices.

### Changes Made

1. **Package Reference**: Replaced `Prism.DryIoc` with `Prism.Container.DryIoc` in `Directory.Packages.props`
2. **Bootstrapper**: Updated `App.cs` to inherit from `PrismApplication` and use `DryIocContainerExtension`
3. **Container Creation**: Implemented `CreateContainerExtension()` to return `new DryIocContainerExtension(new Container())`
4. **Global Usings**: Removed transitive `Prism.DryIoc` usings from downstream projects
5. **DryIoc Version**: Pinned to compatible version (5.4.3) as required by `Prism.Container.DryIoc`

### Benefits

- ✅ Full .NET 9 compatibility
- ✅ Modern Prism 9.0 architecture
- ✅ Better separation of concerns
- ✅ Improved maintainability
- ✅ Future-proof for Prism updates

---

## Views Overview

### 1. DashboardView ⭐⭐⭐⭐⭐ (90% Complete)

**Purpose**: Real-time dashboard with KPIs, charts, and alerts

**Files**:

- `src/Views/DashboardView.xaml`
- `src/Views/DashboardView.xaml.cs`
- `src/ViewModels/DashboardViewModel.cs`

**Features**:

- ✅ KPI summary cards with color-coded metrics
- ✅ Chart integration (Syncfusion SfChart)
- ✅ Auto-refresh functionality
- ✅ Activity and alert feeds
- ✅ Comprehensive data binding
- ✅ Status bar with refresh information

**Missing**:

- ⚠️ Real data loading (currently simulated)
- ⚠️ Export functionality
- ⚠️ Dashboard customization
- ⚠️ Advanced chart types

**ViewModel Integration**:

```csharp
// Registered in App.cs
containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>();

// Navigation
regionManager.RequestNavigate("MainRegion", "DashboardView");
```

**Key Bindings**:

- `TotalRevenue` → Revenue KPI card
- `TotalExpenses` → Expenses KPI card
- `NetProfit` → Profit KPI card
- `RevenueChartData` → Revenue trend chart
- `ActivityLog` → Recent activities list

---

### 2. MunicipalAccountView ⭐⭐⭐⭐ (85% Complete)

**Purpose**: CRUD interface for municipal chart of accounts

**Files**:

- `src/Views/MunicipalAccountView.xaml`
- `src/Views/MunicipalAccountView.xaml.cs`
- `src/ViewModels/MunicipalAccountViewModel.cs`

**Features**:

- ✅ Account hierarchy display
- ✅ CRUD operations (Create, Read, Update, Delete)
- ✅ Search and filtering
- ✅ Account number validation
- ✅ QuickBooks sync integration
- ✅ Budget amount tracking

**Missing**:

- ⚠️ Account import/export
- ⚠️ Bulk operations
- ⚠️ Advanced reporting
- ⚠️ Audit trail view

**ViewModel Integration**:

```csharp
// Registered in App.cs
containerRegistry.RegisterForNavigation<MunicipalAccountView, MunicipalAccountViewModel>();

// Constructor injection
public MunicipalAccountViewModel(
    IMunicipalAccountRepository accountRepository,
    IQuickBooksService quickBooksService)
{
    // Initialization
}
```

**Key Bindings**:

- `MunicipalAccounts` → Account list DataGrid
- `SelectedAccount` → Selected account details
- `AddAccountCommand` → Create new account
- `SaveAccountCommand` → Save changes
- `DeleteAccountCommand` → Delete account
- `SyncQuickBooksCommand` → Sync with QuickBooks

**Data Model**:

```csharp
public class MunicipalAccount
{
    public int Id { get; set; }
    public string AccountNumber { get; set; }  // e.g., "100.1"
    public string Name { get; set; }
    public string Type { get; set; }  // Asset, Liability, Revenue, Expense
    public decimal Balance { get; set; }
    public decimal BudgetAmount { get; set; }
    public string QuickBooksId { get; set; }
    public DateTime? LastSyncDate { get; set; }
}
```

---

### 3. BudgetView ⭐⭐⭐⭐ (80% Complete)

**Purpose**: Budget analysis and financial reporting

**Files**:

- `src/Views/BudgetView.xaml`
- `src/Views/BudgetView.xaml.cs`
- `src/ViewModels/BudgetViewModel.cs`

**Features**:

- ✅ Budget summary cards
- ✅ Data grid with enterprise budget details
- ✅ Break-even analysis
- ✅ Trend analysis
- ✅ Recommendations engine
- ✅ Color-coded balance converter

**Missing**:

- ⚠️ Export functionality
- ⚠️ Budget forecasting
- ⚠️ Historical data comparison
- ⚠️ Budget approval workflow

---

### 4. EnterpriseView ⭐⭐⭐⭐ (85% Complete)

**Purpose**: CRUD interface for municipal enterprises

**Files**:

- `src/Views/EnterpriseView.xaml`
- `src/Views/EnterpriseView.xaml.cs`
- `src/ViewModels/EnterpriseViewModel.cs`

**Features**:

- ✅ Enterprise list management
- ✅ CRUD operations
- ✅ Service rate management
- ✅ Citizen count tracking
- ✅ Revenue/expense tracking

---

### 5. SettingsView ⭐⭐⭐⭐⭐ (95% Complete)

**Purpose**: Comprehensive application settings management

**Files**:

- `src/Views/SettingsView.xaml`
- `src/Views/SettingsView.xaml.cs`
- `src/ViewModels/SettingsViewModel.cs`

**Features**:

- ✅ Multi-tab settings interface
- ✅ Azure Key Vault integration UI
- ✅ QuickBooks configuration
- ✅ Syncfusion license management
- ✅ Advanced settings (logging, performance)
- ✅ Real-time validation and status indicators

**Missing**:

- ⚠️ Azure Key Vault actual integration
- ⚠️ Settings backup/restore
- ⚠️ Advanced validation rules

---

### 6. AnalyticsView ⭐⭐⭐ (70% Complete)

**Purpose**: Advanced analytics and reporting

**Files**:

- `src/Views/AnalyticsView.xaml`
- `src/Views/AnalyticsView.xaml.cs`
- `src/ViewModels/AnalyticsViewModel.cs`

**Status**: Partial implementation

---

### 7. ToolsView ⭐⭐⭐⭐ (85% Complete)

**Purpose**: Administrative tools and utilities

**Files**:

- `src/Views/ToolsView.xaml`
- `src/Views/ToolsView.xaml.cs`
- `src/ViewModels/ToolsViewModel.cs`

**Features**:

- ✅ Database diagnostics
- ✅ Cache management
- ✅ Log viewer
- ✅ System health checks

---

## DashboardViewModel Integration

**Date**: 2025-10-01
**Status**: ✅ RESOLVED

### Solution Summary

The DashboardViewModel was fully implemented but not properly wired into the MainViewModel dependency injection system.

**Fixes Applied**:

1. **Added Private Field**:

```csharp
private readonly DashboardViewModel _dashboardViewModel;
```

2. **Added Public Property**:

```csharp
public DashboardViewModel DashboardViewModel => _dashboardViewModel;
```

3. **Added Constructor Parameter**:

```csharp
public MainViewModel(
    // ... other parameters
    DashboardViewModel? dashboardViewModel = null
)
{
    _dashboardViewModel = dashboardViewModel!;
}
```

4. **Registered in DI Container**:

```csharp
builder.Services.AddScoped<ViewModels.DashboardViewModel>();
```

### Verification

Build Status: ✅ **Success**
Integration: ✅ **Complete**
Pattern: ✅ **Consistent with other view models**

---

## Prism Navigation Pattern

### View Registration

All views must be registered in `App.cs`:

```csharp
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    // Register ViewModels
    containerRegistry.RegisterSingleton<SettingsService>();
    containerRegistry.RegisterSingleton<MainViewModel>();
    containerRegistry.Register<DashboardViewModel>();
    containerRegistry.Register<MunicipalAccountViewModel>();

    // Register Views for Navigation
    containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>();
    containerRegistry.RegisterForNavigation<MunicipalAccountView, MunicipalAccountViewModel>();
}
```

### Navigation Execution

Navigate using RegionManager:

```csharp
var regionManager = Container.Resolve<IRegionManager>();
regionManager.RequestNavigate("MainRegion", "DashboardView");
```

### View Discovery

Prism automatically associates views and view models by:

1. Explicit registration: `RegisterForNavigation<TView, TViewModel>()`
2. View model locator: Automatic by naming convention
3. DataContext binding: Automatic by Prism

---

## Best Practices

### 1. ViewModel Construction

- ✅ Use constructor injection for dependencies
- ✅ Implement `INotifyPropertyChanged` or inherit from `BindableBase`
- ✅ Use `RelayCommand` or `DelegateCommand` for commands
- ✅ Keep view models testable (no UI dependencies)

### 2. View Design

- ✅ Use XAML data binding (avoid code-behind logic)
- ✅ Leverage Syncfusion controls for consistency
- ✅ Follow WPF MVVM patterns
- ✅ Use `RegionManager` for navigation

### 3. Data Binding

- ✅ Use `{Binding}` for two-way binding
- ✅ Use `{OneWay}` for read-only bindings
- ✅ Use converters for complex transformations
- ✅ Validate inputs at ViewModel level

### 4. Testing

- ✅ Unit test view models independently
- ✅ Use mock repositories for testing
- ✅ Test commands and property changes
- ✅ Integration tests for navigation

---

## Common Patterns

### Command Pattern

```csharp
public class MyViewModel : BindableBase
{
    public DelegateCommand SaveCommand { get; }

    public MyViewModel()
    {
        SaveCommand = new DelegateCommand(ExecuteSave, CanExecuteSave);
    }

    private void ExecuteSave()
    {
        // Save logic
    }

    private bool CanExecuteSave()
    {
        return !string.IsNullOrEmpty(SomeProperty);
    }
}
```

### Property Change Notification

```csharp
private string _name;
public string Name
{
    get => _name;
    set => SetProperty(ref _name, value);
}
```

### Async Operations

```csharp
public async Task LoadDataAsync()
{
    IsBusy = true;
    try
    {
        var data = await _repository.GetDataAsync();
        DataCollection = new ObservableCollection<T>(data);
    }
    finally
    {
        IsBusy = false;
    }
}
```

---

## Troubleshooting

### View Not Displaying

1. Check view registration in `App.cs`
2. Verify ViewModel constructor parameters
3. Check region name matches: `prism:RegionManager.RegionName="MainRegion"`
4. Ensure navigation request uses correct view name

### Data Not Binding

1. Verify `DataContext` is set (Prism does this automatically)
2. Check property names match exactly (case-sensitive)
3. Implement `INotifyPropertyChanged`
4. Use `{Binding Path=PropertyName, Mode=TwoWay}`

### Navigation Failures

1. Check Prism logs for navigation errors
2. Verify view model constructors can be resolved
3. Ensure all dependencies are registered in DI container
4. Check for circular dependencies

---

## Prism Best Practices Guide

### Module Development

1. **Module Registration**: Always register modules with `IModuleHealthService` for monitoring
2. **Region Registration**: Use `RegisterViewWithRegion()` in `OnInitialized()` for region-based navigation
3. **Error Handling**: Implement try-catch in module initialization with fallback strategies
4. **Health Tracking**: Mark module initialization status with `MarkModuleInitialized()`

### ViewModelLocator Configuration

1. **Naming Convention**: Follow `ViewName` → `ViewNameViewModel` pattern
2. **Auto-Wiring**: Use `prism:ViewModelLocator.AutoWireViewModel="True"` in XAML
3. **Error Handling**: ViewModelLocator is configured with custom error handling and logging
4. **Validation**: Automatic validation of View-ViewModel naming conventions

### Region Management

1. **Region Names**: Use descriptive region names (e.g., `MainRegion`, `LeftPanelRegion`)
2. **Region Adapters**: Custom region adapters for Syncfusion controls
3. **Navigation**: Use `IRegionManager.RequestNavigate()` for programmatic navigation
4. **Region Behaviors**: Implement custom behaviors for logging and auto-activation

### Dependency Injection

1. **Registration**: Register services in `RegisterTypes()` method
2. **Lifetime**: Use appropriate lifetimes (Singleton, Transient, Scoped)
3. **Navigation**: Use `RegisterForNavigation<TView, TViewModel>()` for navigable views
4. **Resolution**: Resolve services in constructors or via `IContainerProvider`

### XAML Integration

1. **xmlns:prism**: Include `` in all XAML files
2. **AutoWireViewModel**: Set `prism:ViewModelLocator.AutoWireViewModel="True"` for views
3. **Region Names**: Use `prism:RegionManager.RegionName` for region definitions
4. **Data Binding**: Follow WPF data binding best practices

### Error Handling

1. **Module Failures**: Modules continue loading even if one fails
2. **ViewModel Errors**: Logged and handled gracefully by ViewModelLocator
3. **Navigation Failures**: Logged with recovery strategies
4. **XAML Parse Errors**: Attempt recovery with theme re-application

---

## References

- **Prism Documentation**: https://prismlibrary.com/docs/
- **Syncfusion WPF**: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
- **WPF MVVM**: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/
- **Project Guidelines**: See `BusBuddy.instructions.md` for coding standards

---

## Status Legend

- ⭐⭐⭐⭐⭐ (90-100%): Production ready
- ⭐⭐⭐⭐ (80-89%): Nearly complete
- ⭐⭐⭐ (70-79%): Functional but incomplete
- ⭐⭐ (60-69%): Significant work needed
- ⭐ (0-59%): Early development

---

**Note**: This document consolidates information from:

- `DASHBOARDVIEWMODEL_INTEGRATION_FIX.md`
- `views-completion-assessment.md`
- `UI-ARCHITECTURE-REVIEW.md`

All original documentation files have been archived or removed to maintain a single source of truth.
