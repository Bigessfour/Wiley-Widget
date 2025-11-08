# C# MCP Evaluation Toolkit

Complete toolkit for iterative C# development and testing using the C# MCP server.

## 🚀 Quick Start

### Using VS Code Tasks (Recommended)

Press `Ctrl+Shift+P` and run:

- **C# REPL**: `Tasks: Run Task` → `csharp-repl` - Interactive C# shell
- **Evaluate File**: `Tasks: Run Task` → `csharp-eval-file` - Run a .csx file
- **Quick Eval**: `Tasks: Run Task` → `csharp-eval-code` - Evaluate one-liner
- **Test ViewModel**: `Tasks: Run Task` → `csharp-test-viewmodel` - Test Prism ViewModels

### Using Copilot (Direct Execution)

Ask Copilot:

```
Run this C# code using MCP: Console.WriteLine("Hello World!");
```

Or for files:

```
Run the C# script at scripts/examples/csharp/01-basic-test.csx
```

## 📁 Project Structure

```
Wiley_Widget/
├── scripts/
│   ├── eval-csharp.ps1           # PowerShell helper script
│   ├── csharp-eval.py            # Python integration (see scripts/tools)
│   └── examples/csharp/          # Example .csx files
│       ├── 01-basic-test.csx     # Basic C# operations
│       ├── 02-viewmodel-test.csx # Prism ViewModel testing
│       ├── 03-async-test.csx     # Async/await patterns
│       └── 04-linq-test.csx      # LINQ queries
└── .vscode/
    ├── tasks.json                # VS Code tasks
    └── csharp-mcp.code-snippets  # Code snippets
```

## 🛠️ Tools & Methods

### 1. PowerShell Script (`eval-csharp.ps1`)

**File Execution:**

```powershell
.\scripts\eval-csharp.ps1 -File .\scripts\examples\csharp\01-basic-test.csx
```

**Quick Evaluation:**

```powershell
.\scripts\eval-csharp.ps1 -Code "Console.WriteLine('Hello');"
```

**REPL Mode:**

```powershell
.\scripts\eval-csharp.ps1 -Repl
```

**ViewModel Tests:**

```powershell
.\scripts\eval-csharp.ps1 -Test BudgetEntry
.\scripts\eval-csharp.ps1 -Test Municipal
```

### 2. Python Integration (`csharp-eval.py`)

**Evaluate a file:**

```bash
python scripts/tools/csharp-eval.py -f scripts/examples/csharp/01-basic-test.csx
```

**Run all tests in directory:**

```bash
python scripts/tools/csharp-eval.py -d scripts/examples/csharp/
```

**Quick evaluation:**

```bash
python scripts/tools/csharp-eval.py -c "Console.WriteLine('Hello');"
```

**JSON output (for CI/CD):**

```bash
python scripts/tools/csharp-eval.py -d scripts/examples/csharp/ --json
```

### 3. Direct MCP Tool (via Copilot)

The `mcp_csharp-mcp_eval_c_sharp` tool can be invoked directly:

```typescript
mcp_csharp -
  mcp_eval_c_sharp({
    csx: "Console.WriteLine('Hello World!');",
    timeoutSeconds: 30,
  });
```

## 📝 Code Snippets

Type these prefixes in `.csx` files:

| Prefix       | Description              | Use Case                                |
| ------------ | ------------------------ | --------------------------------------- |
| `csx-basic`  | Basic C# script template | General purpose scripts                 |
| `csx-vm`     | Prism ViewModel test     | Testing ViewModels with PropertyChanged |
| `csx-async`  | Async/await template     | Testing async operations                |
| `csx-linq`   | LINQ query template      | Data queries and transformations        |
| `csx-test`   | Simple test assertions   | Unit-style testing                      |
| `csx-budget` | Budget entry model       | Wiley Widget specific                   |

## 🎯 Use Cases

### 1. Quick ViewModel Testing

```csharp
#r "nuget: Prism.Core, 9.0.537"

using Prism.Mvvm;

public class MyViewModel : BindableBase {
    private string _name;
    public string Name {
        get => _name;
        set => SetProperty(ref _name, value);
    }
}

var vm = new MyViewModel();
vm.PropertyChanged += (s, e) => Console.WriteLine($"Changed: {e.PropertyName}");
vm.Name = "Test";
```

### 2. Async Service Testing

```csharp
public class DataService {
    public async Task<string> FetchAsync() {
        await Task.Delay(100);
        return "Data";
    }
}

var service = new DataService();
var result = await service.FetchAsync();
Console.WriteLine(result);
```

### 3. LINQ Data Analysis

```csharp
var budgets = new[] {
    new { Dept = "Police", Amount = 1_000_000m },
    new { Dept = "Fire", Amount = 800_000m }
};

var total = budgets.Sum(b => b.Amount);
Console.WriteLine($"Total: ${total:N2}");
```

### 4. Integration Testing

```csharp
// Test multiple components together
var viewModel = new BudgetEntryViewModel();
var service = new BudgetService();

var data = await service.FetchDataAsync();
viewModel.LoadData(data);

Assert(viewModel.TotalBudget > 0, "Budget loaded");
```

## 🔄 Workflow Integration

### Daily Development

1. **Morning**: Run all example tests to verify setup

   ```powershell
    python scripts/tools/csharp-eval.py -d scripts/examples/csharp/
   ```

2. **During Development**: Use REPL for quick experiments

   ```powershell
   .\scripts\eval-csharp.ps1 -Repl
   ```

3. **Before Commit**: Test ViewModels
   ```powershell
   .\scripts\eval-csharp.ps1 -Test BudgetEntry
   ```

### CI/CD Integration

Add to your workflow:

```yaml
- name: C# MCP Tests
  run: |
    python scripts/tools/csharp-eval.py -d scripts/examples/csharp/ --json > csharp-test-results.json
```

### Debugging

1. Create a `.csx` file with your test case
2. Run via task or command line
3. Iterate quickly without full build cycle
4. Once working, integrate into main codebase

## 🎨 Example Scripts

### 01-basic-test.csx

- Variables and types
- Collections and LINQ
- String manipulation
- DateTime operations

### 02-viewmodel-test.csx

- Prism BindableBase
- PropertyChanged notifications
- Computed properties
- Budget variance calculations

### 03-async-test.csx

- Task-based async operations
- Sequential vs parallel execution
- Error handling
- Performance comparison

### 04-linq-test.csx

- Complex LINQ queries
- Grouping and aggregation
- Filtering and sorting
- Budget data analysis

## 💡 Tips & Best Practices

### 1. NuGet Packages

Add packages at the top of your script:

```csharp
#r "nuget: Prism.Core, 9.0.537"
#r "nuget: Newtonsoft.Json, 13.0.3"
```

### 2. Using Statements

Always include necessary namespaces:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
```

### 3. Return Values

Scripts can return values for verification:

```csharp
return "Success"; // String
return 42;        // Number
return true;      // Boolean
```

### 4. Console Output

Use Console.WriteLine for debugging:

```csharp
Console.WriteLine($"Value: {myValue}");
```

### 5. Error Handling

Add try-catch for robust scripts:

```csharp
try {
    // Your code
} catch (Exception ex) {
    Console.WriteLine($"Error: {ex.Message}");
    return "Failed";
}
```

## 🔧 Troubleshooting

### Issue: "NuGet package not found"

- Check package name and version
- Ensure internet connectivity
- Try a different version

### Issue: "Timeout"

- Increase timeout: `-Timeout 60`
- Optimize your code
- Check for infinite loops

### Issue: "Compilation error"

- Check syntax
- Verify using statements
- Ensure all types are defined

## 📚 Additional Resources

- [C# Scripting Documentation](https://docs.microsoft.com/en-us/archive/msdn-magazine/2016/january/essential-net-csharp-scripting)
- [Prism Documentation](https://prismlibrary.com/docs/)
- [LINQ Query Syntax](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/)

## 🎉 Next Steps

1. ✅ Run the example scripts
2. ✅ Try the REPL mode
3. ✅ Create your own .csx files
4. ✅ Integrate with your workflow
5. ✅ Share with your team

Happy C# scripting! 🚀
