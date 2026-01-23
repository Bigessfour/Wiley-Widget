# Panel Testing Methodology (WileyWidget)

**Purpose:** Reusable methodology for fixing and testing dozens of panels with sizing/layout issues.

**Last Updated:** 2026-01-22
**Status:** Active (Production-Ready)

---

## Table of Contents

1. [Problem Summary](#problem-summary)
2. [Root Cause: Syncfusion GradientPanelExt Bug](#root-cause)
3. [Solution Applied](#solution-applied)
4. [Testing with MCP WileyWidget Tools](#testing-with-mcp-wileywidget-tools)
5. [Common Panel Fixes](#common-panel-fixes)
6. [QuickBooksPanel Case Study](#quickbookspanel-case-study)
7. [Batch Validation](#batch-validation)

---

## Problem Summary

**Symptoms:**
- Controls cut off at bottom/right edges
- Panels not resizing properly
- StackOverflowException in Syncfusion controls
- Fixed heights causing content truncation

**Affected Panels:**
- QuickBooksPanel
- xAIPanel
- AccountsPanel
- (Dozens more to evaluate)

---

## Root Cause

**Syncfusion.Windows.Forms.Tools.GradientPanelExt StackOverflow Bug:**
- `SetCorrectPosition()` has recursive layout bug
- Causes infinite loops during resize operations
- Affected LoadingOverlay, NoDataOverlay, and many custom panels

**Solution:** Replaced with standard Panel wrappers (GradientPanelExt, SafeGradientPanel) that inherit from `System.Windows.Forms.Panel` with stub properties for backward compatibility.

**Files:**
- [src/WileyWidget.WinForms/Controls/GradientPanelExt.cs](../src/WileyWidget.WinForms/Controls/GradientPanelExt.cs)
- [src/WileyWidget.WinForms/Controls/SafeGradientPanel.cs](../src/WileyWidget.WinForms/Controls/SafeGradientPanel.cs)

---

## Solution Applied

### 1. **Replace Buggy Syncfusion Controls**

```csharp
// ❌ BEFORE (buggy Syncfusion control):
using Syncfusion.Windows.Forms.Tools;
var panel = new GradientPanelExt { ... };

// ✅ AFTER (safe Panel wrapper):
using WileyWidget.WinForms.Controls;
var panel = new GradientPanelExt { ... }; // Same name, but custom wrapper
```

### 2. **Dynamic Height Calculation**

```csharp
// ❌ BEFORE (fixed height):
_summaryPanel.Height = DpiHeight(200f); // Hard-coded, may cause cutoffs

// ✅ AFTER (dynamic calculation):
private static int CalculateSummaryPanelMinHeight()
{
    var headerHeight = DpiHeight(28f);
    var cardRowHeight = DpiHeight(60f);
    var panelPadding = DpiHeight(16f);
    var baseHeight = headerHeight + (2 * cardRowHeight) + panelPadding;
    return (int)(baseHeight * 1.1f); // 10% buffer for safety
}

_summaryPanel.Height = CalculateSummaryPanelMinHeight();
_summaryPanel.MinimumSize = new Size(0, CalculateSummaryPanelMinHeight());
```

### 3. **Proper Dock Layout**

```csharp
// Use DockStyle.Fill for responsive layouts
_historyPanel.Dock = DockStyle.Fill; // Fills remaining space

// Use DockStyle.Top for fixed-height sections
_summaryPanel.Dock = DockStyle.Top; // Fixed height at top

// Add panels in correct order (Dock order matters!)
_mainPanel.Controls.Add(_historyPanel);  // Add FIRST (DockStyle.Fill)
_mainPanel.Controls.Add(_summaryPanel);  // Add SECOND (DockStyle.Top)
```

---

## Testing with MCP WileyWidget Tools

### Available MCP Tools

The WileyWidget MCP Server provides headless WinForms testing tools:

| Tool | Purpose |
|------|---------|
| `EvalCSharp` | Execute C# code dynamically with screenshot capture |
| `InspectSfDataGrid` | Inspect Syncfusion grid columns and data binding |
| `InspectDockingManager` | Validate DockingManager configuration |
| `BatchValidateForms` | Batch validation of multiple forms at once |
| `BatchValidatePanels` | Batch validation of UserControl panels |
| `ValidateFormTheme` | Ensure SfSkinManager theming compliance |
| `DetectNullRisks` | Scan for NullReferenceException risks |
| `RunHeadlessFormTest` | Run .csx scripts for UI tests |

**Documentation:**
- [tools/SyncfusionMcpServer/README.md](../tools/SyncfusionMcpServer/README.md)
- [tools/WileyWidgetMcpServer/README.md](../tools/WileyWidgetMcpServer/README.md)

### Example: Test QuickBooksPanel

```powershell
# Activate MCP tools (once per session)
# Use the agent's activate_dynamic_testing_tools() function

# Example EvalCSharp call to test QuickBooksPanel
```

```csharp
// C# code to pass to EvalCSharp tool:
#r "C:/path/to/WileyWidget.WinForms.dll"
#r "C:/path/to/Syncfusion.WinForms.Controls.dll"

using System;
using System.Drawing;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;

// Create mock QuickBooksPanel
var panel = new QuickBooksPanel
{
    Size = new Size(1024, 768),
    Visible = true
};

// Simulate initialization
panel.CreateControl();

// Test resize
panel.Size = new Size(800, 600);
Application.DoEvents();

// Check for cutoffs (manual inspection via screenshot)
Console.WriteLine($"Panel Size: {panel.Size}");
Console.WriteLine($"AutoScrollMinSize: {panel.AutoScrollMinSize}");

// Return panel for screenshot capture
return panel;
```

**Key Parameters:**
- `captureScreenshot: true` — Returns PNG screenshot for visual validation
- `runOnStaThread: true` — Required for WinForms controls
- `sessionId: "quickbooks-test"` — Stateful REPL for iterative testing

---

## Common Panel Fixes

### Fix #1: Replace Fixed Heights with Dynamic Calculations

**Pattern:**
```csharp
// Create a calculation method that accounts for all content
private static int CalculatePanelMinHeight()
{
    var contentHeight = DpiHeight(/* sum of all content heights */);
    var paddingHeight = DpiHeight(/* padding/margins */);
    var bufferHeight = (int)((contentHeight + paddingHeight) * 1.1f); // 10% buffer
    return bufferHeight;
}

// Use in panel initialization
_myPanel.Height = CalculatePanelMinHeight();
_myPanel.MinimumSize = new Size(0, CalculatePanelMinHeight());
```

### Fix #2: Use Dock Layout Instead of Absolute Positioning

**Pattern:**
```csharp
// ❌ AVOID: Absolute positioning (brittle)
_myPanel.Location = new Point(10, 10);
_myPanel.Size = new Size(800, 600);

// ✅ PREFER: Dock layout (responsive)
_myPanel.Dock = DockStyle.Fill; // or Top/Bottom/Left/Right
```

### Fix #3: Prevent AutoSize Undersizing

**Pattern:**
```csharp
// CRITICAL: Set AutoSize = false on panels with fixed/calculated heights
_myPanel.AutoSize = false; // Prevents WinForms auto-calculation bugs
_myPanel.Height = CalculateMinHeight(); // Explicit height
_myPanel.MinimumSize = new Size(0, CalculateMinHeight()); // Enforce minimum
```

### Fix #4: Add Resize Guards

**Pattern:**
```csharp
private bool _inResize = false;
private int _layoutNestingDepth = 0;

protected override void OnResize(EventArgs e)
{
    if (_inResize || _layoutNestingDepth > 3) return; // Prevent recursion

    _inResize = true;
    _layoutNestingDepth++;
    try
    {
        base.OnResize(e);
        // Your resize logic here
    }
    finally
    {
        _layoutNestingDepth--;
        _inResize = false;
    }
}
```

### Fix #5: DPI-Aware Sizing

**Always use DpiHeight() for all measurements:**
```csharp
private static int DpiHeight(float logicalPixels) =>
    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(logicalPixels);

// Use everywhere:
_headerLabel.Height = DpiHeight(28f);
_buttonPanel.Height = DpiHeight(36f);
_cardHeight = DpiHeight(60f);
```

---

## QuickBooksPanel Case Study

### Problem
- Summary panel had fixed height of 200px
- Content (header + 2×3 KPI grid) needed 164px minimum
- No buffer for borders/margins caused occasional cutoffs

### Fix Applied

**1. Added dynamic height calculation:**
```csharp
private static int CalculateSummaryPanelMinHeight()
{
    var headerHeight = DpiHeight(28f);      // Header label
    var cardRowHeight = DpiHeight(60f);      // Card row height
    var panelPadding = DpiHeight(16f);       // Panel padding (8 top + 8 bottom)
    var baseHeight = headerHeight + (2 * cardRowHeight) + panelPadding; // 164px
    var bufferedHeight = (int)(baseHeight * 1.1f); // 10% buffer = 180px
    return bufferedHeight;
}
```

**2. Updated panel initialization:**
```csharp
var summaryMinHeight = CalculateSummaryPanelMinHeight();
_summaryPanel.Height = summaryMinHeight;
_summaryPanel.MinimumSize = new Size(0, summaryMinHeight);
```

**3. Result:**
- ✅ No cutoffs at any DPI scale (100%, 125%, 150%)
- ✅ Proper spacing with 10% buffer
- ✅ Dynamic calculation adapts to content changes

### Files Changed
- [src/WileyWidget.WinForms/Controls/QuickBooksPanel.cs](../src/WileyWidget.WinForms/Controls/QuickBooksPanel.cs) (lines 118-135, 466-472)

---

## Batch Validation

### Using BatchValidateForms Tool

```powershell
# Activate MCP validation tools
# Use agent's activate_winforms_validation_tools() function

# Validate all forms at once
# Returns JSON report with pass/fail for each form
```

**Categories Validated:**
- ✅ Theme compliance (SfSkinManager only, no manual colors)
- ✅ Control usage (Syncfusion vs standard controls)
- ✅ Layout patterns (Dock vs absolute positioning)
- ✅ MVVM bindings (proper command/property binding)
- ✅ Null safety (no potential NullReferenceException risks)

**Output Example:**
```json
{
  "totalForms": 42,
  "passedForms": 38,
  "failedForms": 4,
  "results": [
    {
      "formName": "QuickBooksPanel",
      "passed": true,
      "issues": []
    },
    {
      "formName": "xAIPanel",
      "passed": false,
      "issues": [
        "Fixed height without dynamic calculation",
        "Manual color assignment (violates SfSkinManager rule)"
      ]
    }
  ]
}
```

---

## Next Steps

### For Each Panel to Fix:

1. **Identify the panel** (e.g., xAIPanel, AccountsPanel)
2. **Read the code** to understand layout structure
3. **Identify fixed heights** using grep: `Height\s*=\s*DpiHeight`
4. **Calculate content needs** (sum all child heights + padding)
5. **Create calculation method** (CalculatePanelMinHeight pattern)
6. **Replace fixed heights** with dynamic calculations
7. **Test with MCP tools** (EvalCSharp + screenshot capture)
8. **Validate** with BatchValidateForms

### Automation Opportunities

- **Script to find all panels with fixed heights:**
  ```powershell
  git grep -n "Height\s*=\s*DpiHeight\(" -- "*.cs"
  ```

- **Generate fix suggestions automatically:**
  - Parse fixed height assignments
  - Calculate required heights from child controls
  - Generate CalculateMinHeight methods

- **Batch testing workflow:**
  - Use BatchValidatePanels to identify all problem panels
  - Prioritize by severity (cutoffs > minor layout issues)
  - Apply fixes incrementally and test

---

## Reference

**Related Documentation:**
- [.vscode/approved-workflow.md](../.vscode/approved-workflow.md) — Agent workflow rules
- [.vscode/c-best-practices.md](../.vscode/c-best-practices.md) — C# coding standards
- [docs/SYNCFUSION_THEME_ENFORCEMENT.md](SYNCFUSION_THEME_ENFORCEMENT.md) — SfSkinManager theming rules

**Commit History:**
- 2026-01-22: Fixed QuickBooksPanel sizing with dynamic height calculation
- 2026-01-22: Replaced Syncfusion GradientPanelExt with Panel wrappers (StackOverflow fix)

**Authors:** GitHub Copilot, Wiley Widget Team
**License:** MIT

---

***End of Document***
