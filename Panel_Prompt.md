# Panel Prompt — Evaluation Checklist

Evaluate the following C# WinForms UserControl code for a municipal financial panel in the Wiley-Widget project (repo: <https://github.com/Bigessfour/Wiley-Widget>).

- Use Syncfusion Windows Forms v32.1.19 only (reference: <https://help.syncfusion.com/windowsforms/overview>).
- The panel should be a fully functioning, polished component for creating/editing data, with MVVM binding, validation, and proper UI/UX.

---

## Requested Review Output

Please perform a thorough review across these categories. For each, check for correctness, completeness, and best practices. Validate all Syncfusion controls and properties against the v32.1.19 API docs — flag any misuse, deprecations, or non-Syncfusion controls.

Provide:

- Pass/Fail verdict per category.
- Detailed explanations of issues (with line numbers if possible).
- Suggested code fixes or improvements.
- Overall score (1–10) and recommendations for polish.

---

## Categories To Evaluate

1. Theme Violations
   - Ensure no code contradicts `SfSkinManager` theme management.
   - Remove hard-coded theme names (e.g., `Office2019Colorful`) and direct color assignments.
   - Use Syncfusion API (`SkinManager.LoadAssembly`, `SfSkinManager.SetVisualStyle`, `ThemeName` on controls) where appropriate.

2. Control Usage and API Compliance
   - Use only Syncfusion v32.1.19 controls where feasible (e.g., `SfComboBox`, `SfNumericTextBox`, `SfDataGrid`) and validate properties/methods against the v32.1.19 docs.
   - Flag any vanilla WinForms controls that should be replaced for theme consistency or correctness.
   - Verify correct property usage (examples: `FormatMode`, `DropDownStyle`, `GridDateTimeColumn` vs `GridTextColumn` for timestamps, `GridNumericColumn` for numeric fields).

3. Layout and UI Design
   - Check spacing, padding, row heights, widths, and alignment.
   - Validate resizing behavior (`Anchor`/`Dock`) and that controls behave well on form resize.
   - Accessibility: `AccessibleName`, `AccessibleDescription`, `TabIndex` order, and `ToolTip` presence.

4. Data Binding and MVVM
   - Controls bound correctly to view model properties (`DataBindings.Add` + `OnPropertyChanged`).
   - Use `BindingSource` where appropriate and avoid direct cast assumptions against possibly-missing model types.
   - Commands wired and async-safe (use `async` commands, `Task`-returning handlers, `CancellationToken` support, no `.Result`/`.Wait()`).

5. Validation and Error Handling
   - Use `ErrorProvider` and `ErrorProviderBinding` for field validation; ensure required fields are validated and error messages are user-friendly.
   - Ensure operations log failures (e.g., Serilog/Microsoft logging) and surface actionable messages to users.

6. Event Handling and Functionality
   - Events hooked and unhooked correctly (subscribe/unsubscribe pattern), named handlers for unsubscribing in `Dispose`.
   - Save/cancel workflows validate and handle cancellation/exception cases.
   - Async loads (`LoadDataAsync`) should support `CancellationToken`, exception handling and a progress indicator.

7. Theming and Styling
   - All theming delegated to `SfSkinManager` — no manual `BackColor`/`ForeColor` except semantic status colors (e.g., `Color.Red` for errors).
   - Set `ThemeName` on Syncfusion controls when created dynamically.

8. Cleanup and Resource Management
   - Implement `Dispose(bool)` overrides to unsubscribe events, stop timers, and dispose `BindingSource`, `ErrorProvider`, and controls.
   - Avoid leaks from event handlers and timers.

9. Security and Best Practices
   - Sanitize inputs (e.g., `MaxLength`, numeric ranges) and avoid patterns that risk injection; use parameterized data access elsewhere in app.
   - Proper formatting for currency/numeric fields (culture-aware formatting).

10. Overall Polish and Reusability
    - `InitializeComponent` separation, concise methods, tests for critical logic, edge case handling (empty data sets, mid-load disposal), tooltips and keyboard navigation.

---

## Deliverables / Expectations

- A pass/fail evaluation for each category with line-level issues where possible.
- Suggested code patches (minimal, focused changes) to fix violations.
- A final score (1–10) and checklist of remaining polishing tasks (if any).

---

## Completed (from this prompt)

- AccountsEditPanel.cs
- AccountsPanel.cs
- ActivityLogPanel.cs
- AnalyticsPanel.cs (theme/state cleanup and entity combo disposal review)
- AuditLogPanel.cs (validation of Syncfusion usage and Dispose hygiene)
- BudgetAnalyticsPanel.cs (architectural review, needs ScopedPanelBase migration)
- CustomersPanel.cs (theme/style review flagged manual theming)

---

## Panels Interacted With (do not repeat methods)

Below is the list of panels I recently reviewed/modified — include these when planning further edits so we avoid duplicating method-level changes across files:

- AccountsEditPanel.cs
- AccountsPanel.cs
- ActivityLogPanel.cs
- CsvMappingWizardPanel.cs
- AccountEditPanel.cs
- WarRoomPanel.cs
- BudgetPanel.cs
- SettingsPanel.cs

---

## Notes

- Use the Syncfusion docs for v32.1.19 as the single source of truth for control APIs and theming guidance: [Syncfusion Windows Forms](https://help.syncfusion.com/windowsforms/overview)
- Prefer `BindingSource`-backed bindings and `SfSkinManager`-driven theming. Avoid manual color assignments except for semantic status indicators.

---

## 11. ICompletablePanel Implementation (Base Properties)

All panels deriving from `ScopedPanelBase<TViewModel>` automatically inherit these completion/validation properties via the `ICompletablePanel` interface. These are **runtime checked** to gate save/cancel/workflow operations:

- **`bool IsLoaded`** — True after ViewModel is resolved and `OnViewModelResolved()` completes. Indicates the panel is ready for user interaction.
- **`bool IsBusy`** — Set to true/false by derived panels during async operations (e.g., loading, saving). Bind to UI disable/spinner visibility.
- **`bool HasUnsavedChanges`** — Tracks unsaved edits. Set via `SetHasUnsavedChanges(bool)` in derived panels. Gating: disable Save button if false.
- **`PanelMode Mode`** — Enum `{ View, Create, Edit }` — derived panels set mode to control UI field editability and command availability.
- **`IReadOnlyList<ValidationItem> ValidationErrors`** — List of field-level errors with (FieldName, Message, Severity, Control). Populated by `ValidateAsync()`.
- **`bool IsValid`** — Aggregated read-only property: `ValidationErrors.Count == 0`. Gating: enable Save only if true.
- **`CancellationTokenSource? CurrentOperationCts`** — Token for cancelling running operations; auto-disposed on panel Dispose.

**Virtual Methods (override in derived panels as needed):**

- **`Task<ValidationResult> ValidateAsync(CancellationToken ct)`** — Perform sync/async validation (e.g., uniqueness checks, server calls). Return `ValidationResult.Success` or `ValidationResult.Failed(items)`.
  - Example: Account panel validates uniqueness of AccountNumber.
  - Called before Save to ensure only valid data persists.
- **`Task SaveAsync(CancellationToken ct)`** — Persist changes. Use `IsBusy = true/false` and `SetHasUnsavedChanges()` to update state.
- **`Task LoadAsync(CancellationToken ct)`** — Load/refresh data. Use `RegisterOperation()` to get a `CancellationToken` and set `IsBusy`.
- **`void FocusFirstError()`** — Focus the first control in `ValidationErrors` (default behavior: find and focus the associated Control).

**Helper Methods (protected, for derived panels):**

- **`void SetHasUnsavedChanges(bool value)`** — Update the unsaved flag and trigger `StateChanged` event for UI bindings.
- **`CancellationToken RegisterOperation()`** — Cancel any prior operation and return a fresh token for new async work.
- **`void CancelCurrentOperation()`** — Safely cancel and dispose the current operation token.
- **`event EventHandler StateChanged`** — Fired whenever `IsLoaded`, `IsBusy`, `HasUnsavedChanges`, or `Mode` changes, allowing UI bindings to react.
- **`event PropertyChangedEventHandler PropertyChanged`** — `INotifyPropertyChanged` support for MVVM binding.

**Usage Pattern (in derived panels):**

```csharp
protected override async Task LoadAsync(CancellationToken ct)
{
    try
    {
        var token = RegisterOperation(); // Get cancellation token & set IsBusy = true
        IsBusy = true;
        // ... load data via ViewModel.LoadAsync(token)
        SetHasUnsavedChanges(false); // Clear unsaved flag after load
    }
    catch (OperationCanceledException)
    {
        Logger?.LogDebug("Load cancelled");
    }
    finally
    {
        IsBusy = false; // Clear busy indicator
    }
}

public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
{
    var errors = new List<ValidationItem>();
    // Validate fields, call ViewModel.ValidateAsync() for server checks
    if (someField == null)
        errors.Add(new ValidationItem("SomeField", "Required", ValidationSeverity.Error, controlRef));
    return errors.Count > 0 ? ValidationResult.Failed(errors.ToArray()) : ValidationResult.Success;
}

public override async Task SaveAsync(CancellationToken ct)
{
    try
    {
        var token = RegisterOperation();
        IsBusy = true;
        await ViewModel.SaveAsync(token);
        SetHasUnsavedChanges(false);
    }
    finally
    {
        IsBusy = false;
    }
}
```

**Binding in XAML/Designer (if applicable):**

- Bind `SfButton.Enabled` to `Panel.IsValid && Panel.HasUnsavedChanges && !Panel.IsBusy` for Save button.
- Bind `ProgressBar.Visible` or overlay `Opacity` to `Panel.IsBusy` for visual feedback.
- Listen to `StateChanged` event to refresh button states or summary displays.

**Testing & Automation:**

- Use `IsLoaded` to confirm initialization before running test steps.
- Mock `ValidateAsync` in unit tests to verify error handling.
- Check `HasUnsavedChanges` after edits to confirm dirty tracking.
- Use `CurrentOperationCts.Token` to simulate cancellation in async tests.

---

## MCP Tool Integration for Automated Validation

For batch or automated runs, use the **BatchValidatePanelsTool** in the MCP server. This tool validates panels headlessly against all categories defined in this prompt.

### Tool Details

- **Tool Name:** `BatchValidatePanels`
- **Namespace:** `WileyWidget.McpServer.Tools`
- **Location:** `tools/SyncfusionMcpServer/tools/WileyWidgetMcpServer/Tools/BatchValidatePanelsTool.cs`
- **Chosen approach:** Dedicated `BatchValidatePanelsTool` over ad-hoc `EvalCSharp` for scalable batch runs and CI/CD integration.

### Parameters

| Parameter | Type | Default | Description |
| - | - | - | - |
| `panelTypeNames` | `string[]?` | `null` | Optional array of fully qualified panel type names. If null/empty, validates all UserControls in `WileyWidget.WinForms.Controls` namespace. |
| `expectedTheme` | `string` | `Office2019Colorful` | Theme name to validate against. |
| `failFast` | `bool` | `false` | Stop validation on first failure. |
| `outputFormat` | `string` | `text` | Output format: `text`, `json`, or `html`. |

### Validation Categories

The tool checks the following categories per [Panel_Prompt.md](#categories-to-evaluate):

1. **Theme Compliance** – No manual colors, `SfSkinManager` authority enforced, `ThemeName` set on Syncfusion controls.
2. **Control Usage & Compliance** – Syncfusion v32.1.19 API validation, control properties checked.
3. **MVVM Bindings** – ViewModel presence, DataBindings validation, proper binding setup.
4. **Validation Setup** – ErrorProvider configured, binding mappings validated.
5. **Manual Color Violations** – Recursive tree walk flags BackColor/ForeColor assignments (allows semantic colors: Red/Green/Orange).

### Example Calls

**Validate all panels, text output:**

```powershell
BatchValidatePanels($null, "Office2019Colorful", $false, "text")
```

**Validate specific panels, JSON output:**

```powershell
BatchValidatePanels(
  @(
    "WileyWidget.WinForms.Controls.SettingsPanel",
    "WileyWidget.WinForms.Controls.BudgetPanel"
  ),
  "Office2019Colorful",
  $false,
  "json"
)
```

**Quick debug (fail-fast, HTML report):**

```powershell
BatchValidatePanels($null, "Office2019Colorful", $true, "html")
```

### Integration with CI/CD

The tool is designed for headless, automated validation:

- **Local Testing:** Run via MCP Inspector (see [README.md](README.md#mcp-inspector)) during development.
- **CI/CD:** Call from `.github/workflows/` (e.g., `syncfusion-theming.yml`, `e2e-headless-mcp.yml`).
- **Batch Reporting:** JSON/HTML output formats integrate with dashboards or issue tracking.

### Output Examples

**Text Output (Human-Readable):**

```text
═══════════════════════════════════════════════════════════════════════
📋 PANEL BATCH VALIDATION REPORT
═══════════════════════════════════════════════════════════════════════
Total Panels: 34
Passed: 31
Failed: 3
Duration: 5.42s

❌ Some panels failed validation:

  Panel: SettingsPanel
  Type:  WileyWidget.WinForms.Controls.SettingsPanel
    ❌ Theme: Not valid for Office2019Colorful
    ❌ Manual Colors: 2 violation(s)
    ...
```

**JSON Output (Structured):**

```json
{
  "summary": {
    "totalPanels": 34,
    "passed": 31,
    "failed": 3,
    "durationSeconds": 5.42
  },
  "results": [
    {
      "panelTypeName": "WileyWidget.WinForms.Controls.SettingsPanel",
      "panelName": "SettingsPanel",
      "passed": false,
      "expectedTheme": "Office2019Colorful",
      "validation": {
        "themeValid": false,
        "controlCompliance": true,
        "mvvmValid": true,
        "validationSetupValid": true,
        "violationCount": 2
      },
      "manualColorViolations": [
        "settingsTabControl.BackColor = RGB(255, 255, 255) (manual color - use SkinManager instead)"
      ],
      "durationMs": 145
    }
  ]
}
```

**HTML Output (Visual Report):**

- Interactive visual report with pass/fail color coding, violation lists, and summary metrics.
- Suitable for dashboards or documentation.

### Helper Classes

The tool reuses/introduces:

- **PanelTypeCache** – Thread-safe discovery of all 34 UserControl types; mirrors FormTypeCache.
- **PanelInstantiationHelper** – DI mocking for realistic panel instantiation; handles `IServiceScopeFactory`, `ILogger<T>`, ViewModels.
- **PanelValidationResult** – Structured result object with per-category pass/fail flags and violation details.
- **Existing Helpers** – Reuses `SyncfusionTestHelper` for theme/color validation, `MockFactory` for DI mocking.

### Running the Tool

1. **MCP Inspector (Interactive):**
   - Open MCP Inspector in VS Code: `Tools > MCP Inspector`
   - Select `BatchValidatePanels` from the tools list
   - Fill in parameters and execute
   - View results in output pane

2. **Via C# Code (Test/Workflow):**

```csharp
var report = BatchValidatePanelsTool.BatchValidatePanels(
    null,
    "Office2019Colorful",
    false,
    "html"
);
Console.WriteLine(report);
```

1. **CI/CD Integration:**
   - Call from `.github/workflows/` via dotnet script or MCP CLI
   - Parse JSON output for pass/fail gating
   - Archive HTML report as artifact

---

## Review Progress (January 19, 2026)

- **BudgetOverviewPanel.cs** – Completed the first panel in the queue; theme, layout, controls, and view-model plumbing look good, but the combo box and toolbar buttons are wired with anonymous handlers that never get detached, so repeated creation of this panel leaks event subscriptions. Next panel to tackle: `BudgetPanel.cs` (also confirm its MVVM validation and disposal hygiene).
- **BudgetPanel.cs** – Reviewed and flagged three hazards: filter controls (\_searchTextBox, combo boxes, threshold text, and checkboxes) register events but Dispose never detaches them, so panel reloads accumulate handlers; the embedded `CsvMappingWizardPanel` wires `MappingApplied/Cancelled` without cleanup. Add/Edit/Delete dialogs run ViewModel mutations via fire-and-forget `Task.Run` invocations and never surface `ValidationErrors` or the scoped panel’s `IsBusy/HasUnsavedChanges` state, so Save/Cancel gating and user-friendly validation are missing. Validation not run yet—please execute the BudgetPanel validation suite once the event cleanup is addressed.
