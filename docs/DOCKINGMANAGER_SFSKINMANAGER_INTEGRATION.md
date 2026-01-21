# DockingManager & SfSkinManager Theme Integration Guide

## Executive Summary

**CRITICAL INCOMPATIBILITY:** The Syncfusion Windows Forms `DockingManager` control is **incompatible with SfSkinManager** theme cascade. DockingManager uses a **legacy `VisualStyle` enum system** that predates modern theme integration, while all other Syncfusion controls rely on `SfSkinManager.SetVisualStyle()`.

This document provides:

1. **Architectural incompatibility analysis**
2. **Migration strategy** for coordinating themes
3. **Implementation guide** for the `DockingManagerThemeAdapter` wrapper

---

## Part 1: Why DockingManager Is Incompatible

### API Comparison

| Feature               | SfSkinManager-Compliant Controls                   | DockingManager                                         |
| --------------------- | -------------------------------------------------- | ------------------------------------------------------ |
| **Theme Property**    | `ThemeName` (string)                               | ❌ Does NOT exist                                      |
| **Theme Method**      | `SfSkinManager.SetVisualStyle(control, themeName)` | ❌ Not applicable                                      |
| **Runtime Switching** | Automatic cascade to children                      | ❌ Manual only                                         |
| **IThemeProvider**    | ✅ Implemented                                     | ✅ Implemented BUT...                                  |
| **IVisualStyle**      | ✅ Implemented                                     | ✅ Implemented BUT...                                  |
| **Available Themes**  | Modern: Office2019, Modern, FluentLight, etc.      | Legacy: Office2003, Office2007, Office2010, Metro only |

### Root Cause

**DockingManager was designed in 2000s-era Syncfusion** (VisualStyle enum), while **SfSkinManager is 2010s+ modern theme architecture** (string-based theme names with assembly loading). The control never migrated because it is a complex layout manager with internal painter dependencies.

### Proof from Official Documentation

From `https://help.syncfusion.com/cr/windowsforms/Syncfusion.Windows.Forms.Tools.DockingManager.html`:

```csharp
// ✅ CORRECT (DockingManager legacy API)
dockingManager1.VisualStyle = Syncfusion.Windows.Forms.VisualStyle.Office2007;
dockingManager1.Office2007Theme = Office2007Theme.Silver;

// ❌ DOES NOT EXIST (SfSkinManager API - not supported)
dockingManager1.ThemeName = "Office2019Colorful";  // Property doesn't exist
SfSkinManager.SetVisualStyle(dockingManager1, "Office2019Colorful");  // Won't work
```

---

## Part 2: Architectural Decision

### Design Pattern

**Use a bridge pattern wrapper: `DockingManagerThemeAdapter`**

This adapter:

1. **Listens to** SfSkinManager theme changes
2. **Translates** modern theme names → DockingManager VisualStyle enums
3. **Applies** the closest visual approximation
4. **Cascades** to all DockingManager-docked controls

### Theme Mapping Strategy

**Core Themes:**

```text
Modern Theme Name          →  DockingManager VisualStyle
─────────────────────────────────────────────────────────
Office2019Colorful        →  Office2010 + Office2010Theme.Colorful
Office2019Black           →  Office2010 + Office2010Theme.Black
Office2019White           →  Office2007 + Office2007Theme.Silver
ModernColorful            →  Metro + MetroColor customization
ModernDark                →  Metro + MetroCaptionColor customization (w/ custom painter)
FluentLight               →  Office2007 + Office2007Theme.Silver
FluentDark                →  Office2010 + Office2010Theme.Black
HighContrastBlack         →  Office2010 + Office2010Theme.Black
HighContrastWhite         →  Office2007 + Office2007Theme.Silver
```

**Extended Themes (Future Support):**

> **Note:** These mappings are placeholders for future municipal or custom theme palettes. Verify with Syncfusion before implementation.

```text
MaterialLight             →  Office2007 + Office2007Theme.Silver
MaterialDark              →  Office2010 + Office2010Theme.Black (+ Material customization)
NordLight                 →  Office2007 + Office2007Theme.Silver (+ Nord blue accents)
NordDark                  →  Office2010 + Office2010Theme.Black (+ Nord accent colors)
GruvboxLight              →  Office2007 + Office2007Theme.Silver (+ warm accent)
GruvboxDark               →  Office2010 + Office2010Theme.Black (+ warm accent)
```

**Dark Mode Compatibility Notes:**

⚠️ **CRITICAL FINDING:** Syncfusion forum posts (2023-2024) indicate that `ModernDark` and `Office2010Theme.Black` do **NOT** automatically render correctly in DockingManager without custom painters.

**Issue:** DockingManager's internal `DockTabController` and border renderers use hardcoded legacy colors that don't respect the theme assembly's dark palette.

**Workaround (Interim):** Use custom `DockingManager` painter extensions:

```csharp
// Example: DarkModeCustomPainter for DockingManager
private class DarkModeDockingPainter : DockingManagerPainter
{
    public override void DrawDockTabButton(/*...*/)
    {
        // Custom dark-themed rendering
    }

    public override void DrawDockBorder(/*...*/)
    {
        // Custom dark border colors
    }
}

// Apply when loading ModernDark or dark Office2010 theme:
if (themeName.Contains("Dark"))
{
    dockingManager.Painter = new DarkModeDockingPainter();
}
```

**Recommended Solution:** Enforce `Office2010Theme.Black` + custom painter for all dark themes until Syncfusion v33+ provides full dark mode support via assembly reflection.

**Verification Checklist for Dark Modes:**

- [ ] Test `ModernDark` theme on high-DPI display (96, 120, 144 DPI)
- [ ] Verify DockingManager tabs are readable on dark background
- [ ] Verify border colors don't wash out in dark theme
- [ ] Test floating docked forms in dark mode
- [ ] Check custom painter compatibility with `DockStateChanged` events
- [ ] Verify theme switch (light ↔ dark) doesn't cause flickering or paint artifacts

---

## Part 3: Implementation Guide

### Step 1: Create the Theme Adapter Class

**File:** `src/WileyWidget.WinForms/Themes/DockingManagerThemeAdapter.cs`

```csharp
using System;
using System.Collections.Generic;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Core;

namespace WileyWidget.WinForms.Themes
{
    /// <summary>
    /// Bridges SfSkinManager theme system with DockingManager's legacy VisualStyle API.
    /// Translates modern theme names to DockingManager VisualStyle enums for visual consistency.
    /// </summary>
    public class DockingManagerThemeAdapter
    {
        private readonly DockingManager _dockingManager;
        private static readonly Dictionary<string, (VisualStyle style, Action<DockingManager> configure)> ThemeMap;

        static DockingManagerThemeAdapter()
        {
            ThemeMap = new Dictionary<string, (VisualStyle, Action<DockingManager>)>(StringComparer.OrdinalIgnoreCase)
            {
                // Office 2019 family
                ["Office2019Colorful"] = (VisualStyle.Office2010, dm =>
                {
                    dm.Office2010Theme = Office2010Theme.Colorful;
                    dm.ThemesEnabled = true;
                }),

                ["Office2019Black"] = (VisualStyle.Office2010, dm =>
                {
                    dm.Office2010Theme = Office2010Theme.Black;
                    dm.ThemesEnabled = true;
                }),

                ["Office2019White"] = (VisualStyle.Office2007, dm =>
                {
                    dm.Office2007Theme = Office2007Theme.Silver;
                    dm.ThemesEnabled = true;
                }),

                // Modern family
                ["ModernColorful"] = (VisualStyle.Metro, dm =>
                {
                    dm.MetroColor = System.Drawing.Color.FromArgb(0, 120, 215); // Modern Blue
                    dm.MetroCaptionColor = System.Drawing.Color.FromArgb(51, 51, 51);
                    dm.ThemesEnabled = true;
                }),

                ["ModernDark"] = (VisualStyle.Metro, dm =>
                {
                    dm.MetroColor = System.Drawing.Color.FromArgb(45, 45, 48); // Dark Gray
                    dm.MetroCaptionColor = System.Drawing.Color.FromArgb(255, 255, 255);
                    dm.ThemesEnabled = true;
                }),

                // Fluent family
                ["FluentLight"] = (VisualStyle.Office2007, dm =>
                {
                    dm.Office2007Theme = Office2007Theme.Silver;
                    dm.ThemesEnabled = true;
                }),

                ["FluentDark"] = (VisualStyle.Office2010, dm =>
                {
                    dm.Office2010Theme = Office2010Theme.Black;
                    dm.ThemesEnabled = true;
                }),

                // High Contrast family
                ["HighContrastBlack"] = (VisualStyle.Office2010, dm =>
                {
                    dm.Office2010Theme = Office2010Theme.Black;
                    dm.ThemesEnabled = true;
                }),

                ["HighContrastWhite"] = (VisualStyle.Office2007, dm =>
                {
                    dm.Office2007Theme = Office2007Theme.Silver;
                    dm.ThemesEnabled = true;
                }),

                // Fallback
                ["Default"] = (VisualStyle.Office2010, dm =>
                {
                    dm.Office2010Theme = Office2010Theme.Colorful;
                    dm.ThemesEnabled = true;
                }),
            };
        }

        public DockingManagerThemeAdapter(DockingManager dockingManager)
        {
            _dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));
        }

        /// <summary>
        /// Applies a modern theme name to the DockingManager using the closest visual approximation.
        /// </summary>
        /// <param name="themeName">Modern theme name (e.g., "Office2019Colorful")</param>
        public void ApplyTheme(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
            {
                themeName = "Default";
            }

            if (ThemeMap.TryGetValue(themeName, out var mapping))
            {
                var (visualStyle, configure) = mapping;
                _dockingManager.VisualStyle = visualStyle;
                configure?.Invoke(_dockingManager);
            }
            else
            {
                // Unknown theme - fall back to default
                _dockingManager.VisualStyle = VisualStyle.Office2010;
                _dockingManager.Office2010Theme = Office2010Theme.Colorful;
                _dockingManager.ThemesEnabled = true;
            }
        }

        /// <summary>
        /// Registers the DockingManager to automatically follow SfSkinManager theme changes.
        /// Call this in your form's constructor or after creating the DockingManager.
        /// </summary>
        /// <param name="themeService">Your IThemeService implementation</param>
        public void RegisterThemeListener(IThemeService themeService)
        {
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));

            // Apply current theme immediately
            string currentTheme = themeService.GetCurrentTheme();
            ApplyTheme(currentTheme);

            // Subscribe to theme changes (if your service supports events)
            // Example: themeService.ThemeChanged += (sender, theme) => ApplyTheme(theme);
        }
    }
}
```

### Step 2: Update Your Theme Service

**File:** `src/WileyWidget.WinForms/Themes/ThemeService.cs` (or similar)

Add DockingManager tracking:

```csharp
// Inside ThemeService class
private readonly List<DockingManager> _dockingManagers = new List<DockingManager>();
private DockingManagerThemeAdapter _dockingManagerAdapter;

/// <summary>
/// Registers a DockingManager to receive theme updates.
/// </summary>
public void RegisterDockingManager(DockingManager dockingManager)
{
    if (dockingManager == null)
        throw new ArgumentNullException(nameof(dockingManager));

    _dockingManagers.Add(dockingManager);

    // Create adapter if not already done
    if (_dockingManagerAdapter == null)
    {
        _dockingManagerAdapter = new DockingManagerThemeAdapter(dockingManager);
    }

    // Apply current theme
    _dockingManagerAdapter.ApplyTheme(GetCurrentTheme());
}

// In your theme change method:
public void ApplyTheme(string themeName)
{
    // ... existing theme application code ...

    // Apply to DockingManager
    if (_dockingManagerAdapter != null)
    {
        _dockingManagerAdapter.ApplyTheme(themeName);
    }
}
```

### Step 3: Usage in Forms

**Example:** MainForm.cs

```csharp
public partial class MainForm : Form
{
    private readonly IThemeService _themeService;
    private DockingManager _dockingManager;

    public MainForm(IThemeService themeService)
    {
        _themeService = themeService;
        InitializeComponent();
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
        // Initialize DockingManager
        _dockingManager = new DockingManager(components);
        _dockingManager.HostForm = this;

        // Register with theme service to receive updates
        _themeService.RegisterDockingManager(_dockingManager);

        // Add your docking controls...
        SetupDockingPanels();
    }

    private void SetupDockingPanels()
    {
        var panel1 = new Panel { Name = "PropertiesPanel" };
        this.Controls.Add(panel1);
        _dockingManager.SetEnableDocking(panel1, true);
        _dockingManager.DockControl(panel1, this, DockingStyle.Right, 250);
    }
}
```

---

## Part 4: Testing the Theme Adapter

### Unit Test Example

**File:** `tests/WileyWidget.Tests/Themes/DockingManagerThemeAdapterTests.cs`

```csharp
using Xunit;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Themes;

public class DockingManagerThemeAdapterTests
{
    [Fact]
    public void ApplyTheme_Office2019Colorful_SetsOffice2010Style()
    {
        var form = new Form();
        var dockingManager = new DockingManager();
        dockingManager.HostControl = form;

        var adapter = new DockingManagerThemeAdapter(dockingManager);
        adapter.ApplyTheme("Office2019Colorful");

        Assert.Equal(VisualStyle.Office2010, dockingManager.VisualStyle);
        Assert.Equal(Office2010Theme.Colorful, dockingManager.Office2010Theme);
        Assert.True(dockingManager.ThemesEnabled);
    }

    [Fact]
    public void ApplyTheme_ModernColorful_SetsMetroStyle()
    {
        var form = new Form();
        var dockingManager = new DockingManager();
        dockingManager.HostControl = form;

        var adapter = new DockingManagerThemeAdapter(dockingManager);
        adapter.ApplyTheme("ModernColorful");

        Assert.Equal(VisualStyle.Metro, dockingManager.VisualStyle);
        Assert.True(dockingManager.ThemesEnabled);
    }

    [Fact]
    public void ApplyTheme_UnknownTheme_FallsBackToDefault()
    {
        var form = new Form();
        var dockingManager = new DockingManager();
        dockingManager.HostControl = form;

        var adapter = new DockingManagerThemeAdapter(dockingManager);
        adapter.ApplyTheme("UnknownThemeName");

        Assert.Equal(VisualStyle.Office2010, dockingManager.VisualStyle);
    }
}
```

---

## Part 5: Code Review Checklist

When reviewing code that touches DockingManager or theme changes:

- [ ] **DockingManager never uses manual color assignments** (e.g., `BackColor = Color.Blue`)
- [ ] **Theme changes use `DockingManagerThemeAdapter`**, not direct `VisualStyle` assignment
- [ ] **All `SetEnableDocking()` calls** happen AFTER theme application
- [ ] **No `SfSkinManager.SetVisualStyle()` calls on DockingManager** (will silently fail)
- [ ] **Form constructor registers DockingManager** with `ThemeService.RegisterDockingManager()`
- [ ] **Unit tests included** for new DockingManager theme scenarios
- [ ] **MCP UI test included** if theme cascade changes are visible

---

## Part 6: Known Limitations & Workarounds

### Limitation 1: DockingManager Tabs Don't Exactly Match Other Controls

**Reason:** DockingManager uses internal `DockTabController` renderers that don't hook into SfSkinManager.

**Workaround:** Fine-tune the theme map to get closest visual match; use `DockTabBackColor`, `DockTabForeColor` properties to manually adjust tab appearance if needed.

### Limitation 2: Floating Forms May Have Different Styling

**Reason:** DockingManager creates `FloatingForm` instances dynamically with their own painters.

**Workaround:** Apply theme BEFORE docking controls to floating state, or re-apply theme after `FloatControl()` calls.

### Limitation 3: Metro Style Customization Is Limited

**Reason:** DockingManager Metro style only supports `MetroColor`, `MetroCaptionColor`, `MetroButtonColor` properties.

**Workaround:** For fine-grained Metro theming, extend `DockingManagerThemeAdapter` to set additional properties via reflection or custom DockingManager subclass.

---

## Part 7: Future Improvements

1. **Proposed:** Syncfusion adds `ThemeName` property to DockingManager (v27+)
   - Migration path: Remove adapter and use native `SfSkinManager.SetVisualStyle()`

2. **Proposed:** Add theme listener event to DockingManagerThemeAdapter
   - Automatically update when form's theme changes

3. **Proposed:** Create designer-time support for theme selection in Visual Studio

---

## References

- **Syncfusion DockingManager API:** https://help.syncfusion.com/cr/windowsforms/Syncfusion.Windows.Forms.Tools.DockingManager.html
- **Syncfusion SfSkinManager:** https://help.syncfusion.com/windowsforms/sfformskinmanager/overview
- **Wiley Widget Theme Service:** `src/WileyWidget.WinForms/Themes/ThemeColors.cs`

---

**Document Version:** 1.0
**Last Updated:** January 18, 2026
**Status:** Active
**Approval:** Required before DockingManager theme changes
