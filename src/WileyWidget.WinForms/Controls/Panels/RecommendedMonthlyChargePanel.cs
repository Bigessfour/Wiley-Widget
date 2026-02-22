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
using WileyWidget.WinForms.Themes;
// using WileyWidget.WinForms.Utils; // Consolidated
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Helpers;
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

    public RecommendedMonthlyChargePanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<RecommendedMonthlyChargeViewModel>> logger)
        : base(scopeFactory, logger)
    {
        // NOTE: InitializeControls() moved to OnViewModelResolved()
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        MinimumSize = new Size(1024, 720);
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
        SafeSuspendAndLayout(InitializeControls);
        BindViewModel();
        ApplyCurrentTheme();
    }

    private void InitializeControls()
    {
        // Apply Syncfusion theme via SfSkinManager (single source of truth)
        SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        Name = "RecommendedMonthlyChargePanel";
        Size = new Size(1400, 900);
        MinimumSize = new Size(1024, 720);
        Dock = DockStyle.Fill;

        // Error provider
        _errorProvider = new ErrorProvider
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink,
            BlinkRate = 0
        };

        // ============================================================================
        // Panel Header
        // ============================================================================
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Title = "Recommended Monthly Charges",
            Height = 50
        };
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
        _buttonPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(_buttonPanel, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        var buttonFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false
        };

        _refreshButton = new SfButton
        {
            Text = "&Refresh Data",
            AutoSize = true,
            Margin = new Padding(5),
            TabIndex = 1,
            AccessibleName = "Refresh Data",
            AccessibleDescription = "Refresh department expense data from QuickBooks"
        };
        _refreshButtonClickHandler = async (s, e) => await RefreshDataAsync();
        _refreshButton.Click += _refreshButtonClickHandler;
        var refreshTooltip = new ToolTip();
        refreshTooltip.SetToolTip(_refreshButton, "Load latest expense data from QuickBooks (Alt+R)");
        buttonFlow.Controls.Add(_refreshButton);

        _queryGrokButton = new SfButton
        {
            Text = "Query &AI",
            AutoSize = true,
            Margin = new Padding(5),
            TabIndex = 2,
            AccessibleName = "Query AI",
            AccessibleDescription = "Get AI-driven rate recommendations from Grok"
        };
        _queryGrokButtonClickHandler = async (s, e) => await QueryGrokAsync();
        _queryGrokButton.Click += _queryGrokButtonClickHandler;
        var grokTooltip = new ToolTip();
        grokTooltip.SetToolTip(_queryGrokButton, "Query Grok AI for recommended adjustment factors (Alt+A)");
        buttonFlow.Controls.Add(_queryGrokButton);

        _saveButton = new SfButton
        {
            Text = "&Save Changes",
            AutoSize = true,
            Margin = new Padding(5),
            TabIndex = 3,
            AccessibleName = "Save Changes",
            AccessibleDescription = "Save current charge modifications to database"
        };
        _saveButtonClickHandler = async (s, e) => await SaveAsync(RegisterOperation());
        _saveButton.Click += _saveButtonClickHandler;
        var saveTooltip = new ToolTip();
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
            Height = 180,
            Padding = new Padding(15),
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(_summaryPanel, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        var summaryTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            AutoSize = false
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

        _explanationTextBox = new TextBoxExt
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            TabIndex = 9,
            AccessibleName = "AI Recommendation Explanation",
            AccessibleDescription = "AI-generated explanation of the recommended rate adjustments",
            AutoSize = false
        };
        var explanationTooltip = new ToolTip();
        explanationTooltip.SetToolTip(_explanationTextBox, "AI-generated explanation for the recommended adjustments");
        summaryTable.Controls.Add(_explanationTextBox, 0, 2);
        summaryTable.SetColumnSpan(_explanationTextBox, 2);

        _summaryPanel.Controls.Add(summaryTable);
        Controls.Add(_summaryPanel);

        // ============================================================================
        // Main Split Container - Left (Grids) | Right (Chart)
        // ============================================================================
        _mainSplitContainer = new SplitContainerAdv
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BorderStyle = BorderStyle.FixedSingle
        };
        SafeSplitterDistanceHelper.TrySetSplitterDistance(_mainSplitContainer, 700);

        // ============================================================================
        // Left Split Container - Top (Departments) | Bottom (Benchmarks)
        // ============================================================================
        _leftSplitContainer = new SplitContainerAdv
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            BorderStyle = BorderStyle.None
        };
        SafeSplitterDistanceHelper.TrySetSplitterDistance(_leftSplitContainer, 350);

        // ============================================================================
        // Departments Grid (Top Left)
        // ============================================================================
        var deptGridPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
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

        _departmentsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = true,
            AllowResizingColumns = true,
            AllowSorting = true,
            AllowFiltering = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            SelectionMode = GridSelectionMode.Single,
            EditMode = EditMode.SingleClick,
            TabIndex = 10,
            AccessibleName = "Department Rates Grid",
            AccessibleDescription = "Editable grid showing monthly charges, expenses, and recommendations per department"
        }.PreventStringRelationalFilters(Logger, "Department");

        // Configure department grid columns
        _departmentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Department",
            HeaderText = "Department",
            Width = 120,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "CustomerCount",
            HeaderText = "Customers",
            Width = 90,
            AllowEditing = false,
            Format = "N0"
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "MonthlyExpenses",
            HeaderText = "Monthly Expenses",
            Format = "C2",
            Width = 140,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "CurrentCharge",
            HeaderText = "Current Charge",
            Format = "C2",
            Width = 120,
            AllowEditing = true
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "SuggestedCharge",
            HeaderText = "Suggested",
            Format = "C2",
            Width = 110,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "MonthlyGainLoss",
            HeaderText = "Gain/Loss",
            Format = "C2",
            Width = 120,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "PositionStatus",
            HeaderText = "Status",
            Width = 110,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "StateAverage",
            HeaderText = "State Avg",
            Format = "C2",
            Width = 100,
            AllowEditing = false
        });

        // Mark grid as dirty on edit for unsaved changes tracking
        _departmentsGrid.CurrentCellEndEdit += (s, e) => SetHasUnsavedChanges(true);

        var gridTooltip = new ToolTip();
        gridTooltip.SetToolTip(_departmentsGrid, "Edit Current Charge column to set new rates. Other columns are calculated automatically.");

        deptGridPanel.Controls.Add(_departmentsGrid);
        _leftSplitContainer.Panel1.Controls.Add(deptGridPanel);

        // ============================================================================
        // Benchmarks Grid (Bottom Left)
        // ============================================================================
        var benchmarkPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(benchmarkPanel, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        var benchmarkLabel = new Label
        {
            Text = "State & National Benchmarks",
            Dock = DockStyle.Top,
            Height = 25,
            Padding = new Padding(5, 3, 0, 0)
        };
        benchmarkPanel.Controls.Add(benchmarkLabel);

        _benchmarksGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AllowResizingColumns = true,
            AllowSorting = true,
            AllowFiltering = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            SelectionMode = GridSelectionMode.Single,
            TabIndex = 11,
            AccessibleName = "Benchmarks Grid",
            AccessibleDescription = "State and national benchmark data for comparison"
        }.PreventStringRelationalFilters(Logger, "Department");

        // Configure benchmark grid columns
        _benchmarksGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Department",
            HeaderText = "Department",
            Width = 120,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "StateAverage",
            HeaderText = "State Avg",
            Format = "C2",
            Width = 110,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "TownSizeAverage",
            HeaderText = "Town Size Avg",
            Format = "C2",
            Width = 120,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "NationalAverage",
            HeaderText = "National Avg",
            Format = "C2",
            Width = 110,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "PopulationRange",
            HeaderText = "Pop Range",
            Width = 100,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Source",
            HeaderText = "Source",
            Width = 200,
            AllowEditing = false
        });

        var benchmarkTooltip = new ToolTip();
        benchmarkTooltip.SetToolTip(_benchmarksGrid, "Reference data from state and national utility surveys");

        benchmarkPanel.Controls.Add(_benchmarksGrid);
        _leftSplitContainer.Panel2.Controls.Add(benchmarkPanel);

        _mainSplitContainer.Panel1.Controls.Add(_leftSplitContainer);

        // ============================================================================
        // Chart Panel (Right Side)
        // ============================================================================
        _chartPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(_chartPanel, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        _chartControl = new ChartControl
        {
            Dock = DockStyle.Fill,
            TabIndex = 12,
            AccessibleName = "Department Expense Chart",
            AccessibleDescription = "Visual comparison of department expenses, current charges, and suggested charges"
        };
        _chartRegionEventWiring = new ChartControlRegionEventWiring(_chartControl);

        ChartControlDefaults.Apply(_chartControl, logger: Logger);

        // Configure chart appearance
        _chartControl.Title.Text = "Expenses vs Current vs Suggested Charges";
        _chartControl.Title.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _chartControl.Legend.Visible = true;
        _chartControl.Legend.Position = ChartDock.Top;
        _chartControl.PrimaryXAxis.Title = "Departments";
        _chartControl.PrimaryYAxis.Title = "Amount ($)";
        _chartControl.PrimaryYAxis.RangeType = ChartAxisRangeType.Auto;

        _chartPanel.Controls.Add(_chartControl);
        _mainSplitContainer.Panel2.Controls.Add(_chartPanel);

        Controls.Add(_mainSplitContainer);

        // ============================================================================
        // Status Strip - Bottom Status Bar
        // ============================================================================
        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom
        };
        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
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

        _noDataOverlay = new NoDataOverlay
        {
            Message = "No charge data available\r\nLoad departments to generate charge recommendations",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();

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
                    _overallStatusLabel.ForeColor = ViewModel.OverallStatusColor switch
                    {
                        "Red" => System.Drawing.Color.Red,
                        "Orange" => System.Drawing.Color.Orange,
                        "Green" => System.Drawing.Color.Green,
                        _ => System.Drawing.Color.Gray
                    };
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
        if (_noDataOverlay == null || ViewModel == null) return;
        var hasData = ViewModel.Departments.Any();
        if (!_noDataOverlay.IsDisposed)
            _noDataOverlay.SafeInvoke(() => _noDataOverlay.Visible = !hasData && !ViewModel.IsLoading);
    }

    private void UpdateChart()
    {
        if (_chartControl == null || ViewModel == null || !ViewModel.Departments.Any())
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
            Logger.LogDebug("Updating department expense chart with {Count} departments", ViewModel.Departments.Count);

            // Clear existing series
            _chartControl.Series.Clear();

            // Create series for monthly expenses, current charges revenue, and suggested charges revenue
            var expenseSeries = new ChartSeries("Monthly Expenses", ChartSeriesType.Column);
            var currentRevenueSeries = new ChartSeries("Current Charge", ChartSeriesType.Column);
            var suggestedRevenueSeries = new ChartSeries("Suggested Charge", ChartSeriesType.Column);

            // Configure series appearance with semantic colors
            expenseSeries.Style.Interior = new BrushInfo(System.Drawing.Color.FromArgb(220, 53, 69)); // Red
            currentRevenueSeries.Style.Interior = new BrushInfo(System.Drawing.Color.FromArgb(0, 123, 255)); // Blue
            suggestedRevenueSeries.Style.Interior = new BrushInfo(System.Drawing.Color.FromArgb(40, 167, 69)); // Green

            // Add data points for each department
            foreach (var department in ViewModel.Departments.OrderBy(d => d.Department))
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
            Logger.LogDebug("Chart updated successfully with {DepartmentCount} departments", ViewModel.Departments.Count);
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

    private void ApplyCurrentTheme()
    {
        try
        {
            var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
            SfSkinManager.SetVisualStyle(this, theme);
            ApplyThemeRecursively(this, theme);
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to apply theme");
        }
    }

    private static void ApplyThemeRecursively(Control parent, string themeName)
    {
        try
        {
            SfSkinManager.SetVisualStyle(parent, themeName);
            foreach (Control child in parent.Controls)
                ApplyThemeRecursively(child, themeName);
        }
        catch { /* Best-effort only */ }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (ViewModel != null && !DesignMode)
        {
            // Queue async loading on the UI thread
            BeginInvoke(new Func<Task>(async () =>
            {
                try
                {
                    await LoadAsync(RegisterOperation());
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Error loading panel data");
                }
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
        this.Size = new Size(1400, 900);
        try { this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi; } catch { }
        this.ResumeLayout(false);
    }

    #endregion
}
