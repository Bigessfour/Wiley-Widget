# WinUI 3 Dependency Injection Setup - Official Microsoft Pattern

**Status**: ✅ VALIDATED AGAINST MICROSOFT DOCUMENTATION  
**Last Verified**: 2025-01-23  
**Source**: https://learn.microsoft.com/en-us/windows/apps/tutorials/winui-mvvm-toolkit/dependency-injection

---

## 📋 Official Microsoft WinUI 3 DI Pattern

This document outlines the **correct and complete** dependency injection setup for WinUI 3 applications, validated against official Microsoft documentation.

## ✅ Required App.xaml.cs Structure

According to Microsoft documentation, a proper WinUI 3 App.xaml.cs must include:

### 1. Services Property
```csharp
public static IServiceProvider Services { get; private set; }
```

### 2. App.Current Accessor
```csharp
public new static App Current => (App)Application.Current;
```

### 3. Constructor with DI Configuration
```csharp
public App()
{
    // Configure DI FIRST (before InitializeComponent)
    Services = ConfigureServices();
    
    // Then initialize XAML
    this.InitializeComponent();
}
```

### 4. ConfigureServices Method
```csharp
private static IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();
    
    // Register services (singletons, scoped, transient)
    services.AddSingleton<IMyService, MyService>();
    services.AddTransient<MyViewModel>();
    
    return services.BuildServiceProvider();
}
```

### 5. OnLaunched Override
```csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    m_window = new MainWindow();
    m_window.Activate();
}

private Window? m_window;
```

---

## 🎯 Wiley Widget Implementation Status

### ✅ CORRECT: Current Implementation

Our `App.xaml.cs` **already follows the Microsoft pattern correctly**:

```csharp
public partial class App : Application
{
    // ✅ Static Services property
    public static IServiceProvider? Services { get; private set; }
    
    // ✅ App.Current accessor
    public new static App Current => (App)Application.Current;

    public App()
    {
        // ✅ DI configured BEFORE InitializeComponent
        Services = DependencyInjection.ConfigureServices();
        
        // ✅ XAML initialization
        this.InitializeComponent();
    }

    // ✅ OnLaunched creates and activates window
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        m_window = new MainWindow();
        m_window.Activate();
    }

    private Window? m_window;
}
```

---

## 🔧 Critical Services Registration

### Required Service Lifetimes (Per Microsoft Best Practices)

| Service Type | Lifetime | Reason |
|-------------|----------|--------|
| **DbContext** | Scoped | Per-request lifecycle, avoid threading issues |
| **ViewModels** | Transient | Fresh state per navigation |
| **ISettingsService** | Singleton | Shared configuration |
| **IDialogService** | Transient | Requires XamlRoot per dialog |
| **INavigationService** | Transient | Requires Frame per window |
| **Cache Services** | Singleton | Shared cache across app |
| **API Clients** | Singleton | Expensive to create, stateless |

### 🆕 Missing DbContext Registration (FIXED)

**Issue**: `DashboardViewModel` requires `AppDbContext` but it wasn't registered in DI.

**Fix Applied**:
```csharp
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(connectionString);
    options.EnableSensitiveDataLogging(); // Development only
    options.EnableDetailedErrors();
}, ServiceLifetime.Scoped);
```

**Why Scoped?**
- EF Core DbContext is **NOT thread-safe**
- Scoped lifetime creates new instance per service scope
- Prevents concurrent access issues
- Follows Microsoft best practices for DbContext in desktop apps

---

## 📚 How ViewModels Access Services

### Method 1: Constructor Injection (Recommended)

```csharp
public class DashboardViewModel : ObservableObject
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DashboardViewModel> _logger;

    public DashboardViewModel(
        AppDbContext dbContext,
        ILogger<DashboardViewModel> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }
}
```

### Method 2: Service Locator (Pages/Views)

```csharp
// In Page code-behind
protected override void OnNavigatedTo(NavigationEventArgs e)
{
    base.OnNavigatedTo(e);
    
    // Resolve ViewModel from DI container
    var viewModel = App.Current.Services.GetService<DashboardViewModel>();
    this.DataContext = viewModel;
}
```

---

## 🏗️ Complete DI Configuration Structure

```csharp
public static IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();

    // 1. Logging (Singleton)
    services.AddLogging(builder => builder.AddSerilog());

    // 2. DbContext (Scoped)
    services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(connectionString), 
        ServiceLifetime.Scoped);

    // 3. Core Services (Singleton)
    services.AddSingleton<ISettingsService, SettingsService>();
    services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();

    // 4. UI Services (Transient - need XamlRoot/Frame)
    services.AddTransient<INavigationService, DefaultNavigationService>();
    services.AddTransient<IDialogService, DialogService>();

    // 5. ViewModels (Transient - fresh state per navigation)
    services.AddTransient<MainViewModel>();
    services.AddTransient<DashboardViewModel>();

    return services.BuildServiceProvider();
}
```

---

## 🚨 Common Mistakes to Avoid

### ❌ WRONG: Registering DbContext as Singleton
```csharp
// DON'T DO THIS - DbContext is NOT thread-safe
services.AddSingleton<AppDbContext>();
```

### ❌ WRONG: Creating DbContext with `new`
```csharp
// DON'T DO THIS - Bypasses DI and connection management
var dbContext = new AppDbContext();
```

### ❌ WRONG: Configuring DI after InitializeComponent
```csharp
public App()
{
    this.InitializeComponent(); // ❌ Too early
    Services = ConfigureServices(); // ❌ XAML already parsed
}
```

### ✅ CORRECT: DI before XAML
```csharp
public App()
{
    Services = ConfigureServices(); // ✅ DI first
    this.InitializeComponent();      // ✅ XAML second
}
```

---

## 🔍 Validation Checklist

Use this checklist to verify proper WinUI 3 DI setup:

- [ ] `App.xaml.cs` has `public static IServiceProvider Services` property
- [ ] `App.xaml.cs` has `public new static App Current` accessor
- [ ] `ConfigureServices()` is called in `App()` constructor BEFORE `InitializeComponent()`
- [ ] `OnLaunched()` creates and activates `MainWindow`
- [ ] `AppDbContext` is registered with `ServiceLifetime.Scoped`
- [ ] ViewModels are registered with `ServiceLifetime.Transient`
- [ ] Singleton services (Settings, Cache, API clients) are registered once
- [ ] Transient UI services (Navigation, Dialogs) can be resolved
- [ ] Critical services validation runs at startup
- [ ] All ViewModels receive dependencies via constructor injection

---

## 📖 References

1. **Microsoft Learn**: [Add dependency injection (WinUI 3)](https://learn.microsoft.com/en-us/windows/apps/tutorials/winui-mvvm-toolkit/dependency-injection)
2. **Microsoft Learn**: [.NET dependency injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
3. **Microsoft Learn**: [Entity Framework Core DbContext](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
4. **Microsoft Learn**: [WinUI 3 Application Lifecycle](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle)

---

## ✅ Resolution Summary

**Issue**: Missing `AppDbContext` registration in dependency injection.

**Root Cause**: `DashboardViewModel` constructor requires `AppDbContext`, but it wasn't registered in `DependencyInjection.ConfigureServices()`.

**Solution Applied**:
1. ✅ Added `AppDbContext` registration with Scoped lifetime
2. ✅ Configured SQLite connection string with fallback to LocalApplicationData
3. ✅ Added `AppDbContext` to critical services validation
4. ✅ Verified App.xaml.cs already follows Microsoft pattern correctly

**Files Modified**:
- `src/WileyWidget.WinUI/Configuration/DependencyInjection.cs` - Added DbContext registration

**Status**: ✅ **RESOLVED** - All services properly registered according to Microsoft documentation.
