# Icon System Reference - Production Ready

**Status**: ✅ Production Ready
**Last Updated**: 2025-11-06
**Icon System**: MahApps.Metro.IconPacks v6.2.1

## Overview

Wiley Widget uses **MahApps.Metro.IconPacks** as the exclusive icon system. All previous SVG and PNG icon systems have been removed to prevent confusion and runtime errors.

## Why MahApps.Metro.IconPacks?

✅ **Vector-based** - Scales perfectly at any resolution
✅ **No file management** - Icons are embedded in the library
✅ **Native WPF support** - No SVG rendering libraries needed
✅ **Comprehensive** - 50+ icon packs with thousands of icons
✅ **Zero runtime errors** - No FileFormatException or missing resource issues
✅ **Theme-aware** - Respects Foreground colors automatically

## Installation

Already included in the project:

```xml
<PackageReference Include="MahApps.Metro.IconPacks" />
```

## Usage Patterns

### Pattern 1: DataTemplate (Recommended for Syncfusion RibbonButton)

**Define in Resources:**

```xml
<Window.Resources>
  <DataTemplate x:Key="SaveIconTemplate">
    <iconPacks:PackIconMaterial Kind="ContentSave" Width="16" Height="16" />
  </DataTemplate>
</Window.Resources>
```

**Use in XAML:**

```xml
<syncfusion:RibbonButton
    Label="Save"
    IconTemplate="{StaticResource SaveIconTemplate}"
    Command="{Binding SaveCommand}" />
```

### Pattern 2: Direct Usage (For Image, Button, etc.)

```xml
<Button>
  <iconPacks:PackIconMaterial Kind="Refresh" Width="20" Height="20" />
</Button>
```

### Pattern 3: Programmatic Usage (C# Code-behind)

```csharp
var icon = new MahApps.Metro.IconPacks.PackIconMaterial
{
    Kind = MahApps.Metro.IconPacks.PackIconMaterialKind.AlertCircle,
    Width = 64,
    Height = 64,
    Foreground = System.Windows.Media.Brushes.Orange
};
```

## Available Icon Templates

### Shell.xaml (Main Window)

| Template Key                   | Icon                 | Size  | Purpose                |
| ------------------------------ | -------------------- | ----- | ---------------------- |
| `SaveIconTemplate`             | ContentSave          | 16x16 | Save actions           |
| `RefreshIconTemplate`          | Refresh              | 16x16 | Refresh/reload actions |
| `DashboardIconTemplate`        | ViewDashboard        | 32x32 | Dashboard navigation   |
| `EnterprisesIconTemplate`      | Domain               | 32x32 | Enterprises navigation |
| `AccountsIconTemplate`         | AccountMultiple      | 32x32 | Accounts navigation    |
| `BudgetIconTemplate`           | Finance              | 32x32 | Budget navigation      |
| `DepartmentsIconTemplate`      | OfficeBuildingMarker | 32x32 | Departments navigation |
| `PlaceholderIconTemplate`      | Circle               | 16x16 | Fallback small icon    |
| `LargePlaceholderIconTemplate` | Circle               | 32x32 | Fallback large icon    |

### DashboardView.xaml

| Template Key               | Icon            | Size  | Purpose              |
| -------------------------- | --------------- | ----- | -------------------- |
| `RefreshIconTemplate`      | Refresh         | 16x16 | Refresh data         |
| `RefreshLargeIconTemplate` | Refresh         | 32x32 | Large refresh button |
| `ExportIconTemplate`       | Export          | 16x16 | Export data          |
| `AccountsIconTemplate`     | AccountMultiple | 16x16 | Navigate to accounts |
| `BackIconTemplate`         | ArrowLeft       | 16x16 | Navigation back      |
| `ForwardIconTemplate`      | ArrowRight      | 16x16 | Navigation forward   |

## Common Icon Kinds (Material Design)

**Actions:**

- `ContentSave` - Save
- `Refresh` - Refresh/Reload
- `Delete` - Delete
- `Edit` - Edit/Modify
- `Add` - Add/Create
- `Close` - Close/Cancel
- `Check` - Confirm/Accept
- `Cancel` - Cancel/Reject

**Navigation:**

- `ViewDashboard` - Dashboard
- `Domain` - Enterprise/Organization
- `AccountMultiple` - Accounts/Users
- `Finance` - Budget/Financial
- `OfficeBuildingMarker` - Departments/Offices
- `Settings` - Settings/Configuration
- `ChartLine` - Reports/Analytics
- `Help` - Help/Support

**Data:**

- `Database` - Database operations
- `FileDocument` - Documents
- `Export` - Export data
- `Import` - Import data
- `Download` - Download
- `Upload` - Upload

**Feedback:**

- `AlertCircle` - Warning/Alert
- `Information` - Information
- `CheckCircle` - Success
- `CloseCircle` - Error

## Adding New Icons

### Step 1: Choose Icon Kind

Browse available icons at: https://materialdesignicons.com/

### Step 2: Define DataTemplate

```xml
<DataTemplate x:Key="YourIconTemplate">
  <iconPacks:PackIconMaterial
      Kind="YourIconKind"
      Width="16"
      Height="16" />
</DataTemplate>
```

### Step 3: Use in View

```xml
<syncfusion:RibbonButton
    IconTemplate="{StaticResource YourIconTemplate}" />
```

## Icon Sizing Guidelines

| Context               | Size   | Usage                              |
| --------------------- | ------ | ---------------------------------- |
| Small toolbar buttons | 16x16  | Quick actions, compact UI          |
| Medium buttons        | 20x20  | Standard buttons                   |
| Large ribbon buttons  | 32x32  | Main navigation, prominent actions |
| Dialog icons          | 48x48  | Dialog headers, notifications      |
| Splash/About          | 64x64+ | Branding, large displays           |

## Migration from Old System

### ❌ REMOVED: SVG Icon System

The following have been **removed** from the project:

- `WileyWidget.UI/Resources/Icons/` directory (226 SVG files)
- `<Resource Include="Resources\Icons\**\*.svg" />` from `.csproj`
- All `SmallIcon="pack://application:,,,/.../icon.svg"` references
- PNG fallback system in `Shell.xaml.cs`

### ✅ Migration Pattern

**Old (SVG - REMOVED):**

```xml
<syncfusion:RibbonButton
    SmallIcon="pack://application:,,,/WileyWidget.UI;component/Resources/Icons/16x16/refresh.svg"
    LargeIcon="pack://application:,,,/WileyWidget.UI;component/Resources/Icons/32x32/refresh.svg" />
```

**New (MahApps IconPacks - CURRENT):**

```xml
<syncfusion:RibbonButton
    IconTemplate="{StaticResource RefreshIconTemplate}" />
```

## Troubleshooting

### Issue: Icon not showing

**Solution**: Verify IconTemplate is defined in Resources and xmlns namespace is declared:

```xml
xmlns:iconPacks="http://metro.mahapps.com/winfx/xaml/iconpacks"
```

### Issue: Icon wrong size

**Solution**: Set Width and Height explicitly in DataTemplate:

```xml
<iconPacks:PackIconMaterial Kind="Icon" Width="32" Height="32" />
```

### Issue: Icon wrong color

**Solution**: Icons inherit Foreground. Set explicitly if needed:

```xml
<iconPacks:PackIconMaterial
    Kind="Icon"
    Foreground="{DynamicResource PrimaryBrush}" />
```

## Performance Considerations

✅ **DataTemplate caching** - Templates are created once and reused
✅ **No file I/O** - Icons are embedded resources
✅ **Minimal memory** - Vector paths are lightweight
✅ **Fast rendering** - Native WPF rendering pipeline

## Best Practices

1. ✅ **Define once, reuse everywhere** - Create DataTemplates in shared resources
2. ✅ **Use semantic names** - `SaveIconTemplate` not `Icon1Template`
3. ✅ **Consistent sizing** - Use standard sizes (16, 20, 32, 48, 64)
4. ✅ **Theme-aware colors** - Use DynamicResource for Foreground
5. ✅ **Document templates** - Keep this reference updated

## Related Files

- `WileyWidget.UI/Views/Windows/Shell.xaml` - Main icon template definitions
- `WileyWidget.UI/Views/Main/DashboardView.xaml` - Dashboard icon templates
- `WileyWidget.UI/WileyWidget.UI.csproj` - MahApps.Metro.IconPacks package reference

## Support

For icon requests or issues, reference this document and the MahApps.Metro.IconPacks documentation:

- https://github.com/MahApps/MahApps.Metro.IconPacks
- https://materialdesignicons.com/

---

**This is the authoritative icon system reference. All other icon documentation is deprecated.**
