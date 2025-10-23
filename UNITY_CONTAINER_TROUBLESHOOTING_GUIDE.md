# Unity Container TargetInvocationException Troubleshooting Guide

## Overview

This guide provides comprehensive solutions for diagnosing and resolving `System.Reflection.TargetInvocationException` in Unity Container with Prism WPF applications. These exceptions often hide the real cause deep in the InnerException chain.

## Common Causes

### 1. Missing Service Registrations
**Symptoms**: `ResolutionFailedException` wrapped in `TargetInvocationException`
**Common in**: ViewModel constructors, module initialization

### 2. Circular Dependencies
**Symptoms**: StackOverflowException or ResolutionFailedException
**Common in**: Services with mutual dependencies

### 3. Invalid Constructor Parameters
**Symptoms**: Type resolution failures
**Common in**: Complex object graphs with optional dependencies

### 4. Module Initialization Failures
**Symptoms**: `ModuleInitializeException` during startup
**Common in**: Prism module OnInitialized methods

## Diagnostic Tools

### 1. Enhanced Global Exception Handling

The application now includes comprehensive exception unwrapping:

```csharp
private static Exception UnwrapTargetInvocationException(Exception? exception)
{
    var current = exception;
    var seen = new HashSet<Exception>();

    // Unwrap TargetInvocationException chain
    while (current is TargetInvocationException tie && tie.InnerException != null && !seen.Contains(tie))
    {
        seen.Add(tie);
        current = tie.InnerException;
    }

    return current;
}
```

### 2. Container Resolution Diagnostics

In DEBUG mode, the application automatically tests critical service resolutions:

```csharp
var diagnosticReport = UnityContainerDiagnostics.TestContainerResolutions(containerRegistry, testMode);
```

### 3. Exception Chain Logging

All exceptions are logged with full chain analysis:

```
[0] TargetInvocationException: Exception has been thrown by the target of an invocation.
[1] ResolutionFailedException: Resolution of the dependency failed, type = "IService", name = "(none)".
[2] InvalidOperationException: Service not registered in container
```

## Resolution Strategies

### Strategy 1: Check Service Registrations

**Problem**: Services not registered in `RegisterTypes()`

**Solution**:
1. Check that all required interfaces are registered
2. Verify registration order (dependencies first)
3. Use container diagnostics to identify missing registrations

**Example**:
```csharp
// In RegisterTypes()
containerRegistry.RegisterSingleton<IMyService, MyService>();
containerRegistry.RegisterSingleton<IDependentService, DependentService>();
```

### Strategy 2: Resolve Circular Dependencies

**Problem**: Services have circular constructor dependencies

**Solutions**:
1. Use property injection instead of constructor injection
2. Introduce a third service to break the cycle
3. Use lazy resolution

**Example**:
```csharp
// Instead of constructor injection
public MyService(IOtherService other) { }

// Use property injection
public MyService() { }
[Dependency]
public IOtherService OtherService { get; set; }
```

### Strategy 3: Handle Optional Dependencies

**Problem**: Services fail when optional dependencies are missing

**Solution**:
```csharp
// Use named registrations for optional services
if (container.IsRegistered<IOptionalService>())
{
    containerRegistry.RegisterSingleton<IMyService, MyServiceWithOptional>();
}
else
{
    containerRegistry.RegisterSingleton<IMyService, MyServiceBasic>();
}
```

### Strategy 4: Debug Module Initialization

**Problem**: Modules fail during initialization

**Solution**:
1. Check module dependencies in `ModuleCatalog`
2. Ensure all required services are available before module initialization
3. Use try-catch in `OnInitialized()` methods

**Example**:
```csharp
public class MyModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        try
        {
            var service = containerProvider.Resolve<IMyService>();
            // Module initialization logic
        }
        catch (Exception ex)
        {
            Log.Error(ex.GetRootException(), "Module initialization failed");
            throw;
        }
    }
}
```

## Testing Container Registrations

### Manual Testing in Debug Mode

```csharp
[Test]
public void Container_CanResolveAllServices()
{
    // Setup container
    var containerExt = CreateContainerExtension();
    RegisterTypes(containerExt);

    // Test critical resolutions
    var diagnostics = UnityContainerDiagnostics.TestContainerResolutions(containerExt, testMode: false);
    Assert.False(diagnostics.HasFailures, $"Container resolution failures: {string.Join(", ", diagnostics.FailedResolutions.Select(f => f.ServiceName))}");
}
```

### Runtime Validation

The application automatically validates critical services during startup:

```csharp
private void ValidateCriticalServices(IContainerRegistry containerRegistry, bool testMode)
{
    var criticalServices = new[]
    {
        ("IConfiguration", typeof(IConfiguration)),
        ("ISettingsService", typeof(ISettingsService)),
        // ... more services
    };

    foreach (var (serviceName, serviceType) in criticalServices)
    {
        try
        {
            var service = containerRegistry.GetContainer().Resolve(serviceType);
            Assert.NotNull(service);
        }
        catch (Exception ex)
        {
            Log.Error(ex.GetRootException(), $"Critical service {serviceName} failed to resolve");
            throw;
        }
    }
}
```

## Syncfusion and AI Integration Issues

### Syncfusion License Issues

**Problem**: Syncfusion controls fail to initialize due to license issues

**Symptoms**:
- XamlParseException with license messages
- TargetInvocationException during UI loading

**Solution**:
```csharp
private void EnsureSyncfusionLicenseRegistered()
{
    try
    {
        // Ensure license is registered before any Syncfusion controls are used
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("YOUR_LICENSE_KEY");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to register Syncfusion license");
        // Continue without license - may show dialogs
    }
}
```

### AI Service Initialization

**Problem**: AI services fail during container resolution

**Symptoms**:
- HttpClient configuration issues
- Authentication failures

**Solution**:
```csharp
private void RegisterHttpClientServices(IContainerRegistry containerRegistry, IConfiguration configuration)
{
    // Register HttpClient with proper configuration
    containerRegistry.RegisterSingleton<HttpClient>(provider =>
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "WileyWidget/1.0");
        return client;
    });

    // Register AI services with error handling
    try
    {
        containerRegistry.RegisterSingleton<IAIService, AIService>();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "AI service registration failed - continuing without AI features");
    }
}
```

## Best Practices

### 1. Exception Handling in Service Constructors

```csharp
public class MyService : IMyService
{
    public MyService(ILogger<MyService> logger, IConfiguration config)
    {
        try
        {
            // Validate dependencies
            if (config == null) throw new ArgumentNullException(nameof(config));

            // Service initialization
            InitializeService();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize {ServiceName}", nameof(MyService));
            throw new InvalidOperationException($"Service initialization failed: {ex.GetRootException().Message}", ex);
        }
    }
}
```

### 2. Defensive Registration

```csharp
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    try
    {
        // Register core services first
        RegisterCoreServices(containerRegistry);

        // Register feature services with error handling
        RegisterFeatureServices(containerRegistry);

        // Validate registrations
        ValidateCriticalServices(containerRegistry, testMode);
    }
    catch (Exception ex)
    {
        Log.Fatal(ex.GetRootException(), "Container registration failed");
        throw;
    }
}
```

### 3. Container Validation (CORRECTED)

**Note**: Unity Container does not have a built-in `Verify()` method. Instead, use runtime resolution testing:

```csharp
private void ValidateContainerRegistrations(IUnityContainer container)
{
    // Test resolution of critical services
    var criticalTypes = new[]
    {
        typeof(IConfiguration),
        typeof(ISettingsService),
        typeof(IRegionManager),
        typeof(IEventAggregator)
    };

    foreach (var type in criticalTypes)
    {
        try
        {
            var instance = container.Resolve(type);
            Log.Debug($"✓ Successfully resolved {type.Name}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"✗ Failed to resolve {type.Name}: {ex.GetRootException().Message}");
            throw;
        }
    }
}
```

## Troubleshooting Checklist

1. **Check Inner Exceptions**: Always examine `ex.InnerException` chain
2. **Verify Registrations**: Use diagnostics to confirm all services are registered
3. **Test in Isolation**: Resolve services individually to isolate failures
4. **Check Dependencies**: Ensure all constructor parameters are registered
5. **Review Module Order**: Check `ModuleCatalog` for correct initialization order
6. **Validate Configuration**: Ensure all required configuration values are present

## Common Error Patterns

### Pattern 1: Missing ILogger Registration
```
TargetInvocationException -> ResolutionFailedException -> InvalidOperationException: ILogger<> not registered
```
**Fix**: Register `ILoggerFactory` and `ILogger<>` in `RegisterTypes()`

### Pattern 2: Circular Dependency
```
TargetInvocationException -> ResolutionFailedException -> StackOverflowException
```
**Fix**: Use property injection or introduce dependency breaking service

### Pattern 3: Module Initialization Failure
```
ModuleInitializeException -> TargetInvocationException -> ResolutionFailedException
```
**Fix**: Check module dependencies and service availability order

## Debug Commands

### Enable Verbose Logging
```powershell
# Set environment variable for verbose Unity logging
$env:UNITY_DIAGNOSTICS = "Verbose"
```

### Test Individual Resolutions
```csharp
// In debug console or test
var container = app.Container.GetContainer();
var service = container.Resolve<IMyService>(); // Check for exceptions
```

### Validate at Runtime
```csharp
// Add to App.xaml.cs for runtime validation
var diagnostics = UnityContainerDiagnostics.TestContainerResolutions(Container, testMode: false);
diagnostics.LogSummary();
```

This comprehensive approach ensures that Unity container issues are caught early, diagnosed accurately, and resolved systematically.

---

## Validation Against Online Resources (October 2025)

This guide has been validated against current online resources and Unity/Prism documentation:

### ✅ **Confirmed Accurate**
- **ResolutionFailedException causes**: Missing registrations, circular dependencies, invalid constructor parameters
- **ContainerResolutionException**: Valid Prism exception type for DI resolution failures
- **Exception unwrapping**: TargetInvocationException commonly wraps Unity resolution failures
- **Property injection for circular dependencies**: Standard Unity/Prism best practice
- **Module initialization failures**: Common cause of startup exceptions

### ❌ **Corrections Made**
- **Container.Verify() method**: **DOES NOT EXIST** in Unity Container API. Replaced with runtime resolution testing.
- **Unity.Diagnostics.UnityDiagnosticExtension**: Not a standard Unity extension. Use custom diagnostic approaches instead.

### 🔍 **Additional Findings**
- **Prism ContainerResolutionException**: Real exception type used in Prism for container resolution failures
- **Unity container validation**: Must be done through manual resolution testing, not built-in verification
- **Circular dependency detection**: Best handled through property injection or service locator patterns
- **Module loading order**: Critical for preventing resolution failures during initialization

### 📚 **Recommended Approach**
1. Use runtime resolution testing instead of non-existent `Verify()` method
2. Implement comprehensive exception unwrapping for all TargetInvocationException instances
3. Test critical service resolutions during startup validation
4. Use property injection to break circular dependencies
5. Validate module dependencies before initialization
