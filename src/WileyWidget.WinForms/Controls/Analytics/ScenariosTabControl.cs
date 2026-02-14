using System;
using System.Drawing;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Windows.Forms.Tools;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using TextBoxExt = Syncfusion.Windows.Forms.Tools.TextBoxExt;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.ViewModels;

using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;

namespace WileyWidget.WinForms.Controls.Analytics;

/// <summary>
/// Control for the Scenarios tab in Analytics Hub.
/// Allows users to run what-if scenarios with parameter inputs.
/// </summary>
public partial class ScenariosTabControl : UserControl
{
    private readonly ScenariosTabViewModel? _viewModel;

    // UI Controls
    private LegacyGradientPanel? _inputPanel;
    private SfDataGrid? _resultsGrid;
    private LoadingOverlay? _loadingOverlay;

    // Input controls
    private TextBoxExt? _rateIncreaseTextBox;
    private TextBoxExt? _expenseIncreaseTextBox;
    private TextBoxExt? _revenueTargetTextBox;
    private Button? _runScenarioButton;
    private Button? _saveScenarioButton;

    public bool IsLoaded { get; private set; }

    public ScenariosTabControl(ScenariosTabViewModel? viewModel)
    {
        _viewModel = viewModel;

        // Apply Syncfusion theme
        try
        {
            var theme = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
            SfSkinManager.SetVisualStyle(this, theme);
        }
        catch { /* Theme application is best-effort */ }

        InitializeControls();
        if (_viewModel != null)
        {
            BindViewModel();
        }
    }

    private void InitializeControls()
    {
        this.Dock = DockStyle.Fill;

        // Main layout - inputs on top, results below
        var mainSplit = new SplitContainerAdv
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 120
        };

        // Top: Input panel
        InitializeInputPanel();
        mainSplit.Panel1.Controls.Add(_inputPanel);

        // Bottom: Results grid
        InitializeResultsGrid();
        mainSplit.Panel2.Controls.Add(_resultsGrid);

        // Loading overlay
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Running scenario...",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        Controls.Add(mainSplit);
    }

    private void InitializeInputPanel()
    {
        _inputPanel = new LegacyGradientPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        SfSkinManager.SetVisualStyle(_inputPanel, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 1
        };

        // Column styles: Label, Input, Label, Input, Label, Input, Spacer, Buttons
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Rate label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Rate input
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Expense label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Expense input
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Revenue label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Revenue input
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Spacer
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); // Buttons

        // Rate Increase
        var rateLabel = new Label { Text = "Rate Increase %:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _rateIncreaseTextBox = new TextBoxExt { Dock = DockStyle.Fill };
        _rateIncreaseTextBox.TextChanged += (s, e) =>
        {
            if (_viewModel != null && decimal.TryParse(_rateIncreaseTextBox.Text, out var value))
            {
                _viewModel.RateIncreasePercent = value;
            }
        };

        // Expense Increase
        var expenseLabel = new Label { Text = "Expense Increase %:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _expenseIncreaseTextBox = new TextBoxExt { Dock = DockStyle.Fill };
        _expenseIncreaseTextBox.TextChanged += (s, e) =>
        {
            if (_viewModel != null && decimal.TryParse(_expenseIncreaseTextBox.Text, out var value))
            {
                _viewModel.ExpenseIncreasePercent = value;
            }
        };

        // Revenue Target
        var revenueLabel = new Label { Text = "Revenue Target:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _revenueTargetTextBox = new TextBoxExt { Dock = DockStyle.Fill };
        _revenueTargetTextBox.TextChanged += (s, e) =>
        {
            if (_viewModel != null && decimal.TryParse(_revenueTargetTextBox.Text, out var value))
            {
                _viewModel.RevenueTarget = value;
            }
        };

        // Buttons
        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        _runScenarioButton = new Button { Text = "Run Scenario", Width = 100 };
        _runScenarioButton.Click += async (s, e) =>
        {
            if (_viewModel?.RunScenarioCommand.CanExecute(null) == true)
            {
                await _viewModel.RunScenarioCommand.ExecuteAsync(null);
            }
        };

        _saveScenarioButton = new Button { Text = "Save", Width = 40 };
        _saveScenarioButton.Click += (s, e) => SaveScenario();

        buttonsPanel.Controls.Add(_runScenarioButton);
        buttonsPanel.Controls.Add(_saveScenarioButton);

        layout.Controls.Add(rateLabel, 0, 0);
        layout.Controls.Add(_rateIncreaseTextBox, 1, 0);
        layout.Controls.Add(expenseLabel, 2, 0);
        layout.Controls.Add(_expenseIncreaseTextBox, 3, 0);
        layout.Controls.Add(revenueLabel, 4, 0);
        layout.Controls.Add(_revenueTargetTextBox, 5, 0);
        layout.Controls.Add(new Panel(), 6, 0); // Spacer
        layout.Controls.Add(buttonsPanel, 7, 0);

        _inputPanel.Controls.Add(layout);
    }

    private void InitializeResultsGrid()
    {
        _resultsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AutoGenerateColumns = false
        }.PreventStringRelationalFilters(null, "Description");

        // Configure columns for scenario results
        var columns = new GridColumn[]
        {
            new GridTextColumn { MappingName = "Description", HeaderText = "Scenario" },
            new GridNumericColumn { MappingName = "ProjectedValue", HeaderText = "Projected Value", Format = "C2" },
            new GridNumericColumn { MappingName = "Variance", HeaderText = "Variance", Format = "C2" }
        };

        foreach (var column in columns)
        {
            _resultsGrid.Columns.Add(column);
        }
    }

    private void BindViewModel()
    {
        if (_viewModel == null) return;

        // Bind loading state
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsLoading))
            {
                UpdateLoadingState();
            }
            else if (e.PropertyName == nameof(_viewModel.ScenarioResults))
            {
                UpdateResultsGrid();
            }
            else if (e.PropertyName == nameof(_viewModel.RateIncreasePercent))
            {
                if (_rateIncreaseTextBox != null)
                    _rateIncreaseTextBox.Text = _viewModel.RateIncreasePercent.ToString("F2");
            }
            else if (e.PropertyName == nameof(_viewModel.ExpenseIncreasePercent))
            {
                if (_expenseIncreaseTextBox != null)
                    _expenseIncreaseTextBox.Text = _viewModel.ExpenseIncreasePercent.ToString("F2");
            }
            else if (e.PropertyName == nameof(_viewModel.RevenueTarget))
            {
                if (_revenueTargetTextBox != null)
                    _revenueTargetTextBox.Text = _viewModel.RevenueTarget.ToString("F2");
            }
        };

        // Set initial values
        UpdateInputFields();
        UpdateResultsGrid();
    }

    private void UpdateLoadingState()
    {
        if (_loadingOverlay != null)
        {
            _loadingOverlay.Visible = _viewModel?.IsLoading ?? false;
        }
    }

    private void UpdateInputFields()
    {
        if (_rateIncreaseTextBox != null)
            _rateIncreaseTextBox.Text = _viewModel?.RateIncreasePercent.ToString("F2") ?? "0.00";
        if (_expenseIncreaseTextBox != null)
            _expenseIncreaseTextBox.Text = _viewModel?.ExpenseIncreasePercent.ToString("F2") ?? "0.00";
        if (_revenueTargetTextBox != null)
            _revenueTargetTextBox.Text = _viewModel?.RevenueTarget.ToString("F2") ?? "0.00";
    }

    private void UpdateResultsGrid()
    {
        if (_resultsGrid != null && _viewModel?.ScenarioResults != null)
        {
            _resultsGrid.DataSource = _viewModel.ScenarioResults;
        }
    }

    private void SaveScenario()
    {
        // TODO: Implement scenario saving functionality
        MessageBox.Show("Scenario saving will be implemented", "Scenarios",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public async Task LoadAsync()
    {
        if (IsLoaded) return;

        try
        {
            if (_viewModel != null)
            {
                await _viewModel.LoadAsync();
                UpdateInputFields();
            }

            IsLoaded = true;
        }
        catch (Exception ex)
        {
            // Handle error
            System.Diagnostics.Debug.WriteLine($"Failed to load scenarios tab: {ex.Message}");
        }
    }
}
