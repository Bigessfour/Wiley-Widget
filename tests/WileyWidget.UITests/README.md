# Panel Test Workflow: Headless Validation of WinForms Panels (.csx) – Updated with UI Interaction Checks

## Purpose

This document defines a standardized, repeatable, and fully reproducible workflow for creating and maintaining `.csx` script-based headless tests that validate WinForms panels (e.g., `QuickBooksPanel`, `BudgetPanel`, etc.) in the Wiley-Widget project.

The goal is **100% confidence** that a panel:

- Constructs and initializes correctly
- Binds data properly
- Responds to mocked service states
- Handles **user interactions** predictably (button clicks, grid selections, text input, etc.)
- Updates UI and ViewModel state after interactions
- Calls services correctly via commands
- Logs expected messages

These tests run headlessly using the MCP server's `RunHeadlessFormTest` tooling with `SyncfusionTestHelper` and `MockFactory`.

## Evaluation Summary (Updated)

Previous version covered construction, binding, and basic state checks.
**New addition**: UI interaction checks via direct control invocation in headless mode.
This enables validation of command execution, UI updates, service calls, and error handling without a visible window.

**Why this works in headless mode**:

- `SyncfusionTestHelper.TryLoadForm` processes the WinForms message pump sufficiently for control initialization and event firing.
- Controls are fully instantiated and accessible.
- We can call `PerformClick()`, set properties (e.g., `SelectedItem`, `Text`), or invoke event handlers directly.
- Fake services can include **spy counters** to confirm method calls.

## Prerequisites & Environment Setup

(No changes from previous version – see earlier document for PowerShell profile variables.)

## Standardized .csx Template Structure (Updated)

```csharp
// ================ 1. Usings ================
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq; // for control finding
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Services.Abstractions;
using WileyWidget.McpServer.Helpers;

// ================ 2. Helper Types ================

// NoOpLogger – unchanged
public class NoOpLogger<T> : ILogger<T> { ... }

// Enhanced Fake Service with spy counters
public class FakeYourService : IYourService
{
    public int ConnectCallCount { get; private set; }
    public int DisconnectCallCount { get; private set; }
    public int SyncCallCount { get; private set; }
    public int ImportCallCount { get; private set; }
    public bool ShouldSucceed { get; set; } = true; // for negative testing

    public Task<bool> ConnectAsync(CancellationToken _ = default)
    {
        ConnectCallCount++;
        return Task.FromResult(ShouldSucceed);
    }

    public Task DisconnectAsync(CancellationToken _ = default)
    {
        DisconnectCallCount++;
        return Task.CompletedTask;
    }

    // ... other methods with counters
}

// SingleInstanceServiceProvider – unchanged

// ================ 3. Test Scenarios ================

async Task Test_HappyPath_WithInteractions()
{
    // Setup
    var logger = new NoOpLogger<YourPanelViewModel>();
    var fakeService = new FakeYourService { ShouldSucceed = true };
    var vm = new YourPanelViewModel(logger, fakeService);
    var sp = new SingleInstanceServiceProvider(typeof(YourPanelViewModel), vm, MockFactory.CreateTestServiceProvider());
    var panel = new YourPanel(sp);

    var form = MockFactory.CreateMockMainForm();
    form.Controls.Add(panel);
    SyncfusionTestHelper.TryLoadForm(form, waitMs: 500);

    await vm.RefreshAsync();

    // === UI Interaction Checks ===

    // 1. Find controls (require meaningful Name properties in designer)
    var connectButton = panel.Controls.Find("connectButton", true).FirstOrDefault() as ButtonBase
                        ?? throw new Exception("connectButton not found");
    var syncButton = panel.Controls.Find("syncButton", true).FirstOrDefault() as ButtonBase
                     ?? throw new Exception("syncButton not found");
    var dataGrid = panel.Controls.Find("mainGrid", true).FirstOrDefault() as Syncfusion.WinForms.DataGrid.SfDataGrid
                   ?? throw new Exception("mainGrid not found");

    // 2. Initial state
    if (!connectButton.Enabled) throw new Exception("Connect button should be enabled initially");

    // 3. Simulate button click
    connectButton.PerformClick();
    // Since fake is instant, no await needed; real async would complete immediately in test
    if (fakeService.ConnectCallCount != 1) throw new Exception("ConnectAsync not called");
    if (!vm.IsConnected) throw new Exception("ViewModel not updated after connect");

    // 4. Command state after interaction
    if (!syncButton.Enabled) throw new Exception("Sync button should be enabled after connect");

    // 5. Simulate sync click
    syncButton.PerformClick();
    if (fakeService.SyncCallCount != 1) throw new Exception("SyncDataAsync not called");

    // 6. Grid interaction (example with mock data)
    fakeService.ReturnSomeData = true; // configure to return items
    await vm.RefreshDataCommand.ExecuteAsync(null);
    if (dataGrid.Rows.Count == 0) throw new Exception("Grid not populated");

    var testItem = vm.ItemsSource.First();
    dataGrid.SelectedItem = testItem; // triggers selection changed
    if (vm.SelectedItem != testItem) throw new Exception("Selection binding failed");

    Console.WriteLine("Happy path with interactions: PASSED");
}

async Task Test_ErrorPath_WithInteractions()
{
    var fakeService = new FakeYourService { ShouldSucceed = false };
    // ... similar setup

    var connectButton = panel.Controls.Find("connectButton", true).First() as ButtonBase;

    connectButton.PerformClick();
    if (fakeService.ConnectCallCount != 1) throw new Exception("Service not called on failure");
    if (vm.IsConnected) throw new Exception("Should remain disconnected");
    if (vm.Logs.All(l => !l.Contains("error", StringComparison.OrdinalIgnoreCase)))
        throw new Exception("Expected error log");
}

// ================ 4. Main Execution ================
await Test_HappyPath_WithInteractions();
await Test_ErrorPath_WithInteractions();

var form = panel.FindForm();
form?.Dispose();

Console.WriteLine("All scenarios with UI interactions: PASSED");
```

## New Section: UI Interaction Checks

### Principles

- **Require control naming**: All interactive controls must have meaningful `Name` properties in the designer (e.g., `connectButton`, `syncGrid`). This enables reliable `Controls.Find`.
- **Spy on services**: Add public counters/flags to fake services for every state-changing method.
- **Instant fakes**: All fake methods return completed tasks → interactions complete synchronously in tests.
- **Thread safety**: All interactions occur after `TryLoadForm`. If needed, use `panel.Invoke(() => ...)`.

### Supported Interactions

| Interaction        | Code Pattern                                                   | Typical Assertions                                            |
| ------------------ | -------------------------------------------------------------- | ------------------------------------------------------------- |
| Button click       | `button.PerformClick();`                                       | Service call count, VM property change, command enabled state |
| Grid row selection | `grid.SelectedItem = item;` or `grid.SelectedIndex = 0;`       | `vm.SelectedItem` updated, detail views populated             |
| TextBox input      | `textBox.Text = "test"; textBox_Leave(null, EventArgs.Empty);` | VM property updated, validation messages                      |
| ComboBox selection | `combo.SelectedItem = item;`                                   | Filter applied, grid refreshed                                |
| CheckBox toggle    | `checkBox.Checked = !checkBox.Checked;`                        | VM flag changed, UI state updated                             |

### Best Practices for Interactions

1. Always validate **initial enabled/visible state**.
2. Perform interaction → assert **service called** (via counter).
3. Assert **ViewModel updated**.
4. Assert **secondary UI changes** (other buttons enabled/disabled).
5. Include interactions in **both happy and error paths**.
6. If command is async and shows busy indicator, await a short `Task.Delay(100)` or poll `vm.IsBusy == false`.
7. For Syncfusion-specific controls (SfDataGrid, SfButton, etc.), refer to Syncfusion documentation for event patterns: https://help.syncfusion.com/windowsforms/datagrid/selection

## Workflow Updates (When Creating/Updating Tests)

- After basic state checks, add at least **two interactions** per major command/path.
- Name all interactive controls consistently.
- Update fake services with counters for every command-bound method.

This enhanced workflow now provides near-complete validation of panel behavior, including real user interaction paths, while remaining fast and fully headless.
can
