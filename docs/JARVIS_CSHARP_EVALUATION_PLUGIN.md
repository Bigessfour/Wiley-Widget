# JARVIS C# Evaluation Plugin

## Overview

The C# Evaluation Plugin provides JARVIS with the ability to dynamically evaluate C# code at runtime using Roslyn scripting. This powerful capability allows JARVIS to:

- Perform complex calculations and data transformations
- Inspect application state and runtime environment
- Explore loaded assemblies and types
- Execute diagnostic queries
- Test code snippets without recompilation
- Provide interactive programming assistance

## Architecture

### Plugin Location

- **File**: `src/WileyWidget.WinForms/Plugins/CSharpEvaluationPlugin.cs`
- **Namespace**: `WileyWidget.WinForms.Plugins`
- **Registration**: Auto-registered via `KernelPluginRegistrar`

### Dependencies

- `Microsoft.CodeAnalysis.CSharp.Scripting` (5.0.0)
- `Microsoft.SemanticKernel` (1.70.0)

## Available Functions

### 1. `evaluate_csharp`

**Description**: Evaluates C# code dynamically and returns the result.

**Parameters**:

- `code` (string, required): C# code to evaluate
- `timeoutSeconds` (int, optional): Execution timeout (default: 30s, max: 300s)

**Returns**: Formatted execution result or error message

**Safety Features**:

- Maximum code length: 100KB
- Timeout protection (1-300 seconds)
- Compilation validation before execution
- Exception handling with detailed error messages

**Example Usage**:

```
// Simple calculation
evaluate_csharp: 2 + 2 * 10

// LINQ query
evaluate_csharp: Enumerable.Range(1, 10).Where(x => x % 2 == 0).Sum()

// Multiple statements
evaluate_csharp: """
var numbers = new[] { 1, 2, 3, 4, 5 };
var squared = numbers.Select(n => n * n).ToArray();
return string.Join(", ", squared);
"""

// Inspect WileyWidget components
evaluate_csharp: """
var assemblies = AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.GetName().Name.StartsWith("WileyWidget"))
    .Select(a => a.GetName().Name)
    .ToArray();
return string.Join("\n", assemblies);
"""
```

### 2. `list_loaded_assemblies`

**Description**: Lists all assemblies currently loaded in the application domain.

**Parameters**: None

**Returns**: Formatted list of assembly names and versions

**Example Usage**:

```
list_loaded_assemblies
```

**Output Example**:

```
📦 Loaded Assemblies (147 total):

• mscorlib (v4.0.0.0)
• System (v4.0.0.0)
• WileyWidget.Abstractions (v1.0.0.0)
• WileyWidget.Business (v1.0.0.0)
• WileyWidget.Data (v1.0.0.0)
• WileyWidget.Models (v1.0.0.0)
• WileyWidget.Services (v1.0.0.0)
• WileyWidget.WinForms (v1.0.0.0)
• Syncfusion.Core.WinForms (v32.2.3.0)
...
```

### 3. `inspect_type`

**Description**: Inspects a .NET type and returns its members, properties, methods, and fields.

**Parameters**:

- `typeName` (string, required): Fully qualified type name

**Returns**: Detailed type information including interfaces, properties, methods, and fields

**Example Usage**:

```
// Inspect a WileyWidget type
inspect_type: WileyWidget.WinForms.Forms.MainForm

// Inspect a .NET type
inspect_type: System.String

// Inspect a Syncfusion control
inspect_type: Syncfusion.Windows.Forms.SfDataGrid
```

**Output Example**:

```
🔍 Type: WileyWidget.WinForms.Forms.MainForm
   Assembly: WileyWidget.WinForms
   Namespace: WileyWidget.WinForms.Forms
   Base Type: Form

📋 Interfaces (5):
   • IAsyncInitializable
   • IDisposable
   • IComponent
   • ISynchronizeInvoke
   • IWin32Window

📝 Properties (23):
   public Control[] Controls
   public DockingManager DockingManager
   public RibbonControlAdv Ribbon
   public StatusBarAdv StatusBar
   ...

🔧 Methods (15):
   public void ShowPanel(String panelName)
   public Task InitializeAsync(CancellationToken cancellationToken)
   public void ApplyTheme(String themeName)
   ...
```

### 4. `get_environment_info`

**Description**: Gets information about the current runtime environment.

**Parameters**: None

**Returns**: Detailed environment information (OS, .NET version, process stats, etc.)

**Example Usage**:

```
get_environment_info
```

**Output Example**:

```
🖥️ Runtime Environment:

   OS: Microsoft Windows NT 10.0.19045.0
   .NET Version: 10.0.2
   64-bit OS: True
   64-bit Process: True
   Processor Count: 16
   User: biges
   Machine: DESKTOP-ABC123
   Current Directory: C:\Users\biges\Desktop\Wiley-Widget
   System Directory: C:\WINDOWS\system32

📊 Process Info:
   Process Name: WileyWidget
   Process ID: 12345
   Working Set: 425 MB
   Private Memory: 398 MB
   Threads: 87
   Started: 2026-02-08 11:30:45
   Uptime: 00:15:32
```

## Pre-Imported Namespaces

Scripts automatically have access to these namespaces:

- `System`
- `System.Collections.Generic`
- `System.Linq`
- `System.Text`
- `System.Threading.Tasks`
- `System.IO`
- `System.Diagnostics`
- `Microsoft.Extensions.Logging`

## Pre-Referenced Assemblies

Scripts automatically reference these assemblies:

- System.Private.CoreLib
- System.Console
- System.Linq
- System.Collections
- System.Text
- System.IO
- System.Net.Http
- System.Threading
- WileyWidget.WinForms (current assembly)

## Script Globals

Scripts have access to a `ScriptGlobals` object with:

- `Logger` (ILogger): Logger instance for diagnostics
- `Context` (Dictionary<string, object>): Additional context data

**Usage in scripts**:

```csharp
// Log from within a script
Logger?.LogInformation("Hello from script!");

// Access context data
if (Context.TryGetValue("someKey", out var value))
{
    return value;
}
```

## Use Cases

### 1. Application State Inspection

**User**: "JARVIS, what panels are currently loaded?"

**JARVIS** (uses `evaluate_csharp`):

```csharp
var mainForm = Application.OpenForms.OfType<WileyWidget.WinForms.Forms.MainForm>().FirstOrDefault();
if (mainForm == null) return "MainForm not found";

var docking = mainForm.DockingManager;
var panels = docking.GetDockVisibility()
    .Where(kvp => kvp.Value)
    .Select(kvp => kvp.Key.Name)
    .ToArray();

return $"Active panels ({panels.Length}):\n" + string.Join("\n", panels.Select(p => $"• {p}"));
```

### 2. Performance Diagnostics

**User**: "JARVIS, check memory usage of the application"

**JARVIS** (uses `evaluate_csharp`):

```csharp
var process = Process.GetCurrentProcess();
var workingSet = process.WorkingSet64 / 1024.0 / 1024.0;
var privateMemory = process.PrivateMemorySize64 / 1024.0 / 1024.0;
var gcMemory = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

return $"""
Memory Usage:
• Working Set: {workingSet:N2} MB
• Private Memory: {privateMemory:N2} MB
• GC Heap: {gcMemory:N2} MB
• Gen 0 Collections: {GC.CollectionCount(0)}
• Gen 1 Collections: {GC.CollectionCount(1)}
• Gen 2 Collections: {GC.CollectionCount(2)}
""";
```

### 3. Configuration Inspection

**User**: "JARVIS, what's the current theme?"

**JARVIS** (uses `evaluate_csharp`):

```csharp
return $"Current theme: {SfSkinManager.ApplicationVisualTheme ?? "Not set"}";
```

### 4. Data Transformations

**User**: "JARVIS, calculate compound interest for $10,000 at 5% for 10 years"

**JARVIS** (uses `evaluate_csharp`):

```csharp
double principal = 10000;
double rate = 0.05;
int years = 10;
double finalAmount = principal * Math.Pow(1 + rate, years);
double interest = finalAmount - principal;

return $"""
Compound Interest Calculation:
• Principal: ${principal:N2}
• Annual Rate: {rate:P}
• Years: {years}
• Final Amount: ${finalAmount:N2}
• Total Interest: ${interest:N2}
""";
```

### 5. Type Discovery

**User**: "JARVIS, what properties does the MainForm class have?"

**JARVIS** (uses `inspect_type`):

```
inspect_type: WileyWidget.WinForms.Forms.MainForm
```

## Security Considerations

### Sandboxing

- Scripts run in the same process/AppDomain as the application
- No file system isolation (scripts can access any file the process can)
- No network isolation (scripts can make HTTP requests)
- **Trust Level**: Scripts should only be executed from trusted sources

### Safety Mechanisms

1. **Code Length Limit**: 100KB maximum
2. **Timeout Protection**: 1-300 seconds (default 30s)
3. **Compilation Validation**: Syntax errors caught before execution
4. **Exception Handling**: Runtime errors are caught and reported
5. **Resource Monitoring**: Memory and CPU usage subject to process limits

### Recommended Patterns

- ✅ **DO**: Use for diagnostic queries and inspections
- ✅ **DO**: Use for read-only operations
- ✅ **DO**: Use for calculations and data transformations
- ⚠️ **CAUTION**: Write operations (file I/O, database writes)
- ⚠️ **CAUTION**: Network operations (HTTP requests, external APIs)
- ❌ **DON'T**: Execute untrusted user input directly
- ❌ **DON'T**: Use for production-critical operations without validation

## Troubleshooting

### Common Issues

**Issue**: "Compilation Error: The type or namespace name 'X' could not be found"

- **Solution**: Add assembly reference or use fully qualified type names

**Issue**: "Execution timed out after 30 seconds"

- **Solution**: Increase timeout parameter or optimize code

**Issue**: "Runtime Error: NullReferenceException"

- **Solution**: Add null checks in script code

**Issue**: "Type 'X' not found in any loaded assembly"

- **Solution**: Use `list_loaded_assemblies` to verify assembly is loaded, or use fully qualified type name with assembly inference

### Debugging Tips

1. **Start Simple**: Test with simple expressions first (`2 + 2`)
2. **Check Compilation**: Look for compilation errors in response
3. **Add Logging**: Use `Logger?.LogInformation(...)` in scripts
4. **Inspect Types**: Use `inspect_type` to understand type structure
5. **Check Assemblies**: Use `list_loaded_assemblies` to verify dependencies

## Integration with JARVIS

### Auto-Registration

The plugin is automatically registered by `KernelPluginRegistrar` during Semantic Kernel initialization. No manual registration required.

### Example Conversation Flow

**User**: "JARVIS, can you check if Entity Framework is loaded?"

**JARVIS** (internal reasoning):

1. Use `list_loaded_assemblies` to get all assemblies
2. Filter for "EntityFramework" in assembly names
3. Return results to user

**JARVIS Response**: "Let me check the loaded assemblies..."

_(calls `list_loaded_assemblies`, filters results)_

**JARVIS**: "Yes, Entity Framework is loaded. I found these assemblies:
• Microsoft.EntityFrameworkCore (v10.0.2)
• Microsoft.EntityFrameworkCore.SqlServer (v10.0.2)
• Microsoft.EntityFrameworkCore.InMemory (v10.0.2)"

## Performance Considerations

### Execution Time

- Simple expressions: < 10ms
- Complex LINQ queries: 10-100ms
- Type inspections: 50-200ms
- Assembly listings: 100-500ms

### Memory Usage

- Script compilation: ~5-20 MB temporary
- Script execution: Depends on code (tracked in process memory)
- Result formatting: < 1 MB typically

### Best Practices

1. **Cache Compilation**: Reuse compiled scripts when possible
2. **Limit Output**: Truncate large collections (plugin does this automatically)
3. **Use Timeouts**: Always set reasonable timeouts for long-running code
4. **Avoid Infinite Loops**: Timeout protection helps, but be mindful
5. **Monitor Memory**: Large result sets can consume significant memory

## Future Enhancements

### Planned Features

- [ ] Script result caching and history
- [ ] Support for async script execution with progress reporting
- [ ] Enhanced security sandboxing (restricted file system access)
- [ ] Script libraries/snippets management
- [ ] Visualization support for complex data types
- [ ] Debugger integration (breakpoints, step-through)
- [ ] Script session persistence across JARVIS restarts

### Community Contributions

- Script templates for common diagnostic scenarios
- Pre-built methods for WileyWidget-specific tasks
- Integration with unit testing framework
- Performance profiling tools

## References

- [Roslyn Scripting API Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/scripting-api)
- [Semantic Kernel Plugin Development](https://learn.microsoft.com/en-us/semantic-kernel/agents/plugins/)
- [C# Script Engine Best Practices](https://github.com/dotnet/roslyn/wiki/Scripting-API-Samples)

## Version History

- **v1.0.0** (2026-02-08): Initial implementation with core evaluation capabilities
