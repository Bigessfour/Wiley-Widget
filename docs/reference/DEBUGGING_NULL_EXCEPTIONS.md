# Debugging ArgumentNullException & NullReferenceException Issues

## Current Issues Identified

You're experiencing two critical exceptions:

```
System.ArgumentNullException: Value cannot be null. (Parameter 'errorReporting')
System.NullReferenceException: Object reference not set to an instance of an object.
```

## Root Cause Analysis

### Issue 1: ArgumentNullException for 'errorReporting'

**Location**: `AILoggingService.cs:41`

```csharp
_errorReportingService = errorReportingService ?? throw new ArgumentNullException(nameof(errorReportingService));
```

**Cause**: `ErrorReportingService` is `null` when `AILoggingService` is being constructed.

**Why This Happens**:

1. DI registration order issue - `AILoggingService` is being resolved before `ErrorReportingService`
2. The service is registered at line 90 in `App.DependencyInjection.cs`:

   ```csharp
   containerRegistry.RegisterSingleton<Services.ErrorReportingService>();
   ```

3. But `AILoggingService` registration happens in `RegisterLazyAIServices()` which may execute before or with incomplete dependencies

### Issue 2: NullReferenceException

**Likely Locations**:

- Using `_errorReportingService` in `AILoggingService` when it's null
- Other services trying to use uninitialized dependencies

## How to Debug with "Debug Exceptions - Optimized" Configuration

### ✅ Your Configuration is Already Optimized

The **"Launch WileyWidget (Debug Exceptions - Optimized)"** configuration will **automatically break** on these exceptions because:

```jsonc
"System.ArgumentNullException": "always",  // ✅ Will catch errorReporting issue
"System.NullReferenceException": "always", // ✅ Will catch null reference issues
```

### Step-by-Step Debugging Process

#### 1. **Launch with Exception Debugging**

Press `F5` and select **"Launch WileyWidget (Debug Exceptions - Optimized)"**

#### 2. **Debugger Will Break Immediately**

When the `ArgumentNullException` is thrown, VS Code will:

- Stop at the **exact line** where `errorReporting` is null
- Show the **call stack** showing which service is trying to construct `AILoggingService`
- Display **variable values** in the DEBUG CONSOLE

#### 3. **Inspect the Call Stack**

Look for this pattern in CALL STACK panel:

```
AILoggingService..ctor(ILogger, ErrorReportingService) ← NULL HERE
└─ DryIoc.Container.Resolve<IAILoggingService>()
   └─ SomeOtherService..ctor(..., IAILoggingService)
      └─ RegisterLazyAIServices()
         └─ CreateContainerExtension()
```

#### 4. **Check Variables Panel**

Hover over `errorReportingService` parameter - it will show `null`

## Immediate Fix Options

### Option 1: Fix Registration Order (RECOMMENDED)

Ensure `ErrorReportingService` is registered **before** any service that depends on it:

**File**: `src/WileyWidget/App.DependencyInjection.cs`

```csharp
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    // Register critical services for exception handling and modules
    containerRegistry.RegisterSingleton<Services.ErrorReportingService>();  // ← FIRST

    // Then register services that depend on it
    RegisterLazyAIServices(containerRegistry);  // ← AILoggingService here

    // Other registrations...
}
```

### Option 2: Make ErrorReportingService Optional (TEMPORARY)

**File**: `src/WileyWidget.Services/AILoggingService.cs:41`

```csharp
// Allow null temporarily to prevent startup crash
_errorReportingService = errorReportingService;
// WARN if null
if (_errorReportingService == null)
{
    _logger.LogWarning("ErrorReportingService is null - telemetry will be disabled");
}
```

Then add null checks before using:

```csharp
_errorReportingService?.TrackEvent("AI_Query_Logged", ...);
```

### Option 3: Use Lazy<T> Dependency (ADVANCED)

**File**: `src/WileyWidget.Services/AILoggingService.cs`

```csharp
private readonly Lazy<ErrorReportingService> _errorReportingService;

public AILoggingService(
    ILogger<AILoggingService> logger,
    Lazy<ErrorReportingService> errorReportingService)  // ← Lazy wrapper
{
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _errorReportingService = errorReportingService ?? throw new ArgumentNullException(nameof(errorReportingService));
    // ...
}

// Usage:
_errorReportingService.Value.TrackEvent(...);
```

## Debugging Steps with VS Code

### 1. Set Conditional Breakpoint

**File**: `AILoggingService.cs:41`

- Right-click line 41 → **Add Conditional Breakpoint**
- Condition: `errorReportingService == null`
- This breaks **only when null** is passed

### 2. Enable Call Stack Logging

Add this to your optimized configuration:

```jsonc
"logging": {
    "moduleLoad": true,
    "exceptions": true,
    "programOutput": true,
    "engineLogging": true  // ← Enable for detailed DI resolution
}
```

### 3. Check DI Resolution Order

Add temporary logging in `RegisterTypes`:

```csharp
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    Log.Information("→ Registering ErrorReportingService");
    containerRegistry.RegisterSingleton<Services.ErrorReportingService>();

    Log.Information("→ Registering AILoggingService");
    RegisterLazyAIServices(containerRegistry);

    Log.Information("→ All services registered");
}
```

## Expected Debugger Behavior

When you run with **"Debug Exceptions - Optimized"**, you'll see:

### Break on ArgumentNullException

```
System.ArgumentNullException: Value cannot be null. (Parameter 'errorReporting')
   at WileyWidget.Services.AILoggingService..ctor(ILogger`1 logger, ErrorReportingService errorReportingService)
   at DryIoc.Container.ResolveAndCacheImplicitGenericTypeIfNotAlready(...)
```

### Variables Panel Will Show

```
▼ Local
  ▶ this = null (not constructed yet)
  ▼ logger = {Microsoft.Extensions.Logging.Logger<AILoggingService>}
  ▶ errorReportingService = null  ← THE PROBLEM
```

### Debug Console Will Show

```
Exception thrown: 'System.ArgumentNullException' in WileyWidget.Services.dll
Additional information: Value cannot be null. (Parameter 'errorReporting')
```

## Verification After Fix

After applying Option 1 (fix registration order):

1. **Clean and rebuild**:

   ```powershell
   dotnet clean
   dotnet build
   ```

2. **Run with Debug Exceptions** (F5)

3. **Should NOT break** - application starts normally

4. **Check Debug Console** for:

   ```
   ✓ ErrorReportingService registered
   ✓ AILoggingService initialized with dedicated Serilog file sink
   ```

## Additional Diagnostics

### Check Registration Order

Add this validation in `RegisterTypes`:

```csharp
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    var container = containerRegistry.GetContainer() as IContainer;

    // Register ErrorReportingService first
    containerRegistry.RegisterSingleton<Services.ErrorReportingService>();

    // Verify it's registered before proceeding
    if (!container.IsRegistered<Services.ErrorReportingService>())
    {
        throw new InvalidOperationException("ErrorReportingService must be registered first!");
    }

    // Now safe to register dependent services
    RegisterLazyAIServices(containerRegistry);
}
```

### Add Service Resolution Tracing

```csharp
containerRegistry.RegisterSingleton<Services.ErrorReportingService>();

// Test resolution immediately
try
{
    var test = containerRegistry.GetContainer().Resolve<Services.ErrorReportingService>();
    Log.Information("✓ ErrorReportingService resolved successfully");
}
catch (Exception ex)
{
    Log.Error(ex, "✗ Failed to resolve ErrorReportingService");
}
```

## Using the Optimized Debug Configuration

### Quick Start

1. Press **F5**
2. Select **"Launch WileyWidget (Debug Exceptions - Optimized)"** from dropdown
3. Application will **automatically break** at the null parameter
4. Inspect **CALL STACK** to see which service is causing the issue
5. Check **VARIABLES** to see what's null
6. Apply fix based on root cause

### What You'll See

```
┌─────────────────────────────────────┐
│ System.ArgumentNullException        │
│ Parameter name: errorReporting      │
│                                     │
│ at AILoggingService..ctor()        │
│ at DryIoc.Container.Resolve()      │
│ at RegisterLazyAIServices()        │
└─────────────────────────────────────┘

Variables:
  errorReportingService = null ← FIX THIS
```

## Summary

- ✅ **Your debug configuration is already optimized** for catching these exceptions
- ✅ **The issue is DI registration order** - `ErrorReportingService` needs to be registered before `AILoggingService`
- ✅ **Use "Debug Exceptions - Optimized"** configuration to pinpoint exact location
- ✅ **Fix by ensuring proper registration order** in `App.DependencyInjection.cs`

---

**Next Steps**:

1. Launch with **"Debug Exceptions - Optimized"**
2. Note the exact call stack when it breaks
3. Apply **Option 1** (registration order fix)
4. Verify with clean build and relaunch
