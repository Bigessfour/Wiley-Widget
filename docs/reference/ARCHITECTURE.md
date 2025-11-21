# Wiley Widget Architecture Documentation

**Version**: 0.1.0
**Framework**: .NET 9.0 with WPF
**Architecture**: Prism 9 MVVM with DryIoc Container
**Last Updated**: October 26, 2025

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Technology Stack](#technology-stack)
3. [Layered Architecture](#layered-architecture)
4. [Prism 9 Implementation](#prism-9-implementation)
5. [Module System](#module-system)
6. [Region Management](#region-management)
7. [Dependency Injection](#dependency-injection)
8. [Data Layer](#data-layer)
9. [Performance Optimizations](#performance-optimizations)
10. [Best Practices](#best-practices)

---

## Architecture Overview

Wiley Widget follows a **modular, layered architecture** using Prism 9 framework for WPF. The application emphasizes:

- **Separation of Concerns**: Clear boundaries between UI, business logic, and data access
- **Modularity**: Independent modules that can be loaded dynamically
- **Testability**: Dependency injection and MVVM patterns enable comprehensive testing
- **Maintainability**: Consistent patterns and conventions across the codebase

```
┌─────────────────────────────────────────────────────────────┐
│                    Wiley Widget Application                  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Prism 9 Application Host                   │
│  - DryIoc Container                                          │
│  - Module Catalog                                            │
│  - Region Manager                                            │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│   Modules    │    │   Services   │    │     Data     │
│              │    │              │    │              │
│ - Budget     │    │ - Theme      │    │ - EF Core    │
│ - Dashboard  │    │ - Excel      │    │ - SQL Server │
│ - Enterprise │    │ - AI         │    │ - Repository │
│ - Municipal  │    │ - QuickBooks │    │ - Auditing   │
│ - Reports    │    │ - Settings   │    │ - Health     │
└──────────────┘    └──────────────┘    └──────────────┘
```

---

## Technology Stack

### Core Framework

- **.NET 9.0** (`net9.0-windows10.0.19041.0`)
  - Latest language features (C# 12+)
  - Performance improvements (PGO, ReadyToRun)
  - Native AOT support (future enhancement)

### UI Framework

- **WPF** (Windows Presentation Foundation)
  - XAML-based declarative UI
  - Data binding and commanding
  - Styles and themes

### Application Framework

- **Prism 9.0.537**
  - `Prism.Core` - Base framework
  - `Prism.Uno.WinUI` - WPF-specific implementations
  - `Prism.Container.DryIoc 9.0.107` - DI container

### UI Controls

- **Syncfusion WinUI 31.1.17**
  - `SfChart` - Advanced charting
  - `SfDataGrid` - High-performance grids
  - `SfInput` - Enhanced input controls
  - `SfRichTextBoxAdv` - Rich text editing
  - `SfSkinManager` - Theme management

### Data & Persistence

- **Entity Framework Core 9.0.10**
  - SQL Server provider
  - Migrations and seeding
  - Change tracking and auditing

### Cross-Cutting Concerns

- **Serilog 4.3.0** - Structured logging
- **Polly 8.6.4** - Resilience and retry policies
- **FluentValidation 11.11.0** - Model validation
- **Microsoft.Extensions.\*** - DI, Configuration, Health Checks

---

## Layered Architecture

The application follows a **5-layer architecture**:

### 1. Presentation Layer (`WileyWidget.UI`)

**Responsibility**: User interface and user interaction

**Components**:

- **Views**: XAML-based UI components
  - `Shell.xaml` - Main window with region hosting
  - Module-specific views (BudgetView, DashboardView, etc.)
  - Dialogs and user controls

- **ViewModels**: MVVM pattern implementation
  - Inherit from `Prism.Mvvm.BindableBase`
  - Expose `ICommand` properties (DelegateCommand)
  - Handle UI logic and state management

- **Converters**: Value converters for data binding
  - `BooleanToVisibilityConverter`
  - `BalanceColorConverter`
  - Custom converters for Syncfusion controls

- **Behaviors**: Attached behaviors for UI enhancements
  - `FocusOnLoadBehavior`
  - `ActivateOnMouseOverBehavior`

- **Region Adapters**: Custom adapters for Syncfusion controls
  - `DockingManagerRegionAdapter`
  - `SfDataGridRegionAdapter`

**Dependencies**: WileyWidget.Services, WileyWidget.Models

---

### 2. Services Layer (`WileyWidget.Services`)

**Responsibility**: Business services and cross-cutting concerns

**Key Services**:

#### Theme Management

```csharp
public interface IThemeService
{
    string CurrentTheme { get; }
    bool IsDarkTheme { get; }
    void ApplyTheme(string themeName);
    event EventHandler<ThemeChangedEventArgs> ThemeChanged;
}
```

#### Excel Export

```csharp
public interface IExcelExportService
{
    Task<string> ExportBudgetEntriesAsync(IEnumerable<BudgetEntry> entries, string filePath);
    Task<string> ExportMunicipalAccountsAsync(IEnumerable<MunicipalAccount> accounts, string filePath);
    Task<string> ExportGenericDataAsync<T>(IEnumerable<T> data, string filePath, ...);
}
```

#### Settings Management

```csharp
public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
    void Load();
}
```

**Dependencies**: WileyWidget.Models, WileyWidget.Business

---

### 3. Business Logic Layer (`WileyWidget.Business`)

**Responsibility**: Domain logic and business rules

**Components**:

- Repository interfaces (`IBudgetRepository`, `IMunicipalAccountRepository`)
- Business validators (FluentValidation)
- Domain services
- Business rules and policies

**Dependencies**: WileyWidget.Models, WileyWidget.Data

---

### 4. Data Access Layer (`WileyWidget.Data`)

**Responsibility**: Database operations and persistence

**Components**:

#### DbContext

```csharp
public class WileyWidgetDbContext : DbContext
{
    public DbSet<BudgetEntry> BudgetEntries { get; set; }
    public DbSet<MunicipalAccount> MunicipalAccounts { get; set; }
    public DbSet<Department> Departments { get; set; }
    // ... other entity sets
}
```

#### Features

- Entity configurations (Fluent API)
- Database migrations
- Seeding and initialization
- Auditing interceptor (`AuditInterceptor`)
- Health checks integration

**Dependencies**: WileyWidget.Models

---

### 5. Models Layer (`WileyWidget.Models`)

**Responsibility**: Domain models and DTOs

**Entity Examples**:

```csharp
// Budget Entry with GASB compliance
public class BudgetEntry : IAuditable
{
    public int Id { get; set; }
    public string AccountNumber { get; set; }
    public string Description { get; set; }
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public FundType FundType { get; set; }
    public int FiscalYear { get; set; }
    // ... auditing properties
}

// Municipal Account with hierarchical structure
public class MunicipalAccount
{
    public int Id { get; set; }
    public AccountNumber AccountNumber { get; set; }
    public string Name { get; set; }
    public AccountType Type { get; set; }
    public decimal Balance { get; set; }
    public MunicipalAccount? ParentAccount { get; set; }
    // ... relationships
}
```

**Dependencies**: None (pure models)

---

## Prism 9 Implementation

### Application Bootstrapping

```csharp
// App.xaml.cs
public partial class App : Prism.Uno.WinUI.PrismApplication
{
    protected override void CreateWindow()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // Register services
        containerRegistry.RegisterSingleton<IThemeService, ThemeService>();
        containerRegistry.RegisterSingleton<IExcelExportService, ExcelExportService>();
        containerRegistry.RegisterSingleton<ISettingsService, SettingsService>();

        // Register views for navigation
        containerRegistry.RegisterForNavigation<DashboardView>();
        containerRegistry.RegisterForNavigation<BudgetView>();
        containerRegistry.RegisterForNavigation<SettingsView>();

        // Register dialogs
        containerRegistry.RegisterDialog<ConfirmationDialogView>();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<CoreModule>();
        moduleCatalog.AddModule<DashboardModule>();
        moduleCatalog.AddModule<BudgetModule>();
        moduleCatalog.AddModule<EnterpriseModule>();
        moduleCatalog.AddModule<MunicipalAccountModule>();
        moduleCatalog.AddModule<ReportsModule>();
        moduleCatalog.AddModule<SettingsModule>();
        moduleCatalog.AddModule<AIAssistModule>();
    }

    protected override IContainerExtension CreateContainerExtension()
    {
        return new DryIocContainerExtension(new Container());
    }
}
```

### Key Prism Features Used

1. **ViewModelLocator**: Automatic ViewModel discovery

   ```xaml
   <Window prism:ViewModelLocator.AutoWireViewModel="True">
   ```

2. **Commanding**: DelegateCommand for UI actions

   ```csharp
   public DelegateCommand ExportCommand { get; }
   ExportCommand = new DelegateCommand(ExecuteExport, CanExecuteExport);
   ```

3. **Event Aggregator**: Loosely-coupled communication

   ```csharp
   _eventAggregator.GetEvent<ThemeChangedEvent>().Publish(newTheme);
   ```

4. **Dialog Service**: Modal dialogs with result handling
   ```csharp
   _dialogService.ShowDialog("ConfirmationDialog", parameters, callback);
   ```

---

## Module System

### Module Structure

Each module follows a consistent pattern:

```csharp
public class BudgetModule : IModule
{
    private readonly IRegionManager _regionManager;

    public BudgetModule(IRegionManager regionManager)
    {
        _regionManager = regionManager;
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        // Register views in regions
        _regionManager.RegisterViewWithRegion("BudgetRegion", typeof(BudgetView));
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // Register module-specific services
        containerRegistry.Register<IBudgetRepository, BudgetRepository>();

        // Register views
        containerRegistry.RegisterForNavigation<BudgetView>();
        containerRegistry.RegisterForNavigation<BudgetDetailView>();
    }
}
```

### Module-to-Region Mapping

```csharp
private static readonly Dictionary<string, string[]> ModuleRegionMap = new()
{
    ["CoreModule"] = Array.Empty<string>(),
    ["DashboardModule"] = new[] { "MainRegion" },
    ["BudgetModule"] = new[] { "BudgetRegion", "AnalyticsRegion" },
    ["MunicipalAccountModule"] = new[] { "MunicipalAccountRegion" },
    ["EnterpriseModule"] = new[] { "EnterpriseRegion" },
    ["ReportsModule"] = new[] { "ReportsRegion" },
    ["SettingsModule"] = new[] { "SettingsRegion" },
    ["AIAssistModule"] = new[] { "AIAssistRegion" },
    ["PanelModule"] = new[] { "LeftPanelRegion", "RightPanelRegion", "BottomPanelRegion" }
};
```

---

## Region Management

### Shell with Regions

```xaml
<Window x:Class="WileyWidget.Views.Shell"

        xmlns:syncfusion="http://schemas.syncfusion.com/wpf">
    <Grid>
        <!-- Main content region -->
        <ContentControl prism:RegionManager.RegionName="MainRegion" />

        <!-- Docking manager with region -->
        <syncfusion:DockingManager prism:RegionManager.RegionName="DockingRegion">
            <!-- Docked panels loaded dynamically -->
        </syncfusion:DockingManager>
    </Grid>
</Window>
```

### Custom Region Adapters

For Syncfusion controls, custom region adapters enable Prism integration:

```csharp
public class DockingManagerRegionAdapter : RegionAdapterBase<DockingManager>
{
    public DockingManagerRegionAdapter(IRegionBehaviorFactory regionBehaviorFactory)
        : base(regionBehaviorFactory) { }

    protected override IRegion CreateRegion()
    {
        return new SingleActiveRegion();
    }

    protected override void Adapt(IRegion region, DockingManager regionTarget)
    {
        region.Views.CollectionChanged += (s, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (FrameworkElement view in e.NewItems)
                {
                    regionTarget.Children.Add(view);
                }
            }
        };
    }
}
```

---

## Dependency Injection

### DryIoc Container Benefits

1. **Performance**: Fastest DI container for .NET
2. **Feature-rich**: Supports decorators, interceptors, lifetimes
3. **Compatibility**: Full Prism 9 integration

### Service Registration Patterns

```csharp
// Singleton (one instance per application)
containerRegistry.RegisterSingleton<IThemeService, ThemeService>();

// Transient (new instance each time)
containerRegistry.Register<IBudgetViewModel, BudgetViewModel>();

// Scoped (one instance per scope)
containerRegistry.RegisterScoped<IDbContext, WileyWidgetDbContext>();

// Factory registration
containerRegistry.Register<IRepository>(provider =>
    new Repository(provider.Resolve<IDbContext>()));
```

### Lifetime Scopes

- **Singleton**: Services that maintain state (Theme, Settings)
- **Transient**: ViewModels, repositories (short-lived)
- **Scoped**: Database contexts (per-request or per-operation)

---

## Data Layer

### Entity Framework Core Configuration

```csharp
// Startup configuration
services.AddDbContext<WileyWidgetDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
    options.EnableSensitiveDataLogging(isDevelopment);
    options.AddInterceptors(new AuditInterceptor());
});
```

### Health Checks

```csharp
services.AddHealthChecks()
    .AddDbContextCheck<WileyWidgetDbContext>("database")
    .AddSqlServer(connectionString, name: "sql-server");
```

---

## Performance Optimizations

### Startup Performance

- **ReadyToRun**: Pre-compiled for faster startup
- **TieredPGO**: Profile-guided optimization enabled
- **Module Loading**: Deferred initialization for non-critical modules

### Runtime Performance

- **Compiled Bindings**: x:Bind for faster data binding
- **Virtualization**: UI virtualization for large collections
- **Caching**: Memory caching for frequently accessed data

### Profiling

Use the provided script to measure performance:

```powershell
.\scripts\profile-startup-performance.ps1 -Iterations 5 -Detailed
```

**Target Metrics**:

- Cold start: < 2 seconds
- Warm start: < 500ms
- Module load: < 100ms each

---

## Best Practices

### MVVM Pattern

1. **ViewModels inherit from BindableBase**

   ```csharp
   public class BudgetViewModel : BindableBase
   {
       private string _title;
       public string Title
       {
           get => _title;
           set => SetProperty(ref _title, value);
       }
   }
   ```

2. **Commands use DelegateCommand**

   ```csharp
   public DelegateCommand SaveCommand { get; }
   SaveCommand = new DelegateCommand(ExecuteSave, CanExecuteSave)
       .ObservesProperty(() => IsDirty);
   ```

3. **No code-behind logic in views** (except for Prism registration)

### Module Development

1. **Single Responsibility**: Each module handles one feature area
2. **Loose Coupling**: Modules communicate via EventAggregator
3. **Independent**: Modules can be loaded/unloaded dynamically

### Testing Strategy

1. **Unit Tests**: ViewModels, services, business logic
2. **Integration Tests**: Repository patterns, database operations
3. **UI Tests**: End-to-end scenarios with FlaUI

### Code Organization

```
ModuleName/
├── Views/
│   ├── MainView.xaml
│   └── DetailView.xaml
├── ViewModels/
│   ├── MainViewModel.cs
│   └── DetailViewModel.cs
├── Models/
│   └── ModuleSpecificModel.cs
└── ModuleNameModule.cs (IModule implementation)
```

---

## References

- [Prism Documentation](https://prismlibrary.com/docs/)
- [Syncfusion WPF Controls](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf)
- [.NET 9 Release Notes](https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9)
- [Entity Framework Core 9](https://docs.microsoft.com/en-us/ef/core/)

---

**Last Updated**: October 26, 2025
**Document Version**: 1.0.0
