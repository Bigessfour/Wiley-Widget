# Direct C# MCP Testing Guide

**Version**: 1.0
**Effective Date**: November 8, 2025
**Status**: Official Testing Method

## Overview

This guide demonstrates the **approved method** for exploratory testing in Wiley Widget using the **C# MCP (Model Context Protocol)** evaluation tool. This approach eliminates script file overhead and provides instant feedback during development.

## Key Concepts

### What is Direct MCP Testing?

Instead of creating `.csx` script files, the AI assistant directly invokes the `mcp_csharp-mcp_eval_c_sharp` tool with inline C# code during conversation. This provides:

- **Instant execution** (<1 second response)
- **Zero file overhead** (no create/save/cleanup)
- **Iterative refinement** (adjust and retest immediately)
- **Context preservation** (full conversation history maintained)
- **Natural workflow** (test while discussing code)

### When to Use Direct MCP Testing

✅ **Use for**:

- Exploring new ViewModels, Services, or Models
- Validating edge cases and error handling
- Testing serialization/deserialization
- Prototyping async patterns
- Mocking dependencies with Moq
- Quick property change validation
- Pre-xUnit exploration of functionality

❌ **Don't use for**:

- Regression testing (use xUnit)
- CI/CD pipelines (use xUnit)
- Performance benchmarking (use BenchmarkDotNet)
- Integration tests requiring real databases (use xUnit + TestContainers)

## Capabilities

### Available Features

| Feature                    | Supported  | Example                         |
| -------------------------- | ---------- | ------------------------------- |
| **NuGet Packages**         | ✅ Yes     | `#r "nuget: Moq, 4.20.70"`      |
| **Async/Await**            | ✅ Yes     | `await Task.Delay(10)`          |
| **LINQ**                   | ✅ Yes     | `items.Where(x => x > 5).Sum()` |
| **Mocking (Moq)**          | ✅ Yes     | `new Mock<IService>()`          |
| **JSON (Newtonsoft)**      | ✅ Yes     | `JsonConvert.SerializeObject()` |
| **INotifyPropertyChanged** | ✅ Yes     | Simulate ViewModels             |
| **Exception Handling**     | ✅ Yes     | `try/catch` blocks              |
| **Custom Classes**         | ✅ Yes     | Define inline                   |
| **File I/O**               | ⚠️ Limited | Read-only, specific paths       |
| **WPF Controls**           | ❌ No      | Headless environment            |
| **EF Core**                | ⚠️ Partial | In-memory only                  |

### Environment Details

- **Runtime**: .NET 9.0.8 (as of Nov 2025)
- **Execution Context**: Docker container (`/app` working directory)
- **Timeout**: 30 seconds default
- **Output**: Console stdout/stderr returned to AI assistant

## Testing Patterns

### Pattern 1: ViewModel Property Testing

**Scenario**: Validate `INotifyPropertyChanged` implementation

```csharp
using System;
using System.ComponentModel;

public class TestViewModel : INotifyPropertyChanged
{
    private string _name;
    public event PropertyChangedEventHandler PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
    }
}

var vm = new TestViewModel();
var eventFired = false;
var propertyName = "";

vm.PropertyChanged += (s, e) =>
{
    eventFired = true;
    propertyName = e.PropertyName;
};

vm.Name = "Test";

Console.WriteLine($"✓ Event fired: {eventFired}");
Console.WriteLine($"✓ Property name: {propertyName}");
Console.WriteLine($"✓ Value: {vm.Name}");
```

**Expected Output**:

```
✓ Event fired: True
✓ Property name: Name
✓ Value: Test
```

### Pattern 2: Async Command Execution

**Scenario**: Test async operations with error handling

```csharp
using System;
using System.Threading.Tasks;

public class CommandTest
{
    public async Task<int> ExecuteAsync(int? input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        await Task.Delay(10); // Simulate async work
        return input.Value * 2;
    }
}

var command = new CommandTest();

// Test success path
var result = await command.ExecuteAsync(5);
Console.WriteLine($"✓ Success: {result} (expected 10)");

// Test error path
try
{
    await command.ExecuteAsync(null);
    Console.WriteLine("✗ FAIL: Should have thrown");
}
catch (ArgumentNullException)
{
    Console.WriteLine("✓ Null handling works");
}
```

**Expected Output**:

```
✓ Success: 10 (expected 10)
✓ Null handling works
```

### Pattern 3: Mocking with Moq

**Scenario**: Test ViewModels with mocked dependencies

```csharp
#r "nuget: Moq, 4.20.70"
using System;
using Moq;

public interface IDataService
{
    Task<string> GetDataAsync();
}

public class ViewModelTest
{
    private readonly IDataService _dataService;

    public ViewModelTest(IDataService dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
    }

    public async Task<string> LoadDataAsync()
    {
        return await _dataService.GetDataAsync();
    }
}

// Create mock
var mockService = new Mock<IDataService>();
mockService.Setup(x => x.GetDataAsync())
    .ReturnsAsync("Mock Data");

// Test
var vm = new ViewModelTest(mockService.Object);
var data = await vm.LoadDataAsync();

Console.WriteLine($"✓ Data loaded: {data}");
Console.WriteLine($"✓ Mock invoked: {mockService.Invocations.Count} time(s)");

// Verify
mockService.Verify(x => x.GetDataAsync(), Times.Once());
Console.WriteLine("✓ Verification passed");
```

**Expected Output**:

```
✓ Data loaded: Mock Data
✓ Mock invoked: 1 time(s)
✓ Verification passed
```

### Pattern 4: JSON Serialization

**Scenario**: Test model serialization/deserialization

```csharp
#r "nuget: Newtonsoft.Json, 13.0.3"
using System;
using Newtonsoft.Json;

public class BudgetModel
{
    public int FiscalYear { get; set; }
    public decimal NetBalance { get; set; }
    public string[] Categories { get; set; }
}

// Create model
var original = new BudgetModel
{
    FiscalYear = 2025,
    NetBalance = 50000m,
    Categories = new[] { "Revenue", "Expense" }
};

// Serialize
var json = JsonConvert.SerializeObject(original, Formatting.Indented);
Console.WriteLine($"✓ JSON:\n{json}\n");

// Deserialize
var restored = JsonConvert.DeserializeObject<BudgetModel>(json);

// Validate
Console.WriteLine($"✓ FiscalYear match: {original.FiscalYear == restored.FiscalYear}");
Console.WriteLine($"✓ NetBalance match: {original.NetBalance == restored.NetBalance}");
Console.WriteLine($"✓ Categories count: {restored.Categories.Length}");
```

**Expected Output**:

```
✓ JSON:
{
  "FiscalYear": 2025,
  "NetBalance": 50000.0,
  "Categories": [
    "Revenue",
    "Expense"
  ]
}

✓ FiscalYear match: True
✓ NetBalance match: True
✓ Categories count: 2
```

### Pattern 5: Edge Case Testing

**Scenario**: Validate boundary conditions

```csharp
using System;

public class Calculator
{
    public decimal Divide(decimal numerator, decimal denominator)
    {
        if (denominator == 0)
            throw new DivideByZeroException("Cannot divide by zero");
        return numerator / denominator;
    }
}

var calc = new Calculator();

// Test normal case
var result1 = calc.Divide(10, 2);
Console.WriteLine($"✓ Normal: 10/2 = {result1}");

// Test negative numbers
var result2 = calc.Divide(-10, 2);
Console.WriteLine($"✓ Negative: -10/2 = {result2}");

// Test zero numerator
var result3 = calc.Divide(0, 5);
Console.WriteLine($"✓ Zero numerator: 0/5 = {result3}");

// Test division by zero
try
{
    calc.Divide(10, 0);
    Console.WriteLine("✗ Should have thrown");
}
catch (DivideByZeroException ex)
{
    Console.WriteLine($"✓ Division by zero handled: {ex.Message}");
}

// Test very small numbers
var result4 = calc.Divide(0.0001m, 0.0002m);
Console.WriteLine($"✓ Small numbers: 0.0001/0.0002 = {result4}");
```

## Workflow Integration

### Step-by-Step Process

#### 1. Developer Request

```
"I need to test if BudgetViewModel handles negative balances correctly"
```

#### 2. AI Analysis

- Reviews target file (if needed)
- Identifies test requirements
- Selects appropriate pattern
- Generates inline C# code

#### 3. AI Execution

AI assistant calls:

```
mcp_csharp-mcp_eval_c_sharp(csx: "...inline code...")
```

#### 4. AI Response

```
✓ Negative balance handled correctly: -$1,000.00
✓ PropertyChanged event fired: True
✓ Display format correct
```

#### 5. Developer Iteration

```
"Now test with null input"
```

#### 6. AI Re-execution

AI adjusts code and calls MCP again:

```
✓ ArgumentNullException thrown as expected
✓ Error message: "Value cannot be null. (Parameter 'budget')"
```

#### 7. Formalization

```
"Create xUnit test from these results"
```

AI generates formal test class:

```csharp
public class BudgetViewModelTests
{
    [Theory]
    [InlineData(-1000)]
    [InlineData(0)]
    [InlineData(1000)]
    public void NetBalance_WithVariousValues_UpdatesCorrectly(decimal value)
    {
        // ... xUnit test code
    }
}
```

## Common Use Cases

### Use Case 1: New ViewModel Development

**Goal**: Validate basic ViewModel structure before writing xUnit tests

**Process**:

1. Developer describes ViewModel requirements
2. AI generates mock ViewModel with `INotifyPropertyChanged`
3. AI tests property changes via direct MCP
4. AI tests command execution patterns
5. Results inform xUnit test design

**Time Saved**: 15-30 minutes vs. writing exploratory scripts

### Use Case 2: Service Edge Cases

**Goal**: Discover error conditions in service methods

**Process**:

1. Developer asks to test service with various inputs
2. AI creates test scenarios (null, empty, extreme values)
3. AI executes all scenarios via MCP
4. AI documents which cases fail/succeed
5. Developer decides which to formalize in xUnit

**Time Saved**: 10-20 minutes vs. manual testing

### Use Case 3: Model Validation

**Goal**: Verify serialization and validation rules

**Process**:

1. Developer provides sample JSON
2. AI tests deserialization via MCP
3. AI tests computed properties
4. AI tests validation attributes
5. AI confirms round-trip integrity

**Time Saved**: 5-10 minutes vs. console app testing

## Best Practices

### DO ✅

- **Start Simple**: Test basic scenarios first, add complexity iteratively
- **Use Console Output**: Liberal `Console.WriteLine()` for validation
- **Test Edge Cases**: Null, empty, negative, extreme values
- **Mock Dependencies**: Use Moq for interfaces and external services
- **Validate Exceptions**: Use `try/catch` to confirm error handling
- **Iterate Quickly**: Adjust and retest immediately based on results
- **Document Findings**: AI captures insights in conversation history

### DON'T ❌

- **Don't Test WPF UI**: MCP is headless (no visual controls)
- **Don't Access Real Databases**: No connection strings in exploratory tests
- **Don't Run Long Operations**: Keep tests under 30 seconds
- **Don't Assume File Access**: MCP has restricted file system
- **Don't Skip xUnit**: Direct MCP is exploration, not regression testing
- **Don't Hardcode Secrets**: No credentials or API keys in test code

## Troubleshooting

### Issue: Compilation Error

**Symptom**: `CS0103 - The name 'X' does not exist`

**Solution**: Add missing `using` statements or NuGet references

```csharp
#r "nuget: PackageName, Version"
using System.Namespace;
```

### Issue: Timeout

**Symptom**: No response after 30 seconds

**Solution**: Simplify test code, remove infinite loops, reduce data size

### Issue: Assembly Not Found

**Symptom**: Cannot load Wiley Widget assemblies

**Solution**: Use inline classes to simulate behavior (assemblies not mounted by default)

### Issue: Async Not Working

**Symptom**: `await` keyword error

**Solution**: Ensure `using System.Threading.Tasks;` is included

## Comparison: Direct MCP vs. Script Files

| Aspect              | Direct MCP ✅             | Script Files (.csx)              |
| ------------------- | ------------------------- | -------------------------------- |
| **Execution Speed** | <1 second                 | 1-5 seconds                      |
| **File System**     | No files created          | Create/save/cleanup required     |
| **Iteration**       | Instant refinement        | Edit → save → re-run cycle       |
| **Context**         | Full conversation history | Isolated execution               |
| **Setup**           | Zero (built-in tool)      | Requires `dotnet-script` CLI     |
| **Version Control** | Not needed                | Must commit/manage files         |
| **CI Integration**  | N/A (interactive only)    | Possible but complex             |
| **Learning Curve**  | Natural (conversational)  | Requires script syntax knowledge |
| **Best For**        | Exploration, validation   | E2E tests, complex scenarios     |

## Transition to xUnit

### MANDATORY: Pre-Test Property Analysis ⚠️

**CRITICAL REQUIREMENT**: Before creating any xUnit test, **ALWAYS analyze the target's actual implementation**.

This prevents the frustrating cycle of:

1. Write tests based on assumptions
2. Tests fail to compile (wrong constructor, missing properties, incorrect types)
3. Fix tests repeatedly
4. Waste 15-30 minutes on preventable errors

**Solution**: Read the source file FIRST, understand the structure, THEN write tests.

### Pre-Test Analysis Workflow

#### Step 1: Direct MCP Property Discovery

Before writing xUnit tests, use Direct MCP to analyze the target class:

```csharp
#r "nuget: Microsoft.CodeAnalysis.CSharp, 4.8.0"
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Paste actual source code from target file
var sourceCode = @"
// [AI: Use read_file tool to get actual source]
public class BudgetViewModel : BindableBase
{
    public BudgetViewModel(
        IEventAggregator eventAggregator,
        IRegionManager regionManager,
        IBudgetService budgetService,
        ILogger<BudgetViewModel> logger)
    {
        // ...
    }

    public decimal NetBalance { get; set; }
    public DelegateCommand<BudgetData> RefreshCommand { get; }
    // ...
}
";

Console.WriteLine("=== Target Class Analysis ===\n");

// Parse constructor
var ctorMatch = Regex.Match(sourceCode, @"public\s+(\w+)\s*\((.*?)\)", RegexOptions.Singleline);
if (ctorMatch.Success)
{
    var className = ctorMatch.Groups[1].Value;
    var parameters = ctorMatch.Groups[2].Value
        .Split(',')
        .Select(p => p.Trim())
        .Where(p => !string.IsNullOrEmpty(p))
        .ToList();

    Console.WriteLine($"Class: {className}");
    Console.WriteLine($"Constructor Parameters: {parameters.Count}");
    foreach (var param in parameters)
    {
        Console.WriteLine($"  ✓ {param}");
    }
}

// Parse properties
var propMatches = Regex.Matches(sourceCode, @"public\s+(\w+(?:<.*?>)?)\s+(\w+)\s*\{\s*get");
Console.WriteLine($"\nPublic Properties: {propMatches.Count}");
foreach (Match match in propMatches)
{
    Console.WriteLine($"  ✓ {match.Groups[2].Value}: {match.Groups[1].Value}");
}

// Parse commands
var cmdMatches = Regex.Matches(sourceCode, @"(Delegate|Async)?Command(?:<(.*?)>)?\s+(\w+)");
Console.WriteLine($"\nCommands: {cmdMatches.Count}");
foreach (Match match in cmdMatches)
{
    var name = match.Groups[3].Value;
    var genericType = match.Groups[2].Value;
    var typeInfo = string.IsNullOrEmpty(genericType) ? "" : $"<{genericType}>";
    Console.WriteLine($"  ✓ {name}: {match.Groups[1].Value}Command{typeInfo}");
}

Console.WriteLine("\n✅ Analysis complete - ready for xUnit test generation");
```

**Expected Output**:

```
=== Target Class Analysis ===

Class: BudgetViewModel
Constructor Parameters: 4
  ✓ IEventAggregator eventAggregator
  ✓ IRegionManager regionManager
  ✓ IBudgetService budgetService
  ✓ ILogger<BudgetViewModel> logger

Public Properties: 3
  ✓ NetBalance: decimal
  ✓ FiscalYear: int
  ✓ BudgetTrendItems: ObservableCollection<BudgetTrend>

Commands: 1
  ✓ RefreshCommand: DelegateCommand<BudgetData>

✅ Analysis complete - ready for xUnit test generation
```

#### Step 2: Document Findings

Create analysis checklist (see [Testing Strategy](../core/Testing.md#20-mandatory-pre-test-property-analysis) for full template):

```markdown
## Pre-Test Analysis: BudgetViewModel

✅ Constructor: 4 parameters (IEventAggregator, IRegionManager, IBudgetService, ILogger)
✅ Properties: NetBalance (decimal), FiscalYear (int), BudgetTrendItems (collection)
✅ Commands: RefreshCommand<BudgetData> (DelegateCommand)
✅ Dependencies: IBudgetService (needs mock), Prism interfaces
✅ Edge Cases: Null checks on budgetService parameter
```

#### Step 3: Generate xUnit Tests (Informed by Analysis)

Now write tests that match the actual implementation:

```csharp
public class BudgetViewModelTests
{
    private readonly Mock<IEventAggregator> _eventAggregator;
    private readonly Mock<IRegionManager> _regionManager;
    private readonly Mock<IBudgetService> _budgetService; // ✓ Correct from analysis
    private readonly Mock<ILogger<BudgetViewModel>> _logger; // ✓ Correct generic type
    private readonly BudgetViewModel _sut;

    public BudgetViewModelTests()
    {
        // ✓ All 4 parameters from analysis
        _eventAggregator = new Mock<IEventAggregator>();
        _regionManager = new Mock<IRegionManager>();
        _budgetService = new Mock<IBudgetService>();
        _logger = new Mock<ILogger<BudgetViewModel>>();

        _sut = new BudgetViewModel(
            _eventAggregator.Object,
            _regionManager.Object,
            _budgetService.Object,
            _logger.Object
        );
    }

    [Fact]
    public async Task RefreshCommand_WithValidData_UpdatesNetBalance()
    {
        // ✓ Property name matches analysis
        var data = new BudgetData { FiscalYear = 2025 };

        await _sut.RefreshCommand.Execute(data); // ✓ Command<BudgetData> from analysis

        _sut.NetBalance.Should().BeGreaterThan(0); // ✓ Property exists
    }
}
```

**Result**: Tests compile on first attempt ✅

### When to Create xUnit Tests

After validating via Direct MCP, create formal xUnit tests when:

1. **Behavior Confirmed**: Happy and sad paths tested successfully
2. **Edge Cases Identified**: Know what boundary conditions to test
3. **Patterns Established**: Understand mocking requirements
4. **Regression Risk**: Functionality must be protected in CI
5. **Analysis Complete**: Pre-test property analysis performed ⚠️

### Example Transition (Complete Workflow)

**Phase 1 - Direct MCP Exploration**:

```
Developer: "Test BudgetViewModel property changes and commands"

AI Step 1: Reads source file for BudgetViewModel
AI Step 2: Runs MCP analysis (constructor, properties, commands)
AI Step 3: Tests property changes with inline MCP code
AI Step 4: Tests command execution with inline MCP code
AI Step 5: Tests null handling with inline MCP code

Results: All scenarios work as expected, 4-parameter constructor confirmed
```

**Phase 2 - Pre-Test Analysis**:

```
AI presents analysis:
- Constructor: 4 params (IEventAggregator, IRegionManager, IBudgetService, ILogger<BudgetViewModel>)
- Properties: NetBalance (decimal), FiscalYear (int), BudgetTrendItems (ObservableCollection)
- Commands: RefreshCommand (DelegateCommand<BudgetData>)
- Edge cases: Null check on budgetService throws ArgumentNullException
```

**Phase 3 - xUnit Generation** (informed by analysis):

```csharp
public class BudgetViewModelTests
{
    // ✅ All mocks match actual constructor from analysis
    private readonly Mock<IEventAggregator> _eventAggregator;
    private readonly Mock<IRegionManager> _regionManager;
    private readonly Mock<IBudgetService> _budgetService;
    private readonly Mock<ILogger<BudgetViewModel>> _logger;

    public BudgetViewModelTests()
    {
        // ✅ 4 parameters in correct order from analysis
        _sut = new BudgetViewModel(
            _eventAggregator.Object,
            _regionManager.Object,
            _budgetService.Object,
            _logger.Object
        );
    }

    [Fact] // ✅ Based on MCP findings
    public async Task RefreshCommand_WithValidData_UpdatesNetBalance()
    {
        // Arrange (validated in MCP)
        var data = new BudgetData { FiscalYear = 2025, Entries = [/*...*/] };

        // Act (tested in MCP)
        await _sut.RefreshCommand.Execute(data);

        // Assert (confirmed in MCP)
        _sut.NetBalance.Should().BeGreaterThan(0);
    }

    [Fact] // ✅ Edge case from analysis
    public void Constructor_WithNullBudgetService_ThrowsArgumentNullException()
    {
        Action act = () => new BudgetViewModel(
            _eventAggregator.Object,
            _regionManager.Object,
            null, // ✅ Parameter 3 from analysis
            _logger.Object
        );

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("budgetService");
    }
}
```

**Result**: ✅ Tests compile and pass on first attempt (no fix frustration)

## Summary

**Direct C# MCP Testing** is the approved method for exploratory testing in Wiley Widget because:

1. **Speed**: Instant feedback (<1 sec)
2. **Simplicity**: No file management overhead
3. **Flexibility**: Iterate naturally during conversation
4. **Context**: Full history preserved in AI conversation
5. **Natural**: Fits development workflow seamlessly

### Critical Success Factors

✅ **Phase 1 (Exploration)**: Use Direct MCP for rapid validation
✅ **Phase 2 (Analysis)**: **ALWAYS analyze target properties before xUnit**
✅ **Phase 3 (Formalization)**: Generate xUnit tests informed by analysis

### Common Pitfalls Avoided

❌ **DON'T**: Write xUnit tests based on assumptions
❌ **DON'T**: Skip property analysis step
❌ **DON'T**: Guess constructor parameters

✅ **DO**: Read source file first
✅ **DO**: Use MCP for property discovery
✅ **DO**: Match tests to actual implementation

**Remember**:

- Direct MCP is for **exploration and validation**
- Pre-test analysis is **mandatory** before xUnit
- xUnit is for **regression testing and CI/CD**

---

**Next Steps**:

- Review [Testing Strategy](../core/Testing.md) for full workflow
- See [Pre-Test Analysis Checklist](../core/Testing.md#20-mandatory-pre-test-property-analysis) ⚠️
- Check [xUnit patterns](./StaFact_Testing_Guide.md) for formalization
- View [Coverage Registry](./TEST_COVERAGE_REGISTRY.md) for current metrics
