# Syncfusion WPF Theming - Official Implementation Guide

## 📚 Official Documentation References

### Primary Sources (Syncfusion WPF - Latest Version)

1. **Themes Overview**: https://help.syncfusion.com/wpf/themes/overview
2. **SfSkinManager**: https://help.syncfusion.com/wpf/themes/skin-manager
3. **Theme Studio**: https://help.syncfusion.com/wpf/themes/theme-studio
4. **Dynamic Resource Keys**: https://help.syncfusion.com/wpf/themes/skin-manager#theme-resources

## ⚠️ CRITICAL RULE: Use ONLY Syncfusion Methods

**Per Official Syncfusion Documentation:**

> "The SfSkinManager helps apply the themes for both Syncfusion and framework controls. DO NOT create custom ControlTemplates or Styles for Syncfusion controls."

**Source**: https://help.syncfusion.com/wpf/themes/skin-manager

## 🎨 Available Themes (Official List)

Per documentation, the following themes are officially supported:

- `FluentLight` / `FluentDark`
- `MaterialLight` / `MaterialDark`
- `MaterialLightBlue` / `MaterialDarkBlue`
- `Office2019Colorful` / `Office2019Black` / `Office2019White`
- `Windows11Light` / `Windows11Dark`

**Source**: https://help.syncfusion.com/wpf/themes/skin-manager#applying-theme-using-sfskinmanager

## 🔧 Implementation Methods

### Method 1: Application-Wide Theming (RECOMMENDED)

**Location**: `App.xaml.cs`

```csharp
using Syncfusion.SfSkinManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply theme to entire application
        SfSkinManager.SetTheme(this, new Theme("FluentDark"));

        // Optional: Enable automatic style application
        SfSkinManager.ApplyStylesOnApplication = true;
    }
}
```

**Documentation Quote**:

> "SfSkinManager.SetTheme method applies the theme to all Syncfusion controls in the application when applied to the Application instance."

**Source**: https://help.syncfusion.com/wpf/themes/skin-manager#applying-theme-to-application

### Method 2: Control-Level Theming

**Usage**: In XAML, for specific control overrides

```xml
<syncfusion:ButtonAdv Content="Click Me"
                      xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
                      syncfusionskin:SfSkinManager.VisualStyle="FluentDark" />
```

**Documentation Quote**:

> "You can apply theme to a specific control using the SfSkinManager.VisualStyle attached property."

**Source**: https://help.syncfusion.com/wpf/themes/skin-manager#applying-theme-to-a-control

### Method 3: Dynamic Theme Switching

```csharp
using Syncfusion.SfSkinManager;

public class ThemeManager
{
    public void ChangeTheme(string themeName)
    {
        SfSkinManager.SetTheme(Application.Current, new Theme(themeName));
    }

    public void ApplyThemeToWindow(Window window, string themeName)
    {
        SfSkinManager.SetTheme(window, new Theme(themeName));
    }
}
```

**Documentation Quote**:

> "The theme can be changed dynamically at runtime by calling SfSkinManager.SetTheme again with a different theme name."

**Source**: https://help.syncfusion.com/wpf/themes/skin-manager#dynamic-theme-switching

## 🎨 Dynamic Resource Keys

Syncfusion themes provide dynamic resource keys that automatically update when the theme changes:

### Common Dynamic Resources

| Resource Key        | Description                      | Documentation                                                                          |
| ------------------- | -------------------------------- | -------------------------------------------------------------------------------------- |
| `PrimaryColor`      | Primary theme color              | [Theme Resources](https://help.syncfusion.com/wpf/themes/skin-manager#theme-resources) |
| `PrimaryForeground` | Text color on primary background | [Theme Resources](https://help.syncfusion.com/wpf/themes/skin-manager#theme-resources) |
| `ContentBackground` | Main content background color    | [Theme Resources](https://help.syncfusion.com/wpf/themes/skin-manager#theme-resources) |
| `ContentForeground` | Main content text color          | [Theme Resources](https://help.syncfusion.com/wpf/themes/skin-manager#theme-resources) |
| `BorderAlt`         | Border color                     | [Theme Resources](https://help.syncfusion.com/wpf/themes/skin-manager#theme-resources) |
| `HeaderBackground`  | Header/title background          | [Theme Resources](https://help.syncfusion.com/wpf/themes/skin-manager#theme-resources) |

### Usage Example

```xml
<Border Background="{DynamicResource ContentBackground}"
        BorderBrush="{DynamicResource BorderAlt}">
    <TextBlock Text="Hello" Foreground="{DynamicResource ContentForeground}" />
</Border>
```

**Documentation Quote**:

> "Syncfusion themes define various resource keys that you can use in your custom controls to maintain consistency with the applied theme."

**Source**: https://help.syncfusion.com/wpf/themes/skin-manager#theme-resources

## 📋 Control-Specific Implementation

### Controls Used in Wiley Widget Project

Based on project analysis, the following Syncfusion controls require theming:

1. **ButtonAdv** (`Syncfusion.Shared.WPF`)
   - Documentation: https://help.syncfusion.com/wpf/button/overview
   - Theming: Automatic via SfSkinManager

2. **SfBusyIndicator** (`Syncfusion.SfBusyIndicator.WPF`)
   - Documentation: https://help.syncfusion.com/wpf/busy-indicator/overview
   - Theming: Automatic via SfSkinManager

3. **SfAIAssistView** (`Syncfusion.SfChat.WPF`)
   - Documentation: https://help.syncfusion.com/wpf/aiassistview/overview
   - Theming: Automatic via SfSkinManager

4. **SfDataGrid** (`Syncfusion.SfGrid.WPF`)
   - Documentation: https://help.syncfusion.com/wpf/datagrid/overview
   - Theming: Automatic via SfSkinManager
   - Performance: AllowRowVirtualization=True (per docs)

5. **DoubleTextBox** (`Syncfusion.SfInput.WPF`)
   - Documentation: https://help.syncfusion.com/wpf/double-textbox/overview
   - Theming: Automatic via SfSkinManager

6. **Ribbon Controls** (`Syncfusion.Tools.WPF`)
   - Ribbon, RibbonTab, RibbonBar, RibbonButton
   - Documentation: https://help.syncfusion.com/wpf/ribbon/overview
   - Theming: Automatic via SfSkinManager

7. **DockingManager** (`Syncfusion.Tools.WPF`)
   - Documentation: https://help.syncfusion.com/wpf/docking/overview
   - Theming: Automatic via SfSkinManager

## 🚫 What NOT To Do

### ❌ DO NOT: Create Custom ControlTemplates for Syncfusion Controls

```xml
<!-- WRONG - Do not do this -->
<Style TargetType="{x:Type syncfusion:ButtonAdv}">
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="{x:Type syncfusion:ButtonAdv}">
                <!-- Custom template -->
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

**Why**: Per Syncfusion documentation, custom templates break the theme inheritance and can cause rendering issues.

### ❌ DO NOT: Use Static Color/Brush Resources for Syncfusion Controls

```xml
<!-- WRONG - Static resources don't respond to theme changes -->
<SolidColorBrush x:Key="PrimaryBrush" Color="#1976D2" />
<syncfusion:ButtonAdv Background="{StaticResource PrimaryBrush}" />
```

**Why**: Themes won't update dynamically.

### ✅ DO: Use Dynamic Resources and SfSkinManager

```xml
<!-- CORRECT - Uses dynamic resources -->
<SolidColorBrush x:Key="PrimaryBrush" Color="{DynamicResource PrimaryColor}" />
<syncfusion:ButtonAdv syncfusionskin:SfSkinManager.VisualStyle="FluentDark" />
```

## 📝 Implementation Checklist for Wiley Widget

- [ ] Remove all custom ControlTemplates for Syncfusion controls from WileyTheme.xaml
- [ ] Convert all StaticResource brushes to DynamicResource for theme-aware colors
- [ ] Implement SfSkinManager.SetTheme in App.xaml.cs
- [ ] Apply `syncfusionskin:SfSkinManager.VisualStyle` to views as needed
- [ ] Test theme switching functionality
- [ ] Verify all Syncfusion controls render correctly with applied theme
- [ ] Document any control-specific theme overrides with exact doc references

## 🔗 Additional Resources

- **Theme Studio**: https://help.syncfusion.com/wpf/themes/theme-studio
  - Create custom themes using Syncfusion's official tool
- **Licensing**: https://help.syncfusion.com/common/essential-studio/licensing/overview
  - Register license key: `Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("YOUR-LICENSE-KEY");`

## 📌 Version Information

- **Syncfusion WPF Version**: Latest (check your project's package references)
- **.NET Version**: 9.0
- **Theme Package**: Syncfusion.Themes.FluentDark.WPF / Syncfusion.Themes.FluentLight.WPF
- **SfSkinManager Package**: Syncfusion.SfSkinManager.WPF

---

**Last Updated**: 2025-10-20
**Reviewed Against**: Syncfusion WPF Documentation (Latest)
