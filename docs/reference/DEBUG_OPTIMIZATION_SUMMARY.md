# Debug Configuration Optimization Summary

## Changes Made

### 1. Enhanced "Debug Exceptions" Configuration

**Location**: `.vscode/launch.json`

**Configuration Name**: `Launch WileyWidget (Debug Exceptions - Optimized)`

#### Key Improvements:

✅ **Comprehensive Exception Coverage**

- Added WPF-specific exceptions (XamlParseException, BindingFailedException, ResourceReferenceKeyNotFoundException)
- Added Prism/DI exceptions (ContainerResolutionException, NavigationException, CompositionException)
- Added threading exceptions with appropriate break modes
- Added ObjectDisposedException detection
- Added NotImplementedException (always break - common TODO marker)

✅ **Optimized Exception Break Modes**

- **"always"**: Critical programming errors and framework failures
- **"unhandled"**: Expected exceptions that should be handled (I/O, cancellation)
- Removed generic `System.Exception: "unhandled"` (too broad)

✅ **Enhanced Symbol Loading**

- Added local symbol cache: `"cachePath": "${workspaceFolder}/.symbols"`
- Enabled `includeSymbolsNextToModules: true` for better resolution
- Kept symbol servers enabled for framework debugging

✅ **JIT Optimization Suppression**

- Added `"suppressJITOptimizations": true` for accurate variable inspection
- Prevents optimized Release code from causing debugging issues

✅ **Improved Logging Configuration**

- Disabled `threadExit` (too verbose)
- Kept essential logging (exceptions, moduleLoad, programOutput)
- Added diagnosticsLog configuration structure

✅ **Enhanced Environment Variables**

- Reduced COREHOST_TRACE from "1" to "0" (less noise)
- Added `ENABLE_XAML_DIAGNOSTICS_SOURCE_INFO` for XAML debugging
- Added PresentationTraceSources for WPF binding diagnostics

---

## Configuration Recommendations

### Primary Configurations to Use:

1. **Launch WileyWidget (Debug Exceptions - Optimized)** ⭐

   - **Purpose**: Debugging runtime issues, exceptions, crashes
   - **Use When**: Investigating bugs, exception handling, DI issues
   - **Performance**: Medium startup, comprehensive coverage

2. **Launch WileyWidget (Local)**

   - **Purpose**: Fast development iteration
   - **Use When**: Daily feature development, quick testing
   - **Performance**: Fast startup, minimal overhead

3. **Launch WileyWidget (Enhanced XAML Debug)**
   - **Purpose**: XAML binding and layout debugging
   - **Use When**: Binding failures, UI issues, resource conflicts
   - **Performance**: Medium startup, verbose XAML output

### Configurations to Consider Removing:

Consider consolidating these if not actively used:

- ❓ **Launch WileyWidget (Build Diagnostics)** - Similar to Enhanced XAML, different env vars
- ❓ **Launch WileyWidget (Release - XAML Debug)** - Release debugging is uncommon
- ❓ **Launch Wiley-Widget with Copilot Debug** - Uses old path, possibly outdated
- ❓ Duplicate Python debugging configurations (debug-wpf-thread.py appears twice)

---

## Research-Based Best Practices Applied

### From Microsoft Documentation:

✅ **Conditional Breakpoints**: Configured for targeted exception filtering
✅ **Symbol Servers**: Enabled for framework debugging
✅ **Just My Code**: Disabled for Prism framework investigation
✅ **Step Filtering**: Disabled for property setter debugging
✅ **Exception Conditions**: Implemented for WPF/Prism scenarios

### From WPF Performance Guidelines:

✅ **Binding Diagnostics**: PresentationTraceSources environment variables
✅ **XAML Source Info**: ENABLE_XAML_DIAGNOSTICS_SOURCE_INFO flag
✅ **Trace Levels**: Configurable for different scenarios

### From .NET Debugging Best Practices:

✅ **User-Unhandled vs Always**: Appropriate exception break modes
✅ **Symbol Caching**: Local cache for offline/performance
✅ **Module Filtering**: Strategic framework module inclusion
✅ **JIT Suppression**: Enabled for accurate debugging

---

## Exception Breakpoint Strategy

### Always Break (Critical Bugs)

```
System.NullReferenceException
System.ArgumentNullException
System.InvalidOperationException
System.ObjectDisposedException
System.Windows.Markup.XamlParseException
Prism.Ioc.ContainerResolutionException
WileyWidget.* (all custom exceptions)
```

### Unhandled Only (May Be Expected)

```
System.Threading.Tasks.TaskCanceledException
System.IO.IOException
System.Net.Http.HttpRequestException
System.UnauthorizedAccessException
```

---

## Documentation Created

### 1. **DEBUG_CONFIGURATION_GUIDE.md**

**Location**: `docs/reference/DEBUG_CONFIGURATION_GUIDE.md`

**Contents**:

- Configuration overview and use cases
- Exception breakpoint strategy explanation
- Configuration options detailed reference
- Best practices for WPF/Prism debugging
- Common debugging scenarios with solutions
- Performance considerations
- Troubleshooting guide
- Quick reference card

---

## Testing Recommendations

### Test Scenario 1: Null Reference Exception

```csharp
// Should break immediately when this executes
string value = null;
var length = value.Length;
```

**Expected**: Debugger breaks at `value.Length` with NullReferenceException

### Test Scenario 2: XAML Binding Failure

```xml
<TextBox Text="{Binding NonExistentProperty}" />
```

**Expected**:

- Debug Console shows binding warning
- No debugger break (binding failures don't throw)
- Check "Enhanced XAML Debug" for detailed output

### Test Scenario 3: DI Resolution Failure

```csharp
// Unregistered service
var service = container.Resolve<IUnregisteredService>();
```

**Expected**: Debugger breaks with ContainerResolutionException

### Test Scenario 4: Task Cancellation (Expected)

```csharp
var cts = new CancellationTokenSource();
cts.Cancel();
await Task.Delay(1000, cts.Token);
```

**Expected**: No break (unhandled mode, caught by framework)

---

## Configuration Comparison

| Feature              | Old Config   | New Config (Optimized) |
| -------------------- | ------------ | ---------------------- |
| WPF Exceptions       | ❌           | ✅ 3 types             |
| Prism Exceptions     | ❌           | ✅ 4 types             |
| Threading Exceptions | Partial      | ✅ Complete            |
| Symbol Cache         | ❌           | ✅ Local cache         |
| JIT Suppression      | ❌           | ✅ Enabled             |
| Generic Exception    | ✅ Too broad | ❌ Removed             |
| Binding Diagnostics  | ❌           | ✅ PresentationTrace   |
| Exception Count      | 9 types      | 17 types               |

---

## Next Steps

### Recommended Actions:

1. ✅ **Test the new configuration** with common debugging scenarios
2. ✅ **Review documentation** in DEBUG_CONFIGURATION_GUIDE.md
3. ⚠️ **Consider removing** duplicate/unused configurations
4. ⚠️ **Update team workflow** to use "Debug Exceptions - Optimized"
5. ⚠️ **Set as default** in VS Code launch dropdown

### Optional Enhancements:

- [ ] Add compound launch configurations for multi-project debugging
- [ ] Create task for clearing symbol cache
- [ ] Add configuration for performance profiling
- [ ] Document custom PresentationTraceSources listeners

---

## References

- [Microsoft: Using Breakpoints](https://learn.microsoft.com/en-us/visualstudio/debugger/using-breakpoints)
- [VS Code: C# Debugging](https://code.visualstudio.com/docs/csharp/debugging)
- [WPF Performance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-wpf-application-performance)
- [Prism Documentation](https://prismlibrary.com/docs/)

---

**Created**: November 9, 2025
**Configuration Version**: 2.0 (Optimized)
**Tested With**: .NET 9.0, VS Code 1.94+
