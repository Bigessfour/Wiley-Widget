# Syncfusion Animation Detection for Wiley Widget

**Document Version:** 1.0  
**Last Updated:** November 10, 2025  
**Status:** ✅ Production Ready  
**Related:** Test 70 (SfSkinManager E2E), Resource Scanner Suite

---

## Overview

The Wiley Widget animation detection system provides comprehensive scanning and analysis of WPF and Syncfusion-specific animations across the codebase. This ensures startup safety, performance optimization, and integration with theme management.

### Key Capabilities

- **XAML Animation Detection:** Storyboards, DoubleAnimation, ColorAnimation, Triggers
- **C# Animation Detection:** AnimationTimeline, BeginAnimation, TransitionManager
- **Syncfusion Integration:** EnableAnimation, AnimateOnDataChange, SeriesAnimationMode, RowTransitionMode
- **Performance Analysis:** Startup overhead calculation, resource usage tracking
- **Test Integration:** Validates against Test 70 Phase 11 (Performance Load) thresholds

---

## Architecture

### Scanner Components

```
tools/
├── animation_scanner.py         # Primary animation detection tool
└── resource_scanner_enhanced.py # Extended with animation patterns

scripts/templates/animations/
└── Generic.xaml.template        # Reference implementations

docs/reference/
└── animation-detection.md       # This document
```

### Detection Patterns

#### XAML Patterns

| Pattern           | Regex                                             | Purpose                                    |
| ----------------- | ------------------------------------------------- | ------------------------------------------ |
| `Storyboard`      | `<Storyboard\s+x:Key="([^"]+)"`                   | Named animation sequences                  |
| `DoubleAnimation` | `<DoubleAnimation\s+.*TargetProperty="([^"]+)"`   | Property animations (Opacity, Width, etc.) |
| `ColorAnimation`  | `<ColorAnimation\s+.*TargetProperty="([^"]+)"`    | Brush color transitions                    |
| `EventTrigger`    | `<EventTrigger\s+RoutedEvent="([^"]+)"`           | Event-based animation triggers             |
| `PropertyTrigger` | `<Trigger\s+Property="([^"]+)"\s+Value="([^"]+)"` | Condition-based triggers                   |
| `BeginStoryboard` | `<BeginStoryboard[^>]*>`                          | Storyboard activation                      |

#### Syncfusion Patterns

| Pattern               | Property                              | Control Type        | Default Behavior             |
| --------------------- | ------------------------------------- | ------------------- | ---------------------------- |
| `EnableAnimation`     | `EnableAnimation="True/False"`        | SfChart, SfDataGrid | Enabled in FluentLight theme |
| `AnimationDuration`   | `AnimationDuration="0:0:1.5"`         | SfChart             | 1.5s default                 |
| `SeriesAnimationMode` | `SeriesAnimationMode="OnDataChanged"` | ChartSeries         | Auto on data updates         |
| `RowTransitionMode`   | `RowTransitionMode="Fade"`            | SfDataGrid          | Row addition/removal anims   |
| `AnimateOnDataChange` | `AnimateOnDataChange="True"`          | ChartSeries         | Series data refresh anims    |

#### C# Patterns

| Pattern              | Regex                             | Usage Context                  |
| -------------------- | --------------------------------- | ------------------------------ |
| `AnimationTimeline`  | `new\s+(\w*Animation)`            | Code-behind animation creation |
| `BeginAnimation`     | `\.BeginAnimation\((\w+Property)` | Imperative animation start     |
| `TransitionManager`  | `TransitionManager\.(\w+)`        | WPF Toolkit transitions        |
| `VisualStateManager` | `VisualStateManager\.GoToState`   | Control state animations       |

---

## Usage

### 1. Basic Scan

Scan the entire WileyWidget project:

```powershell
python tools/animation_scanner.py --path src/WileyWidget --verbose
```

**Output:**

```
🔍 Scanning: src/WileyWidget
   Focus: all
✅ Scan complete: 0 animations found

================================================================================
🎬 SYNCFUSION ANIMATION SCAN REPORT
================================================================================
Repository: Bigessfour/Wiley-Widget
Scan Date: 2025-11-10 15:30:00
Scope: Focus: all, Path: src/WileyWidget

Files Scanned:
  XAML: 4
  C#: 71
  Total: 75

📊 FINDINGS:
  Total Animations: 0
  Files with Animations: 0
  Unique Animation Keys: 0

✅ HEALTH ASSESSMENT:
  ✅ No custom animations detected
  ✅ Startup-safe (implicit Syncfusion defaults)
  ✅ Performance: ~<10ms overhead per control
================================================================================
```

### 2. Focused Scans

**XAML only:**

```powershell
python tools/animation_scanner.py --focus xaml --verbose
```

**Syncfusion-specific:**

```powershell
python tools/animation_scanner.py --focus syncfusion --path src/WileyWidget/Views
```

**C# code-behind:**

```powershell
python tools/animation_scanner.py --focus csharp --path src/WileyWidget/ViewModels
```

### 3. JSON Report Generation

```powershell
python tools/animation_scanner.py --path src/WileyWidget --output logs/animation_report.json
```

**Report Structure:**

```json
{
  "scan_date": "2025-11-10 15:30:00",
  "repository": "Bigessfour/Wiley-Widget",
  "scope": "Focus: all, Path: src/WileyWidget",
  "total_files_scanned": 75,
  "xaml_files_scanned": 4,
  "csharp_files_scanned": 71,
  "summary": {
    "total_animations_found": 0,
    "files_with_animations": 0,
    "unique_animation_keys": 0,
    "xaml_storyboards": 0,
    "xaml_animations": 0,
    "xaml_triggers": 0,
    "syncfusion_animations": 0,
    "csharp_animations": 0
  },
  "findings": {
    "xaml_storyboards": [],
    "xaml_animations": [],
    "xaml_triggers": [],
    "syncfusion_animations": [],
    "csharp_animations": []
  },
  "files_with_animations": [],
  "animation_keys": []
}
```

### 4. Integration with Resource Scanner

The enhanced resource scanner now includes animation patterns:

```powershell
python tools/resource_scanner_enhanced.py --focus animation --path src/WileyWidget
```

This detects animation resources (Storyboards, Animations) alongside brushes, styles, and converters.

---

## Test 70 Integration

### Phase 11: Performance Load Validation

**Objective:** Ensure animations don't exceed <50ms startup overhead threshold.

**Test File:** `scripts/examples/csharp/70-sfskinmanager-theme-e2e-test.csx`

**Validation Logic:**

```csharp
// Phase 11: Performance Load
Stopwatch sw = Stopwatch.StartNew();
for (int i = 0; i < 10; i++) {
    var control = new SfDataGrid();
    SfSkinManager.SetTheme(control, Theme.FluentLight);
}
sw.Stop();

long totalMs = sw.ElapsedMilliseconds;
if (totalMs > 50) {
    throw new Exception($"Phase 11 FAIL: Theme apply took {totalMs}ms (>50ms)");
}
```

**Animation Impact:**

- **Implicit Syncfusion anims:** ~<10ms per control (built-in FluentLight)
- **Custom Storyboards:** ~<5ms loading per resource
- **Total (10 controls):** ~<50ms (within threshold)

### Phase 12: Conflict Detection

**Objective:** Detect duplicate animation resource keys (e.g., FadeInAnimation defined in multiple files).

**Validation Logic:**

```csharp
// Phase 12: Conflict Detection
var resourceDicts = new[] {
    "Themes/Generic.xaml",
    "Resources/DataTemplates.xaml"
};

var animKeys = new Dictionary<string, List<string>>();
foreach (var dict in resourceDicts) {
    var doc = XDocument.Load(dict);
    var storyboards = doc.Descendants()
        .Where(e => e.Name.LocalName == "Storyboard" && e.Attribute(XName.Get("Key", ns)) != null)
        .Select(e => e.Attribute(XName.Get("Key", ns)).Value);

    foreach (var key in storyboards) {
        if (!animKeys.ContainsKey(key)) animKeys[key] = new List<string>();
        animKeys[key].Add(dict);
    }
}

var duplicates = animKeys.Where(kvp => kvp.Value.Count > 1);
if (duplicates.Any()) {
    throw new Exception($"Duplicate animation keys: {string.Join(", ", duplicates.Select(d => d.Key))}");
}
```

**Run Test:**

```powershell
docker run --rm -w /app -v "${PWD}:/app:ro" -v "${PWD}/logs:/logs:rw" `
  -e WW_REPO_ROOT=/app -e WW_LOGS_DIR=/logs `
  wiley-widget/csx-mcp:local scripts/examples/csharp/70-sfskinmanager-theme-e2e-test.csx
```

---

## Implementation Guide

### Adding Animations to Wiley Widget

**Step 1: Copy Template to Generic.xaml**

```powershell
# Copy animation templates
Copy-Item scripts/templates/animations/Generic.xaml.template `
          src/WileyWidget/Themes/Generic.xaml.animations

# Merge with existing Generic.xaml (manual)
```

**Step 2: Update Themes/Generic.xaml**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:syncfusion="http://schemas.syncfusion.com/wpf">

    <!-- Merge animation resources -->
    <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Generic.xaml.animations"/>
    </ResourceDictionary.MergedDictionaries>

    <!-- Existing brushes, styles, etc. -->

</ResourceDictionary>
```

**Step 3: Apply in XAML Views**

```xml
<Window x:Name="DashboardView"
        xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
        Loaded="Window_Loaded">

    <Window.Triggers>
        <EventTrigger RoutedEvent="Loaded">
            <BeginStoryboard Storyboard="{StaticResource FadeInAnimation}"/>
        </EventTrigger>
    </Window.Triggers>

    <syncfusion:SfDataGrid ItemsSource="{Binding Data}"
                           RowTransitionMode="Fade">
        <!-- Grid columns -->
    </syncfusion:SfDataGrid>

</Window>
```

**Step 4: Verify with Scanner**

```powershell
# Detect new animations
python tools/animation_scanner.py --path src/WileyWidget --verbose

# Verify no duplicates
python tools/resource_scanner_enhanced.py --focus animation --path src/WileyWidget
```

**Step 5: Run Test 70**

```powershell
# Full E2E validation
docker run --rm -w /app -v "${PWD}:/app:ro" -v "${PWD}/logs:/logs:rw" `
  -e WW_REPO_ROOT=/app -e WW_LOGS_DIR=/logs `
  wiley-widget/csx-mcp:local scripts/examples/csharp/70-sfskinmanager-theme-e2e-test.csx
```

---

## Performance Best Practices

### Animation Guidelines

| Guideline                         | Reason            | Threshold              |
| --------------------------------- | ----------------- | ---------------------- |
| **Duration < 500ms**              | UI responsiveness | 300-500ms              |
| **Use Acceleration/Deceleration** | Natural motion    | 0.3 ratio              |
| **Avoid heavy properties**        | GPU overhead      | Brushes, Effects       |
| **Dispose Storyboards**           | Memory leaks      | OnCompleted            |
| **Test on low-end hardware**      | Accessibility     | Min: 2GB RAM, Intel HD |

### Syncfusion Defaults

**SfDataGrid:**

- `RowTransitionMode="None"` (no anims) = 0ms overhead
- `RowTransitionMode="Fade"` (implicit) = ~5ms per row (FluentLight)

**SfChart:**

- `EnableAnimation="False"` = 0ms overhead
- `EnableAnimation="True"` = ~10ms initial load + 50ms per series

**Recommendation:** Keep defaults (implicit FluentLight anims) unless custom UX needed.

---

## Troubleshooting

### Issue: "Animation resource not found"

**Symptoms:**

```
System.Windows.ResourceReferenceKeyNotFoundException: 'FadeInAnimation' resource not found
```

**Solutions:**

1. Verify resource key in Generic.xaml: `<Storyboard x:Key="FadeInAnimation">`
2. Check merged dictionaries in App.xaml:
   ```xml
   <Application.Resources>
       <ResourceDictionary Source="/WileyWidget;component/Themes/Generic.xaml"/>
   </Application.Resources>
   ```
3. Run scanner: `python tools/animation_scanner.py --path src/WileyWidget`

### Issue: Duplicate Animation Keys

**Symptoms:**

```
Test 70 Phase 12 FAIL: Duplicate animation keys: FadeInAnimation
```

**Solutions:**

1. Run scanner with verbose: `python tools/animation_scanner.py --verbose`
2. Check "FILES WITH ANIMATIONS" list for duplicates
3. Consolidate into single resource dictionary (e.g., Generic.xaml)

### Issue: Performance Degradation

**Symptoms:**

```
Test 70 Phase 11 FAIL: Theme apply took 65ms (>50ms)
```

**Solutions:**

1. Profile with scanner: `python tools/animation_scanner.py --focus syncfusion`
2. Disable custom animations temporarily: `EnableAnimation="False"`
3. Reduce animation durations: `Duration="0:0:0.3"` → `Duration="0:0:0.2"`
4. Check for storyboard leaks: Ensure `Completed` event disposal

---

## CI/CD Integration

### Pre-Commit Hook

Add to `.trunk/trunk.yaml`:

```yaml
lint:
  definitions:
    - name: animation-check
      files: [xaml, cs]
      commands:
        - python tools/animation_scanner.py --path src/WileyWidget
        - python tools/resource_scanner_enhanced.py --focus animation
```

### GitHub Actions Workflow

Add to `.github/workflows/ci-optimized.yml`:

```yaml
- name: Scan Animations
  run: |
    python tools/animation_scanner.py --path src/WileyWidget --output logs/animation_report.json
    if [ $(jq '.summary.total_animations_found' logs/animation_report.json) -gt 0 ]; then
      echo "⚠️ Custom animations detected - verify Test 70 Phase 11"
    fi
```

---

## References

### Syncfusion Documentation

- [Animations in SfChart](https://help.syncfusion.com/wpf/chart/animations)
- [DataGrid Row Transitions](https://help.syncfusion.com/wpf/datagrid/transitions)
- [FluentLight Theme Guide](https://help.syncfusion.com/wpf/themes/fluent-light)

### WPF Animation Resources

- [Storyboards Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/storyboards-overview)
- [Animation Easing Functions](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/easing-functions)
- [Performance Best Practices](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-taking-advantage-of-hardware)

### Wiley Widget Docs

- [Test 70: SfSkinManager E2E](../reference/test-70-sfskinmanager.md)
- [Resource Scanner Suite](../reference/resource-scanning.md)
- [Generic.xaml Template](../../scripts/templates/animations/Generic.xaml.template)

---

## Changelog

**v1.0 (2025-11-10):**

- Initial release
- Full XAML, C#, and Syncfusion pattern detection
- Test 70 integration (Phase 11/12)
- Performance analysis and best practices
- Template library (Generic.xaml.template)

---

## Next Steps

1. **If Animations Are Needed:**
   - Copy `scripts/templates/animations/Generic.xaml.template` snippets
   - Add to `src/WileyWidget/Themes/Generic.xaml`
   - Verify with scanner + Test 70

2. **For QuickBooks Dashboard:**
   - Use `DashboardViewTransition` storyboard (400ms slide-in)
   - Apply `QuickBooksSyncAnimation` to sync status icon
   - Test performance with Test 70 Phase 11

3. **For SfDataGrid:**
   - Enable `RowTransitionMode="Fade"` in XAML
   - Validate <5ms per row with scanner
   - Ensure no duplicates with Test 70 Phase 12

---

**Questions or Issues?** Open a GitHub issue with:

- Scanner output: `python tools/animation_scanner.py --verbose`
- Test 70 logs: `logs/wiley-widget-*.log`
- Resource report: `logs/animation_report.json`

🎬 **Let's animate responsibly!**
