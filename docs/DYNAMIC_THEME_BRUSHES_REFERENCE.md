# Dynamic Theme Brushes Reference - FluentDark & FluentLight

## 📚 Overview

This document provides a complete reference of all dynamic theme brushes defined in `WileyTheme.xaml`. All brushes use `DynamicResource` bindings to Syncfusion's FluentDark and FluentLight theme resource keys, enabling automatic theme switching at runtime.

**Supported Themes**: FluentDark, FluentLight  
**Source**: Syncfusion WPF Themes - https://help.syncfusion.com/wpf/themes/skin-manager

---

## 🎨 Brush Categories

### 1. PRIMARY BRAND COLORS

Brushes for primary brand identity and accent colors.

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `PrimaryBrush` | `PrimaryColor` | Main brand color, primary buttons, links |
| `PrimaryForegroundBrush` | `PrimaryForeground` | Text on primary colored backgrounds |
| `PrimaryLightBrush` | `PrimaryLight` | Light variant of primary color for hover states |
| `PrimaryDarkBrush` | `PrimaryDark` | Dark variant of primary color for pressed states |
| `AccentBrush` | `PrimaryColor` | Accent highlights, focus indicators |

**Example Usage**:
```xml
<Button Background="{DynamicResource PrimaryBrush}" 
        Foreground="{DynamicResource PrimaryForegroundBrush}" />
```

---

### 2. BACKGROUND COLORS

Brushes for window, panel, and content backgrounds.

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `WindowBackgroundBrush` | `ContentBackground` | Main window background |
| `ApplicationBackgroundBrush` | `ContentBackground` | Application-level background |
| `PanelBackgroundBrush` | `ContentBackground` | Panel and container backgrounds |
| `CardBackgroundBrush` | `ContentBackground` | Card and elevated surface backgrounds |
| `ContentBackgroundBrush` | `ContentBackground` | General content area background |
| `ContentBackgroundAlt1Brush` | `ContentBackgroundAlt1` | First alternative background |
| `ContentBackgroundAlt2Brush` | `ContentBackgroundAlt2` | Second alternative background |
| `ContentBackgroundAlt3Brush` | `ContentBackgroundAlt3` | Third alternative background |
| `HeaderBackgroundBrush` | `HeaderBackground` | Header and title bar backgrounds |
| `TitleBarBackgroundBrush` | `HeaderBackground` | Title bar specific background |

**State-based Backgrounds**:

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `HoverBackgroundBrush` | `HoverBackground` | Background when mouse hovers |
| `SelectedBackgroundBrush` | `SelectedBackground` | Background for selected items |
| `PressedBackgroundBrush` | `PressedBackground` | Background when pressed/active |

**Example Usage**:
```xml
<Border Background="{DynamicResource CardBackgroundBrush}"
        BorderBrush="{DynamicResource CardBorderBrush}"
        BorderThickness="1"
        CornerRadius="8">
    <!-- Card content -->
</Border>
```

---

### 3. FOREGROUND/TEXT COLORS

Brushes for text and foreground elements.

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `PrimaryTextBrush` | `ContentForeground` | Primary body text |
| `ForegroundBrush` | `ContentForeground` | General foreground elements |
| `ContentForegroundBrush` | `ContentForeground` | Content area text |
| `SecondaryTextBrush` | `ContentForegroundAlt1` | Secondary/caption text |
| `TertiaryTextBrush` | `ContentForegroundAlt2` | Tertiary/muted text |
| `QuaternaryTextBrush` | `ContentForegroundAlt3` | Quaternary/very muted text |
| `SecondaryBrush` | `ContentForegroundAlt3` | Secondary UI elements |

**State-based Foregrounds**:

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `DisabledTextBrush` | `DisabledForeground` | Disabled text and icons |
| `DisabledForegroundBrush` | `DisabledForeground` | Disabled foreground elements |
| `HoverForegroundBrush` | `HoverForeground` | Foreground when mouse hovers |
| `SelectedForegroundBrush` | `SelectedForeground` | Foreground for selected items |
| `PressedForegroundBrush` | `PressedForeground` | Foreground when pressed |

**Example Usage**:
```xml
<TextBlock Text="Primary Text" 
           Foreground="{DynamicResource PrimaryTextBrush}" />
<TextBlock Text="Secondary Caption" 
           Foreground="{DynamicResource SecondaryTextBrush}" 
           FontSize="11" />
```

---

### 4. BORDER COLORS

Brushes for borders, separators, and dividers.

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `BorderBrush` | `BorderAlt` | General borders |
| `BorderAltBrush` | `BorderAlt` | Alternative border style |
| `CardBorderBrush` | `BorderAlt` | Card and container borders |
| `SeparatorBrush` | `BorderAlt` | Horizontal/vertical separators |
| `DividerBrush` | `BorderAlt` | Section dividers |
| `FocusBorderBrush` | `PrimaryColor` | Focus indicator borders |
| `HoverBorderBrush` | `HoverBorder` | Border when mouse hovers |

**Example Usage**:
```xml
<Border BorderBrush="{DynamicResource BorderBrush}"
        BorderThickness="1">
    <!-- Content -->
</Border>

<Separator Background="{DynamicResource SeparatorBrush}" />
```

---

### 5. CONTROL-SPECIFIC COLORS

#### Button Colors

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `ButtonBackgroundBrush` | `ContentBackground` | Default button background |
| `ButtonForegroundBrush` | `ContentForeground` | Button text color |
| `ButtonHoverBackgroundBrush` | `HoverBackground` | Button hover background |
| `ButtonPressedBackgroundBrush` | `PressedBackground` | Button pressed background |
| `ButtonDisabledBackgroundBrush` | `DisabledBackground` | Disabled button background |
| `ButtonDisabledForegroundBrush` | `DisabledForeground` | Disabled button text |

**Example Usage**:
```xml
<Button Background="{DynamicResource ButtonBackgroundBrush}"
        Foreground="{DynamicResource ButtonForegroundBrush}">
    <Button.Style>
        <Style TargetType="Button">
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="{DynamicResource ButtonHoverBackgroundBrush}" />
                </Trigger>
            </Style.Triggers>
        </Style>
    </Button.Style>
</Button>
```

#### Input Control Colors

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `InputBackgroundBrush` | `ContentBackground` | TextBox, ComboBox background |
| `InputForegroundBrush` | `ContentForeground` | Input text color |
| `InputBorderBrush` | `BorderAlt` | Input control borders |
| `InputFocusBorderBrush` | `PrimaryColor` | Border when input has focus |
| `InputDisabledBackgroundBrush` | `DisabledBackground` | Disabled input background |

**Example Usage**:
```xml
<TextBox Background="{DynamicResource InputBackgroundBrush}"
         Foreground="{DynamicResource InputForegroundBrush}"
         BorderBrush="{DynamicResource InputBorderBrush}">
    <TextBox.Style>
        <Style TargetType="TextBox">
            <Style.Triggers>
                <Trigger Property="IsFocused" Value="True">
                    <Setter Property="BorderBrush" Value="{DynamicResource InputFocusBorderBrush}" />
                </Trigger>
            </Style.Triggers>
        </Style>
    </TextBox.Style>
</TextBox>
```

#### Menu and Popup Colors

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `MenuBackgroundBrush` | `ContentBackground` | Menu background |
| `MenuForegroundBrush` | `ContentForeground` | Menu item text |
| `MenuHoverBackgroundBrush` | `HoverBackground` | Menu item hover background |
| `PopupBackgroundBrush` | `ContentBackground` | Popup/dropdown background |
| `PopupBorderBrush` | `BorderAlt` | Popup border |

#### Toolbar Colors

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `ToolbarBackgroundBrush` | `HeaderBackground` | Toolbar background |
| `ToolbarForegroundBrush` | `ContentForeground` | Toolbar text/icons |

#### StatusBar Colors

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `StatusBarBackgroundBrush` | `HeaderBackground` | Status bar background |
| `StatusBarForegroundBrush` | `ContentForeground` | Status bar text |

#### Scrollbar Colors

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `ScrollBarBackgroundBrush` | `ContentBackgroundAlt1` | Scrollbar track background |
| `ScrollBarThumbBrush` | `ContentForegroundAlt2` | Scrollbar thumb |
| `ScrollBarThumbHoverBrush` | `HoverBackground` | Scrollbar thumb hover |

---

### 6. SEMANTIC/STATUS COLORS

Brushes for success, warning, error, and info states.

#### Success Colors

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `SuccessBrush` | `SuccessBackground` | Success indicators |
| `SuccessBackgroundBrush` | `SuccessBackground` | Success message backgrounds |
| `SuccessForegroundBrush` | `SuccessForeground` | Text on success backgrounds |

**Example Usage**:
```xml
<Border Background="{DynamicResource SuccessBackgroundBrush}"
        Padding="8,4"
        CornerRadius="4">
    <TextBlock Text="✓ Success!" 
               Foreground="{DynamicResource SuccessForegroundBrush}" />
</Border>
```

#### Warning Colors

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `WarningBrush` | `WarningBackground` | Warning indicators |
| `WarningBackgroundBrush` | `WarningBackground` | Warning message backgrounds |
| `WarningForegroundBrush` | `WarningForeground` | Text on warning backgrounds |

#### Error Colors

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `ErrorBrush` | `ErrorBackground` | Error indicators |
| `ErrorBackgroundBrush` | `ErrorBackground` | Error message backgrounds |
| `ErrorForegroundBrush` | `ErrorForeground` | Text on error backgrounds |

#### Info Colors

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `InfoBrush` | `InfoBackground` | Info indicators |
| `InfoBackgroundBrush` | `InfoBackground` | Info message backgrounds |
| `InfoForegroundBrush` | `InfoForeground` | Text on info backgrounds |

---

### 7. OVERLAY AND MODAL COLORS

Brushes for overlays, modals, and dialogs.

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `OverlayBackgroundBrush` | `OverlayBackground` (Opacity=0.5) | Screen overlay for modals |
| `ModalBackgroundBrush` | `ContentBackground` | Modal dialog background |
| `DialogBackgroundBrush` | `ContentBackground` | Dialog window background |

**Example Usage**:
```xml
<!-- Modal Overlay -->
<Grid Background="{DynamicResource OverlayBackgroundBrush}">
    <Border Background="{DynamicResource ModalBackgroundBrush}"
            BorderBrush="{DynamicResource BorderBrush}"
            BorderThickness="1"
            CornerRadius="8"
            Padding="20">
        <!-- Modal content -->
    </Border>
</Grid>
```

---

### 8. CHART AND VISUALIZATION COLORS

Brushes for charts, graphs, and data visualizations.

| Brush Key | Syncfusion Resource | Usage |
|-----------|---------------------|-------|
| `ChartBackgroundBrush` | `ContentBackground` | Chart background |
| `ChartGridLineBrush` | `BorderAlt` | Chart grid lines |
| `ChartAxisBrush` | `ContentForeground` | Chart axes |

**Example Usage**:
```xml
<syncfusion:SfChart Background="{DynamicResource ChartBackgroundBrush}">
    <!-- Chart configuration -->
</syncfusion:SfChart>
```

---

## 🔄 Theme Switching

All brushes automatically update when switching between FluentDark and FluentLight themes.

**Switch theme at runtime**:
```csharp
using Syncfusion.SfSkinManager;

// Switch to FluentLight
SfSkinManager.SetTheme(Application.Current, new Theme("FluentLight"));

// Switch to FluentDark
SfSkinManager.SetTheme(Application.Current, new Theme("FluentDark"));
```

---

## ⚠️ Important Notes

1. **No Static Resources**: All color brushes use `DynamicResource` - no static color values except fallbacks.

2. **Fallback Colors**: Only used when Syncfusion theme is not initialized:
   - `FallbackPrimaryColor`: #0078D4
   - `FallbackContentBackground`: #FFFFFF
   - `FallbackContentForeground`: #212121
   - `FallbackBorderAlt`: #E0E0E0

3. **Syncfusion Controls**: All Syncfusion controls (ButtonAdv, SfDataGrid, etc.) automatically inherit theme - DO NOT style them manually.

4. **Opacity**: Only `OverlayBackgroundBrush` has explicit opacity (0.5). All other brushes inherit opacity from theme.

---

## 📋 Complete Brush List (Alphabetical)

| Brush Key | Category |
|-----------|----------|
| `AccentBrush` | Primary |
| `ApplicationBackgroundBrush` | Background |
| `BorderAltBrush` | Border |
| `BorderBrush` | Border |
| `ButtonBackgroundBrush` | Button |
| `ButtonDisabledBackgroundBrush` | Button |
| `ButtonDisabledForegroundBrush` | Button |
| `ButtonForegroundBrush` | Button |
| `ButtonHoverBackgroundBrush` | Button |
| `ButtonPressedBackgroundBrush` | Button |
| `CardBackgroundBrush` | Background |
| `CardBorderBrush` | Border |
| `ChartAxisBrush` | Chart |
| `ChartBackgroundBrush` | Chart |
| `ChartGridLineBrush` | Chart |
| `ContentBackgroundAlt1Brush` | Background |
| `ContentBackgroundAlt2Brush` | Background |
| `ContentBackgroundAlt3Brush` | Background |
| `ContentBackgroundBrush` | Background |
| `ContentForegroundBrush` | Foreground |
| `DialogBackgroundBrush` | Overlay |
| `DisabledForegroundBrush` | Foreground |
| `DisabledTextBrush` | Foreground |
| `DividerBrush` | Border |
| `ErrorBackgroundBrush` | Semantic |
| `ErrorBrush` | Semantic |
| `ErrorForegroundBrush` | Semantic |
| `FocusBorderBrush` | Border |
| `ForegroundBrush` | Foreground |
| `HeaderBackgroundBrush` | Background |
| `HoverBackgroundBrush` | Background |
| `HoverBorderBrush` | Border |
| `HoverForegroundBrush` | Foreground |
| `InfoBackgroundBrush` | Semantic |
| `InfoBrush` | Semantic |
| `InfoForegroundBrush` | Semantic |
| `InputBackgroundBrush` | Input |
| `InputBorderBrush` | Input |
| `InputDisabledBackgroundBrush` | Input |
| `InputFocusBorderBrush` | Input |
| `InputForegroundBrush` | Input |
| `MenuBackgroundBrush` | Menu |
| `MenuForegroundBrush` | Menu |
| `MenuHoverBackgroundBrush` | Menu |
| `ModalBackgroundBrush` | Overlay |
| `OverlayBackgroundBrush` | Overlay |
| `PanelBackgroundBrush` | Background |
| `PopupBackgroundBrush` | Menu |
| `PopupBorderBrush` | Menu |
| `PressedBackgroundBrush` | Background |
| `PressedForegroundBrush` | Foreground |
| `PrimaryBrush` | Primary |
| `PrimaryDarkBrush` | Primary |
| `PrimaryForegroundBrush` | Primary |
| `PrimaryLightBrush` | Primary |
| `PrimaryTextBrush` | Foreground |
| `QuaternaryTextBrush` | Foreground |
| `ScrollBarBackgroundBrush` | Scrollbar |
| `ScrollBarThumbBrush` | Scrollbar |
| `ScrollBarThumbHoverBrush` | Scrollbar |
| `SecondaryBrush` | Foreground |
| `SecondaryTextBrush` | Foreground |
| `SelectedBackgroundBrush` | Background |
| `SelectedForegroundBrush` | Foreground |
| `SeparatorBrush` | Border |
| `StatusBarBackgroundBrush` | StatusBar |
| `StatusBarForegroundBrush` | StatusBar |
| `SuccessBackgroundBrush` | Semantic |
| `SuccessBrush` | Semantic |
| `SuccessForegroundBrush` | Semantic |
| `TertiaryTextBrush` | Foreground |
| `TitleBarBackgroundBrush` | Background |
| `ToolbarBackgroundBrush` | Toolbar |
| `ToolbarForegroundBrush` | Toolbar |
| `WarningBackgroundBrush` | Semantic |
| `WarningBrush` | Semantic |
| `WarningForegroundBrush` | Semantic |
| `WindowBackgroundBrush` | Background |

**Total**: 89 dynamic brushes

---

## 📚 References

- **Syncfusion Themes Documentation**: https://help.syncfusion.com/wpf/themes/overview
- **SfSkinManager API**: https://help.syncfusion.com/wpf/themes/skin-manager
- **Theme Resources**: https://help.syncfusion.com/wpf/themes/skin-manager#theme-resources
- **Implementation Guide**: `docs/SYNCFUSION_THEMING_OFFICIAL_GUIDE.md`

---

**Last Updated**: 2025-10-20  
**Version**: 1.0  
**Themes Supported**: FluentDark, FluentLight
