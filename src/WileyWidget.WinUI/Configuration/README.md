# Dependency Injection Configuration

## Overview

This directory contains the dependency injection (DI) configuration for the Wiley Widget WinUI 3 application. The configuration follows **Microsoft best practices** as documented in:

- [.NET Dependency Injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [WinUI 3 Dependency Injection Tutorial](https://learn.microsoft.com/en-us/windows/apps/tutorials/winui-mvvm-toolkit/dependency-injection)
- [Dependency Injection Guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)

## Architecture

### Service Lifetimes

Following Microsoft's recommendations, services are registered with appropriate lifetimes:

| Lifetime      | When to Use                                                        | Disposal                 | Examples                           |
| ------------- | ------------------------------------------------------------------ | ------------------------ | ---------------------------------- |
| **Singleton** | Stateless, expensive to create, or globally shared                 | Disposed when app exits  | Settings, Telemetry, API Clients   |
| **Scoped**    | Per-request state in web apps (not typically used in desktop apps) | Disposed at end of scope | DbContext in web apps              |
| **Transient** | New instance every time, lightweight objects                       | Disposed by container    | ViewModels, per-operation services |

### Service Categories

#### 1. Logging Services

- **ILogger<T>** - Serilog integration for structured logging
- Configured to use Serilog as the backing provider

#### 2. Core Services (Singleton)

- **ISettingsService** - Application configuration and user preferences
- **ISecretVaultService** - Encrypted storage for sensitive data (connection strings, API keys)
- **HealthCheckService** - Application health monitoring

#### 3. Data Services (Singleton)

- **IQuickBooksApiClient** - HTTP client for QuickBooks API
- **IQuickBooksService** - Business logic for QuickBooks integration
- Uses `AddHttpClient` pattern with 5-minute handler lifetime

#### 4. UI Services (Transient)

- **INavigationService** - Frame-based navigation (requires Frame from MainWindow)
- **IDialogService** - ContentDialog management (requires XamlRoot per dialog)
- **IDialogTrackingService** - Tracks open dialogs (Singleton)

> **Note**: Navigation and Dialog services are Transient because they need UI context (Frame/XamlRoot) which is only available after window activation.

#### 5. Cache Services (Singleton)

- **ICacheService** - In-memory caching via `AddWileyMemoryCache()` extension
- Uses Microsoft.Extensions.Caching.Memory

#### 6. Feature Services

**Singletons** (stateless, shared across app):

- **IAIService** - AI/ML integration (X.AI/Grok)
- **IAILoggingService** - AI operation logging
- **IAuditService** - User action auditing
- **ITelemetryService** - Application telemetry (SigNoz)
- **IReportExportService** - Report generation
- **IBoldReportService** - Bold Reports integration

**Transients** (per-operation state):

- **IExcelReaderService** - Excel file reading
- **IExcelExportService** - Excel export operations
- **IDataAnonymizerService** - Data anonymization
- **IChargeCalculatorService** - Utility charge calculations

#### 7. ViewModels (Transient)

All ViewModels are registered as Transient to ensure:

- Fresh state per page navigation
- Proper disposal after page unload
- No shared state between instances

Registered ViewModels:

- MainViewModel, DashboardViewModel, BudgetViewModel
- QuickBooksViewModel, AnalyticsViewModel, ReportsViewModel
- SettingsViewModel, ToolsViewModel, EnterpriseViewModel
- DepartmentViewModel, MunicipalAccountViewModel, UtilityCustomerViewModel
- AIAssistViewModel, DataViewModel, ChartViewModel

## Usage

### In App.xaml.cs

```csharp
using WileyWidget.WinUI.Configuration;

public App()
{
    // Configure DI before XAML initialization
    Services = DependencyInjection.ConfigureServices();

    // Validate critical dependencies
    if (!DependencyInjection.ValidateDependencies(Services))
    {
        Log.Warning("Some dependencies failed validation");
    }

    this.InitializeComponent();
}

public static IServiceProvider? Services { get; private set; }
public new static App Current => (App)Application.Current;
```

### In Pages/Views

```csharp
using Microsoft.Extensions.DependencyInjection;

public sealed partial class BudgetOverviewPage : Page
{
    private readonly BudgetViewModel _viewModel;

    public BudgetOverviewPage()
    {
        this.InitializeComponent();

        // Resolve ViewModel from DI container
        _viewModel = App.Current.Services.GetRequiredService<BudgetViewModel>();
        this.DataContext = _viewModel;
    }
}
```

### In ViewModels

```csharp
public class DashboardViewModel : ObservableRecipient
{
    private readonly IQuickBooksService _qbService;
    private readonly ITelemetryService _telemetry;
    private readonly ILogger<DashboardViewModel> _logger;

    // Constructor injection - all dependencies automatically resolved
    public DashboardViewModel(
        IQuickBooksService qbService,
        ITelemetryService telemetry,
        ILogger<DashboardViewModel> logger)
    {
        _qbService = qbService;
        _telemetry = telemetry;
        _logger = logger;
    }
}
```

## Dependency Validation

The `ValidateDependencies()` method checks that all critical services can be resolved at startup:

```csharp
if (!DependencyInjection.ValidateDependencies(Services))
{
    // Log warning but don't crash - allows app to start in degraded mode
    Log.Warning("Some dependencies failed validation - check logs");
}
```

Validated services:

- ILogger<T>
- ISettingsService
- ISecretVaultService
- INavigationService
- IDialogService
- ICacheService
- IQuickBooksService
- IAIService
- ITelemetryService
- IAuditService
- IDialogTrackingService

## Special Considerations

### Navigation Service

The NavigationService requires a Frame instance which is only available after the MainWindow is created. The service is registered with a null Frame and must be properly initialized in MainWindow:

```csharp
// In MainWindow.xaml.cs
private void InitializeNavigation()
{
    var logger = App.Services.GetService<ILogger<DefaultNavigationService>>();
    _navigationService = new DefaultNavigationService(
        ContentFrame,  // Frame is now available
        logger,
        App.Services);
}
```

### Dialog Service

The DialogService needs XamlRoot for each dialog, which varies per dialog invocation. It's registered as Transient and XamlRoot should be set when showing dialogs:

```csharp
var dialogService = App.Services.GetRequiredService<IDialogService>();
await dialogService.ShowErrorAsync("Error", "Something went wrong");
```

### HttpClient Factory

QuickBooksApiClient uses the recommended `AddHttpClient` pattern:

- Manages HttpClient lifecycle
- Prevents socket exhaustion
- 5-minute handler lifetime for connection pooling

## Testing

For unit tests, create a test ServiceCollection:

```csharp
[TestClass]
public class DashboardViewModelTests
{
    [TestMethod]
    public void TestDashboardLoad()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IQuickBooksService>(new MockQuickBooksService());
        services.AddTransient<DashboardViewModel>();

        var provider = services.BuildServiceProvider();
        var viewModel = provider.GetRequiredService<DashboardViewModel>();

        // Act & Assert
        Assert.IsNotNull(viewModel);
    }
}
```

## Troubleshooting

### "Cannot resolve service" errors

1. Check that the service interface and implementation are registered in `DependencyInjection.cs`
2. Verify the service lifetime (Singleton/Transient) is appropriate
3. Ensure all constructor dependencies can be resolved
4. Run `ValidateDependencies()` to identify missing services

### Memory leaks

- Ensure Transient services don't capture Singleton dependencies
- Use `IServiceScope` for scoped operations
- Avoid storing IServiceProvider in long-lived objects

### Circular dependencies

- Refactor services to remove circular references
- Use factory patterns: `Func<IServiceA>` instead of `IServiceA`
- Consider lazy initialization: `Lazy<IServiceA>`

## References

- [Microsoft Docs: Dependency Injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [WinUI 3 DI Tutorial](https://learn.microsoft.com/en-us/windows/apps/tutorials/winui-mvvm-toolkit/dependency-injection)
- [DI Guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
- [Service Lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#service-lifetimes)
- [HttpClient Factory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)

---

**Last Updated**: 2025-11-23  
**Consultant**: Microsoft Docs MCP (Official Documentation)
