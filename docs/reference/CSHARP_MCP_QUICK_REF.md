# C# MCP Quick Reference

## 🎯 One-Liners

```powershell
# REPL Mode
.\scripts\eval-csharp.ps1 -Repl

# Evaluate File
.\scripts\eval-csharp.ps1 -File .\path\to\script.csx

# Quick Eval
.\scripts\eval-csharp.ps1 -Code "Console.WriteLine('Hello');"

# Test ViewModel
.\scripts\eval-csharp.ps1 -Test BudgetEntry
```

## 📋 VS Code Tasks

- `Ctrl+Shift+P` → `Tasks: Run Task`
  - `csharp-repl` - Interactive shell
  - `csharp-eval-file` - Run .csx file
  - `csharp-eval-code` - Quick one-liner
  - `csharp-test-viewmodel` - Test ViewModels

## 🎨 Code Snippets (in .csx files)

| Prefix       | What it does                   |
| ------------ | ------------------------------ |
| `csx-basic`  | Basic script template          |
| `csx-vm`     | ViewModel with PropertyChanged |
| `csx-async`  | Async/await operation          |
| `csx-linq`   | LINQ query                     |
| `csx-test`   | Test with assertions           |
| `csx-budget` | Budget entry model             |

## 💬 Copilot Integration

**Direct execution:**

```
Run this C# code using MCP: <your code>
```

**File execution:**

```
Run the C# script at scripts/examples/csharp/01-basic-test.csx
```

## 📁 Example Scripts

- `scripts/examples/csharp/01-basic-test.csx` - Basic operations
- `scripts/examples/csharp/02-viewmodel-test.csx` - Prism ViewModel
- `scripts/examples/csharp/03-async-test.csx` - Async patterns
- `scripts/examples/csharp/04-linq-test.csx` - LINQ queries

## 🐍 Python Integration

```bash
# Single file
python scripts/tools/csharp-eval.py -f script.csx

# All tests
python scripts/tools/csharp-eval.py -d scripts/examples/csharp/

# JSON output (CI/CD)
python scripts/tools/csharp-eval.py -d scripts/examples/csharp/ --json
```

## 🔨 Common Patterns

### ViewModel Test

```csharp
#r "nuget: Prism.Core, 9.0.537"
using Prism.Mvvm;

public class VM : BindableBase {
    private string _name;
    public string Name {
        get => _name;
        set => SetProperty(ref _name, value);
    }
}

var vm = new VM();
vm.Name = "Test";
```

### Async Test

```csharp
public async Task<string> Fetch() {
    await Task.Delay(100);
    return "Data";
}

var result = await Fetch();
Console.WriteLine(result);
```

### LINQ Query

```csharp
var data = new[] { 1, 2, 3, 4, 5 };
var result = data
    .Where(x => x > 2)
    .Select(x => x * 2)
    .ToList();
```

## 🎓 Full Documentation

See: `docs/CSHARP_MCP_TOOLKIT.md`
