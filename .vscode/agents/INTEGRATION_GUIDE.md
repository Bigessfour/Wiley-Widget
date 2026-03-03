# Winnie Agent Integration with MainForm.Docking.cs

This document explains how the **Winnie** custom Copilot agent integrates with the existing Syncfusion DockingManager architecture in `MainForm.Docking.cs`.

## Architecture Overview

### Current DockingManager Implementation

The `MainForm.Docking.cs` file contains a sophisticated panel management system:

```
MainForm (SfForm)
└── DockingManager (Syncfusion.Windows.Forms.Tools.DockingManager)
    ├── Left Panel (Navigation/Tools)
    ├── Right Panel (Properties/Details)
    ├── Central Panel (Main Document Area)
    └── Activity Log Panel (Bottom)
```

**Key Components:**
- **DockingManager**: Central orchestrator for panel layout
- **DockingLayoutManager**: Saves/loads layout XML to AppData
- **DockingHostFactory**: Factory for creating docking infrastructure
- **SfSkinManager**: Single source of truth for theming
- **Panel Navigation Service**: Service-based panel switching

## Winnie Agent's Understanding

The Winnie agent is trained to understand and follow these patterns:

### 1. Panel Creation Pattern

**How MainForm creates panels:**

```csharp
// From DockingHostFactory.CreateDockingHost()
var (dockingManager, leftPanel, rightPanel, centralPanel, activityLogPanel, _, layoutManager) =
    DockingHostFactory.CreateDockingHost(this, _serviceProvider, _panelNavigator, _logger);

_dockingManager = dockingManager;
_leftDockPanel = leftPanel;
_rightDockPanel = rightPanel;
```

**How Winnie will create new panels:**

When you ask Winnie to create a new docked panel, it will:

1. Create a `UserControl` derived class (e.g., `InvoiceListPanel`)
2. Add corresponding ViewModel (`InvoiceListViewModel`)
3. Register in DI container (`services.AddScoped<InvoiceListPanel>()`)
4. Provide code to dock it:

```csharp
// Example Winnie-generated docking code
var invoicePanel = _serviceProvider.GetRequiredService<InvoiceListPanel>();
_dockingManager.DockControl(invoicePanel, _leftDockPanel, DockingStyle.Docked, 200);
_dockingManager.SetEnableDocking(invoicePanel, true);
_dockingManager.SetDockLabel(invoicePanel, "Invoices");

// Apply theme
var themeName = _themeService?.CurrentTheme ?? "Office2019Colorful";
SfSkinManager.SetVisualStyle(invoicePanel, themeName);
```

### 2. Theme Application Pattern

**Current MainForm theming:**

```csharp
// Centralized theme initialization (Program.cs)
var themeName = themeService.GetCurrentTheme(); // e.g., "Office2019Colorful"
var themeAssembly = themeService.ResolveAssembly(themeName);
SkinManager.LoadAssembly(themeAssembly);
SfSkinManager.ApplicationVisualTheme = themeName;

// Per-form application (MainForm constructor)
SfSkinManager.SetVisualStyle(this, themeName);
```

**How Winnie applies themes:**

Winnie will **never** create competing theme managers or use manual color assignments. It will:

1. Use `SfSkinManager.SetVisualStyle()` on all controls
2. Set `ThemeName` property on Syncfusion controls
3. Trust theme cascade from parent to children
4. Reference `ThemeColors.ApplyTheme()` helper where appropriate

```csharp
// Winnie-generated theme code
public MyCustomPanel(IThemeService themeService)
{
    InitializeComponent();
    
    // Apply theme to entire panel (cascades to children)
    var themeName = themeService?.CurrentTheme ?? "Office2019Colorful";
    SfSkinManager.SetVisualStyle(this, themeName);
    
    // Set ThemeName on Syncfusion-specific controls
    sfDataGrid1.ThemeName = themeName;
    sfButton1.ThemeName = themeName;
}
```

### 3. MVVM Pattern

**Current MainForm uses:**
- Constructor injection for dependencies
- Service-based navigation (`IPanelNavigationService`)
- ViewModels implement `INotifyPropertyChanged` via `ObservableObject`
- Commands use `RelayCommand` from CommunityToolkit.Mvvm

**How Winnie implements MVVM:**

```csharp
// Panel (View) - Pure DataBinding
public partial class InvoiceListPanel : UserControl
{
    private readonly InvoiceListViewModel _viewModel;
    
    public InvoiceListPanel(InvoiceListViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        
        // DataBinding in code or Designer
        sfDataGrid1.DataSource = _viewModel.Invoices;
        addButton.Command = _viewModel.AddInvoiceCommand;
    }
}

// ViewModel - All logic, zero UI references
public partial class InvoiceListViewModel : ObservableObject
{
    private readonly IInvoiceRepository _repository;
    
    [ObservableProperty]
    private ObservableCollection<InvoiceDto> _invoices = new();
    
    [RelayCommand]
    private async Task AddInvoice()
    {
        // Business logic here
        var newInvoice = await _repository.CreateAsync(...);
        Invoices.Add(newInvoice);
    }
}
```

### 4. DockingManager Integration Points

**Key MainForm.Docking.cs methods Winnie understands:**

```csharp
// Initialization
private void InitializeSyncfusionDocking()
{
    // Creates DockingManager infrastructure
    // Applies theme
    // Loads saved layout
}

// Configuration
private void ConfigureDockingManagerSettings()
{
    _dockingManager.EnableDocumentMode = false; // Panels only
    _dockingManager.PersistState = false;
    _dockingManager.AnimateAutoHiddenWindow = true;
}

// Theme application
private void ApplyThemeToDockingPanels()
{
    // Applies theme to all docked controls
}

// Z-order management
private void EnsureProperZOrder()
{
    // Ensures correct stacking order
}
```

**When Winnie adds new panels**, it will respect these patterns and integrate seamlessly.

## Example Workflows with Winnie

### Scenario 1: Create a New Reporting Panel

**User Prompt:**
```
@Winnie Create a new ReportsPanel with the following:
- SfDataGrid for report list
- SfButton for "Generate Report"
- Dock to the left panel like other navigation panels
- Full MVVM with ReportsViewModel
- Office2019Colorful theme
```

**Winnie's Approach:**

1. **Analyze MainForm.Docking.cs** to understand docking patterns
2. **Query Syncfusion MCP** for SfDataGrid and SfButton API
3. **Generate files**:
   - `ReportsPanel.cs` (UserControl)
   - `ReportsPanel.Designer.cs` (Controls layout)
   - `ReportsViewModel.cs` (Business logic)
4. **Provide integration code**:
   ```csharp
   // In DI configuration
   services.AddScoped<ReportsPanel>();
   services.AddScoped<ReportsViewModel>();
   
   // In MainForm initialization
   var reportsPanel = _serviceProvider.GetRequiredService<ReportsPanel>();
   _dockingManager.DockControl(reportsPanel, _leftDockPanel, DockingStyle.Docked, 250);
   ```
5. **Apply theme** using SfSkinManager
6. **Validate** with build task

### Scenario 2: Refactor Existing View to MVVM

**User Prompt:**
```
@Winnie This DashboardPanel has business logic in code-behind. 
Refactor it to pure MVVM following our MainForm patterns.
```

**Winnie's Approach:**

1. **Read existing code** via MCP filesystem tools
2. **Extract business logic** to new `DashboardViewModel`
3. **Implement INotifyPropertyChanged** via `ObservableObject`
4. **Create RelayCommands** for button actions
5. **Update DataBinding** in Designer or code
6. **Remove code-behind logic**
7. **Validate** theme application still works

### Scenario 3: Fix Theme Inconsistencies

**User Prompt:**
```
@Winnie Some controls in SettingsPanel aren't matching the Office2019Colorful theme. 
Why and how do I fix it?
```

**Winnie's Analysis:**

1. **Check SfSkinManager.SetVisualStyle()** is called on the panel
2. **Verify ThemeName property** is set on Syncfusion controls
3. **Identify competing theme code** (manual BackColor/ForeColor assignments)
4. **Propose fix**:
   ```csharp
   // Remove this (VIOLATION):
   // myPanel.BackColor = Color.White;
   
   // Replace with (CORRECT):
   SfSkinManager.SetVisualStyle(settingsPanel, themeName);
   sfDataGrid1.ThemeName = themeName;
   ```

## Technical Details

### Files Winnie References

- **MainForm.Docking.cs**: Panel architecture patterns
- **DockingHostFactory.cs**: Panel creation factory
- **ThemeColors.cs**: Theme application helpers
- **IPanelNavigationService.cs**: Navigation service interface
- **DependencyInjection.cs**: Service registration patterns

### MCP Tools Winnie Uses

1. **mcp_filesystem_read_text_file**: Read existing code
2. **mcp_filesystem_search_files**: Find similar patterns
3. **mcp_filesystem_write_file**: Create new files
4. **apply_patch**: Make surgical code changes
5. **run_task**: Build and validate
6. **Syncfusion WinForms Assistant MCP**: Official API docs

### Constraints Winnie Enforces

✅ **Always:**
- Use SfSkinManager for theming
- Implement pure MVVM (no code-behind logic)
- Follow DockingManager patterns from MainForm
- Apply themes via SetVisualStyle()
- Register panels in DI container

❌ **Never:**
- Create competing theme managers
- Use manual BackColor/ForeColor assignments
- Place business logic in code-behind
- Bypass Syncfusion MCP for API decisions
- Target .NET versions below 8.0

## Integration Checklist

When using Winnie to add/modify panels:

- [ ] Panel inherits from `UserControl` (or `SfForm` for top-level)
- [ ] ViewModel implements `ObservableObject` or `INotifyPropertyChanged`
- [ ] Commands use `RelayCommand` or `AsyncRelayCommand`
- [ ] Panel registered in `DependencyInjection.cs`
- [ ] ViewModel registered in DI container
- [ ] Theme applied via `SfSkinManager.SetVisualStyle()`
- [ ] Syncfusion controls have `ThemeName` property set
- [ ] Panel docked using `_dockingManager.DockControl()`
- [ ] Proper Z-order via `EnsureProperZOrder()` or equivalent
- [ ] Build task passes (`run_task` → `WileyWidget: Build`)
- [ ] Visual inspection confirms theme consistency

## Troubleshooting with Winnie

### "Theme not applying to my control"

**Ask Winnie:**
```
@Winnie This SfButton isn't showing the Office2019Colorful theme. 
Here's the code: [paste code]
```

Winnie will check for:
- Missing `ThemeName` property assignment
- Missing `SfSkinManager.SetVisualStyle()` on parent
- Competing manual color assignments

### "Panel not docking correctly"

**Ask Winnie:**
```
@Winnie I added this panel to DockingManager but it's not visible. 
Here's how I docked it: [paste code]
```

Winnie will verify:
- Control is added to `_dockingManager.Controls`
- `SetEnableDocking()` is called
- `SetDockVisibility()` is true
- Z-order is correct
- Parent panel is visible

### "MVVM binding not working"

**Ask Winnie:**
```
@Winnie My SfDataGrid isn't updating when the ViewModel property changes.
Here's the ViewModel and View code: [paste code]
```

Winnie will check:
- `INotifyPropertyChanged` is implemented
- `OnPropertyChanged()` is called in property setters
- DataBinding is correctly configured
- ObservableCollection is used for collections

## Best Practices

1. **Always provide context**: "Following MainForm.Docking.cs patterns..."
2. **Reference existing code**: "Like DashboardPanel..."
3. **Request validation**: "Build and test the changes"
4. **Ask for rationale**: "Why did you choose this approach?"
5. **Iterate**: Winnie can refine based on feedback

## Additional Resources

- **Syncfusion Docs**: https://help.syncfusion.com/windowsforms/overview
- **MVVM Toolkit**: https://learn.microsoft.com/windows/communitytoolkit/mvvm
- **MainForm.Docking.cs**: Source of truth for docking patterns
- **Agent README**: `.vscode/agents/README.md`

---

**Last Updated:** 2026-02-15
**Target .NET Version:** 10.0
**Syncfusion Version:** 32.1.19
