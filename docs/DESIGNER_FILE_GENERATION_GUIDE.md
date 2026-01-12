# Agentic Designer File Generation Guide

**Version:** 1.1
**Date:** January 9, 2026
**Status:** CANONICAL PATTERN ESTABLISHED
**Purpose:** Systematic, repeatable process for generating WinForms designer files

---

## Executive Summary

This guide provides a **canonical pattern** and **agentic workflow** for generating `.Designer.cs` files for all WinForms panels and views in the Wiley Widget project. The process is designed to be:

- ✅ **Fully Agentic** - Execute without human confirmation
- ✅ **Repeatable** - Same pattern for every panel
- ✅ **Validated** - Compile and test automatically
- ✅ **Documented** - Clear before/after states
- ✅ **Scalable** - 50+ panels can be processed systematically

---

## Part 1: Understanding the Canonical Pattern

### 1.1 Designer File Structure

Every `.Designer.cs` file follows this exact structure:

```csharp
using Syncfusion.WinForms.Core;
using Syncfusion.WinForms.Drawing;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    partial class [PanelName]
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // [INITIALIZATION CODE]
            // [EVENT WIRING]
        }

        // [FIELD DECLARATIONS]
    }
}
```

### 1.2 Key Rules

**Usings Block (Fixed):**

```csharp
using Syncfusion.WinForms.Core;
using Syncfusion.WinForms.Drawing;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
```

**Critical Pattern 1: No Ambiguous Namespaces**

- ✅ Use alias: `using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;`
- ❌ DON'T import: `Syncfusion.Windows.Forms` (causes ambiguity)
- ❌ DON'T import: `Syncfusion.WinForms.Input` directly (use fully qualified)

**Critical Pattern 2: Fully Qualified Names**

- ✅ `new Syncfusion.WinForms.ListView.SfComboBox()`
- ✅ `new Syncfusion.WinForms.Input.SfNumericTextBox()`
- ✅ `Syncfusion.WinForms.Core.SfSkinManager.SetVisualStyle(...)`
- ❌ DON'T use ambiguous short names

**Critical Pattern 3: Theme Application**

- ✅ Use `Syncfusion.WinForms.Core.SfSkinManager.SetVisualStyle(control, ThemeColors.DefaultTheme)`
- ❌ DON'T manually assign colors to BackColor/ForeColor
- ❌ DON'T set per-control themes

**Critical Pattern 4: ToolTip Initialization**

- ✅ Create `_toolTip` in `InitializeComponent()`
- ✅ Initialize before using: `_toolTip = new System.Windows.Forms.ToolTip(this.components);`
- ❌ DON'T use uninitialized `_toolTip`

---

## Part 2: Agentic Generation Workflow

### 2.1 Discovery Phase

**Identify all panels needing designer files:**

```powershell
# Find UserControl and Form classes
Get-ChildItem -Path "src/WileyWidget.WinForms/Controls" -Filter "*.cs" |
  Select-String -Pattern "public partial class.*: (UserControl|ScopedPanelBase)" |
  Select-Object Filename
```

**Expected panels (primary):**

- AccountsPanel
- BudgetPanel
- ChartPanel
- DashboardPanel
- AuditLogPanel
- AnalyticsPanel
- QuickBooksPanel
- SettingsPanel
- ReportsPanel
- CustomersPanel
- UtilityBillPanel
- ChatPanel
- (and 40+ more)

### 2.2 Template Generation Phase

**For each panel, execute these steps atomically:**

#### Step A: Analyze the Panel Class

1. Read the `.cs` file
2. Identify:
   - Private field declarations (controls)
   - Constructor parameters
   - Initialization logic in constructor
   - Event handler wiring

#### Step B: Extract Control Declarations

Example from AccountEditPanel:

```csharp
private System.Windows.Forms.ToolTip _toolTip;
private System.Windows.Forms.Label lblTitle;
private Syncfusion.Windows.Forms.Tools.TextBoxExt txtAccountNumber;
private Syncfusion.WinForms.ListView.SfComboBox cmbDepartment;
// ... etc
```

#### Step C: Generate InitializeComponent

Structure:

```csharp
private void InitializeComponent()
{
    this.components = new System.ComponentModel.Container();

    // Initialize ToolTip FIRST
    _toolTip = new System.Windows.Forms.ToolTip(this.components);
    _toolTip.AutoPopDelay = 5000;
    _toolTip.InitialDelay = 500;
    _toolTip.ReshowDelay = 200;
    _toolTip.ShowAlways = true;

    // Control initialization
    // For each control:
    // 1. Instantiate with fully qualified type
    // 2. Set properties (Name, Location, Size, etc.)
    // 3. Set accessibility properties
    // 4. Set Syncfusion-specific properties
    // 5. Add to parent container
    // 6. Wire tooltip

    // Apply theme to appropriate control (usually buttonPanel or main container)
    Syncfusion.WinForms.Core.SfSkinManager.SetVisualStyle(controlName, ThemeColors.DefaultTheme);

    // Set form/panel properties
    this.Name = "PanelName";
    this.Dock = System.Windows.Forms.DockStyle.Fill;
    this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
    this.Size = new System.Drawing.Size(width, height);
    this.Padding = new System.Windows.Forms.Padding(padding);
}
```

#### Step D: Generate Field Declarations

```csharp
// At bottom of partial class
private System.ComponentModel.IContainer components;
private System.Windows.Forms.ToolTip _toolTip;
private System.Windows.Forms.Label lblTitle;
// ... all other controls
```

### 2.3 Validation Phase

**For each generated designer file:**

1. **Syntax Check**
   - No compilation errors
   - All types fully qualified
   - All required using statements present

2. **Pattern Compliance Check**
   - ✅ ThemeColors alias present
   - ✅ SfSkinManager uses fully qualified name
   - ✅ ToolTip initialized before use
   - ✅ All Syncfusion types use full namespace

3. **Build Check**
   ```powershell
   dotnet build src/WileyWidget.WinForms/WileyWidget.WinForms.csproj -v diagnostic
   # Should complete with 0 errors
   ```

---

## Part 3: Implementation Strategy

### PROGRESS SUMMARY (January 9, 2026)

**Group 1 Progress: 10 of 15 Designer Files Complete (67%)**

| Metric                     | Status                                   |
| -------------------------- | ---------------------------------------- |
| **Designer Files Created** | 10/15 (67%)                              |
| **Build Status**           | ✅ Successful                            |
| **Compilation Errors**     | 0                                        |
| **Test Coverage**          | Ready for validation                     |
| **Next Phase**             | 6 remaining panels (estimated 1-2 hours) |

**Key Achievements:**

- ✅ Canonical pattern established and validated
- ✅ ScopedPanelBase panels properly integrated
- ✅ Theme compliance verified (SfSkinManager, no manual colors)
- ✅ All critical panels (DataGrid-based) complete
- ✅ Zero build failures

---

### 3.1 Batch Processing Approach - UPDATED January 2026

**Group 1: Priority Panels (15 files) - 67% COMPLETE (10 of 15)**

✅ **COMPLETED: 10 Designer Files Generated**

1. AccountEditPanel.Designer.cs
2. AccountsPanel.Designer.cs
3. BudgetPanel.Designer.cs
4. ChartPanel.Designer.cs
5. DashboardPanel.Designer.cs
6. AuditLogPanel.Designer.cs
7. AnalyticsPanel.Designer.cs
8. QuickBooksPanel.Designer.cs
9. SettingsPanel.Designer.cs
10. ReportsPanel.Designer.cs

**Build Status: ✅ SUCCESSFUL - No Compilation Errors**

⏳ **REMAINING: 6 Designer Files (Recommended Next Phase)**

- CustomersPanel (UserControl - fully implemented with toolbar, grid, and summary)
- UtilityBillPanel (ScopedPanelBase - fully implemented with bill and customer grids)
- ChatPanel (ScopedPanelBase - Blazor-based AI chat panel)
- WarRoomPanel (UserControl - complex scenario analysis with charts and gauges)
- ProactiveInsightsPanel (UserControl - insights feed wrapper)
- RevenueTrendsPanel (ScopedPanelBase - revenue analysis with chart and grid)

---

## Conclusion

This guide establishes the **canonical pattern** for generating designer files agentic ally across 50+ WinForms panels. The process is:

✅ **Repeatable** - Same pattern for every panel
✅ **Automatable** - Can be scripted in PowerShell/C#
✅ **Validated** - Compile checks ensure correctness
✅ **Documented** - Clear before/after states
✅ **Scalable** - Handles groups of panels systematically

### Current Status (January 9, 2026)

**Group 1: 67% Complete**

- ✅ 10 of 15 designer files generated
- ✅ Build verified (0 errors)
- ✅ Pattern validated across ScopedPanelBase, UserControl, and complex panels
- ✅ Theme compliance confirmed

**Immediate Next Steps:**

1. **Complete Group 1** (6 remaining panels: CustomersPanel, UtilityBillPanel, ChatPanel, WarRoomPanel, ProactiveInsightsPanel, RevenueTrendsPanel)
2. **Execute Group 2-4** (Overlays, Forms, Dialogs)
3. **Create automated script** for future panels
4. **Update developer guidelines** with finalized pattern

**Estimated Remaining Effort:**

- Group 1 remaining: 1-2 hours
- Group 2-4: 2-3 hours
- Automation & documentation: 1 hour
- **Total: 4-6 hours to 100% completion**

---

**Version History:**

- v1.0 (Jan 9, 2026): Initial canonical pattern established
- v1.1 (Jan 9, 2026): Updated with Group 1 progress (10/15 complete)
