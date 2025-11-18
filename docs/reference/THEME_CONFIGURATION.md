# Syncfusion Theme Configuration Guide

## Overview

This document explains how Wiley Widget integrates Syncfusion themes using the official `SfSkinManager` approach to avoid `KeyNotFoundException` and other runtime theme-related errors.

## Architecture

### 1. App.xaml - Theme Dictionary Merging

Located: `src/App.xaml`

The application uses a layered theme approach:

```xml
<prism:PrismApplication.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <!-- Layer 1: Syncfusion Global Theme -->
            <syncfusion:SfSkinManagerResourceDictionary ThemeName="FluentLight" />

            <!-- Layer 2: Specific Control Themes (as needed) -->
            <ResourceDictionary Source="/Syncfusion.Themes.FluentLight.WPF;component/MSControl/TextBox.xaml" />
            <ResourceDictionary Source="/Syncfusion.Themes.FluentLight.WPF;component/UpDown/UpDown.xaml" />

            <!-- Layer 3: Custom Wiley Widget Theme Overrides -->
            <ResourceDictionary Source="Themes/WileyTheme-Syncfusion.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</prism:PrismApplication.Resources>
```

### 2. App.xaml.cs - Programmatic Theme Initialization

Located: `src/App.xaml.cs` (OnStartup method)

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    // Apply theme globally BEFORE any UI loads
    SfSkinManager.ApplyThemeAsDefaultStyle = true;
    SfSkinManager.ApplicationTheme = new Theme("FluentLight");

    base.OnStartup(e);
}
```

## Supported Themes

### Current: FluentLight

- **Package**: `Syncfusion.Themes.FluentLight.WPF`
- **Best for**: Modern, clean interface with light backgrounds
- **Configured in**: App.xaml + App.xaml.cs

### Alternative: FluentDark

To switch to FluentDark:

1. **Update App.xaml**:

   ```xml
   <syncfusion:SfSkinManagerResourceDictionary ThemeName="FluentDark" />
   <ResourceDictionary Source="/Syncfusion.Themes.FluentDark.WPF;component/MSControl/TextBox.xaml" />
   <ResourceDictionary Source="/Syncfusion.Themes.FluentDark.WPF;component/UpDown/UpDown.xaml" />
   ```

2. **Update App.xaml.cs**:

   ```csharp
   SfSkinManager.ApplicationTheme = new Theme("FluentDark");
   ```

3. **Update xmlns declaration**:
   ```xml
   xmlns:syncfusionThemes="clr-namespace:Syncfusion.Themes.FluentDark.WPF;assembly=Syncfusion.Themes.FluentDark.WPF"
   ```

## Syncfusion Controls Used

The following Syncfusion controls require theme support:

| Control         | Package                        | Theme Path                     |
| --------------- | ------------------------------ | ------------------------------ |
| DoubleTextBox   | Syncfusion.SfInput.WPF         | UpDown/UpDown.xaml             |
| IntegerTextBox  | Syncfusion.SfInput.WPF         | UpDown/UpDown.xaml             |
| SfDataGrid      | Syncfusion.SfGrid.WPF          | _Auto-themed by SfSkinManager_ |
| SfChart         | Syncfusion.SfChart.WPF         | _Auto-themed by SfSkinManager_ |
| SfBusyIndicator | Syncfusion.SfBusyIndicator.WPF | _Auto-themed by SfSkinManager_ |

## Troubleshooting

### KeyNotFoundException at Runtime

**Symptom**: Application crashes with `KeyNotFoundException` for theme resources like "PrimaryBrush" or "PrimaryColor"

**Solution**:

1. Ensure `SfSkinManagerResourceDictionary` is the **first** merged dictionary
2. Verify `SfSkinManager.ApplicationTheme` is set in `OnStartup()` **before** `base.OnStartup()`
3. Check that theme package is referenced in `WileyWidget.csproj`:
   ```xml
   <PackageReference Include="Syncfusion.Themes.FluentLight.WPF" />
   ```

### Control Not Themed

**Symptom**: Specific control appears with default styling instead of Syncfusion theme

**Solution**:
Add explicit theme dictionary for that control type in App.xaml:

```xml
<ResourceDictionary Source="/Syncfusion.Themes.FluentLight.WPF;component/[ControlCategory]/[ControlName].xaml" />
```

Common paths:

- **TextBox/ComboBox**: `MSControl/TextBox.xaml`
- **Button**: `MSControl/Button.xaml`
- **UpDown controls**: `UpDown/UpDown.xaml`
- **TreeView**: `TreeView/TreeView.xaml`

### Custom Resources Not Found

**Symptom**: Application can't find custom resources defined in `WileyTheme-Syncfusion.xaml`

**Solution**:

1. Ensure custom theme dictionary is loaded **after** Syncfusion themes
2. Use `DynamicResource` instead of `StaticResource` for theme-dependent resources:
   ```xml
   <Button Background="{DynamicResource PrimaryBrush}" />
   ```

## Best Practices

1. ✅ **Use SfSkinManager**: Always use `SfSkinManager.ApplicationTheme` for global theming
2. ✅ **Set Early**: Initialize theme in `OnStartup()` before any UI loads
3. ✅ **Layer Correctly**: Order matters - Syncfusion first, custom overrides last
4. ✅ **DynamicResource**: Use for theme-dependent resources that may change
5. ❌ **Avoid Manual Merges**: Don't manually merge individual control themes unless required
6. ❌ **No File Paths**: Don't load XAML dictionaries from filesystem paths

## References

- [Syncfusion SfSkinManager Documentation](https://help.syncfusion.com/wpf/themes/skin-manager)
- [Syncfusion Theme Studio](https://help.syncfusion.com/wpf/themes/theme-studio)
- [Prism Resource Dictionary Integration](https://prismlibrary.com/docs/wpf/legacy/Implementing-MVVM.html)

## Version Information

- **Syncfusion**: 27.x (2024 Volume 4)
- **.NET**: 9.0
- **Prism**: 9.x
- **Theme Package**: Syncfusion.Themes.FluentLight.WPF

---

**Last Updated**: 2025-10-20
**Maintained By**: Wiley Widget Development Team
