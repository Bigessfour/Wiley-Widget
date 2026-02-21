# Syncfusion Control Quick Reference Guide

**Fast lookup for creating Syncfusion controls with complete configuration.**

## 🎯 Factory Injection

```csharp
// In your form/control constructor
public MyForm(SyncfusionControlFactory controlFactory)
{
    _controlFactory = controlFactory;
}

// In Program.cs or DI setup
services.AddSingleton<SyncfusionControlFactory>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SyncfusionControlFactory>>();
    var themeService = sp.GetRequiredService<ThemeService>();
    return new SyncfusionControlFactory(logger, themeService.GetCurrentTheme());
});
```

## 📊 SfDataGrid

```csharp
var grid = _controlFactory.CreateSfDataGrid(g =>
{
    // Core properties already set: Theme, Dock, AllowEditing, AllowFiltering, etc.

    // Customize for your use case:
    g.DataSource = myDataSource;
    g.AutoGenerateColumns = false;
    g.Columns.Add(new GridTextColumn { MappingName = "Name", HeaderText = "Customer Name" });
    g.Columns.Add(new GridNumericColumn { MappingName = "Amount", HeaderText = "Amount", Format = "C2" });
});

this.Controls.Add(grid);
```

**What the factory gives you:**
✅ ThemeName  
✅ Theme applied via SfSkinManager  
✅ String filter protection (prevents crashes)  
✅ AllowEditing, AllowFiltering, AllowSorting all set  
✅ SelectionMode, NavigationMode, FilterRowPosition configured  
✅ EditorSelectionBehavior, AddNewRowPosition set

## 🔘 SfButton

```csharp
var saveButton = _controlFactory.CreateSfButton("Save", btn =>
{
    btn.Location = new Point(10, 10);
    btn.Click += SaveButton_Click;
});

this.Controls.Add(saveButton);
```

**What the factory gives you:**
✅ ThemeName  
✅ Theme applied  
✅ Default size (120x32)  
✅ Flat style  
✅ Segoe UI font

## 📈 RadialGauge

```csharp
var gauge = _controlFactory.CreateRadialGauge(0, 100, 75, g =>
{
    g.Location = new Point(20, 20);
    g.GaugeLabel = "CPU Usage %";
});

this.Controls.Add(gauge);
```

**What the factory gives you:**
✅ Theme applied  
✅ Min/Max/Value set  
✅ FrameType configured  
✅ GaugeArcColor set  
✅ Default scale with ticks

## 📊 ChartControl

```csharp
var chart = _controlFactory.CreateChartControl("Monthly Sales", c =>
{
    var series = new ChartSeries("Sales");
    series.Points.Add(1, 1000);
    series.Points.Add(2, 1500);
    series.Points.Add(3, 1200);
    series.Type = ChartSeriesType.Column;

    c.Series.Add(series);
    c.PrimaryXAxis.Title = "Month";
    c.PrimaryYAxis.Title = "Revenue ($)";
});

this.Controls.Add(chart);
```

**What the factory gives you:**
✅ Theme applied  
✅ PrimaryXAxis/PrimaryYAxis configured  
✅ Legend visible  
✅ Title set  
✅ Docked to fill

## 🎨 SfComboBox

```csharp
var comboBox = _controlFactory.CreateSfComboBox(cb =>
{
    cb.DataSource = myItems;
    cb.DisplayMember = "Name";
    cb.ValueMember = "Id";
    cb.Location = new Point(10, 50);
});

this.Controls.Add(comboBox);
```

**What the factory gives you:**
✅ ThemeName  
✅ Theme applied  
✅ DropDownStyle set  
✅ Default size (200x28)  
✅ AllowFilter enabled  
✅ AutoCompleteMode set

## 📑 TabControlAdv

```csharp
var tabControl = _controlFactory.CreateTabControlAdv(tc =>
{
    var page1 = new TabPageAdv("Overview");
    page1.Controls.Add(myOverviewPanel);

    var page2 = new TabPageAdv("Details");
    page2.Controls.Add(myDetailsPanel);

    tc.TabPages.Add(page1);
    tc.TabPages.Add(page2);
});

this.Controls.Add(tabControl);
```

**What the factory gives you:**
✅ ThemeName  
✅ Theme applied  
✅ TabStyle set (Metro)  
✅ Alignment (Top)  
✅ ItemSize configured

## 🎀 RibbonControlAdv

```csharp
var ribbon = _controlFactory.CreateRibbonControlAdv("File", r =>
{
    var homeTab = new ToolStripTabItem("Home");
    var editTab = new ToolStripTabItem("Edit");

    r.RibbonItems.Add(homeTab);
    r.RibbonItems.Add(editTab);
});

this.Controls.Add(ribbon);
```

**What the factory gives you:**
✅ ThemeName  
✅ Theme applied  
✅ Docked to top  
✅ MenuButtonText set  
✅ RibbonStyle (Office2016)  
✅ OfficeColorScheme configured

## 🪟 DockingManager

```csharp
var dockingManager = _controlFactory.CreateDockingManager(this, dm =>
{
    dm.SetDockLabel(myPanel, "My Panel");
    dm.SetEnableDocking(myPanel, true);
    dm.DockControl(myPanel, this, DockingStyle.Right, 300);
});
```

**What the factory gives you:**
✅ ThemeName  
✅ Theme applied  
✅ HostControl set  
✅ DockBehavior configured  
✅ EnableDocumentMode set  
✅ AnimateAutoHiddenWindow enabled

## 🛠️ Advanced: Custom Control Configuration

If you need a control type not in the factory:

```csharp
// 1. Consult Syncfusion MCP first
// @syncfusion-winforms What are ALL mandatory properties for SfListView?

// 2. Create with full property checklist
var listView = new SfListView
{
    ThemeName = _currentTheme,
    Dock = DockStyle.Fill,
    AutoFitMode = AutoFitMode.Height,
    ItemHeight = 40,
    SelectionMode = Syncfusion.WinForms.ListView.Enums.SelectionMode.Single,
    AllowGroupExpandCollapse = true,
    ShowCheckBoxes = false
};

// 3. Apply theme
listView.ApplySyncfusionTheme(_currentTheme, _logger);

// 4. Add to factory for reuse (update SyncfusionControlFactory.cs)
```

## ⚠️ Common Mistakes

### ❌ Don't: Create controls inline without factory

```csharp
var grid = new SfDataGrid { Dock = DockStyle.Fill }; // Missing 90% of properties!
```

### ✅ Do: Use factory

```csharp
var grid = _controlFactory.CreateSfDataGrid();
```

### ❌ Don't: Assume default values

```csharp
var button = new SfButton { Text = "Save" }; // Missing theme, size, style!
```

### ✅ Do: Let factory handle defaults

```csharp
var button = _controlFactory.CreateSfButton("Save");
```

## 📖 Reference

- **Factory Implementation:** `src/WileyWidget.WinForms/Factories/SyncfusionControlFactory.cs`
- **Enforcement Rules:** `.vscode/rules/syncfusion-control-enforcement.md`
- **Extensions:** `src/WileyWidget.WinForms/Extensions/SyncfusionThemingExtensions.cs`
- **Syncfusion Docs:** Use `@syncfusion-winforms` MCP queries
- **Local Samples:** `C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\32.1.19`

---

**Quick Tip:** When in doubt, check the factory source code for the complete property list!
