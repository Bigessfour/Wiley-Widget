# High DPI Support Implementation Guide

## Overview

This document describes the comprehensive High DPI support implementation for WileyWidget Windows Forms application, following Syncfusion's official guidelines at <https://help.syncfusion.com/windowsforms/highdpi-support>.

## Implementation Status

✅ **Completed:**

- Application manifest with `dpiAware=true` and `PerMonitorV2` awareness
- MainForm AutoScaleMode.Dpi configuration
- DashboardPanel AutoScaleMode.Dpi configuration
- DpiAwareImageService using Syncfusion ImageListAdv for automatic multi-DPI icon scaling
- DI registration for DpiAwareImageService as singleton

🔄 **In Progress:**

- Migration from IThemeIconService.GetIcon() to DpiAwareImageService.GetImage()
- Population of ImageListAdv with real icon assets at multiple DPI levels

⏳ **Pending:**

- Creation of DPI-specific icon assets (DPI96, DPI120, DPI144, DPI192)
- Full migration of all panels to use DpiAwareImageService
- Testing at 125%, 150%, 200% scaling factors

## Architecture

### 1. Application Manifest (`app.manifest`)

**Location:** `src/WileyWidget.WinForms/app.manifest`

**Purpose:** Declares DPI awareness to Windows, enabling per-monitor V2 DPI scaling.

**Configuration:**

```xml
<windowsSettings>
  <!-- Per-Monitor DPI Aware -->
  <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>

  <!-- Per-Monitor V2 DPI Awareness (Windows 10 1703+) -->
  <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>

  <!-- Long Path Support -->
  <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
</windowsSettings>
```

**Benefits:**

- PerMonitorV2 provides best-in-class DPI scaling on Windows 10 1703+
- Prevents bitmap stretching and blurry text at high DPI
- Enables mixed-DPI scenarios (moving window between monitors with different scaling)

**Project File Reference:**

```xml
<PropertyGroup>
  <ApplicationManifest>app.manifest</ApplicationManifest>
</PropertyGroup>
```

### 2. Form AutoScaleMode Configuration

**Location:** `src/WileyWidget.WinForms/Forms/MainForm.UI.cs`

**Implementation:**

```csharp
private void InitializeChrome()
{
    // Enable Per-Monitor V2 DPI Awareness (syncs with app.manifest)
    AutoScaleMode = AutoScaleMode.Dpi;

    // ... rest of initialization
}
```

**Impact:** MainForm and all child controls automatically scale based on monitor DPI.

**Applied To:**

- MainForm (MainForm.UI.cs - InitializeChrome method)
- DashboardPanel (DashboardPanel.cs - constructor)
- Other panels: ⏳ Pending review and application

### 3. DpiAwareImageService (Syncfusion ImageListAdv)

**Location:** `src/WileyWidget.WinForms/Services/DpiAwareImageService.cs`

**Purpose:** Provides automatic DPI-scaled icons using Syncfusion's ImageListAdv component.

**Architecture:**

```
┌─────────────────────────────────────────────┐
│       DpiAwareImageService (Singleton)      │
├─────────────────────────────────────────────┤
│ - ImageListAdv _imageList                   │
│ - Dictionary<string, int> _iconNameToIndex  │
├─────────────────────────────────────────────┤
│ + GetImage(iconName): Image?                │
│ + GetImageIndex(iconName): int              │
│ + ImageList: ImageListAdv (property)        │
└─────────────────────────────────────────────┘
         │
         ├─> Images Collection (DPI96 - 16x16)
         │   ├─ "save"
         │   ├─ "open"
         │   ├─ "dashboard"
         │   └─ ... (60+ icons)
         │
         └─> DPIImages Collection
             ├─ DPI96Image: 16x16 (100%)
             ├─ DPI120Image: 20x20 (125%)
             ├─ DPI144Image: 24x24 (150%)
             └─ DPI192Image: 32x32 (200%)
```

**Usage Pattern:**

```csharp
// Option 1: Direct image retrieval (for dynamic scenarios)
var dpiService = serviceProvider.GetService<DpiAwareImageService>();
myButton.Image = dpiService?.GetImage("save");

// Option 2: ImageList assignment (for ToolStrip, ListView, TreeView)
var dpiService = serviceProvider.GetService<DpiAwareImageService>();
myToolStrip.ImageList = dpiService?.ImageList;
myButton.ImageIndex = dpiService?.GetImageIndex("save") ?? -1;
```

**DPI Scaling Matrix:**

| DPI Setting | Scale % | Icon Size | Property Used         |
| ----------- | ------- | --------- | --------------------- |
| 96 DPI      | 100%    | 16x16     | Images collection     |
| 120 DPI     | 125%    | 20x20     | DPIImages.DPI120Image |
| 144 DPI     | 150%    | 24x24     | DPIImages.DPI144Image |
| 192 DPI     | 200%    | 32x32     | DPIImages.DPI192Image |

**DI Registration:**

```csharp
// Configuration/DependencyInjection.cs
services.AddSingleton<DpiAwareImageService>();
```

### 4. Manual DPI Scaling (Existing Pattern)

**Location:** Various panels (e.g., UtilityBillPanel.cs)

**Pattern:**

```csharp
using Syncfusion.Windows.Forms;

var scaledSize = DpiAware.LogicalToDeviceUnits(originalSize);
control.Size = new Size(scaledSize, scaledSize);
```

**Usage:** For non-image UI elements (sizes, padding, margins) that need manual scaling.

**When to Use:**

- Control dimensions (Width, Height, Padding, Margin)
- Font sizes (if not using AutoScaleMode.Dpi)
- Custom drawing operations (Graphics.DrawString, DrawLine, etc.)

**When NOT to Use:**

- Images (use DpiAwareImageService instead)
- Text rendering (handled by AutoScaleMode.Dpi)
- Syncfusion controls (handle DPI internally)

## Migration Guide

### Phase 1: Icon Migration (Current Focus)

**Goal:** Replace all `IThemeIconService.GetIcon()` calls with `DpiAwareImageService.GetImage()`.

**Files Requiring Migration:**

1. DashboardPanel.cs (7 usages)
2. ChartPanel.cs (3 usages)
3. UtilityBillPanel.cs (2 usages)
4. ReportsPanel.cs (1 usage)
5. SettingsPanel.cs (1 usage)
6. MainForm.UI.cs (2 usages in toolbar initialization)

**Migration Pattern:**

**Before:**

```csharp
var iconService = Program.Services.GetService<IThemeIconService>();
button.Image = iconService?.GetIcon("save", ThemeManager.CurrentTheme, 16);
```

**After:**

```csharp
var dpiService = Program.Services.GetService<DpiAwareImageService>();
button.Image = dpiService?.GetImage("save");
```

**Benefits:**

- Automatic DPI scaling (no manual size parameter)
- No theme parameter needed (ImageListAdv handles theme internally if configured)
- Simpler, cleaner API

### Phase 2: Icon Asset Creation

**Directory Structure:**

```
src/WileyWidget.WinForms/
└── Resources/
    └── Icons/
        ├── DPI96/   (16x16 - 100%)
        │   ├── save.png
        │   ├── open.png
        │   ├── dashboard.png
        │   └── ...
        ├── DPI120/  (20x20 - 125%)
        │   ├── save.png
        │   ├── open.png
        │   ├── dashboard.png
        │   └── ...
        ├── DPI144/  (24x24 - 150%)
        │   ├── save.png
        │   ├── open.png
        │   ├── dashboard.png
        │   └── ...
        └── DPI192/  (32x32 - 200%)
            ├── save.png
            ├── open.png
            ├── dashboard.png
            └── ...
```

**Required Icons:** (from DpiAwareImageService.LoadIconsAsync)

- File operations: save, open, export, import, print
- Navigation: home, back, forward, refresh
- Data operations: add, edit, delete, search, filter
- Dashboard: dashboard, chart, gauge, kpi
- Reports: report, pdf, excel
- Settings: settings, config, theme
- Status: success, warning, error, info
- QuickBooks: quickbooks, sync
- Utilities: calculator, calendar, email, help

**Asset Guidelines:**

- Format: PNG with transparency (32-bit RGBA)
- Icon design: Follow Windows 11 design language (simple, flat, monochromatic with accent colors)
- Naming: Lowercase, no spaces (e.g., `save.png`, `export_excel.png`)
- Consistency: Maintain visual weight across all DPI levels (don't just scale - redraw)

### Phase 3: Theme Integration (Optional Enhancement)

**Scenario:** If icons need to change with theme (light/dark modes).

**Approach 1: Multiple ImageListAdv instances:**

```csharp
private readonly Dictionary<AppTheme, ImageListAdv> _themeLists = new();

public ImageListAdv GetImageListForTheme(AppTheme theme)
{
    return _themeLists[theme];
}
```

**Approach 2: Runtime image replacement:**

```csharp
public void UpdateTheme(AppTheme theme)
{
    for (int i = 0; i < _imageList.Images.Count; i++)
    {
        var iconName = _imageList.Images.Keys[i];
        _imageList.Images[i] = LoadIconForTheme(iconName, theme);
        // Update DPIImages collection as well
    }
}
```

**Recommendation:** Approach 1 is simpler and more performant (no runtime image processing).

## Testing Checklist

### Visual Verification

**100% Scaling (96 DPI):**

- [ ] All icons display at 16x16 (crisp, not blurry)
- [ ] Text is readable and properly sized
- [ ] Controls have appropriate spacing

**125% Scaling (120 DPI):**

- [ ] Icons switch to 20x20 variants
- [ ] Text scales proportionally
- [ ] No clipping or overflow issues

**150% Scaling (144 DPI):**

- [ ] Icons switch to 24x24 variants
- [ ] UI remains usable and readable
- [ ] Padding/margins scale correctly

**200% Scaling (200 DPI):**

- [ ] Icons switch to 32x32 variants
- [ ] All UI elements scale proportionally
- [ ] No layout issues or overlapping controls

### Multi-Monitor Scenarios

**Mixed DPI:**

- [ ] Drag window from 100% monitor to 150% monitor
- [ ] Icons update automatically (verify via breakpoint in ImageListAdv)
- [ ] No visual artifacts or glitches

**Runtime DPI Change:**

- [ ] Change system scaling while app is running
- [ ] Windows sends WM_DPICHANGED message
- [ ] App scales correctly without restart

### Performance

- [ ] Icon loading time < 500ms on startup
- [ ] No memory leaks (verify with dotnet-gcdump)
- [ ] CPU usage during DPI change < 10%

## Troubleshooting

### Issue: Icons not scaling

**Symptoms:** Icons remain 16x16 at all DPI levels.

**Diagnosis:**

1. Check app.manifest is embedded: `dotnet msbuild /t:ResolveReferences /v:diag | Select-String manifest`
2. Verify AutoScaleMode.Dpi is set on form
3. Check ImageListAdv.DPIImages collection is populated

**Fix:** Ensure all three components are configured correctly.

### Issue: Blurry icons at high DPI

**Symptoms:** Icons appear stretched or pixelated.

**Diagnosis:**

1. Check if higher DPI variants exist in DPIImages collection
2. Verify image assets are correct sizes (not scaled-up versions)

**Fix:** Create proper high-resolution icon assets for each DPI level.

### Issue: Icons show wrong theme colors

**Symptoms:** Dark icons on dark background, or vice versa.

**Diagnosis:**

1. Check if ThemeManager.CurrentTheme is propagating correctly
2. Verify icon assets match current theme

**Fix:** Either:

- Create separate icon sets per theme, OR
- Use monochromatic icons with ForeColor inheritance

## Syncfusion Control DPI Support

Most Syncfusion controls handle DPI automatically when `AutoScaleMode.Dpi` is set. No additional configuration needed for:

- ✅ SfDataGrid
- ✅ ChartControl
- ✅ RadialGauge
- ✅ SfListView
- ✅ DockingManager
- ✅ RibbonControlAdv
- ✅ StatusBarAdv

**Manual Intervention Required For:**

- ❌ Custom drawing (override OnPaint, use DpiAware.LogicalToDeviceUnits)
- ❌ Hardcoded sizes in code (use LogicalToDeviceUnits)
- ❌ Images loaded from disk (use ImageListAdv or DpiAwareImageService)

## References

- [Syncfusion High DPI Support](https://help.syncfusion.com/windowsforms/highdpi-support)
- [Microsoft High DPI Guidelines](https://learn.microsoft.com/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)
- [PerMonitorV2 DPI Awareness](https://learn.microsoft.com/windows/win32/hidpi/dpi-awareness-context)
- [ImageListAdv API Documentation](https://help.syncfusion.com/cr/windowsforms/Syncfusion.Windows.Forms.Tools.ImageListAdv.html)

## Version History

| Version | Date       | Changes                                                                           |
| ------- | ---------- | --------------------------------------------------------------------------------- |
| 1.0     | 2026-01-02 | Initial implementation with app.manifest, AutoScaleMode.Dpi, DpiAwareImageService |

---

**Last Updated:** 2026-01-02
**Maintained By:** WileyWidget Development Team
**Status:** Active Development
