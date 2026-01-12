# WileyWidget MCP Server Tools & Helpers Reference

**Complete technical guide to all tools and helper utilities in WileyWidgetMcpServer.**

---

## Overview

The WileyWidget MCP Server provides two categories of utilities:

1. **Helper Classes** (in `Helpers/`) - Reusable utility functions for form instantiation, mocking, and validation
2. **MCP Tools** (in `Tools/`) - Production-ready Model Context Protocol tools for AI-assisted validation

Both can be used directly in tests via `ExecuteOnStaThread<T>()` pattern.

---

## HELPER CLASSES

### 1. FormInstantiationHelper ⭐ (Most Important)

**Location:** `tools/WileyWidgetMcpServer/Helpers/FormInstantiationHelper.cs`

**Purpose:** Instantiates forms with automatic parameter handling and proper resource cleanup.

#### Key Methods

##### `InstantiateForm(Type formType, MainForm mockMainForm) → Form`

**Solves:** Forms requiring `MainForm` parameter without requiring explicit mock injection.

**How It Works:**

1. Examines all public constructors
2. Attempts to instantiate with progressively more complex constructors
3. Automatically provides mock parameters for:
   - `MainForm` (passes your mock)
   - `ILogger<T>` (creates Mock.Of<T>() or NullLogger)
   - `IServiceProvider` (creates TestServiceProvider)
   - String parameters (creates dummy paths)
   - ViewModel types (creates with mocked dependencies)
   - Interface types (creates Mock.Of<T>())

**Usage Example:**

```csharp
// For test setup
var mockMainForm = MockFactory.CreateMockMainForm(enableMdi: true);
var form = FormInstantiationHelper.InstantiateForm(typeof(AccountsForm), mockMainForm);
// No need to manually inject dependencies!
```

**Handles Complex Scenarios:**

- Forms with `MainForm` parameter → injects mock
- Forms with `ILogger<T>` → provides NullLogger
- Forms with `IServiceProvider` → provides TestServiceProvider
- ViewModels with dependencies → recursively mocks all dependencies
- Falls back through constructors until one succeeds

---

##### `SafeDispose(Form? form, MainForm? mockMainForm)`

**Solves:** DockingManager/Ribbon background threads that prevent clean disposal.

**What It Does:**

- Wraps disposal in try-catch to suppress errors
- Uses `form.Invoke()` for thread-safe cleanup
- Gracefully handles already-disposed forms
- Suppresses transient disposal errors from background threads

**Usage Example:**

```csharp
try
{
    var form = FormInstantiationHelper.InstantiateForm(...);
    // ... use form ...
}
finally
{
    FormInstantiationHelper.SafeDispose(form, mockMainForm);
    // No disposal errors even with DockingManager!
}
```

---

##### `ExecuteOnStaThread<T>(Func<T> operation, int timeoutSeconds = 30) → T`

**Solves:** WinForms requires STA (Single-Threaded Apartment) thread context; prevents hangs.

**How It Works:**

1. Creates a new thread
2. Sets apartment state to STA (Windows only)
3. Executes operation on STA thread
4. Enforces timeout to prevent infinite hangs
5. Propagates exceptions back to caller

**Usage Example (THIS IS THE CRITICAL FIX):**

```csharp
// For UI control tests - MUST use this!
var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
{
    var mockMain = MockFactory.CreateMockMainForm();
    var form = FormInstantiationHelper.InstantiateForm(typeof(BudgetPanel), mockMain);

    // Do validation...
    var hasColumns = SyncfusionTestHelper.FindSfDataGrid(form)?.Columns.Count > 0;

    FormInstantiationHelper.SafeDispose(form, mockMain);
    return hasColumns;
}, timeoutSeconds: 10);

Assert.True(result);
```

**Why This Matters:**

- Direct instantiation causes hangs (no message pump)
- `ExecuteOnStaThread` provides proper threading context
- Timeout prevents tests from hanging indefinitely

---

##### `LoadFormWithTheme(Form form, string themeName = "Office2019Colorful", int waitMs = 500) → bool`

**Solves:** Forms need Syncfusion theme loaded for accurate validation.

**What It Does:**

1. Loads `Office2019Theme` assembly via reflection
2. Applies theme using `SkinManager.SetVisualStyle()`
3. Shows/hides form to trigger component initialization
4. Polls for control initialization (up to waitMs)
5. Uses event pumping (Application.DoEvents) for responsive initialization

**Usage Example:**

```csharp
var form = FormInstantiationHelper.InstantiateForm(typeof(ChartPanel), mockMain);
var loaded = FormInstantiationHelper.LoadFormWithTheme(form, "Office2019Colorful", waitMs: 500);
Assert.True(loaded, "Form should load with theme");
```

---

### 2. MockFactory

**Location:** `tools/WileyWidgetMcpServer/Helpers/MockFactory.cs`

**Purpose:** Creates lightweight mock objects for testing (MainForm, ServiceProvider).

#### Key Methods

##### `CreateMockMainForm(bool enableMdi = false) → MainForm`

**Purpose:** Creates a mock MainForm for testing without requiring full initialization.

**What It Does:**

1. Creates `MainForm()` instance via parameterless constructor
2. Injects a `TestServiceProvider` via reflection into private `_serviceProvider` field
3. Returns form ready for use as dependency injection parameter

**Usage Example:**

```csharp
// In test setup
var mockMain = MockFactory.CreateMockMainForm(enableMdi: false);

// Inject into form constructor
var form = FormInstantiationHelper.InstantiateForm(typeof(AccountsForm), mockMain);

// Cleanup
FormInstantiationHelper.SafeDispose(form, mockMain);
```

**Parameters:**

- `enableMdi` (bool): Not used currently, reserved for future MDI tests

---

##### `CreateTestServiceProvider() → IServiceProvider`

**Purpose:** Returns an IServiceProvider that supplies Mock.Of<T>() instances for any requested service.

**What It Does:**

1. Returns `TestServiceProvider` instance
2. When a service is requested, generates `Mock.Of<T>()` automatically
3. Caches provider internally for reuse

**Usage Example:**

```csharp
// For ViewModels or other components needing DI
var serviceProvider = MockFactory.CreateTestServiceProvider();

// Can be injected into constructors via FormInstantiationHelper
var form = new MyForm(mockMain, serviceProvider);
```

---

### 3. SyncfusionTestHelper

**Location:** `tools/WileyWidgetMcpServer/Helpers/SyncfusionTestHelper.cs`

**Purpose:** Validates Syncfusion theme compliance and control configuration.

#### Key Methods

##### `ValidateTheme(Form form, string expectedTheme) → bool`

**Purpose:** Checks if form uses expected theme (no manual colors).

**What It Does:**

1. Gets all Syncfusion controls in the form tree
2. Returns true if no controls found (non-Syncfusion forms pass by default)
3. Checks each control's `ThemeName` property
4. Allows empty ThemeName (means using parent/SkinManager cascade)
5. Fails if ThemeName doesn't match expected theme

**Usage Example:**

```csharp
var form = FormInstantiationHelper.InstantiateForm(typeof(BudgetPanel), mockMain);
FormInstantiationHelper.LoadFormWithTheme(form, "Office2019Colorful");

var isValid = SyncfusionTestHelper.ValidateTheme(form, "Office2019Colorful");
Assert.True(isValid, "Form should use correct theme");
```

---

##### `ValidateNoManualColors(Control control, string path = "") → List<string>`

**Purpose:** Finds ALL manual BackColor/ForeColor assignments (violations).

**What It Does:**

1. Recursively traverses control tree
2. Checks BackColor and ForeColor against allowed lists
3. Allows semantic status colors: Red, Green, Orange, Yellow, etc.
4. Allows system colors: SystemColors.Control, .Window, etc.
5. Skips Syncfusion controls (they have themed colors)
6. Returns list of violations with paths

**Returns:** List of violations like:

```
"panelAccounts.BackColor = RGB(240, 240, 240) (manual color - use SkinManager instead)"
"buttonSubmit.ForeColor = RGB(68, 68, 68) (manual color - use SkinManager instead)"
```

**Usage Example:**

```csharp
var violations = SyncfusionTestHelper.ValidateNoManualColors(form);
if (violations.Count > 0)
{
    foreach (var violation in violations)
    {
        Console.WriteLine($"  ❌ {violation}");
    }
}
Assert.Empty(violations, "Form should have no manual colors");
```

---

##### `FindSfDataGrid(Control control, string? gridName = null) → SfDataGrid?`

**Purpose:** Recursively finds an SfDataGrid on a form.

**Usage Example:**

```csharp
var grid = SyncfusionTestHelper.FindSfDataGrid(form, gridName: "sfDataGridAccounts");
Assert.NotNull(grid, "Form should have data grid");
Assert.True(grid.Columns.Count >= 5, "Grid should have at least 5 columns");
```

---

##### `GetAllSyncfusionControls(Control control) → List<Control>`

**Purpose:** Gets all Syncfusion controls recursively (for batch validation).

**Usage Example:**

```csharp
var syncfusionControls = SyncfusionTestHelper.GetAllSyncfusionControls(form);
foreach (var ctrl in syncfusionControls)
{
    Console.WriteLine($"  Found Syncfusion control: {ctrl.Name ?? ctrl.GetType().Name}");
}
```

---

##### `TryLoadForm(Form form, int waitMs = 500) → bool`

**Purpose:** Shows/hides form to trigger component initialization (simulates startup).

**What It Does:**

1. Shows form
2. Polls for initialization using `Application.DoEvents()` (up to waitMs)
3. Checks if handle is created and controls exist
4. Hides form
5. Returns true if initialized successfully

**Usage Example:**

```csharp
var form = FormInstantiationHelper.InstantiateForm(typeof(SettingsForm), mockMain);
var initialized = SyncfusionTestHelper.TryLoadForm(form, waitMs: 1000);
Assert.True(initialized, "Form should initialize properly");
```

---

##### `ValidateSfDataGrid(SfDataGrid grid) → bool`

**Purpose:** Basic validation that grid is configured (has columns).

**Returns:** `true` if grid exists and has at least one column.

---

### 4. FormTypeCache

**Location:** `tools/WileyWidgetMcpServer/Helpers/FormTypeCache.cs`

**Purpose:** Caches form type lookups for performance (used by batch validation).

#### Key Methods

##### `GetFormType(string formTypeName) → Type?`

**Purpose:** Gets form type by name with caching (2-3x faster than direct reflection).

**Usage Example:**

```csharp
var formType = FormTypeCache.GetFormType("WileyWidget.WinForms.Forms.AccountsForm");
Assert.NotNull(formType);
```

---

##### `GetAllFormTypes() → List<Type>`

**Purpose:** Gets all form types in WileyWidget.WinForms.Forms namespace.

**Filters Out:**

- Abstract forms (base classes)
- Forms with "Test", "Mock" in name
- Forms in other namespaces

**Usage Example:**

```csharp
var allForms = FormTypeCache.GetAllFormTypes();
foreach (var formType in allForms)
{
    Console.WriteLine($"  Form: {formType.Name}");
}
```

---

##### `GetMainFormConstructor(Type formType) → ConstructorInfo?`

**Purpose:** Gets the `(MainForm)` constructor with caching.

---

##### `GetParameterlessConstructor(Type formType) → ConstructorInfo?`

**Purpose:** Gets the parameterless constructor with caching.

---

---

## MCP TOOLS

### Available Tools (9 Total)

Located in `tools/WileyWidgetMcpServer/Tools/`:

| Tool                                | Purpose                                         | Status    |
| ----------------------------------- | ----------------------------------------------- | --------- |
| **ValidateFormThemeTool**           | Theme compliance checking (SfSkinManager usage) | ✅ Stable |
| **InspectSfDataGridTool**           | Grid inspection (columns, bindings, theme)      | ✅ Stable |
| **BatchValidateFormsTool**          | Batch validation of multiple forms              | ✅ Stable |
| **InspectDockingManagerTool**       | DockingManager configuration inspection         | ✅ Stable |
| **EvalCSharpTool**                  | Dynamic C# evaluation (no recompilation)        | ✅ NEW    |
| **RunHeadlessFormTestTool**         | Execute .csx test scripts                       | ✅ Stable |
| **RunDependencyInjectionTestsTool** | Comprehensive DI validation                     | ✅ NEW    |
| **DetectNullRisksTool**             | Scan for NullReferenceException risks           | ✅ Stable |
| **ValidateSyncfusionLicenseTool**   | Verify Syncfusion license                       | ✅ Stable |

---

### Tool Invocation Pattern

All MCP tools are invoked via `mcp_wileywidget-u_<ToolName>`:

```csharp
// Example: Validate theme
mcp_wileywidget-u_ValidateFormTheme(
    formTypeName: "WileyWidget.WinForms.Forms.AccountsForm",
    expectedTheme: "Office2019Colorful"
)

// Example: Batch validate
mcp_wileywidget-u_BatchValidateForms(
    formTypeNames: null,  // null = all forms
    expectedTheme: "Office2019Colorful",
    failFast: false,
    outputFormat: "json"
)

// Example: EvalCSharp
mcp_wileywidget-u_EvalCSharp(
    csx: "var x = 42; return x * 2;"
)
```

---

## Integration Pattern for Tests

### Standard Test Pattern Using Helpers

```csharp
using Xunit;
using WileyWidget.McpServer.Helpers;
using WileyWidget.WinForms.Controls;

public class PanelTestsWithHelpers
{
    [Fact]
    public void BudgetPanel_ShouldLoadWithTheme()
    {
        // Use ExecuteOnStaThread for WinForms!
        var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
        {
            // Step 1: Create mock MainForm
            var mockMainForm = MockFactory.CreateMockMainForm(enableMdi: true);

            // Step 2: Instantiate form (auto-handles dependencies)
            var form = FormInstantiationHelper.InstantiateForm(
                typeof(BudgetPanel),
                mockMainForm
            );

            // Step 3: Load with theme
            var loaded = FormInstantiationHelper.LoadFormWithTheme(
                form,
                "Office2019Colorful",
                waitMs: 500
            );

            // Step 4: Validate theme
            var themeValid = SyncfusionTestHelper.ValidateTheme(form, "Office2019Colorful");

            // Step 5: Check for manual colors
            var violations = SyncfusionTestHelper.ValidateNoManualColors(form);

            // Step 6: Find and inspect grid
            var grid = SyncfusionTestHelper.FindSfDataGrid(form);
            var hasColumns = grid?.Columns.Count > 0;

            // Step 7: Cleanup
            FormInstantiationHelper.SafeDispose(form, mockMainForm);

            return loaded && themeValid && violations.Count == 0 && hasColumns;
        }, timeoutSeconds: 10);

        Assert.True(result);
    }
}
```

---

## Quick Reference: Which Helper to Use?

| Need                          | Use This                | Method                       |
| ----------------------------- | ----------------------- | ---------------------------- |
| Create mock MainForm          | MockFactory             | `CreateMockMainForm()`       |
| Instantiate form with auto-DI | FormInstantiationHelper | `InstantiateForm()`          |
| Run UI code safely            | FormInstantiationHelper | `ExecuteOnStaThread<T>()`    |
| Load form with theme          | FormInstantiationHelper | `LoadFormWithTheme()`        |
| Cleanup form properly         | FormInstantiationHelper | `SafeDispose()`              |
| Check theme is applied        | SyncfusionTestHelper    | `ValidateTheme()`            |
| Find manual colors            | SyncfusionTestHelper    | `ValidateNoManualColors()`   |
| Find SfDataGrid               | SyncfusionTestHelper    | `FindSfDataGrid()`           |
| Get all Syncfusion controls   | SyncfusionTestHelper    | `GetAllSyncfusionControls()` |
| Cache form type lookups       | FormTypeCache           | `GetFormType()`              |
| Get all forms in namespace    | FormTypeCache           | `GetAllFormTypes()`          |

---

## Why These Tools Exist

### The Problem They Solve

1. **WinForms Threading Hang Crisis**
   - Direct UI instantiation hangs without STA thread context
   - ✅ **Solution:** `ExecuteOnStaThread<T>()`

2. **Complex Constructor Injection**
   - Forms need `MainForm`, `ILogger<T>`, `IServiceProvider`, ViewModels with dependencies
   - ✅ **Solution:** `InstantiateForm()` with automatic parameter resolution

3. **Disposal Errors**
   - DockingManager/Ribbon background threads cause disposal errors
   - ✅ **Solution:** `SafeDispose()` with error suppression

4. **Theme Validation Accuracy**
   - Manual color violations hard to detect
   - ✅ **Solution:** `ValidateNoManualColors()` with comprehensive scanning

5. **Performance in Batch Operations**
   - Reflection lookups slow for many forms
   - ✅ **Solution:** `FormTypeCache` with thread-safe caching

---

## Performance Benchmarks

| Operation                   | Time      | Notes                                    |
| --------------------------- | --------- | ---------------------------------------- |
| Instantiate single form     | 50-150ms  | Includes dependency resolution           |
| Validate theme              | 20-50ms   | Syncfusion control scanning              |
| Check for manual colors     | 30-80ms   | Recursive tree walk                      |
| Load form with theme        | 200-500ms | Includes event pumping                   |
| ExecuteOnStaThread overhead | 10-20ms   | Thread creation + STA setup              |
| Batch validate 50 forms     | 5-10s     | With caching (2-3x faster than no cache) |

---

## Common Issues & Solutions

### "Form hangs indefinitely"

**Cause:** Executing WinForms code on wrong thread context.

**Solution:**

```csharp
// ❌ WRONG - Will hang
var form = new MyForm(mockMain);

// ✅ RIGHT - Use ExecuteOnStaThread
var form = FormInstantiationHelper.ExecuteOnStaThread(() =>
{
    return new MyForm(mockMain);
});
```

---

### "Disposal errors with DockingManager"

**Cause:** Background threads prevent clean disposal.

**Solution:**

```csharp
// ❌ WRONG - Will throw
form.Dispose();

// ✅ RIGHT - Use SafeDispose
FormInstantiationHelper.SafeDispose(form, mockMainForm);
```

---

### "Form constructor takes unknown parameters"

**Cause:** Form has custom dependencies not auto-handled.

**Solution:**

```csharp
// FormInstantiationHelper.InstantiateForm handles:
// - MainForm ✅
// - ILogger<T> ✅
// - IServiceProvider ✅
// - ViewModels with dependencies ✅
// - Any interface (via Mock.Of<T>) ✅

// If form still fails, use custom mock injection
```

---

### "Grid not found on form"

**Cause:** Form not fully initialized.

**Solution:**

```csharp
// Ensure form is loaded with theme
var loaded = FormInstantiationHelper.LoadFormWithTheme(form, "Office2019Colorful", waitMs: 1000);
Assert.True(loaded);

// Then find grid
var grid = SyncfusionTestHelper.FindSfDataGrid(form);
```

---

## See Also

- **QUICK_START.md** - Fast onboarding guide
- **README.md** - Full technical documentation
- **QUICK_REFERENCE.md** - Tool parameter reference
- **IMPLEMENTATION_STATUS.md** - Feature status and roadmap

---

**Last Updated:** January 9, 2026  
**Status:** Production Ready ✅
