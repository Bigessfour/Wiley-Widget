# Fetchability / Log Analysis Report (Automated)

Date: 2025-09-03

## Summary
Recent application runs produced repeated startup failures during MainWindow XAML loading due to missing static resources (`DashboardCard`, `CardTitle`) and earlier theme dictionary load failures for Syncfusion Fluent themes.

## Key Findings
- Theme application fallback cycle executed multiple times; initial failures attempting to locate `/Syncfusion.Themes.FluentDark.WPF;component/Themes/FluentDark.xaml` and `/Syncfusion.Themes.FluentLight.WPF;component/Themes/FluentLight.xaml`.
- After ThemeService refactor, subsequent runs succeeded applying `FluentDark` via `SfSkinManager` but MainWindow still crashed with missing resource keys.
- Missing resource keys observed:
  - `DashboardCard` (multiple runs, line 92 position 34 in `MainWindow.xaml`)
  - `CardTitle` (line 109 position 52 in `MainWindow.xaml`)
- Application exit codes consistently `1` after fatal XamlParseException.

## Diagnostics Added
- Pre-theme snapshot logging: counts of merged dictionaries and top-level resource keys.
- Post-theme key probe (`LogMissingResourceKeys`) for: `DashboardCard`, `CardTitle`, `PrimaryAccentBrush`.
- Minimal guard for exceptions while probing keys.

## Root Cause Hypothesis
1. Resource dictionaries containing style definitions for `DashboardCard` and `CardTitle` are not merged into the application scope before MainWindow loads.
2. Earlier manual ResourceDictionary loading attempts were removed; equivalent implicit inclusion via pack URIs may now be missing or file names differ from expected keys.

## Recommended Next Steps
- Locate definitions for `DashboardCard` and `CardTitle` (search *.xaml for Style/Resource keys).
- Ensure the containing XAML file is merged in `App.xaml` *before* window creation, or move styles into a shared dictionary already merged.
- If keys are defined in `SyncfusionResources.xaml`, confirm their exact `x:Key` casing.
- Add a startup validation step that enumerates required style keys (extend current list) and fails early with a clearer message if absent.

## Log Evidence (Excerpts)
```
System.Windows.Markup.XamlParseException: 'Provide value on 'System.Windows.StaticResourceExtension' threw an exception.' ... Cannot find resource named 'DashboardCard'.
System.Windows.Markup.XamlParseException: ... Cannot find resource named 'CardTitle'.
```

## Metrics
- Attempts per run: 1 theme success, 0 successful window loads.
- Uptime before crash: 1.9s – 13s across runs (longer when fallback sequencing extended).

## Action Items
- [ ] Add search for style keys and confirm dictionary inclusion.
- [ ] Decide canonical location for dashboard style resources.
- [ ] Optionally introduce a `RequiredResourceKeys` array and validation method.

---
Generated automatically by assistant tooling.
