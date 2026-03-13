using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using Syncfusion.Windows.Forms.Chart;
using WileyWidget.WinForms.Controls.Base;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.Themes;
// using WileyWidget.WinForms.Utils; // Consolidated
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Utilities;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Panel for viewing and managing recommended monthly charges per department.
/// Features AI-driven recommendations, state benchmarking, and profitability analysis.
/// Fully integrated with ICompletablePanel lifecycle for proper async validation and save workflows.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class RecommendedMonthlyChargePanel : ScopedPanelBase<RecommendedMonthlyChargeViewModel>
{
    // UI Controls
    private SfDataGrid? _departmentsGrid;
    private SfDataGrid? _benchmarksGrid;
    private ChartControl? _chartControl;
    private ChartControlRegionEventWiring? _chartRegionEventWiring;
    private SfButton? _refreshButton;
    private SfButton? _saveButton;
    private SfButton? _queryGrokButton;
    private Label? _totalRevenueLabel;
    private Label? _totalExpensesLabel;
    private Label? _suggestedRevenueLabel;
    private Label? _overallStatusLabel;
    private TextBoxExt? _explanationTextBox;
    private Panel? _summaryPanel;
    private Panel? _chartPanel;
    private Panel? _buttonPanel;
    private SplitContainerAdv? _mainSplitContainer;
    private SplitContainerAdv? _leftSplitContainer;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private ErrorProvider? _errorProvider;

    // Event handler storage for proper cleanup (Pattern A & K)
    private EventHandler? _refreshButtonClickHandler;
    private EventHandler? _queryGrokButtonClickHandler;
    private EventHandler? _saveButtonClickHandler;
    private EventHandler? _panelHeaderRefreshClickedHandler;
    private EventHandler? _panelHeaderHelpClickedHandler;
    private EventHandler? _panelHeaderCloseClickedHandler;
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    private System.ComponentModel.IContainer? components;
    private IReadOnlyList<DepartmentRateModel>? _diagnosticsFallbackDepartments;
    private IReadOnlyList<StateBenchmarkModel>? _diagnosticsFallbackBenchmarks;

    public RecommendedMonthlyChargePanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<RecommendedMonthlyChargeViewModel>> logger)
        : base(scopeFactory, logger)
    {
        // NOTE: InitializeControls() moved to OnViewModelResolved()
        if (_mainSplitContainer == null)
        {
            SafeSuspendAndLayout(InitializeControls);

            if (ViewModel != null)
            {
                BindViewModel();
                ApplyCurrentTheme();
            }
            else
            {
                ApplyDiagnosticsFallbackContentIfNeeded();
            }
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        MinimumSize = ScaleLogicalToDevice(new Size(1024, 720));
        PerformLayout();
        Invalidate(true);
    }

    #region ICompletablePanel Overrides

    /// <summary>
    /// Loads the panel asynchronously and initializes charge recommendation data.
    /// </summary>
    public override async Task LoadAsync(CancellationToken ct)
    {
        if (IsLoaded) return;

        try
        {
            IsBusy = true;
            if (ViewModel != null && !DesignMode && ViewModel.RefreshDataCommand.CanExecute(null))
            {
                await ViewModel.RefreshDataCommand.ExecuteAsync(null);
            }
            IsLoaded = true;
            _logger?.LogDebug("RecommendedMonthlyChargePanel loaded successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("RecommendedMonthlyChargePanel load cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load RecommendedMonthlyChargePanel");
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Saves the panel asynchronously, persisting charge modifications.
    /// </summary>
    public override async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            if (ViewModel != null && ViewModel.SaveCurrentChargesCommand.CanExecute(null))
            {
                await ViewModel.SaveCurrentChargesCommand.ExecuteAsync(null);
                SetHasUnsavedChanges(false);
            }
            _logger?.LogDebug("RecommendedMonthlyChargePanel saved successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("RecommendedMonthlyChargePanel save cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save RecommendedMonthlyChargePanel");
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Validates the panel asynchronously. Ensures grid data is valid and consistent.
    /// </summary>
    public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            var errors = new List<ValidationItem>();

            if (ViewModel == null)
            {
                errors.Add(new ValidationItem("ViewModel", "ViewModel not initialized", ValidationSeverity.Error));
            }
            else
            {
                // Check departments grid has data
                if (!ViewModel.Departments.Any())
                {
                    errors.Add(new ValidationItem("Data", "No departments available for recommendations", ValidationSeverity.Warning));
                }

                // Validate all charges are non-negative
                foreach (var dept in ViewModel.Departments)
                {
                    if (dept.CurrentCharge < 0)
                    {
                        errors.Add(new ValidationItem("CurrentCharge", $"Charge for {dept.Department} cannot be negative", ValidationSeverity.Error));
                    }
                }
            }

            await Task.CompletedTask;
            return errors.Count == 0 ? ValidationResult.Success : ValidationResult.Failed(errors.ToArray());
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("RecommendedMonthlyChargePanel validation cancelled");
            return ValidationResult.Failed(new ValidationItem("Cancelled", "Validation was cancelled", ValidationSeverity.Info));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Validation error in RecommendedMonthlyChargePanel");
            return ValidationResult.Failed(new ValidationItem("Validation", ex.Message, ValidationSeverity.Error));
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Focuses the first validation error control.
    /// </summary>
    public override void FocusFirstError()
    {
        if (_departmentsGrid?.Visible == true)
            _departmentsGrid.Focus();
        else
            _summaryPanel?.Focus();
    }

    #endregion

    /// <summary>
    /// Called when the ViewModel is resolved from the scoped provider.
    /// </summary>
    protected override void OnViewModelResolved(object? viewModel)
    {
        base.OnViewModelResolved(viewModel);
        if (viewModel is not RecommendedMonthlyChargeViewModel)
        {
            return;
        }

        if (_mainSplitContainer == null)
        {
            SafeSuspendAndLayout(InitializeControls);
        }

        BindViewModel();
        ApplyDiagnosticsFallbackContentIfNeeded();
        ApplyCurrentTheme();
    }

    private void InitializeControls()
    {
        // Apply Syncfusion theme via SfSkinManager (single source of truth)
        SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        Name = "RecommendedMonthlyChargePanel";
        Size = ScaleLogicalToDevice(new Size(1400, 900));
        MinimumSize = ScaleLogicalToDevice(new Size(1024, 720));
        Dock = DockStyle.Fill;

        // Error provider
        _errorProvider = ControlFactory.CreateErrorProvider(errorProvider =>
        {
            errorProvider.BlinkStyle = ErrorBlinkStyle.NeverBlink;
            errorProvider.BlinkRate = 0;
        });

        // ============================================================================
        // Panel Header
        // ============================================================================
        _panelHeader = ControlFactory.CreatePanelHeader(header =>
        {
            header.Dock = DockStyle.Top;
            header.Title = "Recommended Monthly Charges";
            header.Height = LayoutTokens.GetScaled(LayoutTokens.HeaderHeightLarge);
        });
        // Store handlers for cleanup in Dispose (Pattern A)
        _panelHeaderRefreshClickedHandler = async (s, e) => await RefreshDataAsync();
        _panelHeaderHelpClickedHandler = (s, e) => Dialogs.ChartWizardFaqDialog.ShowModal(this);
        _panelHeaderCloseClickedHandler = (s, e) => ClosePanel();
        _panelHeader.RefreshClicked += _panelHeaderRefreshClickedHandler;
        _panelHeader.HelpClicked += _panelHeaderHelpClickedHandler;
        _panelHeader.CloseClicked += _panelHeaderCloseClickedHandler;
        Controls.Add(_panelHeader);

        // ============================================================================
        // Button Panel - Top Action Buttons (Dock-based, responsive)
        // ============================================================================
        var actionRowHeight = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge + (AppLayoutConstants.PanelPadding * 2));
        _buttonPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = actionRowHeight,
            MinimumSize = new Size(0, actionRowHeight),
            Padding = new Padding(AppLayoutConstants.PanelPadding, 0, AppLayoutConstants.PanelPadding, 0),
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(_buttonPanel, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        var buttonFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = false,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };

        _refreshButton = ControlFactory.CreateSfButton("&Refresh Data", button =>
        {
            button.AutoSize = false;
            button.Size = LayoutTokens.GetScaled(new Size(152, LayoutTokens.StandardControlHeightLarge));
            button.MinimumSize = LayoutTokens.GetScaled(new Size(152, LayoutTokens.StandardControlHeightLarge));
            button.Margin = new Padding(0, 0, LayoutTokens.GetScaled(AppLayoutConstants.ButtonSpacing), 0);
            button.TabIndex = 1;
            button.AccessibleName = "Refresh Data";
            button.AccessibleDescription = "Refresh department expense data from QuickBooks";
        });
        _refreshButtonClickHandler = async (s, e) => await RefreshDataAsync();
        _refreshButton.Click += _refreshButtonClickHandler;
        var refreshTooltip = ControlFactory.CreateToolTip();
        refreshTooltip.SetToolTip(_refreshButton, "Load latest expense data from QuickBooks (Alt+R)");
        buttonFlow.Controls.Add(_refreshButton);

        _queryGrokButton = ControlFactory.CreateSfButton("Query &AI", button =>
        {
            button.AutoSize = false;
            button.Size = LayoutTokens.GetScaled(new Size(152, LayoutTokens.StandardControlHeightLarge));
            button.MinimumSize = LayoutTokens.GetScaled(new Size(152, LayoutTokens.StandardControlHeightLarge));
            button.Margin = new Padding(0, 0, LayoutTokens.GetScaled(AppLayoutConstants.ButtonSpacing), 0);
            button.TabIndex = 2;
            button.AccessibleName = "Query AI";
            button.AccessibleDescription = "Get AI-driven rate recommendations from Grok";
        });
        _queryGrokButtonClickHandler = async (s, e) => await QueryGrokAsync();
        _queryGrokButton.Click += _queryGrokButtonClickHandler;
        var grokTooltip = ControlFactory.CreateToolTip();
        grokTooltip.SetToolTip(_queryGrokButton, "Query Grok AI for recommended adjustment factors (Alt+A)");
        buttonFlow.Controls.Add(_queryGrokButton);

        _saveButton = ControlFactory.CreateSfButton("&Save Changes", button =>
        {
            button.AutoSize = false;
            button.Size = LayoutTokens.GetScaled(new Size(152, LayoutTokens.StandardControlHeightLarge));
            button.MinimumSize = LayoutTokens.GetScaled(new Size(152, LayoutTokens.StandardControlHeightLarge));
            button.Margin = Padding.Empty;
            button.TabIndex = 3;
            button.AccessibleName = "Save Changes";
            button.AccessibleDescription = "Save current charge modifications to database";
        });
        _saveButtonClickHandler = async (s, e) => await SaveAsync(RegisterOperation());
        _saveButton.Click += _saveButtonClickHandler;
        var saveTooltip = ControlFactory.CreateToolTip();
        saveTooltip.SetToolTip(_saveButton, "Save modified charges to database (Alt+S)");
        buttonFlow.Controls.Add(_saveButton);

        _buttonPanel.Controls.Add(buttonFlow);
        Controls.Add(_buttonPanel);

        // ============================================================================
        // Summary Panel - Revenue, Expenses, Status Display (Dock-based, responsive)
        // ============================================================================
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = LayoutTokens.GetScaled(LayoutTokens.SummaryPanelHeight * 2),
            Padding = new Padding(AppLayoutConstants.SummaryPanelPadding),
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(_summaryPanel, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        var summaryTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            AutoSize = false,
            Padding = LayoutTokens.GetScaled(LayoutTokens.ContentInnerPadding)
        };
        summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 3; i++)
            summaryTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var summaryTitleLabel = new Label
        {
            Text = "Financial Summary",
            Dock = DockStyle.Top,
            Height = 25,
            TabIndex = 4,
            Margin = new Padding(0, 0, 0, 10)
        };
        _summaryPanel.Controls.Add(summaryTitleLabel);

        _totalRevenueLabel = new Label
        {
            Text = "Current Revenue: $0.00",
            Dock = DockStyle.Fill,
            TabIndex = 5,
            AccessibleName = "Total Current Revenue",
            AccessibleDescription = "Total monthly revenue from all departments at current rates",
            AutoSize = false
        };
        summaryTable.Controls.Add(_totalRevenueLabel, 0, 0);

        _suggestedRevenueLabel = new Label
        {
            Text = "Suggested Revenue: $0.00",
            Dock = DockStyle.Fill,
            TabIndex = 6,
            AccessibleName = "Total Suggested Revenue",
            AccessibleDescription = "Total monthly revenue at AI-suggested rates",
            AutoSize = false
        };
        summaryTable.Controls.Add(_suggestedRevenueLabel, 1, 0);

        _totalExpensesLabel = new Label
        {
            Text = "Total Expenses: $0.00",
            Dock = DockStyle.Fill,
            TabIndex = 7,
            AccessibleName = "Total Monthly Expenses",
            AccessibleDescription = "Total monthly expenses across all departments",
            AutoSize = false
        };
        summaryTable.Controls.Add(_totalExpensesLabel, 0, 1);

        _overallStatusLabel = new Label
        {
            Text = "Status: Unknown",
            Dock = DockStyle.Fill,
            TabIndex = 8,
            AccessibleName = "Overall Profitability Status",
            AccessibleDescription = "Overall profitability status: Losing Money, Breaking Even, or Profitable",
            AutoSize = false
        };
        summaryTable.Controls.Add(_overallStatusLabel, 1, 1);

        _explanationTextBox = ControlFactory.CreateTextBoxExt(textBox =>
        {
            textBox.Multiline = true;
            textBox.ReadOnly = true;
            textBox.Dock = DockStyle.Fill;
            textBox.ScrollBars = ScrollBars.Vertical;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.TabIndex = 9;
            textBox.AccessibleName = "AI Recommendation Explanation";
            textBox.AccessibleDescription = "AI-generated explanation of the recommended rate adjustments";
            textBox.AutoSize = false;
        });
        var explanationTooltip = ControlFactory.CreateToolTip();
        explanationTooltip.SetToolTip(_explanationTextBox, "AI-generated explanation for the recommended adjustments");
        summaryTable.Controls.Add(_explanationTextBox, 0, 2);
        summaryTable.SetColumnSpan(_explanationTextBox, 2);

        _summaryPanel.Controls.Add(summaryTable);
        Controls.Add(_summaryPanel);

        // ============================================================================
        // Main Split Container - Left (Grids) | Right (Chart)
        // ============================================================================
        _mainSplitContainer = ControlFactory.CreateSplitContainerAdv(splitter =>
        {
            splitter.Dock = DockStyle.Fill;
            splitter.Orientation = Orientation.Vertical;
            splitter.BorderStyle = BorderStyle.FixedSingle;
        });
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(_mainSplitContainer, 720, 360, 660);
        SafeSplitterDistanceHelper.SetupProportionalResizing(_mainSplitContainer, 0.60d);

        // ============================================================================
        // Left Split Container - Top (Departments) | Bottom (Benchmarks)
        // ============================================================================
        _leftSplitContainer = ControlFactory.CreateSplitContainerAdv(splitter =>
        {
            splitter.Dock = DockStyle.Fill;
            splitter.Orientation = Orientation.Horizontal;
            splitter.BorderStyle = BorderStyle.None;
        });
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(_leftSplitContainer, 220, 180, 350);
        SafeSplitterDistanceHelper.SetupProportionalResizing(_leftSplitContainer, 0.58d);

        // ============================================================================
        // Departments Grid (Top Left)
        // ============================================================================
        var deptGridPanel = new Panel
        {
            Dock = DockStyle.Fill,
            MinimumSize = LayoutTokens.GetScaled(new Size(0, 220)),
            Padding = LayoutTokens.GetScaled(LayoutTokens.PanelPaddingTight),
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(deptGridPanel, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        var deptGridLabel = new Label
        {
            Text = "Department Rate Analysis",
            Dock = DockStyle.Top,
            Height = 25,
            Padding = new Padding(5, 3, 0, 0)
        };
        deptGridPanel.Controls.Add(deptGridLabel);

        _departmentsGrid = ControlFactory.CreateSfDataGrid(grid =>
        {
            grid.Dock = DockStyle.Fill;
            grid.AllowEditing = true;
            grid.AllowResizingColumns = true;
            grid.AllowSorting = true;
            grid.AllowFiltering = false;
            grid.AutoGenerateColumns = false;
            grid.AutoSizeColumnsMode = AutoSizeColumnsMode.Fill;
            grid.ShowGroupDropArea = false;
            grid.RowHeight = LayoutTokens.GetScaled(34);
            grid.HeaderRowHeight = LayoutTokens.GetScaled(42);
            grid.SelectionMode = GridSelectionMode.Single;
            grid.EditMode = EditMode.SingleClick;
            grid.TabIndex = 10;
            grid.AccessibleName = "Department Rates Grid";
            grid.AccessibleDescription = "Editable grid showing monthly charges, expenses, and recommendations per department";
        }).PreventStringRelationalFilters(Logger, "Department");

        // Configure department grid columns
        _departmentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Department",
            HeaderText = "Department",
            Width = 112,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "CustomerCount",
            HeaderText = "Customers",
            Width = 84,
            AllowEditing = false,
            Format = "N0"
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "MonthlyExpenses",
            HeaderText = "Monthly Expenses",
            Format = "C2",
            Width = 132,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "CurrentCharge",
            HeaderText = "Current Charge",
            Format = "C2",
            Width = 112,
            AllowEditing = true
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "SuggestedCharge",
            HeaderText = "Suggested",
            Format = "C2",
            Width = 104,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "MonthlyGainLoss",
            HeaderText = "Gain/Loss",
            Format = "C2",
            Width = 112,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "PositionStatus",
            HeaderText = "Status",
            Width = 100,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "StateAverage",
            HeaderText = "State Avg",
            Format = "C2",
            Width = 96,
            AllowEditing = false
        });

        SetMinimumColumnWidth(_departmentsGrid, "MonthlyExpenses", 120);
        SetMinimumColumnWidth(_departmentsGrid, "CurrentCharge", 120);
        SetMinimumColumnWidth(_departmentsGrid, "SuggestedCharge", 120);
        SetMinimumColumnWidth(_departmentsGrid, "MonthlyGainLoss", 120);

        // Mark grid as dirty on edit for unsaved changes tracking
        _departmentsGrid.CurrentCellEndEdit += (s, e) => SetHasUnsavedChanges(true);

        var gridTooltip = ControlFactory.CreateToolTip();
        gridTooltip.SetToolTip(_departmentsGrid, "Edit Current Charge column to set new rates. Other columns are calculated automatically.");

        deptGridPanel.Controls.Add(_departmentsGrid);
        _leftSplitContainer.Panel1.Controls.Add(deptGridPanel);

        // ============================================================================
        // Benchmarks Grid (Bottom Left)
        // ============================================================================
        var benchmarkPanel = new Panel
        {
            Dock = DockStyle.Fill,
            MinimumSize = LayoutTokens.GetScaled(new Size(0, 180)),
            Padding = LayoutTokens.GetScaled(LayoutTokens.PanelPaddingTight),
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(benchmarkPanel, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        var benchmarkLabel = new Label
        {
            Text = "Benchmarks",
            Dock = DockStyle.Top,
            Height = LayoutTokens.GetScaled(40),
            Padding = new Padding(LayoutTokens.GetScaled(6), LayoutTokens.GetScaled(6), 0, 0),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _benchmarksGrid = ControlFactory.CreateSfDataGrid(grid =>
        {
            grid.Dock = DockStyle.Fill;
            grid.AllowEditing = false;
            grid.AllowResizingColumns = true;
            grid.AllowSorting = true;
            grid.AllowFiltering = false;
            grid.AutoGenerateColumns = false;
            grid.AutoSizeColumnsMode = AutoSizeColumnsMode.Fill;
            grid.ShowGroupDropArea = false;
            grid.RowHeight = LayoutTokens.GetScaled(34);
            grid.HeaderRowHeight = LayoutTokens.GetScaled(42);
            grid.SelectionMode = GridSelectionMode.Single;
            grid.TabIndex = 11;
            grid.AccessibleName = "Benchmarks Grid";
            grid.AccessibleDescription = "State and national benchmark data for comparison";
        }).PreventStringRelationalFilters(Logger, "Department");

        // Configure benchmark grid columns
        _benchmarksGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Department",
            HeaderText = "Department",
            Width = 110,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "StateAverage",
            HeaderText = "State Avg",
            Format = "C2",
            Width = 100,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "TownSizeAverage",
            HeaderText = "Town Size Avg",
            Format = "C2",
            Width = 112,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "NationalAverage",
            HeaderText = "National Avg",
            Format = "C2",
            Width = 100,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "PopulationRange",
            HeaderText = "Pop Range",
            Width = 92,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Source",
            HeaderText = "Source",
            Width = 180,
            AllowEditing = false
        });

        var benchmarkTooltip = ControlFactory.CreateToolTip();
        benchmarkTooltip.SetToolTip(_benchmarksGrid, "Reference data from state and national utility surveys");

        benchmarkPanel.Controls.Add(_benchmarksGrid);
        benchmarkPanel.Controls.Add(benchmarkLabel);
        benchmarkLabel.BringToFront();
        _leftSplitContainer.Panel2.Controls.Add(benchmarkPanel);

        _mainSplitContainer.Panel1.Controls.Add(_leftSplitContainer);

        // ============================================================================
        // Chart Panel (Right Side)
        // ============================================================================
        _chartPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = LayoutTokens.GetScaled(LayoutTokens.PanelPaddingTight),
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(_chartPanel, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        _chartControl = ControlFactory.CreateChartControl("Expenses vs Current vs Suggested Charges", chart =>
        {
            chart.Dock = DockStyle.Fill;
            chart.MinimumSize = LayoutTokens.GetScaled(new Size(420, 320));
            chart.TabIndex = 12;
            chart.AccessibleName = "Department Expense Chart";
            chart.AccessibleDescription = "Visual comparison of department expenses, current charges, and suggested charges";
        });
        _chartRegionEventWiring = new ChartControlRegionEventWiring(_chartControl);

        ChartControlDefaults.Apply(_chartControl, logger: Logger);

        // Configure chart appearance
        _chartControl.Title.Text = "Expenses vs Current vs Suggested Charges";
        _chartControl.Legend.Visible = true;
        _chartControl.Legend.Position = ChartDock.Top;
        _chartControl.PrimaryXAxis.Title = "Departments";
        _chartControl.PrimaryYAxis.Title = "Amount ($)";
        _chartControl.PrimaryYAxis.RangeType = ChartAxisRangeType.Auto;

        _chartPanel.MinimumSize = LayoutTokens.GetScaled(new Size(420, 320));
        _chartPanel.Controls.Add(_chartControl);
        _mainSplitContainer.Panel2.Controls.Add(_chartPanel);

        Controls.Add(_mainSplitContainer);

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            AutoSize = false,
            Name = "RecommendedMonthlyChargeRootLayout"
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, _panelHeader.Height));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, _buttonPanel.Height));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, _summaryPanel.Height));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        Controls.Remove(_panelHeader);
        Controls.Remove(_buttonPanel);
        Controls.Remove(_summaryPanel);
        Controls.Remove(_mainSplitContainer);

        rootLayout.Controls.Add(_panelHeader, 0, 0);
        rootLayout.Controls.Add(_buttonPanel, 0, 1);
        rootLayout.Controls.Add(_summaryPanel, 0, 2);
        rootLayout.Controls.Add(_mainSplitContainer, 0, 3);
        Controls.Add(rootLayout);

        // ============================================================================
        // Status Strip - Bottom Status Bar
        // ============================================================================
        _statusStrip = ControlFactory.CreateStatusStrip(statusStrip =>
        {
            statusStrip.Dock = DockStyle.Bottom;
        });
        _statusLabel = ControlFactory.CreateToolStripStatusLabel(statusLabel =>
        {
            statusLabel.Text = "Ready";
            statusLabel.Spring = true;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        });
        _statusStrip.Items.Add(_statusLabel);
        Controls.Add(_statusStrip);

        // ============================================================================
        // Overlays - Loading and No Data
        // ============================================================================
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading charge data...",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        _noDataOverlay = ControlFactory.CreateNoDataOverlay(overlay =>
        {
            overlay.Message = "No charge data available\r\nLoad departments to generate charge recommendations";
            overlay.Dock = DockStyle.Fill;
            overlay.Visible = false;
        });
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();

        ApplyProfessionalPanelLayout();
        ApplyDiagnosticsFallbackContentIfNeeded();

        this.PerformLayout();
        this.Refresh();

        Logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
    }

    private void BindViewModel()
    {
        if (ViewModel == null) return;

        try
        {
            // Bind departments collection to grids
            if (_departmentsGrid != null)
                _departmentsGrid.DataSource = ViewModel.Departments;

            if (_benchmarksGrid != null)
                _benchmarksGrid.DataSource = ViewModel.Benchmarks;

            // Subscribe to property changes (single consolidated handler)
            _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += _viewModelPropertyChangedHandler;

            // Subscribe to collection changes for live updates
            ViewModel.Departments.CollectionChanged += (s, e) =>
            {
                if (InvokeRequired)
                    BeginInvoke(new System.Action(() => { UpdateChart(); UpdateNoDataOverlay(); }));
                else
                { UpdateChart(); UpdateNoDataOverlay(); }
            };

            ViewModel.Benchmarks.CollectionChanged += (s, e) =>
            {
                if (InvokeRequired)
                    BeginInvoke(new System.Action(() => _benchmarksGrid?.Refresh()));
                else
                    _benchmarksGrid?.Refresh();
            };

            Logger.LogDebug("ViewModel bound to RecommendedMonthlyChargePanel");
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to bind ViewModel to RecommendedMonthlyChargePanel");
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            BeginInvoke(new System.Action(() => ViewModel_PropertyChanged(sender, e)));
            return;
        }

        if (ViewModel == null) return;

        switch (e.PropertyName)
        {
            case nameof(ViewModel.TotalCurrentRevenue):
                if (_totalRevenueLabel != null)
                    _totalRevenueLabel.Text = $"Current Revenue: {ViewModel.TotalCurrentRevenue:C2}/month";
                break;

            case nameof(ViewModel.TotalSuggestedRevenue):
                if (_suggestedRevenueLabel != null)
                {
                    var increase = ViewModel.TotalSuggestedRevenue - ViewModel.TotalCurrentRevenue;
                    var increasePercent = ViewModel.TotalCurrentRevenue > 0
                        ? (increase / ViewModel.TotalCurrentRevenue) * 100m
                        : 0m;
                    _suggestedRevenueLabel.Text = $"Suggested Revenue: {ViewModel.TotalSuggestedRevenue:C2}/month ({increasePercent:+0.0;-0.0;0}%)";
                }
                break;

            case nameof(ViewModel.TotalMonthlyExpenses):
                if (_totalExpensesLabel != null)
                    _totalExpensesLabel.Text = $"Total Expenses: {ViewModel.TotalMonthlyExpenses:C2}/month";
                break;

            case nameof(ViewModel.OverallStatus):
                if (_overallStatusLabel != null)
                {
                    _overallStatusLabel.Text = $"Status: {ViewModel.OverallStatus}";
                    // Use semantic status colors (approved exception to SfSkinManager)
                    if (string.Equals(ViewModel.OverallStatusColor, "Red", StringComparison.OrdinalIgnoreCase))
                    {
                        _overallStatusLabel.ForeColor = Color.Red;
                    }
                    else if (string.Equals(ViewModel.OverallStatusColor, "Orange", StringComparison.OrdinalIgnoreCase))
                    {
                        _overallStatusLabel.ForeColor = Color.Orange;
                    }
                    else if (string.Equals(ViewModel.OverallStatusColor, "Green", StringComparison.OrdinalIgnoreCase))
                    {
                        _overallStatusLabel.ForeColor = Color.Green;
                    }
                    else
                    {
                        _overallStatusLabel.ForeColor = Color.Orange;
                    }
                }
                break;

            case nameof(ViewModel.IsLoading):
                EnableControls(!ViewModel.IsLoading);
                if (_loadingOverlay != null)
                {
                    _loadingOverlay.Visible = ViewModel.IsLoading;
                    if (ViewModel.IsLoading)
                        _loadingOverlay.BringToFront();
                }
                UpdateNoDataOverlay();
                break;

            case nameof(ViewModel.Departments):
                UpdateChart();
                if (_departmentsGrid != null)
                    _departmentsGrid.SafeInvoke(() => _departmentsGrid.Refresh());
                UpdateNoDataOverlay();
                break;

            case nameof(ViewModel.Benchmarks):
                if (_benchmarksGrid != null)
                    _benchmarksGrid.SafeInvoke(() => _benchmarksGrid.Refresh());
                break;

            case nameof(ViewModel.RecommendationExplanation):
                if (_explanationTextBox != null)
                    _explanationTextBox.Text = ViewModel.RecommendationExplanation ?? "Click 'Query AI' to get AI-powered rate recommendations.";
                break;

            case nameof(ViewModel.StatusText):
                UpdateStatus(ViewModel.StatusText);
                break;

            case nameof(ViewModel.ErrorMessage):
                if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
                {
                    UpdateStatus($"Error: {ViewModel.ErrorMessage}");
                }
                break;
        }
    }

    private void UpdateNoDataOverlay()
    {
        if (_noDataOverlay == null) return;

        if (LayoutDiagnosticsMode.IsActive)
        {
            ApplyDiagnosticsFallbackContentIfNeeded();

            if (GetActiveDepartments().Any())
            {
                _noDataOverlay.SafeInvoke(() => _noDataOverlay.Visible = false);
                return;
            }
        }

        if (ViewModel == null) return;
        var hasData = ViewModel.Departments.Any();
        if (!_noDataOverlay.IsDisposed)
            _noDataOverlay.SafeInvoke(() => _noDataOverlay.Visible = !hasData && !ViewModel.IsLoading);
    }

    private static void SetMinimumColumnWidth(SfDataGrid grid, string mappingName, int minLogical)
    {
        foreach (var column in grid.Columns)
        {
            if (column.MappingName == mappingName)
            {
                column.MinimumWidth = LayoutTokens.GetScaled(minLogical);
                break;
            }
        }
    }

    private void UpdateChart()
    {
        var departments = GetActiveDepartments();
        if (_chartControl == null || !departments.Any())
        {
            if (_chartControl != null)
            {
                _chartControl.Series.Clear();
                _chartControl.Refresh();
            }
            return;
        }

        if (_chartControl.InvokeRequired)
        {
            _chartControl.SafeInvoke(() => UpdateChart());
            return;
        }

        try
        {
            Logger.LogDebug("Updating department expense chart with {Count} departments", departments.Count);

            // Clear existing series
            _chartControl.Series.Clear();

            // Create series for monthly expenses, current charges revenue, and suggested charges revenue
            var expenseSeries = new ChartSeries("Monthly Expenses", ChartSeriesType.Column);
            var currentRevenueSeries = new ChartSeries("Current Charge", ChartSeriesType.Column);
            var suggestedRevenueSeries = new ChartSeries("Suggested Charge", ChartSeriesType.Column);

            // Add data points for each department
            foreach (var department in departments.OrderBy(d => d.Department))
            {
                var departmentName = department.Department ?? $"Dept {department.CustomerCount}";
                var monthlyExpenses = (double)department.MonthlyExpenses;
                var currentCharge = (double)department.CurrentCharge;
                var suggestedCharge = (double)department.SuggestedCharge;

                expenseSeries.Points.Add(departmentName, monthlyExpenses);
                currentRevenueSeries.Points.Add(departmentName, currentCharge);
                suggestedRevenueSeries.Points.Add(departmentName, suggestedCharge);
            }

            // Add series to chart
            _chartControl.Series.Add(expenseSeries);
            _chartControl.Series.Add(currentRevenueSeries);
            _chartControl.Series.Add(suggestedRevenueSeries);

            // Configure chart appearance
            _chartControl.Title.Text = "Department Expenses vs Current vs Suggested Charges";
            _chartControl.Legend.Visible = true;
            _chartControl.Legend.Position = ChartDock.Top;
            _chartControl.PrimaryXAxis.Title = "Departments";
            _chartControl.PrimaryXAxis.LabelRotateAngle = 45;
            _chartControl.PrimaryYAxis.Title = "Amount ($)";
            _chartControl.PrimaryYAxis.RangeType = ChartAxisRangeType.Auto;
            _chartControl.ShowToolTips = true;

            _chartControl.Refresh();
            Logger.LogDebug("Chart updated successfully with {DepartmentCount} departments", departments.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating chart visualization");
            if (_chartControl != null)
            {
                _chartControl.Series.Clear();
                _chartControl.Title.Text = "Error Loading Chart Data";
                _chartControl.Refresh();
            }
        }
    }

    private async Task RefreshDataAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel == null) return;

        try
        {
            var token = RegisterOperation();
            IsBusy = true;
            UpdateStatus("Loading department expense data...");

            if (ViewModel.RefreshDataCommand.CanExecute(null))
            {
                await ViewModel.RefreshDataCommand.ExecuteAsync(null);
                UpdateChart();
                UpdateStatus("Data refreshed successfully");
            }
        }
        catch (OperationCanceledException)
        {
            Logger?.LogDebug("Data refresh cancelled");
            UpdateStatus("Refresh cancelled");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error refreshing data");
            UpdateStatus($"Error: {ex.Message}");
            MessageBox.Show($"Error refreshing data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { IsBusy = false; }
    }

    private async Task QueryGrokAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel == null) return;

        try
        {
            var token = RegisterOperation();
            IsBusy = true;
            UpdateStatus("Querying Grok AI for rate recommendations...");

            if (ViewModel.QueryGrokCommand.CanExecute(null))
            {
                await ViewModel.QueryGrokCommand.ExecuteAsync(null);
                UpdateChart();
                SetHasUnsavedChanges(true);
                UpdateStatus("AI recommendations applied successfully");
            }
        }
        catch (OperationCanceledException)
        {
            Logger?.LogDebug("AI query cancelled");
            UpdateStatus("AI query cancelled");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error querying Grok AI");
            UpdateStatus($"AI query failed: {ex.Message}");
            MessageBox.Show($"Error querying AI: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { IsBusy = false; }
    }

    private void UpdateStatus(string message)
    {
        this.InvokeIfRequired(() =>
        {
            try
            {
                if (_statusLabel != null && !_statusLabel.IsDisposed)
                    _statusLabel.Text = message ?? "Ready";
            }
            catch { }
        });
    }

    private void EnableControls(bool enabled)
    {
        if (_refreshButton != null) _refreshButton.Enabled = enabled;
        if (_queryGrokButton != null) _queryGrokButton.Enabled = enabled;
        if (_saveButton != null) _saveButton.Enabled = enabled;
        if (_departmentsGrid != null) _departmentsGrid.Enabled = enabled;
        if (_benchmarksGrid != null) _benchmarksGrid.Enabled = enabled;
    }

    private IReadOnlyList<DepartmentRateModel> GetActiveDepartments()
    {
        if (ViewModel?.Departments.Any() == true)
        {
            return ViewModel.Departments;
        }

        return _diagnosticsFallbackDepartments ?? Array.Empty<DepartmentRateModel>();
    }

    private void ApplyDiagnosticsFallbackContentIfNeeded()
    {
        if (!LayoutDiagnosticsMode.IsActive || _departmentsGrid == null || _benchmarksGrid == null)
        {
            return;
        }

        if (ViewModel?.Departments.Any() == true)
        {
            _diagnosticsFallbackDepartments = null;
            _diagnosticsFallbackBenchmarks = null;
            return;
        }

        _diagnosticsFallbackDepartments ??= CreateDiagnosticsFallbackDepartments();
        _diagnosticsFallbackBenchmarks ??= CreateDiagnosticsFallbackBenchmarks();

        var departments = _diagnosticsFallbackDepartments;
        var benchmarks = _diagnosticsFallbackBenchmarks;
        var totalExpenses = departments.Sum(department => department.MonthlyExpenses);
        var currentRevenue = departments.Sum(department => department.CurrentCharge * department.CustomerCount);
        var suggestedRevenue = departments.Sum(department => department.SuggestedCharge * department.CustomerCount);

        if (ViewModel != null)
        {
            ViewModel.Departments.Clear();
            foreach (var department in departments)
            {
                ViewModel.Departments.Add(department);
            }

            ViewModel.Benchmarks.Clear();
            foreach (var benchmark in benchmarks)
            {
                ViewModel.Benchmarks.Add(benchmark);
            }

            ViewModel.TotalMonthlyExpenses = totalExpenses;
            ViewModel.TotalCurrentRevenue = currentRevenue;
            ViewModel.TotalSuggestedRevenue = suggestedRevenue;
            ViewModel.OverallStatus = "Rate study ready";
            ViewModel.OverallStatusColor = "Orange";
            ViewModel.RecommendationExplanation = "Diagnostics sample data is active so the panel can be scored against a stable departmental rate study layout.";
            ViewModel.StatusText = "Diagnostics sample rate study active";
            _departmentsGrid.DataSource = ViewModel.Departments;
            _benchmarksGrid.DataSource = ViewModel.Benchmarks;
        }
        else
        {
            _departmentsGrid.DataSource = departments.ToList();
            _benchmarksGrid.DataSource = benchmarks.ToList();
        }

        if (_totalRevenueLabel != null)
        {
            _totalRevenueLabel.Text = $"Current Revenue: {currentRevenue:C2}";
        }

        if (_suggestedRevenueLabel != null)
        {
            _suggestedRevenueLabel.Text = $"Suggested Revenue: {suggestedRevenue:C2}";
        }

        if (_totalExpensesLabel != null)
        {
            _totalExpensesLabel.Text = $"Total Expenses: {totalExpenses:C2}";
        }

        if (_overallStatusLabel != null)
        {
            _overallStatusLabel.Text = "Status: Rate study ready";
        }

        if (_explanationTextBox != null)
        {
            _explanationTextBox.Text = "Diagnostics sample data is active so layout scoring uses a stable departmental rate study instead of an empty shell.";
        }

        if (_noDataOverlay != null)
        {
            _noDataOverlay.Visible = false;
        }

        UpdateChart();
        UpdateStatus("Diagnostics sample rate study active");
    }

    private static List<DepartmentRateModel> CreateDiagnosticsFallbackDepartments()
    {
        return new List<DepartmentRateModel>
        {
            new()
            {
                Department = "Water",
                CustomerCount = 3200,
                MonthlyExpenses = 148000m,
                CurrentCharge = 55.00m,
                SuggestedCharge = 58.50m,
                MonthlyGainLoss = 3.50m,
                PositionStatus = "Profitable",
                PositionColor = "Green",
                AiAdjustmentFactor = 1.08m,
                StateAverage = 56.00m,
            },
            new()
            {
                Department = "Sewer",
                CustomerCount = 3200,
                MonthlyExpenses = 201600m,
                CurrentCharge = 73.00m,
                SuggestedCharge = 78.00m,
                MonthlyGainLoss = -2.00m,
                PositionStatus = "Losing Money",
                PositionColor = "Red",
                AiAdjustmentFactor = 1.10m,
                StateAverage = 75.00m,
            },
            new()
            {
                Department = "Trash",
                CustomerCount = 2800,
                MonthlyExpenses = 81200m,
                CurrentCharge = 30.00m,
                SuggestedCharge = 33.00m,
                MonthlyGainLoss = 1.15m,
                PositionStatus = "Breaking Even",
                PositionColor = "Orange",
                AiAdjustmentFactor = 1.06m,
                StateAverage = 32.00m,
            },
            new()
            {
                Department = "Apartments",
                CustomerCount = 280,
                MonthlyExpenses = 29400m,
                CurrentCharge = 118.00m,
                SuggestedCharge = 126.00m,
                MonthlyGainLoss = 4.00m,
                PositionStatus = "Profitable",
                PositionColor = "Green",
                AiAdjustmentFactor = 1.07m,
                StateAverage = 135.00m,
            },
        };
    }

    private static List<StateBenchmarkModel> CreateDiagnosticsFallbackBenchmarks()
    {
        return new List<StateBenchmarkModel>
        {
            new()
            {
                Department = "Water",
                StateAverage = 56.00m,
                TownSizeAverage = 53.50m,
                NationalAverage = 51.00m,
                Source = "AWWA / EPA WaterSense 2024",
                Year = 2024,
                PopulationRange = "5,000-10,000",
            },
            new()
            {
                Department = "Sewer",
                StateAverage = 75.00m,
                TownSizeAverage = 72.00m,
                NationalAverage = 70.00m,
                Source = "Bluefield Research / Move.org",
                Year = 2024,
                PopulationRange = "5,000-10,000",
            },
            new()
            {
                Department = "Trash",
                StateAverage = 32.00m,
                TownSizeAverage = 30.00m,
                NationalAverage = 30.00m,
                Source = "Local utility survey blend",
                Year = 2024,
                PopulationRange = "5,000-10,000",
            },
            new()
            {
                Department = "Apartments",
                StateAverage = 135.00m,
                TownSizeAverage = 128.00m,
                NationalAverage = 121.00m,
                Source = "Multifamily Housing Council",
                Year = 2024,
                PopulationRange = "5,000-10,000",
            },
        };
    }

    private void ApplyCurrentTheme()
    {
        try
        {
            var theme = ThemeColors.ValidateTheme(SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme, Logger);
            ThemeColors.EnsureThemeAssemblyLoadedForTheme(theme, Logger);
            SfSkinManager.SetVisualStyle(this, theme);
            Logger?.LogDebug("[THEME] Applied panel theme {Theme} to {Panel}", theme, Name);
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to apply theme");
        }
    }

    protected override void OnPanelLoaded(EventArgs e)
    {
        base.OnPanelLoaded(e);
        if (!DesignMode)
        {
            BeginInvoke(new MethodInvoker(() =>
            {
                ApplyProfessionalPanelLayout();
                ForceFullLayout();
            }));
        }
    }

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from ViewModel events
            if (ViewModel != null && _viewModelPropertyChangedHandler != null)
            {
                ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            }

            // Unsubscribe from button click handlers
            if (_refreshButton != null && _refreshButtonClickHandler != null)
                _refreshButton.Click -= _refreshButtonClickHandler;

            if (_queryGrokButton != null && _queryGrokButtonClickHandler != null)
                _queryGrokButton.Click -= _queryGrokButtonClickHandler;

            if (_saveButton != null && _saveButtonClickHandler != null)
                _saveButton.Click -= _saveButtonClickHandler;

            // Unsubscribe from panel header events (Pattern K)
            if (_panelHeader != null && _panelHeaderRefreshClickedHandler != null)
                _panelHeader.RefreshClicked -= _panelHeaderRefreshClickedHandler;
            if (_panelHeader != null && _panelHeaderHelpClickedHandler != null)
                _panelHeader.HelpClicked -= _panelHeaderHelpClickedHandler;
            if (_panelHeader != null && _panelHeaderCloseClickedHandler != null)
                _panelHeader.CloseClicked -= _panelHeaderCloseClickedHandler;

            // Dispose Syncfusion controls
            _departmentsGrid?.SafeDispose();
            _benchmarksGrid?.SafeDispose();
            _chartControl?.Dispose();
            _chartRegionEventWiring?.Dispose();

            // Dispose containers
            _leftSplitContainer?.SafeDispose();
            _mainSplitContainer?.SafeDispose();

            // Dispose other controls
            _statusStrip?.SafeDispose();
            _panelHeader?.SafeDispose();
            _loadingOverlay?.SafeDispose();
            _noDataOverlay?.SafeDispose();
            _errorProvider?.Dispose();
            _buttonPanel?.SafeDispose();
            _summaryPanel?.SafeDispose();
            _chartPanel?.SafeDispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.components = new System.ComponentModel.Container();
        this.Name = "RecommendedMonthlyChargePanel";
        this.Size = ScaleLogicalToDevice(new Size(1400, 900));
        try { this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi; } catch { }
        this.ResumeLayout(false);
    }

    #endregion
}
