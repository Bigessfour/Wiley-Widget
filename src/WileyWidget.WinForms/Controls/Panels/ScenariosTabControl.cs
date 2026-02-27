using System;
using System.ComponentModel;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualBasic;
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
using WileyWidget.WinForms.Utilities;


namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Control for the Scenarios tab in Analytics Hub.
/// Allows users to run what-if scenarios with parameter inputs.
/// </summary>
public partial class ScenariosTabControl : UserControl
{
    private readonly ScenariosTabViewModel? _viewModel;
    private readonly ILogger _logger;

    // UI Controls
    private Panel? _inputPanel;
    private SfDataGrid? _resultsGrid;
    private LoadingOverlay? _loadingOverlay;
    private ToolTip? _toolTip;

    // Input controls
    private TextBoxExt? _rateIncreaseTextBox;
    private TextBoxExt? _expenseIncreaseTextBox;
    private TextBoxExt? _revenueTargetTextBox;
    private SfButton? _runScenarioButton;
    private SfButton? _saveScenarioButton;

    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;

    public bool IsLoaded { get; private set; }

    public ScenariosTabControl(ScenariosTabViewModel? viewModel, ILogger? logger = null)
    {
        _viewModel = viewModel;
        _logger = logger ?? NullLogger.Instance;

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
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.AutoScroll = true;
        this.MinimumSize = ScopedPanelBase.RecommendedEmbeddedPanelMinimumLogicalSize;
        this.Padding = new Padding(8);
        _toolTip = new ToolTip();

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
        var themeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";

        _inputPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(LayoutTokens.PanelPadding)
        };
        SfSkinManager.SetVisualStyle(_inputPanel, themeName);

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
        _rateIncreaseTextBox = new TextBoxExt
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Rate increase percent",
            AccessibleDescription = "Rate increase percentage input"
        };
        _toolTip?.SetToolTip(_rateIncreaseTextBox, "Enter the projected rate increase percentage.");
        _rateIncreaseTextBox.TextChanged += RateIncreaseTextBox_TextChanged;

        // Expense Increase
        var expenseLabel = new Label { Text = "Expense Increase %:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _expenseIncreaseTextBox = new TextBoxExt
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Expense increase percent",
            AccessibleDescription = "Expense increase percentage input"
        };
        _toolTip?.SetToolTip(_expenseIncreaseTextBox, "Enter the projected expense increase percentage.");
        _expenseIncreaseTextBox.TextChanged += ExpenseIncreaseTextBox_TextChanged;

        // Revenue Target
        var revenueLabel = new Label { Text = "Revenue Target:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _revenueTargetTextBox = new TextBoxExt
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Revenue target",
            AccessibleDescription = "Revenue target input"
        };
        _toolTip?.SetToolTip(_revenueTargetTextBox, "Enter the projected revenue target.");
        _revenueTargetTextBox.TextChanged += RevenueTargetTextBox_TextChanged;

        // Buttons
        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false
        };

        _runScenarioButton = new SfButton
        {
            Text = "Run Scenario",
            AutoSize = false,
            Size = new Size(120, 32),
            ThemeName = themeName,
            Margin = new Padding(4, 0, 4, 0)
        };
        _runScenarioButton.AccessibleName = "Run scenario";
        _runScenarioButton.AccessibleDescription = "Executes the scenario with current inputs";
        _toolTip?.SetToolTip(_runScenarioButton, "Run the scenario with current parameters.");
        _runScenarioButton.Click += RunScenarioButton_Click;

        _saveScenarioButton = new SfButton
        {
            Text = "Save",
            AutoSize = false,
            Size = new Size(96, 32),
            ThemeName = themeName,
            Margin = new Padding(4, 0, 4, 0)
        };
        _saveScenarioButton.AccessibleName = "Save scenario";
        _saveScenarioButton.AccessibleDescription = "Saves the current scenario configuration";
        _toolTip?.SetToolTip(_saveScenarioButton, "Save this scenario configuration.");
        _saveScenarioButton.Click += SaveScenarioButton_Click;

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
        var themeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
        _resultsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AutoGenerateColumns = false,
            ThemeName = themeName,
            AccessibleName = "Scenario results grid",
            AccessibleDescription = "Results of the scenario calculation"
        }.PreventStringRelationalFilters(null, "Description");
        _toolTip?.SetToolTip(_resultsGrid, "Scenario outcomes and variance results.");

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

        _viewModelPropertyChangedHandler ??= ViewModel_PropertyChanged;
        _viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        _viewModel.PropertyChanged += _viewModelPropertyChangedHandler;

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

    private void RateIncreaseTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null && _rateIncreaseTextBox != null && decimal.TryParse(_rateIncreaseTextBox.Text, out var value))
        {
            _viewModel.RateIncreasePercent = value;
        }
    }

    private void ExpenseIncreaseTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null && _expenseIncreaseTextBox != null && decimal.TryParse(_expenseIncreaseTextBox.Text, out var value))
        {
            _viewModel.ExpenseIncreasePercent = value;
        }
    }

    private void RevenueTargetTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null && _revenueTargetTextBox != null && decimal.TryParse(_revenueTargetTextBox.Text, out var value))
        {
            _viewModel.RevenueTarget = value;
        }
    }

    private async void RunScenarioButton_Click(object? sender, EventArgs e)
    {
        if (_viewModel?.RunScenarioCommand.CanExecute(null) == true)
        {
            await _viewModel.RunScenarioCommand.ExecuteAsync(null);
        }
    }

    private async void SaveScenarioButton_Click(object? sender, EventArgs e)
    {
        await SaveScenarioAsync().ConfigureAwait(true);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

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
            {
                _rateIncreaseTextBox.Text = _viewModel.RateIncreasePercent.ToString("F2");
            }
        }
        else if (e.PropertyName == nameof(_viewModel.ExpenseIncreasePercent))
        {
            if (_expenseIncreaseTextBox != null)
            {
                _expenseIncreaseTextBox.Text = _viewModel.ExpenseIncreasePercent.ToString("F2");
            }
        }
        else if (e.PropertyName == nameof(_viewModel.RevenueTarget))
        {
            if (_revenueTargetTextBox != null)
            {
                _revenueTargetTextBox.Text = _viewModel.RevenueTarget.ToString("F2");
            }
        }
    }

    private async Task SaveScenarioAsync()
    {
        if (_viewModel == null)
        {
            MessageBox.Show("Scenario data is not available.", "Scenarios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var defaultName = $"Scenario {DateTime.Now:yyyy-MM-dd HH:mm}";
        var scenarioName = Interaction.InputBox("Enter a name for this scenario:", "Save Scenario", defaultName);
        if (string.IsNullOrWhiteSpace(scenarioName))
        {
            return;
        }

        try
        {
            if (_saveScenarioButton != null)
            {
                _saveScenarioButton.Enabled = false;
            }

            var saved = await _viewModel.SaveCurrentScenarioAsync(scenarioName.Trim(), CancellationToken.None).ConfigureAwait(true);
            if (saved)
            {
                MessageBox.Show($"Saved scenario '{scenarioName}'.", "Scenarios", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(_viewModel.ErrorMessage ?? "Scenario save failed.", "Scenarios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save scenario: {ex.Message}", "Scenarios", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (_saveScenarioButton != null)
            {
                _saveScenarioButton.Enabled = true;
            }
        }
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
            _logger.LogError(ex, "[ScenariosTabControl] LoadAsync failed");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_rateIncreaseTextBox != null)
            {
                _rateIncreaseTextBox.TextChanged -= RateIncreaseTextBox_TextChanged;
            }
            if (_expenseIncreaseTextBox != null)
            {
                _expenseIncreaseTextBox.TextChanged -= ExpenseIncreaseTextBox_TextChanged;
            }
            if (_revenueTargetTextBox != null)
            {
                _revenueTargetTextBox.TextChanged -= RevenueTargetTextBox_TextChanged;
            }
            if (_runScenarioButton != null)
            {
                _runScenarioButton.Click -= RunScenarioButton_Click;
            }
            if (_saveScenarioButton != null)
            {
                _saveScenarioButton.Click -= SaveScenarioButton_Click;
            }
            if (_viewModel != null && _viewModelPropertyChangedHandler != null)
            {
                _viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            }
            _toolTip?.Dispose();
        }

        base.Dispose(disposing);
    }
}
