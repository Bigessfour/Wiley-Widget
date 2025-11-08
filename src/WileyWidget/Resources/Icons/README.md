# Icon Resources

**Quick Reference:** See `docs/ICON_RESOURCES_GUIDE.md` for comprehensive sourcing and implementation guide.

## 🎯 Recommended Source

**Fluent UI System Icons** (Microsoft, MIT License)
📦 Repository: https://github.com/microsoft/fluentui-system-icons
🌐 Browse: https://aka.ms/fluentui-system-icons

**Perfect match for our Syncfusion FluentDark theme!**

## Directory Structure

- **16x16/** - Small icons for ExtraSmall and Small ribbon buttons
- **32x32/** - Large icons for Large size form ribbon buttons

## Quick Download Links

### From Fluent UI System Icons GitHub:

```
https://github.com/microsoft/fluentui-system-icons/tree/main/assets
```

Navigate to `assets/[IconName]/SVG/` for each icon.

### Example Path:

```
assets/Dashboard/SVG/ic_fluent_dashboard_16_regular.svg
assets/Dashboard/SVG/ic_fluent_dashboard_32_regular.svg
```

## Required Icons

Based on the UI Polish Audit Report (Issue #1), the following icons are needed:

### Navigation (Priority 1)

| Icon Need   | Fluent Name                       | Path                      |
| ----------- | --------------------------------- | ------------------------- |
| dashboard   | `dashboard`                       | `assets/Dashboard/`       |
| enterprises | `building` or `building_multiple` | `assets/Building/`        |
| accounts    | `person_accounts`                 | `assets/Person Accounts/` |
| budget      | `money` or `calculator`           | `assets/Money/`           |
| departments | `organization`                    | `assets/Organization/`    |

### Actions (Priority 1)

| Icon Need | Fluent Name                       | Path                      |
| --------- | --------------------------------- | ------------------------- |
| save      | `save`                            | `assets/Save/`            |
| refresh   | `arrow_clockwise` or `arrow_sync` | `assets/Arrow Clockwise/` |
| undo      | `arrow_undo`                      | `assets/Arrow Undo/`      |
| redo      | `arrow_redo`                      | `assets/Arrow Redo/`      |
| search    | `search`                          | `assets/Search/`          |
| help      | `question_circle`                 | `assets/Question Circle/` |
| settings  | `settings`                        | `assets/Settings/`        |

### Data Operations (Priority 2)

- import_16.png / import_32.png
- export_16.png / export_32.png
- sync_16.png / sync_32.png
- backup_16.png / backup_32.png
- restore_16.png / restore_32.png

### CRUD Operations (Priority 2)

- add_16.png / add_32.png
- edit_16.png / edit_32.png
- delete_16.png / delete_32.png
- view_16.png / view_32.png
- copy_16.png / copy_32.png

### Reports (Priority 3)

- financial_16.png / financial_32.png
- budget_analysis_16.png / budget_analysis_32.png
- performance_16.png / performance_32.png
- custom_16.png / custom_32.png

### Tools (Priority 3)

- calculator_16.png / calculator_32.png
- ai_assist_16.png / ai_assist_32.png
- analytics_16.png / analytics_32.png
- chart_16.png / chart_32.png

### Status (Priority 3)

- success_16.png / success_32.png
- warning_16.png / warning_32.png
- error_16.png / error_32.png
- info_16.png / info_32.png
- loading_16.png / loading_32.png

## Recommended Icon Sources

1. **Fluent UI System Icons** (MIT License)
   - https://github.com/microsoft/fluentui-system-icons
   - Best match for Windows 11 / FluentDark theme
   - Available in multiple sizes including 16px and 32px

2. **Material Design Icons** (Apache 2.0)
   - https://materialdesignicons.com/
   - Comprehensive icon set
   - Consistent style

3. **Iconoir** (MIT License)
   - https://iconoir.com/
   - Minimalist, line-based icons
   - Great for modern UIs

## Style Guidelines

- **Format**: PNG with transparency
- **Color**: Monochrome with #2196F3 accent (match AccentBlueBrush)
- **Style**: Line icons, minimal, consistent with FluentDark theme
- **Background**: Transparent
- **Padding**: 2px around the icon content for proper spacing

## Usage Example

```xml
<syncfusion:RibbonButton
    Label="Dashboard"
    SizeForm="Large"
    SmallIcon="/src/Resources/Icons/16x16/dashboard_16.png"
    LargeIcon="/src/Resources/Icons/32x32/dashboard_32.png"
    Command="{Binding NavigateToDashboardCommand}"
    ToolTip="Navigate to Dashboard (Ctrl+1)" />
```

## Implementation Priority

1. **Phase 1** (Day 1-2): Navigation + Actions icons (12 icons × 2 sizes = 24 files)
2. **Phase 2** (Day 3): Data Operations + CRUD icons (10 icons × 2 sizes = 20 files)
3. **Phase 3** (Day 4): Reports + Tools + Status icons (14 icons × 2 sizes = 28 files)

**Total**: 36 icons, 72 PNG files

## Notes

- Icons should be optimized for file size (use PNG compression)
- Consider using SVG paths for better scalability (see Shell.xaml for IconTemplate examples)
- All icons must be added to the project with Build Action: Resource
