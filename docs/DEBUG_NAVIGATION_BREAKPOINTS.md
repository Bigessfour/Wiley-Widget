# 🔴 Navigation Debugging Breakpoints - Quick Reference

## ✅ Programmatic Breakpoints Installed!

Your navigation code now includes **7 strategic breakpoints** that will automatically pause execution when debugging.

---

## 📍 Breakpoint Locations

| ID      | Location                          | When It Breaks                    | Always On?           |
| ------- | --------------------------------- | --------------------------------- | -------------------- |
| **BP1** | `ShowPanel<T>()` entry            | When any panel show is requested  | ⏸️ Optional          |
| **BP2** | `ExecuteDockedNavigation()` start | Before navigation orchestration   | ⏸️ Optional          |
| **BP3** | `_panelNavigator == null` check   | **When PanelNavigator is NULL**   | ✅ **Always**        |
| **BP4** | Before `navigationAction()` call  | Right before executing navigation | ⏸️ Optional          |
| **BP5** | Navigation success                | When panel activates successfully | ⏸️ Never (just logs) |
| **BP6** | Navigation failure                | **When panel fails to activate**  | ✅ **Always**        |
| **BP7** | Exception handler                 | **When exception occurs**         | ✅ **Always**        |

---

## 🚀 Quick Start

### **Step 1: Enable/Disable Breakpoints**

Run this PowerShell script to control breakpoints:

```powershell
# Check current status
.\scripts\Debug-Navigation.ps1 -Action Status

# Enable ALL optional breakpoints (will break on every navigation)
.\scripts\Debug-Navigation.ps1 -Action Enable

# Disable optional breakpoints (keep only critical ones)
.\scripts\Debug-Navigation.ps1 -Action Disable
```

### **Step 2: Start Debugging**

1. Press `F5` to start debugging
2. Click a Ribbon button to show a panel
3. Debugger will automatically pause at enabled breakpoints

### **Step 3: Manual Control (if needed)**

Edit `src/WileyWidget.WinForms/Diagnostics/NavigationDebugger.cs`:

```csharp
// TO ENABLE a breakpoint:
Debugger.Break();  // ✅ Active

// TO DISABLE a breakpoint:
// Debugger.Break();  // ⏸️ Commented out
```

---

## 🎯 Debugging Workflows

### **Workflow 1: "Why isn't my panel showing?"**

**Recommended breakpoints:**

- ✅ Enable BP1 (ShowPanel entry)
- ✅ Enable BP3 (PanelNavigator null) - already enabled
- ✅ Enable BP6 (Navigation failure) - already enabled

**What to check:**

1. BP1 hits → Panel show was called ✅
2. BP3 hits → **PanelNavigator is null** ❌ → Fix initialization
3. BP6 hits → **Panel not activated** ❌ → Check PanelNavigationService

---

### **Workflow 2: "I want to see the entire navigation flow"**

**Recommended breakpoints:**

```powershell
.\scripts\Debug-Navigation.ps1 -Action Enable
```

This enables ALL optional breakpoints. Debugger will pause at:

- Panel show request
- Navigation start
- Before navigation execution

---

### **Workflow 3: "Only break on errors"**

**Recommended breakpoints:**

```powershell
.\scripts\Debug-Navigation.ps1 -Action Disable
```

This keeps only critical breakpoints (BP3, BP6, BP7) enabled:

- PanelNavigator NULL
- Navigation failures
- Exceptions

---

## 🔍 Using Breakpoints Effectively

### **When stopped at a breakpoint:**

**1. Check the Locals Window** (`Ctrl+Alt+V, L`):

```csharp
navigationTarget = "AccountsPanel"
_panelNavigator = null    // ⚠️ Problem!
_dockingManager = {Syncfusion.Windows.Forms.Tools.DockingManager}
```

**2. Use the Watch Window** (`Ctrl+Alt+W, 1`):
Add these expressions:

- `this._panelNavigator != null`
- `this._dockingManager != null`
- `this.IsDisposed`
- `navigationTarget`

**3. Check the Output Window** (`Ctrl+Alt+O`):
Look for `[BP1]`, `[BP2]`, etc. messages showing breakpoint context

**4. Use the Immediate Window** (`Ctrl+Alt+I`):

```csharp
// Check state
? _panelNavigator
? _dockingManager.Controls.Count

// Test navigation
NavigationDebugger.TestNavigation(this, "SettingsPanel")

// Get diagnostic report
? NavigationDebugger.LogNavigationState(this, "Manual Check")
```

---

## 🛠️ Diagnostic Helpers

### **From Immediate Window while debugging:**

```csharp
// Validate navigation infrastructure
NavigationDebugger.ValidateNavigationInfrastructure(this, out var error)
? error  // Shows what's wrong if validation fails

// Get full state dump
NavigationDebugger.LogNavigationState(this, "Debug Check")

// Test specific panel (must use concrete type)
this.ShowPanel<SettingsPanel>("Test Settings")
```

---

## 📊 Breakpoint Behavior

### **Critical Breakpoints (Always Break):**

**BP3: PanelNavigator NULL**

- **Why:** This is a critical failure - navigation cannot work
- **When:** PanelNavigator initialization failed
- **What to check:**
  - `_dockingManager != null`
  - `_serviceProvider != null`
  - `_centralDocumentPanel != null`

**BP6: Navigation Failed**

- **Why:** Panel didn't activate after navigation
- **When:** `IsNavigationTargetActive()` returns false
- **What to check:**
  - `activePanelName` vs `navigationTarget`
  - Panel visibility
  - Docking state

**BP7: Exception**

- **Why:** Unexpected error during navigation
- **When:** Any exception thrown
- **What to check:**
  - Exception message
  - Exception type
  - Call stack

### **Optional Breakpoints (Commented by Default):**

**BP1: ShowPanel Entry**

- Uncomment in `NavigationDebugger.BreakOnShowPanelEntry()`
- Breaks on every panel show request
- Use to verify: "Is my button click reaching the navigation code?"

**BP2: Navigation Start**

- Uncomment in `NavigationDebugger.BreakOnNavigationStart()`
- Breaks before orchestration
- Use to check: Thread context, form state

**BP4: Before Action**

- Uncomment in `NavigationDebugger.BreakBeforeNavigationAction()`
- Breaks right before PanelNavigationService is called
- Use to verify: PanelNavigator is ready

---

## 🎨 Customization

### **Add Your Own Breakpoints:**

Edit `NavigationDebugger.cs`:

```csharp
[Conditional("DEBUG")]
public static void BreakOnMyCondition(string context)
{
    if (!Debugger.IsAttached) return;

    // Add your condition
    if (/* your condition */)
    {
        Debugger.Break();
        Debug.WriteLine($"[BP_CUSTOM] {context}");
    }
}
```

Then call it from `MainForm.Navigation.cs`:

```csharp
Diagnostics.NavigationDebugger.BreakOnMyCondition("Before my check");
```

---

## 🚦 Troubleshooting

### **Breakpoints not hitting?**

1. **Debugger attached?** Check Debug → Attach to Process
2. **Running in Release mode?** Switch to Debug configuration
3. **Breakpoints commented out?** Check `NavigationDebugger.cs`

### **Too many breakpoints?**

```powershell
# Disable optional ones
.\scripts\Debug-Navigation.ps1 -Action Disable
```

### **Want to see everything?**

```powershell
# Enable all breakpoints
.\scripts\Debug-Navigation.ps1 -Action Enable
```

---

## 📝 Files Created

✅ **`src/WileyWidget.WinForms/Diagnostics/NavigationDebugger.cs`**

- Contains all breakpoint logic
- Diagnostic helpers
- Validation methods

✅ **`scripts/Debug-Navigation.ps1`**

- Enable/disable breakpoints
- Check breakpoint status

✅ **`docs/DEBUG_NAVIGATION_BREAKPOINTS.md`** (this file)

- Quick reference guide

✅ **Modified: `src/WileyWidget.WinForms/Forms/MainForm/MainForm.Navigation.cs`**

- Integrated breakpoint calls at strategic points

---

## 🎓 Next Steps

1. **Check breakpoint status:**

   ```powershell
   .\scripts\Debug-Navigation.ps1 -Action Status
   ```

2. **Start with critical breakpoints only:**

   ```powershell
   .\scripts\Debug-Navigation.ps1 -Action Disable
   ```

3. **Press F5** and click a Ribbon button!

4. **When you hit a breakpoint:**
   - Check Locals window
   - Add watches
   - Review Output window
   - Use Immediate window for diagnostics

---

**Happy Debugging!** 🐛🔨
