# Testing Strategy - Two-Phase Workflow (APPROVED METHOD)

**Status**: Official testing methodology for Wiley Widget (Effective: November 8, 2025)

Wiley Widget employs a **two-phase hybrid testing approach** that prioritizes **rapid iteration** with **direct C# MCP evaluation**, followed by **formalized regression testing** with xUnit. This workflow ensures continuous validation during development while building a robust safety net for production.

## Quick Reference Card 📋

```
┌─────────────────────────────────────────────────────────────────────────┐
│ WILEY WIDGET TESTING WORKFLOW - QUICK REFERENCE                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│ Phase 1: EXPLORATORY (Direct C# MCP)                                   │
│ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ │
│ Tool: mcp_csharp-mcp_eval_c_sharp (AI-driven, inline)                  │
│ Duration: <1 second per test                                            │
│ Purpose: Rapid validation, edge case discovery, pattern exploration    │
│                                                                         │
│ ✓ Test ViewModel property changes                                      │
│ ✓ Test async command execution                                         │
│ ✓ Test error handling with mocks                                       │
│ ✓ Test JSON serialization                                              │
│ ✓ Discover edge cases interactively                                    │
│                                                                         │
│ ──────────────────────────────────────────────────────────────────────│
│                                                                         │
│ Phase 2: FORMALIZATION (xUnit)                                         │
│ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ │
│ ⚠️  MANDATORY: Pre-Test Property Analysis (READ SOURCE FIRST!)         │
│                                                                         │
│ Step 1: Read target source file (AI: use read_file tool)               │
│ Step 2: Analyze constructor, properties, commands, dependencies        │
│ Step 3: Document findings in analysis checklist                        │
│ Step 4: ONLY THEN write xUnit tests (match actual implementation)      │
│                                                                         │
│ Tool: xUnit + Moq + FluentAssertions                                   │
│ Duration: 2-3 hours per target (including analysis)                    │
│ Purpose: Regression safety, CI/CD integration, coverage metrics        │
│                                                                         │
│ ✓ Constructor parameter validation                                     │
│ ✓ Property change notifications                                        │
│ ✓ Command execution (sync/async)                                       │
│ ✓ Exception handling with correct types                                │
│ ✓ Edge cases from Phase 1 findings                                     │
│                                                                         │
│ ──────────────────────────────────────────────────────────────────────│
│                                                                         │
│ CRITICAL SUCCESS FACTORS:                                              │
│ • Phase 1: Explore with MCP before committing to xUnit                 │
│ • Phase 2: ALWAYS analyze properties before writing tests ⚠️           │
│ • Never skip analysis step (prevents 15-30 min of fix frustration)     │
│ • Match tests to actual implementation, not assumptions                │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## Philosophy

- **Direct Over Files**: Use `mcp_csharp-mcp_eval_c_sharp` tool for instant testing (no script files required)
- **Analysis Before Tests**: **ALWAYS read source file before writing xUnit** to prevent compilation failures
- **Continuity Over Isolation**: Start with real-world exploration, transition to repeatable tests
- **Data-Driven Targeting**: Prioritize tests based on manifest intelligence (LOC, complexity, dependencies, coverage gaps)
- **Tools for Stages**: Direct MCP for dev loops (<1 sec); xUnit for CI gates (regression)
- **Coverage Goal**: 80%+ overall; 90%+ for critical paths (AI, Security, Budget)

## Test Types & Execution Model

| Phase | Type             | Execution Method | Tool                          | Purpose                                           | Duration       | CI Role      |
| ----- | ---------------- | ---------------- | ----------------------------- | ------------------------------------------------- | -------------- | ------------ |
| **1** | Exploratory      | Direct MCP call  | `mcp_csharp-mcp_eval_c_sharp` | Intelligence gathering, validation with real data | <1 sec         | Dev feedback |
| **2** | Unit/Integration | Test files       | xUnit + Coverlet              | Regression safety, formalized assertions          | 5-10 min suite | Gate checks  |
| **-** | E2E (Legacy)     | Docker scripts   | .csx files                    | Prism module lifecycle, complex scenarios         | Variable       | Supplemental |

**Workflow**: Direct MCP → Insights → xUnit → Coverage → Repeat

**Key Advantage**: No file creation overhead - test code executes immediately via AI assistant

---

## Phase 1: Exploratory Testing with Direct C# MCP

**Goal**: Instant feedback on functionality, edge cases, and integration points using the C# MCP evaluation tool.

**Method**: AI assistant invokes `mcp_csharp-mcp_eval_c_sharp` with inline C# code - no files created.

### 1.1 Target Selection Criteria

Prioritize files based on **manifest intelligence**:

- **High LOC**: >500 lines (e.g., `DashboardViewModel.cs`)
- **Business Critical**: AI (xAI Grok), Budget analysis, QuickBooks
- **Dependency Rich**: Prism navigation, Syncfusion WinUI controls, EF Core

## 🧪 Dual Testing Strategy: CSX Scripts + xUnit

**Official Methodology**: Two-Phase Hybrid Approach (Effective: November 14, 2025)

### Phase 1: Exploratory Testing with Direct C# MCP ⚡

**Scope**: Rapid validation and intelligence gathering  
**Tool**: `mcp_csharp-mcp_eval_c_sharp` (AI-driven, inline execution)  
**Duration**: Seconds per test

**Key Features**:

- **Zero Overhead**: No script files - AI executes C# code inline
- **Instant Feedback**: <1 second execution time
- **Iterative**: Refine and retest immediately during conversation
- **Mocking**: Full Moq support for dependencies
- **Context**: Complete conversation history maintained

**Use For**:

- New ViewModel/Service exploration
- Edge case validation
- Async pattern testing
- JSON serialization verification
- Pre-xUnit prototyping

**See**: [Direct MCP Testing Guide](../reference/DIRECT_MCP_TESTING_GUIDE.md)

### Phase 2: Formalized Regression Testing with xUnit 🛡️

**Scope**: Permanent test suite with CI/CD integration  
**Tools**: xUnit, Moq, FluentAssertions, AutoFixture

**Coverage by Layer**:

- **Models**: Entity validation, property constraints
- **Data**: Repository operations, query logic
- **Business**: Service methods, validation rules
- **ViewModels**: Command execution, property changes

**Testing Patterns**:

- **Arrange-Act-Assert**: Clear test structure
- **Builder Pattern**: TestDataBuilder for complex objects
- **Mocking**: External dependencies (database, APIs)
- **Theory Tests**: Data-driven test scenarios
- **Coverage Gaps**: Files with <50% coverage or outdated dependencies

**Example Targets**:

- ViewModels: `AIAssistViewModel.cs`, `BudgetViewModel.cs`, `DashboardViewModel.cs`
- Services: `QuickBooksService.cs`, `DataAnonymizerService.cs`, `AuditService.cs`
- Models: `BudgetInsights.cs`, `MunicipalAccount.cs`, `AnalyticsData.cs`

### 1.2 Test Parameters per Category

#### For ViewModels

- **Dependencies**: Mock Prism (`IEventAggregator`, `IRegionManager`), Syncfusion controls via Moq
- **Test Cases**:
  - Constructor injection (null checks)
  - Command execution simulation
  - Property change notifications (`INotifyPropertyChanged`)
  - Navigation lifecycle patterns
  - Async error handling
- **Validation**: Console output confirms expected behavior

#### For Services

- **Dependencies**: Mock external APIs (QuickBooks, xAI), simulate EF Core with in-memory data
- **Test Cases**:
  - Async operations (success/failure paths)
  - Exception handling (timeout, auth failures)
  - Data transformations (input → output validation)
  - Edge cases with sample data
- **Validation**: Assert expected outputs, log errors

#### For Models

- **Test Cases**:
  - Serialization round-trips (JSON ↔ Object) via Newtonsoft.Json
  - Validation rules (DataAnnotations simulation)
  - Computed properties (e.g., `NetBalance` calculations)
  - Edge cases (null, empty, extreme values)
- **Validation**: Deserialize and verify properties

### 1.3 Direct MCP Execution Pattern

**Usage**: AI assistant calls the tool directly during conversation

```csharp
// Example: Test BudgetViewModel pattern
#r "nuget: Moq, 4.20.70"
#r "nuget: Newtonsoft.Json, 13.0.3"
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;

// Simulate ViewModel pattern
public class BudgetViewModelTest : INotifyPropertyChanged
{
    private decimal _netBalance;
    public event PropertyChangedEventHandler PropertyChanged;

    public decimal NetBalance
    {
        get => _netBalance;
        set
        {
            if (_netBalance != value)
            {
                _netBalance = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NetBalance)));
            }
        }
    }

    public async Task LoadBudgetAsync(string json)
    {
        await Task.Delay(10); // Simulate async
        var data = JsonConvert.DeserializeObject<dynamic>(json);
        NetBalance = data.NetBalance;
    }
}

// Execute test
var vm = new BudgetViewModelTest();
var propertyChanged = false;
vm.PropertyChanged += (s, e) => propertyChanged = true;

var sampleJson = "{\"FiscalYear\": 2025, \"NetBalance\": 50000}";
await vm.LoadBudgetAsync(sampleJson);

Console.WriteLine($"✓ NetBalance: {vm.NetBalance:C}");
Console.WriteLine($"✓ PropertyChanged fired: {propertyChanged}");
Console.WriteLine("✓ Test PASSED");
```

**AI Assistant Action**: Invoke `mcp_csharp-mcp_eval_c_sharp` with above code inline

### 1.4 Advantages Over Script Files

| Aspect             | Direct MCP                | Script Files (.csx)            |
| ------------------ | ------------------------- | ------------------------------ |
| **Speed**          | <1 second                 | 1-5 seconds (file I/O)         |
| **Overhead**       | Zero (inline code)        | Create, save, execute, cleanup |
| **Iteration**      | Instant refinement        | Edit → save → re-run           |
| **Context**        | Full conversation history | Isolated script                |
| **Cleanup**        | None needed               | Delete obsolete scripts        |
| **CI Integration** | Natural (AI-driven)       | Requires script management     |

### 1.5 Testing Workflow

1. **Developer Request**: "Test BudgetViewModel property changes"
2. **AI Assistant**:
   - Analyzes target file (if needed)
   - Generates test code inline
   - Invokes `mcp_csharp-mcp_eval_c_sharp`
   - Returns results immediately
3. **Developer Review**: Validates output, requests refinements
4. **Iteration**: AI adjusts code and re-tests instantly
5. **Transition**: Once validated, AI generates formal xUnit tests

### 1.6 Example Test Scenarios

#### Test 1: PropertyChanged Event

```csharp
// AI generates and executes inline
var vm = new MockViewModel();
var fired = false;
vm.PropertyChanged += (s, e) => fired = true;
vm.SomeProperty = "new value";
Console.WriteLine($"✓ Event fired: {fired}");
```

#### Test 2: Async Command with Exception

```csharp
// Test error handling
try
{
    await vm.ExecuteCommandAsync(null);
    Console.WriteLine("✗ Should have thrown ArgumentNullException");
}
catch (ArgumentNullException)
{
    Console.WriteLine("✓ Null argument handled correctly");
}
```

#### Test 3: JSON Serialization

```csharp
#r "nuget: Newtonsoft.Json, 13.0.3"
var model = new { FiscalYear = 2025, Amount = 50000 };
var json = JsonConvert.SerializeObject(model);
var restored = JsonConvert.DeserializeObject<dynamic>(json);
Console.WriteLine($"✓ Round-trip: {restored.FiscalYear} = {model.FiscalYear}");
```

### 1.7 Transition Criteria

- **When**: Behavior confirmed via multiple MCP tests (happy/sad paths)
- **Output**: AI documents findings in conversation
- **Next**: Generate formal xUnit test class based on MCP test results

---

## Phase 2: Formalized Testing with xUnit

**Goal**: Build regression safety net with structured tests, full coverage, and CI integration.

### 2.0 MANDATORY: Pre-Test Property Analysis ⚠️

**CRITICAL REQUIREMENT**: Before writing any xUnit test, **ALWAYS analyze the target's actual implementation**.

#### Why This Matters

**Common Failure Pattern**:

1. Developer: "Create tests for `BudgetViewModel`"
2. AI writes tests based on assumptions
3. Tests fail due to mismatched:
   - Constructor parameters (expected 2, actual has 5)
   - Property names (assumed `Balance`, actual is `NetBalance`)
   - Method signatures (expected sync, actual is async)
   - Command types (expected `ICommand`, actual is `DelegateCommand<T>`)
   - Dependency interfaces (mocked wrong types)

**Prevention**: **Read the source file FIRST**, then design tests to match reality.

#### Mandatory Analysis Checklist

Before writing ANY xUnit test, complete this checklist:

```markdown
## Pre-Test Analysis for [TargetName]

### 1. File Location

- [ ] Confirmed file path exists
- [ ] Verified namespace

### 2. Class Structure

- [ ] Class name: **\*\***\_**\*\***
- [ ] Base class(es): **\*\***\_**\*\***
- [ ] Implemented interfaces: **\*\***\_**\*\***
- [ ] Generic type parameters: **\*\***\_**\*\***

### 3. Constructor Analysis

- [ ] Number of parameters: **\_**
- [ ] Parameter types and names:
  1. Type: **\_\_\_\_** Name: **\_\_\_\_** (Required/Optional/Default: **\_**)
  2. Type: **\_\_\_\_** Name: **\_\_\_\_** (Required/Optional/Default: **\_**)
     (Continue for all parameters)
- [ ] Constructor logic (DI registration, initialization, validation)

### 4. Properties

- [ ] List all public properties with types:
  - Name: **\_\_\_\_** Type: **\_\_\_\_** (get/set/init) Default: **\_**
  - (Include backing fields if relevant)
- [ ] Which properties implement `INotifyPropertyChanged`?
- [ ] Any computed properties?

### 5. Commands

- [ ] List all command properties:
  - Name: **\_\_\_\_** Type: **\_\_\_\_** (ICommand/DelegateCommand/AsyncCommand)
  - Generic parameter (if any): **\_\_\_\_**
  - CanExecute logic: **\_\_\_\_**
  - Execute signature: **\_\_\_\_**

### 6. Methods

- [ ] Public method signatures:
  - Name: **\_\_\_\_** Return: **\_\_\_\_** Parameters: **\_\_\_\_** (Sync/Async)
  - (Include overloads)
- [ ] Protected/virtual methods (if testing inheritance)

### 7. Dependencies

- [ ] Injected interfaces:
  1. Type: **\_\_\_\_** Usage: **\_\_\_\_**
  2. (List all)
- [ ] External service calls (APIs, DB, file system)
- [ ] Prism-specific: IEventAggregator, IRegionManager, INavigationService?

### 8. Edge Cases from Code Review

- [ ] Null checks present?
- [ ] Exception types thrown: **\_\_\_\_**
- [ ] Validation logic: **\_\_\_\_**
- [ ] Async patterns: Task/ValueTask/CancellationToken?

### 9. Test Strategy Decision

- [ ] Mock requirements identified
- [ ] Test data requirements
- [ ] Number of test methods needed: ~**\_**
- [ ] Theory vs. Fact distribution
```

#### Execution Process

**Step 1**: Developer/AI reads target source file
**Step 2**: Complete checklist above
**Step 3**: ONLY THEN write xUnit test class
**Step 4**: Validate test compiles and matches analysis

#### Example: Proper Pre-Test Analysis

**Target**: `BudgetViewModel.cs`

**Step 1 - Read Source** (AI uses `read_file` tool)

**Step 2 - Analysis**:

```markdown
## Pre-Test Analysis for BudgetViewModel

### Constructor

- Parameters: 5
  1. IEventAggregator eventAggregator (Required)
  2. IRegionManager regionManager (Required)
  3. IBudgetService budgetService (Required)
  4. ILogger<BudgetViewModel> logger (Required)
  5. IConfiguration configuration (Optional, default: null)

### Properties

- NetBalance: decimal (get/set with PropertyChanged)
- FiscalYear: int (get/set with PropertyChanged)
- BudgetTrendItems: ObservableCollection<BudgetTrend> (get, initialized in ctor)
- IsLoading: bool (get/set with PropertyChanged)

### Commands

- RefreshBudgetDataCommand: DelegateCommand<BudgetData> (async)
  - CanExecute: () => !IsLoading
  - Execute: async Task ExecuteRefreshAsync(BudgetData data)
- ExportCommand: DelegateCommand (sync)

### Dependencies

- budgetService.GetBudgetAsync() → Task<Budget>
- eventAggregator.GetEvent<BudgetUpdatedEvent>().Publish()
- logger.LogInformation/LogError

### Edge Cases

- Null check on budgetService parameter (throws ArgumentNullException)
- RefreshBudgetDataCommand throws InvalidOperationException if data.Entries is empty
- CancellationToken support in async methods
```

**Step 3 - Write Tests** (now properly informed):

```csharp
public class BudgetViewModelTests
{
    private readonly Mock<IEventAggregator> _eventAggregator;
    private readonly Mock<IRegionManager> _regionManager;
    private readonly Mock<IBudgetService> _budgetService; // ✓ Correct interface
    private readonly Mock<ILogger<BudgetViewModel>> _logger;
    private readonly BudgetViewModel _sut;

    public BudgetViewModelTests()
    {
        // ✓ All 5 dependencies properly mocked
        _eventAggregator = new Mock<IEventAggregator>();
        _regionManager = new Mock<IRegionManager>();
        _budgetService = new Mock<IBudgetService>();
        _logger = new Mock<ILogger<BudgetViewModel>>();

        _sut = new BudgetViewModel(
            _eventAggregator.Object,
            _regionManager.Object,
            _budgetService.Object,
            _logger.Object
            // configuration omitted (optional)
        );
    }

    [Fact] // ✓ Correct async pattern
    public async Task RefreshBudgetDataCommand_WithValidData_UpdatesNetBalance()
    {
        // Arrange - ✓ Using correct property name
        var data = new BudgetData { FiscalYear = 2025, Entries = new[] { /*...*/ } };

        // Act - ✓ Correct command type (DelegateCommand<BudgetData>)
        await _sut.RefreshBudgetDataCommand.Execute(data);

        // Assert - ✓ Property exists as confirmed
        _sut.NetBalance.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Constructor_WithNullBudgetService_ThrowsArgumentNullException()
    {
        // ✓ Test matches actual null check from analysis
        Action act = () => new BudgetViewModel(
            _eventAggregator.Object,
            _regionManager.Object,
            null, // ✓ This is the parameter that throws
            _logger.Object
        );

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("budgetService"); // ✓ Exact parameter name
    }
}
```

**Result**: Tests compile on first attempt, no frustrating fix cycles.

---

### 2.0.1 AI Assistant Analysis Tool

**When Developer Requests xUnit Tests**, AI Assistant MUST:

1. **Read Target File First**:

   ```
   AI Action: read_file(target_file_path, full_content)
   ```

2. **Optionally: Use Direct MCP for Property Discovery**:

   ```csharp
   // AI can use MCP to quickly analyze structure
   #r "nuget: Microsoft.CodeAnalysis.CSharp, 4.8.0"
   using System.Reflection;
   using Microsoft.CodeAnalysis.CSharp;
   using Microsoft.CodeAnalysis.CSharp.Syntax;

   var sourceCode = @"[paste class code]";
   var tree = CSharpSyntaxTree.ParseText(sourceCode);
   var root = tree.GetRoot();

   // Extract constructors
   var constructors = root.DescendantNodes()
       .OfType<ConstructorDeclarationSyntax>();

   foreach (var ctor in constructors)
   {
       Console.WriteLine($"Constructor: {ctor.ParameterList}");
   }

   // Extract properties
   var properties = root.DescendantNodes()
       .OfType<PropertyDeclarationSyntax>();

   foreach (var prop in properties)
   {
       Console.WriteLine($"Property: {prop.Type} {prop.Identifier}");
   }
   ```

3. **Present Analysis to Developer**:

   ```markdown
   ## Analysis Complete for BudgetViewModel

   **Constructor**: 5 parameters (IEventAggregator, IRegionManager, IBudgetService, ILogger, IConfiguration?)
   **Properties**: 4 (NetBalance, FiscalYear, BudgetTrendItems, IsLoading)
   **Commands**: 2 (RefreshBudgetDataCommand<BudgetData>, ExportCommand)
   **Dependencies**: IBudgetService, Prism events

   Proceeding with test generation...
   ```

4. **Generate Tests Based on Analysis** (not assumptions)

#### Automation: MCP-Powered Pre-Analysis

AI can use this inline analysis before every xUnit generation:

```csharp
// Quick target analysis via MCP
using System;
using System.Linq;
using System.Text.RegularExpressions;

var sourceCode = @"
[PASTE ACTUAL SOURCE CODE HERE FROM read_file]
";

// Parse constructor parameters
var ctorMatch = Regex.Match(sourceCode, @"public\s+\w+\s*\((.*?)\)", RegexOptions.Singleline);
if (ctorMatch.Success)
{
    var parameters = ctorMatch.Groups[1].Value
        .Split(',')
        .Select(p => p.Trim())
        .Where(p => !string.IsNullOrEmpty(p))
        .ToList();

    Console.WriteLine($"✓ Constructor has {parameters.Count} parameters:");
    foreach (var param in parameters)
    {
        Console.WriteLine($"  - {param}");
    }
}

// Parse properties
var propMatches = Regex.Matches(sourceCode, @"public\s+(\w+(?:<\w+>)?)\s+(\w+)\s*\{\s*get");
Console.WriteLine($"\n✓ Found {propMatches.Count} public properties:");
foreach (Match match in propMatches)
{
    Console.WriteLine($"  - {match.Groups[2].Value}: {match.Groups[1].Value}");
}

// Parse commands
var cmdMatches = Regex.Matches(sourceCode, @"(Delegate|Async)?Command(?:<(\w+)>)?\s+(\w+)");
Console.WriteLine($"\n✓ Found {cmdMatches.Count} commands:");
foreach (Match match in cmdMatches)
{
    var commandName = match.Groups[3].Value;
    var genericType = match.Groups[2].Value;
    var typeInfo = string.IsNullOrEmpty(genericType) ? "" : $"<{genericType}>";
    Console.WriteLine($"  - {commandName}{typeInfo}");
}

Console.WriteLine("\n✅ Analysis complete - ready for test generation");
```

---

### 2.1 Test Structure Standards (Post-Analysis)

```csharp
using FluentAssertions;
using Moq;
using Xunit;
using WileyWidget.ViewModels.Main;

namespace WileyWidget.Tests.ViewModels
{
    public class BudgetViewModelTests : IDisposable
    {
        private readonly Mock<IEventAggregator> _eventAggregator;
        private readonly BudgetViewModel _sut; // System Under Test

        public BudgetViewModelTests()
        {
            _eventAggregator = new Mock<IEventAggregator>();
            _sut = new BudgetViewModel(_eventAggregator.Object);
        }

        [Fact]
        public async Task RefreshBudgetDataCommand_WithValidData_UpdatesNetBalance()
        {
            // Arrange
            var budget = new BudgetData { FiscalYear = 2025, Entries = [/* ... */] };

            // Act
            await _sut.RefreshBudgetDataCommand.ExecuteAsync(budget);

            // Assert
            _sut.NetBalance.Should().BeGreaterThan(0);
            _sut.BudgetTrendItems.Should().NotBeEmpty();
        }

        [Theory]
        [InlineData(null, typeof(ArgumentNullException))]
        [InlineData("invalid", typeof(InvalidOperationException))]
        public async Task RefreshBudgetDataCommand_WithInvalidData_Throws(object input, Type expectedException)
        {
            // Act
            Func<Task> act = async () => await _sut.RefreshBudgetDataCommand.ExecuteAsync(input);

            // Assert
            await act.Should().ThrowAsync<Exception>().Where(e => e.GetType() == expectedException);
        }

        public void Dispose()
        {
            _sut?.Dispose();
        }
    }
}
```

### 2.2 Test Categories & Patterns

#### Unit Tests (Isolated)

- **Target**: ViewModels, Services, Models without external dependencies
- **Mocking**: Use Moq for interfaces (Prism, EF Core contexts)
- **Assertions**: FluentAssertions for readable comparisons
- **Location**: `tests/WileyWidget.Tests/ViewModels/`, `tests/WileyWidget.Tests/Services/`

#### Integration Tests

- **Target**: EF Core repositories, API services, complex workflows
- **Setup**: `InMemoryDb` for EF Core, TestContainers for SQL Server
- **Isolation**: Use `Respawn` to reset DB state between tests
- **Location**: `tests/WileyWidget.Tests/RepositoryTests/`

#### UI Tests (Limited)

- **Target**: Critical WinUI interactions (Syncfusion grids, navigation, WinUI 3 controls)
- **Tool**: FlaUI or manual .csx scripts (via Docker)
- **Note**: Keep minimal; prefer ViewModels + mocks
- **Location**: `tests/WileyWidget.Tests/E2E/`

### 2.3 Coverage Requirements

| Component   | Target Coverage | Priority Files                                     |
| ----------- | --------------- | -------------------------------------------------- |
| AI Services | ≥90%            | `DataAnonymizerService.cs`, `AIAssistViewModel.cs` |
| Budget Core | ≥90%            | `BudgetViewModel.cs`, `BudgetImporter.cs`          |
| QuickBooks  | ≥80%            | `QuickBooksService.cs`, `QBAuthService.cs`         |
| Models      | ≥85%            | All in `WileyWidget.Models`                        |
| Utilities   | ≥75%            | Helpers, converters, validators                    |

**Measurement**:

```powershell
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report
```

### 2.4 CI Integration

**Phase 1 (Direct MCP)**: Development feedback only - not in CI pipeline
**Phase 2 (xUnit)**: Full CI/CD integration with coverage gates

**In `.github/workflows/ci-optimized.yml`**:

```yaml
# Phase 1 is interactive (AI-driven), no CI automation needed

- name: Run xUnit Tests (Regression Gate)
  run: dotnet test --configuration Release --no-build --collect:"XPlat Code Coverage" --logger trx

- name: Enforce Coverage Thresholds
  uses: codecov/codecov-action@v3
  with:
    files: "**/coverage.cobertura.xml"
    fail_ci_if_error: true
    flags: unittests
    name: wiley-widget-coverage
```

**Note**: Direct MCP testing is for **human-in-the-loop** exploration. xUnit provides **automated regression safety**.

---

## Execution Plan

### Timeline (Per Target)

| Phase                    | Duration  | Activities                                                      |
| ------------------------ | --------- | --------------------------------------------------------------- |
| **Phase 1** (Direct MCP) | Minutes   | AI generates inline tests, executes via MCP, documents findings |
| **Phase 2** (xUnit)      | 2-3 hours | Convert MCP insights to xUnit, add coverage, integrate CI       |

### Initial Targets (Priority Order)

1. **AIAssistViewModel.cs** → AI integration, xAI Grok, anonymization
2. **BudgetViewModel.cs** → Core business logic, hierarchical data
3. **QuickBooksService.cs** → External API, auth, retries

### Tools & Environment

- **Phase 1**: C# MCP (`mcp_csharp-mcp_eval_c_sharp` tool) - AI assistant driven
- **Phase 2**: VS2022 for xUnit debugging; `dotnet test` for execution
- **Docs**: Syncfusion WPF, Prism 9.x, xUnit.net
- **CI**: GitHub Actions with xUnit + Coverlet

### Workflow Example

1. **Developer**: "Test BudgetViewModel with negative balances"
2. **AI Assistant**:
   - Generates inline C# test code
   - Calls `mcp_csharp-mcp_eval_c_sharp` tool
   - Returns: "✓ Negative balance handled correctly: -$1,000.00"
3. **Developer**: "Now test with null data"
4. **AI Assistant**:
   - Adjusts code, adds null check
   - Calls MCP again
   - Returns: "✓ ArgumentNullException thrown as expected"
5. **Developer**: "Create xUnit test from these results"
6. **AI Assistant**: Generates formal test class in `tests/WileyWidget.Tests/`

---

## Migration from Legacy Tests

1. **Audit Existing**: Review `tests/WileyWidget.Tests/` for coverage gaps
2. **Interactive Testing**: Use direct MCP calls for untested components (instant feedback)
3. **Refactor xUnit**: Apply new patterns (FluentAssertions, Theory attributes)
4. **Archive Legacy**: Move obsolete Docker/script tests to `scripts/archive/`
5. **Document**: Update test README files with MCP-first approach

**Key Change**: **No more script file creation for exploratory testing** - use AI assistant's direct MCP capability instead.

---

## Guidelines

- **Determinism**: Avoid time-based or random data in xUnit; use fixed seeds
- **Isolation**: Each test must run independently (no shared state)
- **Speed**: Keep unit tests <100ms; integration <5s
- **Readability**: Use descriptive test names (`MethodName_Scenario_ExpectedResult`)
- **Mocking**: Prefer constructor injection; use `AutoFixture` for test data

---

## Extended References

- [Testing Checklist](../reference/testing-checklist.md)
- [StaFact Testing Guide](../reference/StaFact_Testing_Guide.md)
- [UI Testing README](../reference/UI_TESTING_README.md)
- [Coverage Registry](../reference/TEST_COVERAGE_REGISTRY.md)
- [C# MCP Troubleshooting](../reference/CSHARP_MCP_TROUBLESHOOTING.md)
