# C# 14 Feature Adoption Guide - Wiley Widget

**Last Updated:** January 2026  
**Target Framework:** .NET 10.0  
**Language Version:** 14.0  

This guide documents C# 14 language features adopted in the Wiley Widget codebase with practical examples relevant to Windows Forms and Syncfusion UI development.

---

## 1. Field Keyword (`field`) - Clean Auto-Properties

The `field` keyword (C# 14) enables cleaner auto-properties with custom initialization logic.

### When to Use
- Simple properties with initialization/validation
- ViewModels and panel properties
- Syncfusion control bindings

### Example: Before vs After

#### Before (Traditional)
```csharp
private string? _selectedTheme;

public string? SelectedTheme
{
    get => _selectedTheme;
    set
    {
        if (_selectedTheme != value)
        {
            _selectedTheme = value;
            OnSelectedThemeChanged(value);
        }
    }
}
```

#### After (C# 14)
```csharp
[ObservableProperty]
private string? selectedTheme;

partial void OnSelectedThemeChanged(string? value)
{
    Logger.LogInformation("Theme changed to: {Theme}", value);
    ThemeService.ApplyTheme(value);
}
```

### In PanelNavigationService (Already Implemented)
```csharp
private readonly ILogger<PanelNavigationService> Logger;  // C# 14: Clean property access

public PanelNavigationService(
    DockingManager dockingManager,
    Control parentControl,
    IServiceProvider serviceProvider,
    ILogger<PanelNavigationService> logger)
{
    Logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

---

## 2. Pattern Matching: `is not null` vs `!= null`

C# 14 pattern matching provides more expressive null checking.

### Recommended Pattern
```csharp
// C# 14: More expressive
if (control is not null && !control.IsDisposed)
{
    control.Invalidate();
}

// ❌ Avoid: Old style
if (control != null && !control.IsDisposed)
{
    control.Invalidate();
}
```

### In DockPanelInternal (Already Implemented)
```csharp
// C# 14: Using 'is not null' for cleaner guard
var header = panel.Controls.OfType<PanelHeader>().FirstOrDefault();
if (header is not null)
{
    header.Title = panelName;
}
```

---

## 3. Extension Members (Properties, Methods, Operators)

C# 14 enables extension properties without declaring backing fields.

### Extension Method for Safe Theme Application
```csharp
// In SyncfusionThemingExtensions.cs (Already Implemented)
public static void ApplySyncfusionTheme(this Control control, string themeName, ILogger? logger = null)
{
    if (control is null || string.IsNullOrWhiteSpace(themeName))
        return;

    try
    {
        // SfSkinManager is the single source of truth
        SfSkinManager.SetVisualStyle(control, themeName);
        control.Invalidate(true);
        control.Update();
        
        logger?.LogDebug("Applied theme '{Theme}' to {ControlType}", themeName, control.GetType().Name);
    }
    catch (Exception ex)
    {
        logger?.LogWarning(ex, "Failed to apply theme '{Theme}'", themeName);
    }
}
```

### Extension Property for Panel Sizing (Already Implemented)
```csharp
public static Size PreferredDockSize(this UserControl panel) => panel switch
{
    DashboardPanel => new Size(560, 420),
    AccountsPanel => new Size(620, 380),
    BudgetPanel => new Size(560, 400),
    _ => new Size(540, 400) // Default fallback
};
```

### Extension Property for Safe Dock Resize (New in Enhanced Extensions)
```csharp
public static Size SafeDockSize(this UserControl panel, DockingStyle style, Size containerSize) => 
    (style, containerSize.Width, containerSize.Height) switch
{
    // Vertical docking: width-aware sizing
    (DockingStyle.Left or DockingStyle.Right, > 300, > 0) => 
        new Size(Math.Min(panel.Width, containerSize.Width / 3), containerSize.Height),
    
    // Horizontal docking: height-aware sizing
    (DockingStyle.Top or DockingStyle.Bottom, > 0, > 200) => 
        new Size(containerSize.Width, Math.Min(panel.Height, containerSize.Height / 3)),
    
    // Fallback: sensible defaults
    _ => new Size(400, 300)
};
```

---

## 4. Null-Conditional Assignment Operator (`?.=`)

Safe null-conditional method invocation.

### Example: Configuring DockingManager
```csharp
// C# 14: Safe null-conditional chaining
_dockingManager?.ConfigureDockingCaption(panel, caption, allowFloating, logger);

// Equivalent to:
if (_dockingManager is not null)
{
    _dockingManager.ConfigureDockingCaption(panel, caption, allowFloating, logger);
}
```

### In SyncfusionThemingExtensions (Already Implemented)
```csharp
public static void ConfigureDockingCaption(
    this DockingManager? dockingManager,
    UserControl panel,
    string caption,
    bool allowFloating = true,
    ILogger? logger = null)
{
    // C# 14: Null-conditional operator (?.) for safe chaining
    dockingManager?.SetDockLabel(panel, caption);
    dockingManager?.SetAllowFloating(panel, allowFloating);
    dockingManager?.SetCloseButtonVisibility(panel, true);
    dockingManager?.SetAutoHideButtonVisibility(panel, true);
    dockingManager?.SetMenuButtonVisibility(panel, true);

    logger?.LogDebug("Configured docking caption for panel: {Caption}", caption);
}
```

---

## 5. Records for Data Transfer Objects

C# 14 records provide lightweight, immutable-like structures.

### Example: Control Visibility State (Already Implemented)
```csharp
/// <summary>
/// Record type for visibility state information (C# 14).
/// Demonstrates use of positional records for clean data passing.
/// </summary>
public record VisibilityState(bool IsVisible, bool HasValidSize, bool IsInvalidated)
{
    /// <summary>Whether control is fully ready for rendering.</summary>
    public bool IsReady => IsVisible && HasValidSize;
}

// Usage:
public static VisibilityState GetVisibilityState(this Control control)
{
    if (control is null)
        return new VisibilityState(false, false, false);

    return new VisibilityState(
        IsVisible: control.Visible && !control.IsDisposed,
        HasValidSize: control.Width > 0 && control.Height > 0,
        IsInvalidated: control is UserControl uc && uc.IsHandleCreated
    );
}
```

---

## 6. Lambda Parameter Modifiers (`ref`, `in`, `out`)

C# 14 allows modifiers on lambda parameters for advanced scenarios.

### Example: Thread-Safe Panel Operations
```csharp
// C# 14: Using lambda with ref parameters for efficient data passing
Action<ref string> updatePanelName = ref (name) =>
{
    if (!string.IsNullOrWhiteSpace(name))
    {
        _activePanelName = name;  // Ref parameter allows modification
    }
};

// Usage in panel activation:
var panelName = "Dashboard";
updatePanelName(ref panelName);
```

---

## 7. Partial Constructors / Events

C# 14 enables splitting constructors and events across files for large forms.

### Example: Splitting Form Initialization
```csharp
// In MainForm.cs
public partial class MainForm : SfForm
{
    // Constructor initialization can be split:
    
    // File 1: MainForm.Constructor.cs
    partial void InitializeControls()
    {
        // Initialize UI controls
    }

    // File 2: MainForm.Events.cs
    partial void SubscribeToEvents()
    {
        // Subscribe to all event handlers
    }
}
```

---

## 8. Tuple Pattern Matching with Switch Expressions

C# 14 improves pattern matching with tuples for complex conditions.

### Example: Docking Style-Aware Sizing
```csharp
// In SafeDockSize extension (Already Implemented)
var size = (dockingStyle, containerWidth, containerHeight) switch
{
    // Pattern: (dockingStyle, width condition, height condition) => result
    (DockingStyle.Left or DockingStyle.Right, > 300, > 0) => 
        new Size(Math.Min(panel.Width, containerSize.Width / 3), containerSize.Height),
    
    (DockingStyle.Top or DockingStyle.Bottom, > 0, > 200) => 
        new Size(containerSize.Width, Math.Min(panel.Height, containerSize.Height / 3)),
    
    _ => new Size(400, 300)
};
```

### Another Example: Theme Application
```csharp
// Determine theme based on OS theme and user preference
var themeName = (userPreference, systemDarkMode) switch
{
    ("Auto", true) => "Office2019Black",
    ("Auto", false) => "Office2019White",
    (custom, _) => custom,
    _ => "Office2019Colorful"
};
```

---

## 9. Required Members (Minimal Use in WinForms)

C# 14 `required` keyword for properties that must be initialized.

### Example: Safe Service Injection
```csharp
// In a service configuration context (not typical in WinForms panels)
public class ThemeConfiguration
{
    public required string CurrentTheme { get; init; }
    public required ILogger Logger { get; init; }
    public required DockingManager DockingManager { get; init; }
}

// Usage: All required properties must be set
var config = new ThemeConfiguration
{
    CurrentTheme = "Office2019Colorful",
    Logger = _logger,
    DockingManager = _dockingManager
};
```

---

## 10. Global Using Statements

Already used in the codebase for cleaner imports.

### Example
```csharp
// global.cs or .csproj <Using> elements
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Windows.Forms;
global using Microsoft.Extensions.Logging;
global using Syncfusion.WinForms.Themes;
```

---

## Best Practices for Wiley Widget

### 1. **SfSkinManager is Authoritative**
   - Always use `SfSkinManager.SetVisualStyle()` for theming
   - Never assign `BackColor` or `ForeColor` manually
   - Use extension methods (C# 14) for safe theme cascading

### 2. **Use `is not null` Consistently**
   - Improves readability in null-heavy code
   - Pairs well with Dispose pattern checks
   - Makes intent clearer in event handlers

### 3. **Leverage Extension Methods**
   - Encapsulate Syncfusion API quirks
   - Reduce duplication across panels
   - Example: `ApplySyncfusionTheme()`, `PreferredDockSize()`

### 4. **Records for Data Structures**
   - Use for visibility, sizing, and configuration tuples
   - Immutable semantics prevent accidental mutations
   - Example: `VisibilityState`, `PanelConfiguration`

### 5. **Tuple Patterns for Complex Logic**
   - Replaces nested if-else chains
   - Better for style/size calculations
   - Example: `SafeDockSize()` tuple-based switch

### 6. **MVVM Toolkit Integration**
   - `ObservableProperty` attribute (MVVM Toolkit)
   - Partial property definitions for validation hooks
   - Combines well with C# 14 patterns

---

## Refactoring Checklist

When updating code to use C# 14:

- [ ] Replace `!= null` with `is not null`
- [ ] Replace `== null` with `is null`
- [ ] Extract reusable logic to extension methods
- [ ] Use extension properties for computed values
- [ ] Replace manual color assignments with SfSkinManager calls
- [ ] Use records for small data structures
- [ ] Leverage tuple patterns in switch expressions
- [ ] Document C# 14 usage in comments for clarity

---

## Related Documentation

- [PanelNavigationService.cs](../src/WileyWidget.WinForms/Services/PanelNavigationService.cs) - Real-world example
- [SyncfusionThemingExtensions.cs](../src/WileyWidget.WinForms/Extensions/SyncfusionThemingExtensions.cs) - Extension members
- [SettingsViewModel.cs](../src/WileyWidget.WinForms/ViewModels/SettingsViewModel.cs) - MVVM patterns
- [.vscode/c-best-practices.md](../vscode/c-best-practices.md) - General C# guidelines

---

## Performance Notes

**C# 14 Zero-Overhead Abstractions:**
- Extension methods: Compiled away at compile time
- Records: No runtime overhead vs. classes with `IEquatable<T>`
- Tuple patterns: Optimized by JIT compiler
- Null-conditional operators: One null check per operator chain

**Syncfusion Considerations:**
- `SfSkinManager.SetVisualStyle()` is cached per control type
- Recursive theme application benefits from C# 14 pattern matching
- Extension properties avoid repeated switch statements

---
