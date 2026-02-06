# 🚀 Panel Validation Quick Start

## TL;DR - Get Started in 5 Minutes

### 1. Run Automated Validation (Recommended First Step)

```powershell
# Validate all panels
.\scripts\test-all-panels.ps1

# Validate specific panels
.\scripts\test-all-panels.ps1 -PanelNames "DashboardPanel","AccountsPanel"

# Get JSON output
.\scripts\test-all-panels.ps1 -OutputFormat json
```

### 2. Inspect a Specific Panel (When Issues Found)

```powershell
# Inspect SfDataGrid configuration
npx --yes @modelcontextprotocol/cli call wileywidget-ui InspectSfDataGrid --params '{
    "formTypeName": "WileyWidget.WinForms.Controls.DashboardPanel",
    "includeSampleData": true
}'

# Check for null reference risks
npx --yes @modelcontextprotocol/cli call wileywidget-ui DetectNullRisks --params '{
    "formTypeNames": ["WileyWidget.WinForms.Controls.DashboardPanel"],
    "outputFormat": "json"
}'
```

### 3. Query Syncfusion Documentation

```powershell
# Get SfDataGrid best practices
npx --yes @modelcontextprotocol/cli call wileywidget-ui SyncfusionWinFormsAssistant --params '{
    "query": "SfDataGrid data binding column configuration styling best practices",
    "components": "SfDataGrid"
}'

# Get DockingManager documentation
npx --yes @modelcontextprotocol/cli call wileywidget-ui SyncfusionWinFormsAssistant --params '{
    "query": "DockingManager panel docking layout save restore",
    "components": "DockingManager"
}'
```

### 4. Fix Common Issues

#### Issue: Panel doesn't render

**Cause:** Missing Handle creation or Dock/Size not set
**Fix:**

```csharp
public MyPanel()
{
    InitializeComponent();

    // Ensure size is set
    this.Size = new Size(800, 600);
    this.Dock = DockStyle.Fill; // or specific docking

    // Create handle if needed
    if (!this.IsHandleCreated)
    {
        this.CreateControl();
    }
}
```

#### Issue: Theme not applied

**Cause:** Manual BackColor/ForeColor set, or ThemeName not set on Syncfusion controls
**Fix:**

```csharp
// Remove manual color assignments
// this.BackColor = Color.White; ❌ REMOVE THIS

// Set ThemeName on Syncfusion controls
if (_sfDataGrid != null)
{
    _sfDataGrid.ThemeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
}
```

#### Issue: Data not binding

**Cause:** DataSource set before grid is initialized, or column mappings wrong
**Fix:**

```csharp
protected override void OnLoad(EventArgs e)
{
    base.OnLoad(e);

    // Ensure grid is initialized
    if (_sfDataGrid != null && _sfDataGrid.IsHandleCreated)
    {
        _sfDataGrid.BeginUpdate();
        try
        {
            // Configure columns first
            _sfDataGrid.Columns.Clear();
            _sfDataGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "AccountNumber",
                HeaderText = "Account #"
            });

            // Then set DataSource
            _sfDataGrid.DataSource = _viewModel?.Accounts;
        }
        finally
        {
            _sfDataGrid.EndUpdate();
        }
    }
}
```

#### Issue: Layout broken or controls overlapping

**Cause:** Conflicting Dock/Anchor properties or missing Padding
**Fix:**

```csharp
// Use TableLayoutPanel for complex layouts
var tableLayout = new TableLayoutPanel
{
    Dock = DockStyle.Fill,
    ColumnCount = 2,
    RowCount = 2,
    Padding = new Padding(10)
};

tableLayout.Controls.Add(headerPanel, 0, 0);
tableLayout.Controls.Add(dataGrid, 0, 1);
```

### 5. Use Syncfusion Demos as Reference

**Find the demo for your control:**

| Control          | Demo URL                                                          |
| ---------------- | ----------------------------------------------------------------- |
| SfDataGrid       | https://github.com/syncfusion/winforms-demos/tree/master/datagrid |
| SfChart          | https://github.com/syncfusion/winforms-demos/tree/master/chart    |
| DockingManager   | https://github.com/syncfusion/winforms-demos/tree/master/docking  |
| SfListView       | https://github.com/syncfusion/winforms-demos/tree/master/listview |
| RibbonControlAdv | https://github.com/syncfusion/winforms-demos/tree/master/ribbon   |

**Compare your code:**

1. Open the demo on GitHub
2. Find the matching scenario (e.g., "Data Binding")
3. Compare initialization, property settings, event handlers
4. Copy pattern (not code) to your panel

---

## Common Validation Patterns

### Pattern 1: Grid-Based Panel

```csharp
public class MyGridPanel : ScopedPanelBase
{
    private SfDataGrid _grid;
    private IMyViewModel? _viewModel;

    public MyGridPanel()
    {
        InitializeComponent();
    }

    [ActivatorUtilitiesConstructor]
    public MyGridPanel(IMyViewModel viewModel) : this()
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void InitializeComponent()
    {
        // Size & Dock
        this.Size = new Size(800, 600);
        this.Dock = DockStyle.Fill;
        this.Padding = new Padding(10);

        // Create grid
        _grid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AllowSorting = true,
            AllowFiltering = true,
            AutoGenerateColumns = false,
            ThemeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful"
        };

        // Configure columns
        _grid.Columns.Add(new GridTextColumn
        {
            MappingName = "Id",
            HeaderText = "ID",
            Width = 100
        });

        this.Controls.Add(_grid);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Bind data after load
        if (_grid != null && _viewModel != null)
        {
            _grid.DataSource = _viewModel.Items;
        }
    }
}
```

### Pattern 2: Chart-Based Panel

```csharp
public class MyChartPanel : ScopedPanelBase
{
    private SfChart _chart;

    public MyChartPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Size = new Size(800, 600);
        this.Dock = DockStyle.Fill;

        _chart = new SfChart
        {
            Dock = DockStyle.Fill,
            ThemeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful"
        };

        // Configure chart
        _chart.PrimaryXAxis = new CategoryAxis { Title = new ChartAxisTitle { Text = "Month" } };
        _chart.PrimaryYAxis = new NumericalAxis { Title = new ChartAxisTitle { Text = "Amount" } };

        this.Controls.Add(_chart);
    }

    public void LoadData(IEnumerable<DataPoint> data)
    {
        var series = new ColumnSeries
        {
            ItemsSource = data,
            XBindingPath = "Category",
            YBindingPath = "Value",
            Label = "Revenue"
        };

        _chart.Series.Add(series);
    }
}
```

---

## VS Code Tasks (Add to .vscode/tasks.json)

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "🔍 Validate All Panels",
      "type": "shell",
      "command": "${workspaceFolder}/scripts/test-all-panels.ps1",
      "problemMatcher": [],
      "presentation": {
        "reveal": "always",
        "panel": "new"
      },
      "group": {
        "kind": "test",
        "isDefault": false
      }
    },
    {
      "label": "🔬 Inspect Current Panel",
      "type": "shell",
      "command": "npx --yes @modelcontextprotocol/cli call wileywidget-ui InspectSfDataGrid --params '{\"formTypeName\":\"WileyWidget.WinForms.Controls.${fileBasenameNoExtension}\",\"includeSampleData\":true}'",
      "problemMatcher": [],
      "presentation": {
        "reveal": "always"
      }
    },
    {
      "label": "📚 Syncfusion Help: SfDataGrid",
      "type": "shell",
      "command": "npx --yes @modelcontextprotocol/cli call wileywidget-ui SyncfusionWinFormsAssistant --params '{\"query\":\"SfDataGrid data binding styling\",\"components\":\"SfDataGrid\"}'",
      "problemMatcher": []
    }
  ]
}
```

---

## Next Steps

1. **Run Validation:** `.\scripts\test-all-panels.ps1`
2. **Review Results:** Check `tmp/panel-validation-*.csv`
3. **Fix Failures:** Start with simple panels, use patterns above
4. **Test Changes:** Re-run validation after fixes
5. **Document:** Update `docs/UI_COMPONENTS.md` with panel details

See [PANEL_VALIDATION_PROCESS.md](.vscode/PANEL_VALIDATION_PROCESS.md) for the complete methodology.
