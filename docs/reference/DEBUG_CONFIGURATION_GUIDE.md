# WileyWidget Debug Configuration Guide

## Overview

This guide explains the optimized debug configurations for the Wiley Widget WPF/Prism application, focusing on effective runtime issue diagnosis and exception handling.

## Recommended Debug Configurations

### 1. **Launch WileyWidget (Debug Exceptions - Optimized)** ⭐ RECOMMENDED

**Use Case**: Primary configuration for debugging runtime issues, exceptions, and application crashes.

**Key Features**:

- ✅ Comprehensive exception breakpoint coverage
- ✅ WPF-specific exception handling (XAML parsing, bindings)
- ✅ Prism/DI exception detection (container resolution, navigation)
- ✅ Enhanced symbol loading for framework code
- ✅ Optimal logging configuration

**When to Use**:

- Investigating null reference exceptions
- Debugging XAML parsing errors
- Troubleshooting dependency injection issues
- Analyzing Prism navigation failures
- Tracking down binding failures

### 2. **Launch WileyWidget (Local)**

**Use Case**: Standard development debugging with optimized performance.

**Key Features**:

- ✅ Fast startup with minimal symbol loading
- ✅ Excludes framework modules for performance
- ✅ Basic environment configuration

**When to Use**:

- Day-to-day development
- Quick testing of features
- Performance-sensitive debugging

### 3. **Launch WileyWidget (Enhanced XAML Debug)**

**Use Case**: Deep XAML and binding diagnostics.

**Key Features**:

- ✅ Maximum XAML trace output
- ✅ Detailed binding diagnostics
- ✅ Dependency property tracking
- ✅ Animation and routing strategy logging

**When to Use**:

- XAML binding issues
- Layout and rendering problems
- Resource dictionary conflicts
- Style/template debugging

---

## Exception Breakpoint Strategy

### Critical Runtime Exceptions (Always Break)

These exceptions **always** stop the debugger because they indicate serious bugs:

```jsonc
"System.NullReferenceException": "always",
"System.ArgumentNullException": "always",
"System.InvalidOperationException": "always",
"System.ArgumentException": "always",
"System.ObjectDisposedException": "always",
```

**Why**: These are programming errors that should never occur in production code.

### WPF-Specific Exceptions (Always Break)

WPF framework exceptions that need immediate attention:

```jsonc
"System.Windows.Markup.XamlParseException": "always",
"System.Windows.Data.BindingFailedException": "always",
"System.Windows.ResourceReferenceKeyNotFoundException": "always",
```

**Why**: XAML errors often cause UI failures or application crashes.

### Prism/DI Exceptions (Always Break)

Dependency injection and navigation framework exceptions:

```jsonc
"Prism.Ioc.ContainerResolutionException": "always",
"Prism.Navigation.NavigationException": "always",
"System.ComponentModel.Composition.CompositionException": "always",
"Microsoft.Extensions.DependencyInjection.ActivationException": "always",
```

**Why**: DI failures indicate configuration issues that prevent application startup.

### Threading/Async Exceptions (User Unhandled)

Async operation exceptions - break only if not handled:

```jsonc
"System.Threading.Tasks.TaskCanceledException": "unhandled",
"System.OperationCanceledException": "unhandled",
"System.Threading.SynchronizationLockException": "always",
```

**Why**: Cancellation is often intentional; lock violations are always bugs.

### I/O and Network Exceptions (User Unhandled)

Common exceptions that may be expected:

```jsonc
"System.IO.IOException": "unhandled",
"System.IO.FileNotFoundException": "unhandled",
"System.Net.Http.HttpRequestException": "unhandled",
"System.UnauthorizedAccessException": "unhandled",
```

**Why**: These may be handled gracefully in application code.

---

## Configuration Options Explained

### `justMyCode: false`

**What it does**: Allows stepping into framework and library code.

**When to use**:

- ✅ Debugging Prism framework behavior
- ✅ Investigating WPF binding internals
- ✅ Understanding DI container resolution
- ❌ Everyday development (slows debugging)

### `enableStepFiltering: false`

**What it does**: Disables automatic skipping of properties, operators, and compiler-generated code.

**When to use**:

- ✅ Debugging property setters with side effects
- ✅ Investigating INotifyPropertyChanged implementations
- ✅ Analyzing DependencyProperty callbacks
- ❌ Standard feature development

### `suppressJITOptimizations: true`

**What it does**: Prevents the JIT compiler from optimizing code, making debugging more accurate.

**When to use**:

- ✅ Variables showing incorrect values
- ✅ Breakpoints being skipped
- ✅ Stack traces appearing incorrect
- ❌ Performance testing (introduces overhead)

### `symbolOptions.searchMicrosoftSymbolServer: true`

**What it does**: Downloads symbols for Microsoft framework code for better stack traces.

**When to use**:

- ✅ Exception occurring in framework code
- ✅ Need detailed call stacks
- ✅ Investigating framework bugs
- ❌ Offline development (requires internet)

---

## Best Practices

### 1. **Start with the Right Configuration**

| Scenario               | Configuration                |
| ---------------------- | ---------------------------- |
| App crashes on startup | Debug Exceptions - Optimized |
| Binding not working    | Enhanced XAML Debug          |
| Performance testing    | Local                        |
| Navigation failures    | Debug Exceptions - Optimized |
| UI layout issues       | Enhanced XAML Debug          |

### 2. **Use Conditional Breakpoints**

For frequently hit code:

```csharp
// Break only when condition is true
if (viewModel.SelectedItem == null) // <-- Set condition here
{
    throw new InvalidOperationException("No item selected");
}
```

**In VS Code**: Right-click breakpoint → Edit Condition → `viewModel.SelectedItem == null`

### 3. **Use Logpoints for Tracing**

Instead of `Debug.WriteLine()`:

```
Right-click line → Add Logpoint → {variableName} = {variableValue}
```

### 4. **Exception Conditions**

Filter exceptions by type:

```
System.NullReferenceException, System.InvalidOperationException
```

Or exclude specific exceptions:

```
!System.Threading.Tasks.TaskCanceledException
```

### 5. **Watch Window Expressions**

Useful expressions for WPF/Prism debugging:

```csharp
// View current DataContext
((FrameworkElement)this).DataContext

// Check DI container registrations
((IContainerProvider)Container).Resolve<IMyService>()

// Binding diagnostics
BindingOperations.GetBindingExpression(myControl, TextBox.TextProperty).Status

// Current dispatcher thread
System.Threading.Thread.CurrentThread.Name
```

---

## Environment Variables for Enhanced Debugging

### WPF Tracing

```json
"PresentationTraceSources.TraceLevel": "High",
"PresentationTraceSources.DataBindingSource": "All"
```

**Output**: Detailed binding information in Debug Console.

### XAML Diagnostics

```json
"ENABLE_XAML_DIAGNOSTICS_SOURCE_INFO": "1"
```

**Output**: Source file information for XAML elements.

### CoreCLR Tracing

```json
"COREHOST_TRACE": "1"  // Enable for startup issues
"COREHOST_TRACE": "0"  // Disable for normal debugging
```

**Output**: Assembly loading and resolution diagnostics.

---

## Common Debugging Scenarios

### Scenario 1: "Binding Not Working"

1. **Use**: "Enhanced XAML Debug" configuration
2. **Check**: Debug Console for binding warnings:
   ```
   System.Windows.Data Warning: 40 : BindingExpression path error:
   'PropertyName' property not found on 'object' ''ViewModelType'
   ```
3. **Verify**:
   - Property exists on ViewModel
   - Property implements INotifyPropertyChanged
   - DataContext is set correctly

### Scenario 2: "Null Reference on Navigation"

1. **Use**: "Debug Exceptions - Optimized" configuration
2. **Debugger stops at**: `System.NullReferenceException` (always break)
3. **Check**:
   - Call stack for navigation context
   - ViewModel constructor dependencies
   - INavigationAware implementation

### Scenario 3: "DI Container Resolution Failure"

1. **Use**: "Debug Exceptions - Optimized" configuration
2. **Debugger stops at**: `ContainerResolutionException` (always break)
3. **Check**:
   - Service registration in `RegisterTypes()`
   - Constructor parameter types
   - Circular dependencies

### Scenario 4: "App Crashes on Startup - No Info"

1. **Use**: "Debug Exceptions - Optimized" configuration
2. **Enable**: `"stopAtEntry": true` (temporarily)
3. **Step through**: Application initialization
4. **Monitor**: Exception breakpoints window
5. **Check**:
   - Module load events in Debug Console
   - First-chance exceptions
   - Static constructor failures

---

## Symbol Server Configuration

### Local Symbol Cache

```json
"cachePath": "${workspaceFolder}/.symbols"
```

**Benefit**: Faster subsequent debugging sessions, no re-download.

### Symbol Search Order

1. Local symbol cache (`.symbols/` folder)
2. Next to module (`.pdb` next to `.dll`)
3. NuGet.org symbol server
4. Microsoft symbol server

---

## Performance Considerations

| Configuration                | Startup Time | Symbol Load Time | Best For     |
| ---------------------------- | ------------ | ---------------- | ------------ |
| Local                        | Fast         | Fast             | Development  |
| Debug Exceptions - Optimized | Medium       | Medium           | Bug fixing   |
| Enhanced XAML Debug          | Medium       | Medium           | UI debugging |

**Tips**:

- Disable symbol servers when offline
- Use local symbols cache
- Exclude unnecessary modules
- Disable `COREHOST_TRACE` for normal debugging

---

## Troubleshooting

### "Breakpoints Not Hit"

1. Check symbol loading: Debug Console → "Loaded symbols for..."
2. Verify build configuration: Debug vs Release
3. Ensure `justMyCode: false` if debugging framework code
4. Check `suppressJITOptimizations: true` for optimized code

### "Too Many Exception Breaks"

1. Add exception conditions to filter types
2. Change `"always"` to `"unhandled"` for specific exceptions
3. Use `!ExceptionType` to exclude known exceptions

### "Slow Debugging Performance"

1. Switch to "Local" configuration for standard development
2. Disable symbol servers: `searchMicrosoftSymbolServer: false`
3. Add framework modules to `excludedModules`
4. Set `COREHOST_TRACE: "0"`

### "Missing Stack Traces"

1. Enable: `searchMicrosoftSymbolServer: true`
2. Enable: `searchNuGetOrgSymbolServer: true`
3. Set: `moduleFilter.mode: "loadAllButExcluded"`
4. Clear and rebuild: `dotnet clean && dotnet build`

---

## References

- [VS Code C# Debugging Documentation](https://code.visualstudio.com/docs/csharp/debugging)
- [Microsoft Debugging Best Practices](https://learn.microsoft.com/en-us/visualstudio/debugger/using-breakpoints)
- [WPF Performance Optimization](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-wpf-application-performance)
- [Prism Documentation](https://prismlibrary.com/docs/)

---

## Quick Reference Card

### Exception Breakpoint Quick Commands

| Exception Type               | Break Mode | Reason                 |
| ---------------------------- | ---------- | ---------------------- |
| NullReferenceException       | Always     | Programming error      |
| XamlParseException           | Always     | XAML syntax error      |
| ContainerResolutionException | Always     | DI configuration error |
| TaskCanceledException        | Unhandled  | May be intentional     |
| IOException                  | Unhandled  | May be handled         |

### Keyboard Shortcuts

| Action            | Shortcut      |
| ----------------- | ------------- |
| Start Debugging   | F5            |
| Step Over         | F10           |
| Step Into         | F11           |
| Step Out          | Shift+F11     |
| Continue          | F5            |
| Stop Debugging    | Shift+F5      |
| Restart           | Ctrl+Shift+F5 |
| Toggle Breakpoint | F9            |

---

**Last Updated**: November 9, 2025
**Applies To**: Wiley Widget WPF Application (.NET 9.0)
**Recommended Configuration**: "Launch WileyWidget (Debug Exceptions - Optimized)"
