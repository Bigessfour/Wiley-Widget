# Testing Architecture

## Overview

**WileyWidget is production code only.** All testing is delegated to the external MCP server repository.

## Testing Repository

**All tests for WileyWidget run through the external MCP server:**

📍 **Repository:** <https://github.com/Bigessfour/syncfusion-winforms-mcp>
📍 **Local Location:** `C:\Users\biges\Desktop\syncfusion-winforms-mcp` (sibling to Wiley-Widget)

The MCP server is the **universal test layer** providing:

### Universal Test Capabilities via MCP

✅ **Service Layer Tests**

- Dependency injection validation
- Service registration and lifetimes
- Async initialization patterns
- Service behavior through form instantiation

✅ **UI Layer Tests (Headless)**

- Form instantiation without GUI visibility
- ViewModel testing via form inspection
- Control property validation
- Data grid inspection and assertions

✅ **Integration Tests**

- DI container comprehensive validation
- Async/await pattern enforcement
- Form lifecycle testing
- Cross-layer service integration

✅ **E2E Tests**

- Complete user workflows
- Form state validation
- Multi-panel interactions
- Full application scenarios

### MCP Test Methods

The MCP server exposes these test methods:

| Method                                 | Purpose                                             |
| -------------------------------------- | --------------------------------------------------- |
| `RunDependencyInjectionTests()`        | Validate DI container, service lifetimes, disposals |
| `InspectForm(formTypeName)`            | Instantiate form headlessly and inspect state       |
| `BatchValidateForms(formTypes, theme)` | Theme compliance validation                         |
| `InspectSfDataGrid(formTypeName)`      | Grid structure, columns, data validation            |
| `InspectDockingManager(formTypeName)`  | Docking layout and panel visibility                 |
| `DetectNullRisks(formTypeNames)`       | Scan for NullReferenceException risks               |
| `EvalCSharp(code)`                     | Dynamic C# evaluation for integration tests         |
| `RunHeadlessFormTest(testCode)`        | Execute UI workflows without GUI                    |

## Removed Test Projects

The following test projects have been archived in `tests/.archive/`:

- `WileyWidget.Services.Tests` → MCP validates service layer
- `WileyWidget.WinForms.Tests` → MCP validates UI layer headlessly
- `WileyWidget.WinForms.E2ETests` → MCP provides E2E workflow testing
- `WileyWidget.McpServer.Tests` → MCP itself has self-tests

## How to Run Tests

### Run all MCP tests

```bash
cd syncfusion-winforms-mcp
dotnet test
```

### Run specific test category

```bash
# DI tests
dotnet test --filter "Category=DependencyInjection"

# UI tests
dotnet test --filter "Category=UI"

# E2E tests
dotnet test --filter "Category=E2E"
```

### Run MCP server in test mode

```bash
dotnet run -- --mode headless --test
```

## CI/CD Integration

CI/CD pipelines should:

1. **Build WileyWidget** (production code only)

   ```bash
   dotnet build WileyWidget.sln
   ```

2. **Run MCP tests** (in separate repository)

   ```bash
   cd ../syncfusion-winforms-mcp
   dotnet test
   ```

3. **MCP validates WileyWidget** against its exported assemblies

## Architecture Benefits

✅ **Unified test interface** - All testing goes through MCP, no layer-specific test frameworks
✅ **Headless UI testing** - Form testing without GUI overhead
✅ **Production-only codebase** - WileyWidget contains only shipping code
✅ **External test authority** - MCP is source of truth for all assertions
✅ **Simplified CI/CD** - Single test command against all layers
✅ **Reduced maintenance** - One test framework instead of 4 separate projects
✅ **Universal test layer** - Services, UI, Integration, E2E all use MCP

## Debugging Tests

When test failures occur in MCP:

1. Check MCP repository issue/test logs
2. MCP provides detailed inspection results via JSON output
3. Use `EvalCSharp()` for interactive debugging
4. Run individual form inspection to isolate issues

Example:

```csharp
// MCP test code
var form = await mcpClient.InspectForm("WileyWidget.WinForms.Forms.MainForm");
Assert.NotNull(form);
Assert.True(form.Visible);
```

## Future: Adding New Tests

When adding new features to WileyWidget:

1. ✅ Add code to WileyWidget (production code)
2. 🔧 Add corresponding test to MCP repository
3. ✓ Run MCP tests to validate
4. ✓ Push both repositories

**Never add test code to WileyWidget.** All tests belong in the external MCP repository.
