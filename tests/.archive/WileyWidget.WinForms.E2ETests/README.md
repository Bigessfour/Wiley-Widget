# WileyWidget FlaUI E2E Tests

This directory contains end-to-end (E2E) UI automation tests for the WileyWidget Windows Forms application using [FlaUI](https://github.com/FlaUI/FlaUI).

## Test Structure

Each panel has its own test file following the pattern `{PanelName}E2ETests.cs`:

- **AnalyticsPanelE2ETests.cs** - Tests for exploratory analysis, scenario modeling, and forecasting
- **BudgetPanelE2ETests.cs** - Tests for CRUD operations, filtering, and export
- **SettingsPanelE2ETests.cs** - Tests for theme selection, AI settings, and API key management
- **ReportsPanelE2ETests.cs** - Tests for report selection, parameters, and export
- **QuickBooksPanelE2ETests.cs** - Tests for connection management and sync operations
- **ChartPanelE2ETests.cs** - Tests for chart rendering and export (PNG/PDF)
- **AuditLogPanelE2ETests.cs** - Tests for event grid, timeline chart, and filters
- **DepartmentSummaryPanelE2ETests.cs** - Tests for department metrics and budget comparisons
- **RevenueTrendsPanelE2ETests.cs** - Tests for revenue trend visualization and forecasts

## Test Helpers

The `Helpers/` directory contains shared utilities:

- **TestAppHelper.cs** - Resolves the WileyWidget executable path for testing
- **WaitHelpers.cs** - Waits for busy indicators to complete before interactions
- **NavigationHelper.cs** - Provides navigation and UI tree inspection utilities

## CI/CD Integration

Tests are environment-aware and skip in CI environments unless explicitly enabled:

```csharp
private bool EnsureInteractiveOrSkip()
{
    var labels = Environment.GetEnvironmentVariable("RUNNER_LABELS") ?? string.Empty;
    var optedIn = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
    var selfHosted = labels.IndexOf("self-hosted", StringComparison.OrdinalIgnoreCase) >= 0;
    var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    if (isCi && !optedIn && !selfHosted)
    {
        return false;
    }

    return true;
}
```

### Environment Variables

- `CI=true` - Indicates running in CI environment
- `WILEYWIDGET_UI_TESTS=true` - Opts into running UI tests in CI
- `RUNNER_LABELS` - Comma-separated runner labels (e.g., `self-hosted,windows,x64`)

## Running Tests

### From VS Code Tasks

```bash
# Run all E2E tests
> Tasks: Run Task → test: ui-e2e

# Run specific test file
dotnet test tests/WileyWidget.WinForms.E2ETests/BudgetPanelE2ETests.cs
```

### From Command Line

```powershell
# Run all E2E UI tests
dotnet test tests/WileyWidget.WinForms.E2ETests/WileyWidget.WinForms.E2ETests.csproj --filter "Category=UI"

# Run specific panel tests
dotnet test tests/WileyWidget.WinForms.E2ETests/WileyWidget.WinForms.E2ETests.csproj --filter "Panel=Budget"

# Run without building (assumes prior build)
dotnet test tests/WileyWidget.WinForms.E2ETests/WileyWidget.WinForms.E2ETests.csproj --no-build --filter "Category=UI"
```

## Discovering UI Elements

### Using dump-ui-tree.ps1 Script

The `scripts/tools/dump-ui-tree.ps1` PowerShell script is your primary tool for discovering AutomationIds, control names, and UI hierarchy when authoring new tests.

#### Quick Start

```powershell
# 1. Start WileyWidget application manually
dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj

# 2. Navigate to the panel you want to test (e.g., Settings Panel)

# 3. Run dump-ui-tree.ps1 to capture the UI structure
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/tools/dump-ui-tree.ps1

# 4. Review the generated JSON file
# Output: logs/ui-tree-dump-YYYYMMDD-HHmmss.json
```

#### Script Features

- **Auto-Installs FlaUI Dependencies** - No manual NuGet installs required
- **Attaches to Running WileyWidget** - Finds your application automatically
- **Recursive Tree Traversal** - Captures entire UI hierarchy with configurable depth
- **Rich Metadata** - Includes Name, AutomationId, ClassName, ControlType for each element
- **Flexible Output** - Saves to file or prints to console

#### Example Output

```json
{
  "Name": "WileyWidget - Dashboard",
  "AutomationId": "",
  "ClassName": "WindowsForms10.Window.8.app.0.141b42a_r9_ad1",
  "ControlType": "Window",
  "Children": [
    {
      "Name": "Settings",
      "AutomationId": "navSettings",
      "ClassName": "Button",
      "ControlType": "Button"
    },
    {
      "Name": "API Key",
      "AutomationId": "txtApiKey",
      "ClassName": "TextBox",
      "ControlType": "Edit"
    }
  ]
}
```

#### Using the Output in Tests

Once you have the UI tree JSON, extract AutomationIds and Names to build test locators:

```csharp
// From dump: "AutomationId": "navSettings"
var settingsButton = WaitForElement(window, cf => cf.ByAutomationId("navSettings"));
settingsButton?.Click();

// From dump: "Name": "API Key", "AutomationId": "txtApiKey"
var apiKeyBox = WaitForElement(window, cf => cf.ByAutomationId("txtApiKey"))?.AsTextBox();
apiKeyBox?.Enter("sk-test123");

// Fallback to Name when AutomationId is not set
var saveButton = WaitForElement(window, cf => cf.ByName("Save"));
```

#### Advanced Usage

```powershell
# Specify process ID manually
pwsh scripts/tools/dump-ui-tree.ps1 -ProcessId 12345

# Limit depth for large UIs
pwsh scripts/tools/dump-ui-tree.ps1 -MaxDepth 2

# Print to console instead of file
pwsh scripts/tools/dump-ui-tree.ps1 -ToConsole

# Custom output path
pwsh scripts/tools/dump-ui-tree.ps1 -OutputPath "C:\temp\my-ui-tree.json"
```

### Using VS Code Task

```bash
> Tasks: Run Task → ui: dump-tree
```

This task runs `dump-ui-tree.ps1` and saves the output to `logs/ui-tree-dump-{timestamp}.json`.

### Manual Inspection (Fallback)

If the script fails, you can manually inspect the UI using Windows SDK tools:

1. **Inspect.exe** (Windows SDK) - Visual tree inspector with hover-to-inspect
2. **Accessibility Insights** - Microsoft's accessibility testing tool with automation properties

Both tools show AutomationId, Name, ControlType, and other properties for WinForms controls.

## Threading Requirements

**CRITICAL:** All Windows Forms UI tests MUST use `[STAFact]` instead of `[Fact]` to run on the Single-Threaded Apartment (STA) thread required by Windows Forms components. Using `[Fact]` will cause IDE crashes and test failures.

### Why STAFact?

Windows Forms controls require STA threading model for proper initialization and interaction. The `[STAFact]` attribute (from `Xunit.StaFact` NuGet package) ensures tests run on an STA thread, preventing:

- IDE crashes during test execution
- `InvalidOperationException` when creating controls
- Thread affinity violations
- Unstable UI automation

### Installing STAFact

Add to your test project's `.csproj`:

```xml
<PackageReference Include="Xunit.StaFact" Version="1.1.11" />
```

## Writing New Tests

### Test Pattern

```csharp
[STAFact]  // REQUIRED for Windows Forms tests (note: STAFact, not StaFact)
[Trait("Category", "UI")]
[Trait("Panel", "YourPanel")]
public void YourPanel_Feature_Behavior()
{
    if (!EnsureInteractiveOrSkip()) return;

    StartApp();
    var window = GetMainWindow();

    // Navigate to panel
    var navButton = WaitForElement(window, cf => cf.ByAutomationId("navYourPanel"));
    navButton?.Click();

    // Wait for busy indicator
    WaitForBusyIndicator(window);

    // Perform test actions
    var button = WaitForElement(window, cf => cf.ByAutomationId("btnAction"));
    button?.Click();

    // Assert expected outcomes
    var result = WaitForElement(window, cf => cf.ByName("Result Label"));
    Assert.NotNull(result);
    Assert.Equal("Expected Value", result.Name);
}
```

### Best Practices

1. **Use AutomationIds** - Primary locator strategy. Set `AutomationProperties.AutomationId` on all interactive controls in WinForms code.

2. **Wait for Busy Indicators** - Always call `WaitForBusyIndicator(window)` after navigation or actions that trigger async operations.

3. **Use WaitForElement()** - Provides implicit waits and avoids race conditions:

   ```csharp
   var element = WaitForElement(window, cf => cf.ByAutomationId("myControl"), TimeSpan.FromSeconds(10));
   ```

4. **Check for Null** - UI elements may not exist due to permissions, state, or timing:

   ```csharp
   var button = WaitForElement(window, cf => cf.ByAutomationId("btnOptional"));
   if (button != null)
   {
       button.Click();
   }
   ```

5. **Use Fallback Locators** - Chain locators with `.Or()` for resilience:

   ```csharp
   var element = WaitForElement(window, cf => cf.ByAutomationId("txtInput").Or(cf.ByName("Input")));
   ```

6. **Tag Tests Appropriately**:
   - `[Trait("Category", "UI")]` - Enables filtering for UI-specific tests
   - `[Trait("Panel", "PanelName")]` - Groups tests by panel

7. **Dispose Resources** - All test classes implement `IDisposable` to clean up FlaUI resources:

   ```csharp
   public void Dispose()
   {
       Dispose(true);
       GC.SuppressFinalize(this);
   }

   protected virtual void Dispose(bool disposing)
   {
       if (!_disposed)
       {
           if (disposing)
           {
               try
               {
                   _app?.Close();
                   _app?.Dispose();
                   _app = null;
                   _automation?.Dispose();
                   _automation = null;
               }
               catch
               {
                   // Suppress cleanup errors
               }
           }
           _disposed = true;
       }
   }
   ```

## Troubleshooting

### Tests Skip in CI

Ensure environment variables are set:

```bash
export WILEYWIDGET_UI_TESTS=true
export CI=true
```

Or use self-hosted runners with `self-hosted` in `RUNNER_LABELS`.

### Application Not Found

Verify the application path resolution in `TestAppHelper.cs`. Default search paths:

1. `bin/Debug/net9.0-windows10.0.26100.0/WileyWidget.WinForms.exe`
2. `src/WileyWidget.WinForms/bin/Debug/net9.0-windows10.0.26100.0/WileyWidget.WinForms.exe`
3. Relative to solution root

### Element Not Found

1. Use `dump-ui-tree.ps1` to verify AutomationId and Name
2. Increase timeout: `WaitForElement(window, locator, TimeSpan.FromSeconds(30))`
3. Check if element is in a different window or popup
4. Verify control visibility: `Assert.True(element.IsEnabled)`

### Race Conditions

- Always call `WaitForBusyIndicator(window)` after navigation or actions
- Use `WaitForElement()` instead of `FindFirstDescendant()`
- Avoid `Thread.Sleep()` - use retry helpers instead

### FlaUI Version Mismatch

Ensure all projects reference the same FlaUI versions:

```xml
<PackageReference Include="FlaUI.Core" Version="4.0.0" />
<PackageReference Include="FlaUI.UIA3" Version="4.0.0" />
```

## References

- [FlaUI Documentation](https://github.com/FlaUI/FlaUI/wiki)
- [UI Automation Overview](https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32)
- [xUnit Documentation](https://xunit.net/)
- [PANEL_PRODUCTION_READINESS.md](../../docs/PANEL_PRODUCTION_READINESS.md) - Production readiness criteria for panels

## Contributing

When adding new E2E tests:

1. Use `dump-ui-tree.ps1` to discover AutomationIds and control structure
2. Follow the established test pattern (StaFact, IDisposable, WaitHelpers)
3. Add appropriate xUnit traits (`Category=UI`, `Panel=YourPanel`)
4. Test locally before committing (run `test: ui-e2e` task)
5. Document any new helper methods or patterns in this README

---

**Last Updated:** 2025-01-01
**Maintainer:** WileyWidget Development Team
