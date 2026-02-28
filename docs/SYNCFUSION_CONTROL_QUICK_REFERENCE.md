# Syncfusion Control Quick Reference Guide (v32.2.3)

**Fast lookup for creating Syncfusion controls with complete configuration.**

## 🎯 Factory Injection

```csharp
// In form constructor
public MyForm(SyncfusionControlFactory controlFactory)
{
    _controlFactory = controlFactory;
}

// DI setup (Program.cs)
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
    g.DataSource = myDataSource;
    g.AutoGenerateColumns = false;
    g.Columns.Add(new GridTextColumn { MappingName = "Name", HeaderText = "Customer Name" });
    g.Columns.Add(new GridNumericColumn { MappingName = "Amount", HeaderText = "Amount", Format = "C2" });
});

this.Controls.Add(grid);
```

**Factory Sets:**
✅ ThemeName (v32.2.3)  
✅ SfSkinManager applied  
✅ String filter protection  
✅ AllowEditing/Filtering/Sorting  
✅ SelectionMode, FilterRowPosition  
✅ EditorSelectionBehavior, AddNewRowPosition

## 🔘 SfButton

```csharp
var saveButton = _controlFactory.CreateSfButton("Save", btn =>
{
    btn.Location = new Point(10, 10);
    btn.Click += SaveButton_Click;
});

this.Controls.Add(saveButton);
```

**Factory Sets:**
✅ ThemeName  
✅ Default size (120x32)  
✅ Flat style, Segoe UI font

## 📈 RadialGauge

```csharp
var gauge = _controlFactory.CreateRadialGauge(0, 100, 75, g =>
{
    g.Location = new Point(20, 20);
    g.GaugeLabel = "CPU Usage %";
});

this.Controls.Add(gauge);
```

**Factory Sets:**
✅ Theme applied  
✅ Min/Max/Value  
✅ FrameType, GaugeArcColor  
✅ Scale/ticks

## 📊 ChartControl

```csharp
var chart = _controlFactory.CreateChartControl("Monthly Sales", c =>
{
    var series = new ChartSeries("Sales");
    series.Points.Add(1, 1000); series.Points.Add(2, 1500); series.Points.Add(3, 1200);
    series.Type = ChartSeriesType.Column;
    c.Series.Add(series);
    c.PrimaryXAxis.Title = "Month";
    c.PrimaryYAxis.Title = "Revenue ($)";
});

this.Controls.Add(chart);
```

**Factory Sets:**
✅ Theme applied  
✅ Axes configured  
✅ Legend visible  
✅ Title, docked fill

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

**Factory Sets:**
✅ ThemeName  
✅ DropDownStyle  
✅ Size (200x28), AllowFilter  
✅ AutoCompleteMode

## 📑 TabControlAdv

```csharp
var tabControl = _controlFactory.CreateTabControlAdv(tc =>
{
    var page1 = new TabPageAdv("Overview"); page1.Controls.Add(myOverviewPanel);
    var page2 = new TabPageAdv("Details"); page2.Controls.Add(myDetailsPanel);
    tc.TabPages.AddRange(new[] { page1, page2 });
});

this.Controls.Add(tabControl);
```

**Factory Sets:**
✅ ThemeName  
✅ TabStyle (Metro), Alignment (Top)  
✅ ItemSize

## 🎀 RibbonControlAdv

```csharp
var ribbon = _controlFactory.CreateRibbonControlAdv("File", r =>
{
    var homeTab = new ToolStripTabItem("Home");
    var editTab = new ToolStripTabItem("Edit");
    r.RibbonItems.AddRange(new[] { homeTab, editTab });
});

this.Controls.Add(ribbon);
```

**Factory Sets:**
✅ ThemeName  
✅ Docked top, MenuButtonText  
✅ RibbonStyle (Office2016), OfficeColorScheme

## 🪟 DockingManager

```csharp
var dockingManager = _controlFactory.CreateDockingManager(this, dm =>
{
    dm.SetDockLabel(myPanel, "My Panel");
    dm.SetEnableDocking(myPanel, true);
    dm.DockControl(myPanel, this, DockingStyle.Right, 300);
});
```

**Factory Sets:**
✅ ThemeName  
✅ HostControl, DockBehavior  
✅ EnableDocumentMode, AnimateAutoHiddenWindow

## 🛠️ Custom Controls

For new types:

```csharp
// Query MCP: @syncfusion-winforms Mandatory properties for SfListView?

var listView = new SfListView
{
    ThemeName = _currentTheme,
    Dock = DockStyle.Fill,
    AutoFitMode = AutoFitMode.Height,
    ItemHeight = 40,
    SelectionMode = SelectionMode.Single,
    AllowGroupExpandCollapse = true,
    ShowCheckBoxes = false
};

listView.ApplySyncfusionTheme(_currentTheme, _logger);  // Extension

// Add to factory: Update SyncfusionControlFactory.cs
```

## ⚠️ Mistakes to Avoid

❌ Inline creation: `new SfDataGrid { Dock = DockStyle.Fill };` (missing props)

✅ Factory: `_controlFactory.CreateSfDataGrid();`

❌ Assume defaults: Missing theme/size

✅ Factory handles: Theme, defaults, v32.2.3 compliance

## 📖 Reference

- Factory: src/WileyWidget.WinForms/Factories/SyncfusionControlFactory.cs
- Rules: .vscode/rules/syncfusion-control-enforcement.md
- Extensions: src/WileyWidget.WinForms/Extensions/SyncfusionThemingExtensions.cs
- Docs: @syncfusion-winforms MCP
- Samples: C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\32.2.3

**Tip:** Factory source has full prop lists!
