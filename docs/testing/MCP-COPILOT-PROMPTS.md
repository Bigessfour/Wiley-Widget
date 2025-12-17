# WileyWidget MCP Server - Copilot Prompt Library

This document provides example prompts and workflows for using the **WileyWidget MCP Server** through GitHub Copilot Chat in VS Code. These prompts leverage the four core MCP tools (`EvalCSharp`, `ValidateFormTheme`, `InspectSfDataGrid`, `RunHeadlessFormTest`) for rapid UI development and validation.

## Table of Contents

- [Quick Start](#quick-start)
- [EvalCSharp Prompts](#evalcsharp-prompts)
- [ValidateFormTheme Prompts](#validateformtheme-prompts)
- [InspectSfDataGrid Prompts](#inspectfsdatagrid-prompts)
- [RunHeadlessFormTest Prompts](#runheadlessformtest-prompts)
- [Combined Workflow Prompts](#combined-workflow-prompts)
- [Advanced Scenarios](#advanced-scenarios)

## Quick Start

### Verify MCP Connection

```
List available MCP tools for wileywidget-ui-mcp
```

Expected response should include:

- `EvalCSharp`
- `ValidateFormTheme`
- `InspectSfDataGrid`
- `RunHeadlessFormTest`

---

## EvalCSharp Prompts

The `EvalCSharp` tool is the most powerful - it provides dynamic C# REPL-like execution with full access to WileyWidget assemblies. Use it for rapid prototyping and validation.

### Basic Form Instantiation

```
Using EvalCSharp, instantiate AccountsForm with a mock MainForm and check if it loads successfully.
```

### Theme Validation (Quick Check)

```
Using EvalCSharp, create BudgetOverviewForm and use SyncfusionTestHelper.ValidateNoManualColors() to check for theme violations. Return the count of violations.
```

### Grid Column Inspection

```
Using EvalCSharp, instantiate AccountsForm, find the first SfDataGrid control, and list all column names.
```

**Expected Code Pattern:**

```csharp
var mockMainForm = MockFactory.CreateMockMainForm();
var form = new WileyWidget.WinForms.Forms.AccountsForm(mockMainForm);
SyncfusionTestHelper.TryLoadForm(form);

var grid = form.Controls.OfType<Syncfusion.WinForms.DataGrid.SfDataGrid>().FirstOrDefault();
if (grid != null)
{
    var columns = grid.Columns.Select(c => c.MappingName).ToList();
    return string.Join(", ", columns);
}
return "No grid found";
```

### Control Property Verification

```
Using EvalCSharp, check if DashboardForm has a chart control and verify its ChartType property.
```

### Mock Data Binding Test

```
Using EvalCSharp, create a BudgetOverviewForm, bind mock budget data with 5 entries, and verify the grid's row count.
```

### Custom Assertion

```
Using EvalCSharp, create SettingsForm and assert that all TextBox controls have a minimum width of 200 pixels. Return "PASS" or "FAIL" with details.
```

### Performance Check

```
Using EvalCSharp, measure how long it takes to instantiate and load ChartForm. Return the duration in milliseconds.
```

**Expected Code Pattern:**

```csharp
var sw = System.Diagnostics.Stopwatch.StartNew();
var mockMainForm = MockFactory.CreateMockMainForm();
var form = new WileyWidget.WinForms.Forms.ChartForm(mockMainForm);
SyncfusionTestHelper.TryLoadForm(form);
sw.Stop();
return $"{sw.ElapsedMilliseconds}ms";
```

---

## ValidateFormTheme Prompts

The `ValidateFormTheme` tool performs comprehensive theme compliance checks per the SfSkinManager rules.

### Single Form Validation

```
Validate AccountsForm theme using ValidateFormTheme tool.
```

```
Check if BudgetOverviewForm complies with Office2019Colorful theme.
```

### Batch Validation (Manual)

```
Validate theme compliance for all these forms:
- AccountsForm
- BudgetOverviewForm
- ChartForm
- DashboardForm
```

Copilot will invoke `ValidateFormTheme` for each form sequentially.

### Expected Theme Verification

```
Using ValidateFormTheme, check if SettingsForm uses Office2019Colorful theme and has no manual color assignments.
```

### Pre-Commit Check

```
I'm about to commit changes to CustomersForm. Validate its theme before I push.
```

---

## InspectSfDataGrid Prompts

The `InspectSfDataGrid` tool provides detailed grid configuration and data inspection.

### Basic Grid Inspection

```
Inspect the SfDataGrid on AccountsForm.
```

```
Using InspectSfDataGrid, show me the columns configured on BudgetOverviewForm's grid.
```

### Specific Grid by Name

```
Inspect the "sfDataGridTransactions" grid on CustomersForm.
```

### Column Configuration Check

```
Using InspectSfDataGrid, verify that AccountsForm grid has the following columns: AccountNumber, AccountName, Balance, Type. Also check if they're all visible.
```

### Data Binding Verification

```
Inspect BudgetOverviewForm grid and show sample data for the first 3 rows.
```

### Grid Settings Audit

```
Using InspectSfDataGrid, check if AccountsForm grid has sorting and filtering enabled.
```

---

## RunHeadlessFormTest Prompts

The `RunHeadlessFormTest` tool executes existing .csx test scripts or inline test code.

### Run Existing Test Script

```
Run the headless test script at tests/WileyWidget.UITests/Scripts/AccountsFormTest.csx
```

### Inline Test Execution

```
Using RunHeadlessFormTest, execute this inline test for BudgetOverviewForm:
- Instantiate form with mock data
- Verify grid has at least 5 rows
- Check that total budget is greater than zero
```

### Regression Test

```
Run all .csx test scripts in tests/WileyWidget.UITests/Scripts/ directory.
```

### Custom Test Code

```
Using RunHeadlessFormTest, create an inline test that:
1. Creates AccountsForm
2. Finds the search TextBox
3. Verifies it has MaxLength = 50
4. Returns "PASS" if true, "FAIL" otherwise
```

---

## Combined Workflow Prompts

These prompts chain multiple MCP tools for comprehensive validation.

### Complete Form Audit

```
For AccountsForm, perform a complete audit:
1. Validate theme compliance (ValidateFormTheme)
2. Inspect the SfDataGrid configuration (InspectSfDataGrid)
3. Run a quick test to ensure it loads without errors (EvalCSharp)
```

### New Form Verification

```
I just created PaymentsForm. Help me verify it's production-ready:
- Check theme compliance
- Verify it has at least one SfDataGrid
- Test that it loads in under 500ms
```

### Debug Grid Issue

```
BudgetOverviewForm grid isn't showing data. Help me debug:
1. Inspect the grid configuration
2. Check if DataSource is null
3. Verify columns are visible
```

**Expected Flow:**

1. Copilot invokes `InspectSfDataGrid` → shows columns/binding
2. Copilot invokes `EvalCSharp` with code to check `grid.DataSource`
3. Copilot analyzes results and suggests fixes

### Pre-Release Validation

```
I'm preparing for a release. Validate all main forms:
- AccountsForm
- BudgetOverviewForm
- ChartForm
- DashboardForm
- CustomersForm

Check theme compliance and grid configurations for each.
```

---

## Advanced Scenarios

### Comparative Analysis

```
Using EvalCSharp, compare the load times of AccountsForm vs CustomersForm and report which is faster.
```

### Data-Driven Testing

```
Using EvalCSharp, create BudgetOverviewForm with 3 different mock datasets (5 rows, 50 rows, 500 rows) and measure grid rendering time for each.
```

### Control Tree Inspection

```
Using EvalCSharp, walk the control tree of DashboardForm and list all Syncfusion controls (SfDataGrid, SfChart, SfButton, etc.) with their names.
```

**Expected Code Pattern:**

```csharp
var mockMainForm = MockFactory.CreateMockMainForm();
var form = new WileyWidget.WinForms.Forms.DashboardForm(mockMainForm);
SyncfusionTestHelper.TryLoadForm(form);

var sfControls = new List<string>();
void Walk(Control control)
{
    if (control.GetType().Namespace?.StartsWith("Syncfusion") == true)
        sfControls.Add($"{control.Name} ({control.GetType().Name})");
    foreach (Control child in control.Controls)
        Walk(child);
}
Walk(form);
return string.Join("\n", sfControls);
```

### Accessibility Audit

```
Using EvalCSharp, check that all Button and TextBox controls on SettingsForm have non-empty AccessibleName properties.
```

### Memory Leak Detection

```
Using EvalCSharp, instantiate AccountsForm 100 times in a loop and measure memory before/after to detect potential leaks.
```

**Expected Code Pattern:**

```csharp
GC.Collect();
GC.WaitForPendingFinalizers();
var memBefore = GC.GetTotalMemory(true);

for (int i = 0; i < 100; i++)
{
    var mock = MockFactory.CreateMockMainForm();
    var form = new WileyWidget.WinForms.Forms.AccountsForm(mock);
    form.Dispose();
    mock.Dispose();
}

GC.Collect();
GC.WaitForPendingFinalizers();
var memAfter = GC.GetTotalMemory(true);

var leakMb = (memAfter - memBefore) / 1024.0 / 1024.0;
return $"Memory delta: {leakMb:F2} MB";
```

### Theme Switching Test

```
Using EvalCSharp, create AccountsForm, apply Office2019Colorful theme, then switch to MaterialDark, and verify both transitions succeed without errors.
```

### Control State Validation

```
Using EvalCSharp, create BudgetOverviewForm and verify that the "Save" button is initially disabled and the "Cancel" button is enabled.
```

---

## Tips for Effective Prompts

### Be Specific with Form Names

✅ **Good:** "Validate AccountsForm theme"  
❌ **Vague:** "Check the accounts form"

### Use Fully Qualified Types in EvalCSharp

✅ **Good:** `new WileyWidget.WinForms.Forms.AccountsForm(...)`  
❌ **Bad:** `new AccountsForm(...)` (may not resolve)

### Request Structured Output

✅ **Good:** "Return a list of column names separated by commas"  
❌ **Vague:** "Show me the columns"

### Chain Operations Logically

✅ **Good:** "First validate theme, then inspect grid, then run test"  
❌ **Bad:** "Do everything at once" (hard to debug)

### Provide Context for Debugging

✅ **Good:** "Grid shows no data. Inspect binding and sample 3 rows"  
❌ **Bad:** "Grid broken, fix it"

---

## Common Patterns Reference

### Pattern 1: Quick Theme Check

```
Using EvalCSharp:
var mock = MockFactory.CreateMockMainForm();
var form = new WileyWidget.WinForms.Forms.<FormName>(mock);
SyncfusionTestHelper.TryLoadForm(form);
var violations = SyncfusionTestHelper.ValidateNoManualColors(form);
return violations.Count == 0 ? "✅ PASS" : $"❌ {violations.Count} violations";
```

### Pattern 2: Grid Column Names

```
Using EvalCSharp:
var mock = MockFactory.CreateMockMainForm();
var form = new WileyWidget.WinForms.Forms.<FormName>(mock);
SyncfusionTestHelper.TryLoadForm(form);
var grid = form.Controls.OfType<Syncfusion.WinForms.DataGrid.SfDataGrid>().FirstOrDefault();
return grid != null
    ? string.Join(", ", grid.Columns.Select(c => c.MappingName))
    : "No grid found";
```

### Pattern 3: Control Search

```
Using EvalCSharp:
var mock = MockFactory.CreateMockMainForm();
var form = new WileyWidget.WinForms.Forms.<FormName>(mock);
SyncfusionTestHelper.TryLoadForm(form);
var button = form.Controls.Find("btnSave", true).FirstOrDefault();
return button != null ? $"Found: {button.GetType().Name}" : "Not found";
```

### Pattern 4: Performance Measurement

```
Using EvalCSharp:
var sw = System.Diagnostics.Stopwatch.StartNew();
// ... operation to measure ...
sw.Stop();
return $"{sw.ElapsedMilliseconds}ms";
```

---

## Troubleshooting Prompts

### When MCP Server Doesn't Respond

```
Check if wileywidget-ui-mcp server is running and list its available tools.
```

### When Form Fails to Load

```
Using EvalCSharp, try to instantiate <FormName> and wrap it in try-catch to see the full exception message.
```

### When Grid Shows No Data

```
Using EvalCSharp, instantiate <FormName>, inspect the grid's DataSource property, and report its type and count (if collection).
```

### When Theme Validation Fails

```
Using ValidateFormTheme on <FormName>, then use EvalCSharp to manually check which controls have custom BackColor/ForeColor values.
```

---

## Example Session Transcript

**User:** "I just refactored AccountsForm. Verify it's still working correctly."

**Copilot:** [Invokes ValidateFormTheme → ✅ PASS]  
**Copilot:** [Invokes InspectSfDataGrid → Shows 8 columns configured]  
**Copilot:** [Invokes EvalCSharp to test load time → 187ms]

**Response:** "AccountsForm validation complete:

- ✅ Theme compliance: PASS
- ✅ Grid configuration: 8 columns properly configured
- ✅ Load time: 187ms (within acceptable range)

No issues detected. Form is production-ready."

---

**User:** "BudgetOverviewForm grid only shows 3 columns but should show 8."

**Copilot:** [Invokes InspectSfDataGrid]

**Response:** "Found issue: Grid has 8 columns configured, but 5 have `Visible=False`:

- MonthlyBudget
- ActualSpend
- Variance
- PercentUsed
- Category

Would you like me to set these columns to visible?"

**User:** "Yes, make them visible."

**Copilot:** [Generates fix, applies edit, validates]

**Response:** "✅ Fixed: All 8 columns now visible. Re-validated with InspectSfDataGrid."

---

## See Also

- [WileyWidget MCP Server README](../../tools/WileyWidgetMcpServer/README.md)
- [MCP Integration Guide](MCP-INTEGRATION-GUIDE.md)
- [Syncfusion Theme Guidelines](../../.vscode/copilot-instructions.md#syncfusion-sfskinmanager-theme-enforcement)
- [UI Test Helpers Reference](../../tests/WileyWidget.UITests/README.md)
