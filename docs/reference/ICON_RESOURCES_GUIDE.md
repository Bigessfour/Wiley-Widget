# Icon Resources Guide for Wiley Widget

**Generated:** November 4, 2025
**Purpose:** Comprehensive guide for sourcing, downloading, and implementing open-source icons

---

## 🎯 Requirements Summary

### Icon Specifications

- **Sizes Required:** 16×16px, 32×32px (20×20px optional)
- **Format:** PNG with transparency OR SVG
- **Style:** Fluent Design System / line icons, monochrome with #2196F3 accent
- **License:** Must be MIT, Apache 2.0, or similar permissive license
- **Total Needed:** ~48 icons minimum

### Icon Categories

1. **Navigation** (5): dashboard, enterprises, accounts, budget, departments
2. **Actions** (7): save, refresh, undo, redo, search, help, settings
3. **Data Operations** (5): import, export, sync, backup, restore
4. **CRUD Operations** (5): add, edit, delete, view, copy
5. **Reports** (4): financial, budget-analysis, performance, custom
6. **Tools** (4): calculator, ai-assist, analytics, chart
7. **Status** (5): success, warning, error, info, loading
8. **Communication** (3): mail, notification, message
9. **Files** (3): folder, document, file
10. **System** (3): power, lock, user

---

## 📦 Recommended Icon Libraries

### 1. **Fluent UI System Icons** (⭐ RECOMMENDED)

**Provider:** Microsoft
**License:** MIT
**Repository:** https://github.com/microsoft/fluentui-system-icons
**Stats:** 10.2K stars, 1,671 icons

#### Why Choose This?

- ✅ **Perfect match** for Syncfusion FluentDark theme
- ✅ Native Windows 11/Fluent Design aesthetic
- ✅ Comprehensive collection with multiple sizes
- ✅ Available in SVG (scalable) and PNG formats
- ✅ Both `regular` and `filled` styles
- ✅ Actively maintained by Microsoft
- ✅ Excellent documentation

#### Available Sizes

- 16×16px ✅
- 20×20px ✅
- 24×24px ✅
- 28×28px ✅
- 32×32px ✅
- 48×48px ✅

#### Icon Naming Convention

```
[name]_[size]_[style].svg
```

Examples:

- `dashboard_16_regular.svg`
- `save_32_filled.svg`
- `settings_20_regular.svg`

#### How to Download

**Option 1: Direct SVG Download (Recommended)**

```bash
# Navigate to the assets folder
https://github.com/microsoft/fluentui-system-icons/tree/main/assets

# Each icon has its own folder with all sizes
# Example: assets/Dashboard/SVG/ic_fluent_dashboard_16_regular.svg
```

**Option 2: NPM Package**

```bash
npm install @fluentui/svg-icons
```

**Option 3: Clone Entire Repository**

```bash
git clone https://github.com/microsoft/fluentui-system-icons.git
cd fluentui-system-icons/assets
```

#### Icon Mapping for Our Needs

| Our Need   | Fluent Icon Name                        | Path                        |
| ---------- | --------------------------------------- | --------------------------- |
| Dashboard  | `dashboard`                             | `assets/Dashboard/`         |
| Add        | `add`                                   | `assets/Add/`               |
| Edit       | `edit`                                  | `assets/Edit/`              |
| Delete     | `delete`                                | `assets/Delete/`            |
| Save       | `save`                                  | `assets/Save/`              |
| Refresh    | `arrow_sync` or `arrow_clockwise`       | `assets/Arrow Sync/`        |
| Search     | `search`                                | `assets/Search/`            |
| Settings   | `settings`                              | `assets/Settings/`          |
| Import     | `arrow_import`                          | `assets/Arrow Import/`      |
| Export     | `arrow_export`                          | `assets/Arrow Export/`      |
| Calculator | `calculator`                            | `assets/Calculator/`        |
| Chart      | `data_bar_vertical` or `chart_multiple` | `assets/Data Bar Vertical/` |
| Error      | `error_circle` or `dismiss_circle`      | `assets/Error Circle/`      |
| Warning    | `warning`                               | `assets/Warning/`           |
| Success    | `checkmark_circle`                      | `assets/Checkmark Circle/`  |
| Help       | `question_circle`                       | `assets/Question Circle/`   |
| Undo       | `arrow_undo`                            | `assets/Arrow Undo/`        |
| Redo       | `arrow_redo`                            | `assets/Arrow Redo/`        |
| Budget     | `money` or `payment`                    | `assets/Money/`             |
| Sync       | `arrow_sync`                            | `assets/Arrow Sync/`        |
| Backup     | `database_arrow_up`                     | `assets/Database Arrow Up/` |
| Folder     | `folder`                                | `assets/Folder/`            |
| Document   | `document`                              | `assets/Document/`          |
| User       | `person`                                | `assets/Person/`            |
| Lock       | `lock_closed`                           | `assets/Lock Closed/`       |

---

### 2. **Material Design Icons**

**Provider:** Google / Pictogrammers
**License:** Apache License 2.0
**Website:** https://fonts.google.com/icons
**Repository:** https://github.com/google/material-design-icons

#### Why Consider This?

- ✅ Massive collection (2,000+ icons)
- ✅ Industry standard, widely recognized
- ✅ Multiple style variants (outlined, filled, rounded, sharp, two-tone)
- ✅ Available as web fonts or individual SVGs
- ✅ Excellent browser/framework support

#### Available Sizes

- 18×18px
- 24×24px (default)
- 36×36px
- 48×48px
- Custom via SVG scaling

#### How to Download

**Option 1: Download from Web Interface**

1. Visit https://fonts.google.com/icons
2. Search for icon name
3. Click icon → Click "Download" button
4. Select size and format (SVG recommended)

**Option 2: Clone Repository**

```bash
git clone https://github.com/google/material-design-icons.git
cd material-design-icons/symbols/web/
```

#### Note on Sizing

Material Icons don't provide native 16×16 or 32×32, but SVG files can be resized without quality loss. For WPF, you can specify `Width="16"` or `Width="32"` on the `Image` element.

---

### 3. **Iconoir**

**Provider:** Iconoir Community
**License:** MIT
**Website:** https://iconoir.com/
**Repository:** https://github.com/iconoir-icons/iconoir
**Stats:** 4.1K stars, 1,671 icons

#### Why Consider This?

- ✅ Beautiful, minimalist line art style
- ✅ MIT license (most permissive)
- ✅ Consistent stroke width across all icons
- ✅ Perfect for modern, clean UI
- ✅ Active community

#### Available Sizes

- SVG files (scalable to any size)
- Default 24×24 viewBox

#### How to Download

**Option 1: Individual Download from Website**

1. Visit https://iconoir.com/
2. Search icon by name or category
3. Click icon → "Download SVG"
4. Icons are 24×24 viewBox (easily scalable)

**Option 2: NPM Package**

```bash
npm install iconoir
```

**Option 3: Clone Repository**

```bash
git clone https://github.com/iconoir-icons/iconoir.git
cd iconoir/icons/regular
```

---

## 🔧 Implementation in WPF/Syncfusion

### Method 1: Using PNG Images (Simplest)

1. **Download icons in 16×16 and 32×32**
2. **Save to project structure:**

   ```
   src/Resources/Icons/
     16x16/
       dashboard.png
       save.png
       ...
     32x32/
       dashboard.png
       save.png
       ...
   ```

3. **Set Build Action to `Resource`** in project file

4. **Use in XAML:**
   ```xml
   <syncfusion:RibbonButton
       Label="Dashboard"
       SizeForm="Large"
       SmallIcon="/Resources/Icons/16x16/dashboard.png"
       LargeIcon="/Resources/Icons/32x32/dashboard.png"
       Command="{Binding NavigateToDashboardCommand}" />
   ```

### Method 2: Using SVG with Converted Path Data (Best Quality)

1. **Download SVG files**
2. **Convert SVG to XAML Path Data:**
   - Use online converter: https://svg2xaml.azurewebsites.net/
   - Or use Visual Studio extension: "SVG to XAML Converter"

3. **Define in ResourceDictionary:**

   ```xml
   <!-- In src/Resources/IconPaths.xaml -->
   <PathGeometry x:Key="DashboardIconPath"
                 Figures="M3 3v8h8V3H3zm10 0v8h8V3h-8zM3 13v8h8v-8H3zm10 0v8h8v-8h-8z"/>
   ```

4. **Use with IconTemplate:**
   ```xml
   <syncfusion:RibbonButton Label="Dashboard" SizeForm="Large">
       <syncfusion:RibbonButton.IconTemplate>
           <DataTemplate>
               <Path Data="{StaticResource DashboardIconPath}"
                     Fill="{StaticResource AccentBlueBrush}"
                     Stretch="Uniform" />
           </DataTemplate>
       </syncfusion:RibbonButton.IconTemplate>
   </syncfusion:RibbonButton>
   ```

### Method 3: Direct SVG Embedding (Requires Library)

Using a library like `SharpVectors` or `Svg.Skia`:

```xml
<svgc:SvgViewbox Source="/Resources/Icons/dashboard.svg"
                 Width="32" Height="32" />
```

---

## 📋 Step-by-Step Implementation Plan

### Phase 1: Icon Selection & Download (2 hours)

1. ✅ Choose icon library (Recommended: **Fluent UI System Icons**)
2. ✅ Create list of 48 required icons with specific names
3. ✅ Download icons in both 16×16 and 32×32 (or SVG equivalents)
4. ✅ Organize into folder structure

### Phase 2: Project Integration (1 hour)

1. ✅ Copy icons to `src/Resources/Icons/` directory
2. ✅ Update `.csproj` to include icons as embedded resources
3. ✅ Verify build action is set to `Resource`

### Phase 3: XAML Implementation (2-3 hours)

1. ✅ Update `Shell.xaml` ribbon buttons with icons
2. ✅ Update `DashboardView.xaml` buttons with icons
3. ✅ Test icon display in Light and Dark themes
4. ✅ Verify icon sizing is correct

### Phase 4: Testing & Refinement (1 hour)

1. ✅ Visual inspection of all icons
2. ✅ Check icon alignment and spacing
3. ✅ Ensure accessibility (tooltips still visible)
4. ✅ Performance check (load times)

---

## 🎨 Icon Style Consistency Guidelines

### Color Usage

- **Primary icons:** Use theme accent color (`#2196F3`)
- **Disabled icons:** Use disabled foreground color
- **Status icons:**
  - Success: `#4CAF50` (green)
  - Warning: `#FF9800` (orange)
  - Error: `#F44336` (red)
  - Info: `#2196F3` (blue)

### Size Guidelines

- **Large buttons (SizeForm="Large"):** Use 32×32 icons
- **Small buttons (SizeForm="Small"):** Use 16×16 icons
- **Quick Access Toolbar:** Use 16×16 icons
- **Context menus:** Use 16×16 icons

### Naming Conventions

Use consistent, descriptive names:

```
dashboard_16.png
dashboard_32.png
add_16.png
add_32.png
```

---

## 🔍 Icon Search Tips

### Finding Icons in Fluent UI System Icons

1. Visit: https://github.com/microsoft/fluentui-system-icons/blob/main/icons_regular.md
2. Use browser search (Ctrl+F) to find icon names
3. Each icon links to its asset folder

### Icon Name Translations

| Generic Term | Fluent Name               | Material Name         | Iconoir Name       |
| ------------ | ------------------------- | --------------------- | ------------------ |
| Dashboard    | `home_*` or `apps_*`      | `dashboard`           | `dashboard`        |
| Settings     | `settings_*`              | `settings`            | `settings`         |
| Delete       | `delete_*`                | `delete`              | `trash`            |
| Save         | `save_*`                  | `save`                | `save_floppy_disk` |
| Edit         | `edit_*`                  | `edit`                | `edit_pencil`      |
| Search       | `search_*`                | `search`              | `search`           |
| Add          | `add_*` or `add_circle_*` | `add` or `add_circle` | `plus`             |
| Refresh      | `arrow_clockwise_*`       | `refresh`             | `refresh`          |

---

## 📦 Batch Download Script

For Fluent UI System Icons, you can use this PowerShell script to batch download:

```powershell
# download-fluent-icons.ps1
$icons = @(
    "Dashboard",
    "Add",
    "Edit",
    "Delete",
    "Save",
    "Settings",
    "Search"
    # Add more as needed
)

$sizes = @(16, 32)
$style = "regular"  # or "filled"
$baseUrl = "https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets"

foreach ($icon in $icons) {
    foreach ($size in $sizes) {
        $url = "$baseUrl/$icon/SVG/ic_fluent_${icon.ToLower()}_${size}_${style}.svg"
        $outputPath = "src/Resources/Icons/${size}x${size}/${icon.ToLower()}.svg"

        Write-Host "Downloading: $icon ($size)"
        Invoke-WebRequest -Uri $url -OutFile $outputPath
    }
}
```

---

## ✅ Verification Checklist

After implementation, verify:

- [ ] All 48+ required icons are present
- [ ] Icons display correctly at 16×16 and 32×32
- [ ] Icons match FluentDark theme aesthetic
- [ ] Icons are properly aligned in ribbon buttons
- [ ] Icons work in both light and dark themes
- [ ] Tooltips are still visible and accurate
- [ ] Build action is set to `Resource` for all icons
- [ ] No broken image placeholders
- [ ] Performance is acceptable (no noticeable load delay)
- [ ] Icons are accessible (proper alt text via AutomationProperties)

---

## 🔗 Quick Links

- **Fluent Icons Browser:** https://aka.ms/fluentui-system-icons
- **Material Icons Browser:** https://fonts.google.com/icons
- **Iconoir Browser:** https://iconoir.com/
- **SVG to XAML Converter:** https://svg2xaml.azurewebsites.net/
- **Icon Size Validator:** Use browser dev tools to inspect rendered size

---

## 📚 Additional Resources

### WPF Icon Implementation Guides

- [Microsoft Docs: WPF Graphics](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/)
- [Syncfusion Ribbon Documentation](https://help.syncfusion.com/wpf/ribbon/getting-started)

### Design Guidelines

- [Fluent Design System](https://www.microsoft.com/design/fluent/)
- [Material Design Icons Guidelines](https://m3.material.io/styles/icons/overview)

---

**Last Updated:** November 4, 2025
**Prepared For:** Wiley Widget UI Polish Initiative
**Status:** Ready for Implementation
