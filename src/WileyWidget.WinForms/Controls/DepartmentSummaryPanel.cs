using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Docked panel displaying Department Summary with key metrics and drill-down grid.
/// Inherits from ScopedPanelBase to ensure proper DI lifetime management for scoped dependencies.
/// </summary>
public partial class DepartmentSummaryPanel : ScopedPanelBase<DepartmentSummaryViewModel>
{
    // UI Controls
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private Panel? _summaryPanel;
    private SfDataGrid? _metricsGrid;
    private TableLayoutPanel? _summaryCardsPanel;

    // Summary metric labels
    private Label? _lblTotalBudgetValue;
    private Label? _lblTotalActualValue;
    private Label? _lblVarianceValue;
    private Label? _lblOverBudgetCountValue;
    private Label? _lblUnderBudgetCountValue;
    private Label? _lblLastUpdated;

    // Event handlers for cleanup
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _metricsCollectionChangedHandler;
    private EventHandler<AppTheme>? _themeChangedHandler;
    private EventHandler? _panelHeaderRefreshHandler;
    private EventHandler? _panelHeaderCloseHandler;

    /// <summary>
    /// Initializes a new instance with required DI dependencies.
    /// </summary>
    public DepartmentSummaryPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<DepartmentSummaryViewModel>> logger)
        : base(scopeFactory, logger)
    {
        InitializeComponent();
        SetupUI();
        SubscribeToThemeChanges();
    }

    private void InitializeComponent()
    {
        Name = "DepartmentSummaryPanel";
        Size = new Size(1000, 700);
        Dock = DockStyle.Fill;
        AccessibleName = "Department Summary Panel";
        AccessibleDescription = "Displays department budget summary with key metrics and detailed grid";

        try
        {
            AutoScaleMode = AutoScaleMode.Dpi;
        }
        catch
        {
            // Fall back if DPI scaling not supported
        }
    }

    private void SetupUI()
    {
        SuspendLayout();

        // Panel header with title and actions
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Title = "Department Summary",
            AccessibleName = "Department Summary header"
        };
        _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
        _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
        _panelHeaderCloseHandler = (s, e) => ClosePanel();
        _panelHeader.CloseClicked += _panelHeaderCloseHandler;
        Controls.Add(_panelHeader);

        // Summary cards panel (top section)
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 120,
            Padding = new Padding(8),
            AccessibleName = "Summary metrics panel"
        };

        _summaryCardsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            AutoSize = true,
            AccessibleName = "Summary cards"
        };

        // Configure equal column widths
        for (int i = 0; i < 5; i++)
        {
            _summaryCardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        }
        _summaryCardsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // Create summary cards
        _lblTotalBudgetValue = CreateSummaryCard(_summaryCardsPanel, "Total Budget", "$0", 0, "Total budgeted amount");
        _lblTotalActualValue = CreateSummaryCard(_summaryCardsPanel, "Total Actual", "$0", 1, "Total actual spending");
        _lblVarianceValue = CreateSummaryCard(_summaryCardsPanel, "Variance", "$0", 2, "Difference between budget and actual");
        _lblOverBudgetCountValue = CreateSummaryCard(_summaryCardsPanel, "Over Budget", "0", 3, "Number of departments over budget");
        _lblUnderBudgetCountValue = CreateSummaryCard(_summaryCardsPanel, "Under Budget", "0", 4, "Number of departments under budget");

        _summaryPanel.Controls.Add(_summaryCardsPanel);
        Controls.Add(_summaryPanel);

        // Data grid for detailed metrics
        _metricsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowFiltering = true,
            AllowSorting = true,
            AllowGrouping = false,
            ShowRowHeader = false,
            SelectionMode = GridSelectionMode.Single,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28.0f),
            HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(32.0f),
            AllowResizingColumns = true,
            AllowTriStateSorting = true,
            AccessibleName = "Department metrics grid",
            AccessibleDescription = "Grid displaying budget metrics for each department"
        };

        ConfigureGridColumns();
        Controls.Add(_metricsGrid);

        // Last updated timestamp label
        _lblLastUpdated = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            TextAlign = ContentAlignment.MiddleRight,
            Text = "Last updated: --",
            Font = new Font("Segoe UI", 8F, FontStyle.Italic),
            Padding = new Padding(0, 0, 8, 0),
            AccessibleName = "Last updated timestamp"
        };
        Controls.Add(_lblLastUpdated);

        // Loading overlay
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading department data...",
            AccessibleName = "Loading overlay"
        };
        Controls.Add(_loadingOverlay);

        // No-data overlay
        _noDataOverlay = new NoDataOverlay
        {
            Message = "No department data available",
            AccessibleName = "No data overlay"
        };
        Controls.Add(_noDataOverlay);

        ResumeLayout(false);
    }

    private Label CreateSummaryCard(TableLayoutPanel parent, string title, string value, int columnIndex, string description)
    {
        var cardPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            Padding = new Padding(8),
            AccessibleName = $"{title} card",
            AccessibleDescription = description
        };

        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            AutoSize = false,
            AccessibleName = $"{title} label"
        };
        cardPanel.Controls.Add(lblTitle);

        var lblValue = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            AutoSize = false,
            AccessibleName = $"{title} value"
        };
        cardPanel.Controls.Add(lblValue);

        parent.Controls.Add(cardPanel, columnIndex, 0);
        return lblValue;
    }

    private void ConfigureGridColumns()
    {
        if (_metricsGrid == null) return;

        _metricsGrid.Columns.Clear();

        _metricsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(DepartmentMetric.DepartmentName),
            HeaderText = "Department",
            MinimumWidth = 150,
            AllowSorting = true,
            AllowFiltering = true
        });

        _metricsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(DepartmentMetric.BudgetedAmount),
            HeaderText = "Budgeted",
            MinimumWidth = 120,
            Format = "C2",
            AllowSorting = true
        });

        _metricsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(DepartmentMetric.ActualAmount),
            HeaderText = "Actual",
            MinimumWidth = 120,
            Format = "C2",
            AllowSorting = true
        });

        _metricsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(DepartmentMetric.Variance),
            HeaderText = "Variance",
            MinimumWidth = 120,
            Format = "C2",
            AllowSorting = true
        });

        _metricsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(DepartmentMetric.VariancePercent),
            HeaderText = "Variance %",
            MinimumWidth = 100,
            Format = "N2",
            AllowSorting = true
        });

        _metricsGrid.Columns.Add(new GridCheckBoxColumn
        {
            MappingName = nameof(DepartmentMetric.IsOverBudget),
            HeaderText = "Over Budget",
            MinimumWidth = 100,
            AllowSorting = true,
            AllowFiltering = true
        });
    }

    /// <summary>
    /// Called after ViewModel is resolved from scoped service provider.
    /// Binds ViewModel data and initiates data load.
    /// </summary>
    protected override void OnViewModelResolved(DepartmentSummaryViewModel viewModel)
    {
        if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
        base.OnViewModelResolved(viewModel);

        // Subscribe to ViewModel property changes
        _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
        viewModel.PropertyChanged += _viewModelPropertyChangedHandler;

        // Subscribe to Metrics collection changes
        _metricsCollectionChangedHandler = (s, e) => UpdateGridData();
        viewModel.Metrics.CollectionChanged += _metricsCollectionChangedHandler;

        // Initial UI update
        UpdateUI();

        // Load data asynchronously
        _ = LoadDataSafeAsync();
    }

    private async Task LoadDataSafeAsync()
    {
        try
        {
            if (ViewModel != null)
            {
                await ViewModel.LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new System.Action(() => ShowError(ex)));
            }
            else
            {
                ShowError(ex);
            }
        }
    }

    private void ShowError(Exception ex)
    {
        MessageBox.Show(
            $"Failed to load department data: {ex.Message}",
            "Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed || ViewModel == null) return;

        // Thread-safe UI updates
        if (InvokeRequired)
        {
            BeginInvoke(new System.Action(() => ViewModel_PropertyChanged(sender, e)));
            return;
        }

        try
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.IsLoading):
                    if (_loadingOverlay != null)
                        _loadingOverlay.Visible = ViewModel.IsLoading;
                    break;

                case nameof(ViewModel.TotalBudget):
                case nameof(ViewModel.TotalActual):
                case nameof(ViewModel.Variance):
                case nameof(ViewModel.VariancePercent):
                case nameof(ViewModel.DepartmentsOverBudget):
                case nameof(ViewModel.DepartmentsUnderBudget):
                case nameof(ViewModel.LastUpdated):
                    UpdateSummaryCards();
                    break;

                case nameof(ViewModel.ErrorMessage):
                    if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
                    {
                        MessageBox.Show(
                            ViewModel.ErrorMessage,
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    break;
            }
        }
        catch (ObjectDisposedException)
        {
            // Ignore if disposed during update
        }
    }

    private void UpdateUI()
    {
        if (IsDisposed || ViewModel == null) return;

        if (InvokeRequired)
        {
            BeginInvoke(new System.Action(UpdateUI));
            return;
        }

        try
        {
            UpdateSummaryCards();
            UpdateGridData();
            UpdateNoDataOverlay();
        }
        catch (ObjectDisposedException)
        {
            // Ignore if disposed
        }
    }

    private void UpdateSummaryCards()
    {
        if (ViewModel == null) return;

        try
        {
            if (_lblTotalBudgetValue != null)
                _lblTotalBudgetValue.Text = ViewModel.TotalBudget.ToString("C0", CultureInfo.CurrentCulture);

            if (_lblTotalActualValue != null)
                _lblTotalActualValue.Text = ViewModel.TotalActual.ToString("C0", CultureInfo.CurrentCulture);

            if (_lblVarianceValue != null)
            {
                _lblVarianceValue.Text = ViewModel.Variance.ToString("C0", CultureInfo.CurrentCulture);
                // Semantic status color: green for under budget, red for over budget
                _lblVarianceValue.ForeColor = ViewModel.Variance >= 0 ? Color.Green : Color.Red;
            }

            if (_lblOverBudgetCountValue != null)
            {
                _lblOverBudgetCountValue.Text = ViewModel.DepartmentsOverBudget.ToString(CultureInfo.CurrentCulture);
                _lblOverBudgetCountValue.ForeColor = Color.Red; // Semantic status
            }

            if (_lblUnderBudgetCountValue != null)
            {
                _lblUnderBudgetCountValue.Text = ViewModel.DepartmentsUnderBudget.ToString(CultureInfo.CurrentCulture);
                _lblUnderBudgetCountValue.ForeColor = Color.Green; // Semantic status
            }

            if (_lblLastUpdated != null)
                _lblLastUpdated.Text = $"Last updated: {ViewModel.LastUpdated:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            // Log but don't crash on UI update failure
            Console.WriteLine($"DepartmentSummaryPanel: UpdateSummaryCards failed: {ex.Message}");
        }
    }

    private void UpdateGridData()
    {
        if (_metricsGrid == null || ViewModel == null) return;

        try
        {
            _metricsGrid.SuspendLayout();

            // Create snapshot to avoid collection modification issues
            var snapshot = ViewModel.Metrics.ToList();
            _metricsGrid.DataSource = snapshot;

            _metricsGrid.ResumeLayout();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DepartmentSummaryPanel: UpdateGridData failed: {ex.Message}");
        }
    }

    private void UpdateNoDataOverlay()
    {
        if (_noDataOverlay == null || ViewModel == null) return;

        try
        {
            _noDataOverlay.Visible = !ViewModel.IsLoading && !ViewModel.Metrics.Any();
        }
        catch
        {
            // Ignore
        }
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            if (ViewModel != null)
            {
                await ViewModel.LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to refresh data: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ClosePanel()
    {
        try
        {
            var parentForm = FindForm();
            if (parentForm == null) return;

            // Try to find ClosePanel method on parent form
            var closePanelMethod = parentForm.GetType().GetMethod(
                "ClosePanel",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            closePanelMethod?.Invoke(parentForm, new object[] { Name });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DepartmentSummaryPanel: ClosePanel failed: {ex.Message}");
        }
    }

    private void SubscribeToThemeChanges()
    {
        _themeChangedHandler = (s, theme) =>
        {
            if (IsDisposed) return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ApplyTheme()));
            }
            else
            {
                ApplyTheme();
            }
        };

        ThemeManager.ThemeChanged += _themeChangedHandler;
    }

    private void ApplyTheme()
    {
        try
        {
            // Theme is applied automatically by SfSkinManager cascade from parent form
            // No manual color assignments needed
            ThemeManager.ApplyThemeToControl(this);
        }
        catch
        {
            // Ignore theme application failures
        }
    }

    /// <summary>
    /// Disposes resources using SafeDispose pattern to prevent disposal errors.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from events
            try
            {
                if (_themeChangedHandler != null)
                    ThemeManager.ThemeChanged -= _themeChangedHandler;
            }
            catch { }

            try
            {
                if (ViewModel != null && _viewModelPropertyChangedHandler != null)
                    ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            }
            catch { }

            try
            {
                if (ViewModel != null && _metricsCollectionChangedHandler != null)
                    ViewModel.Metrics.CollectionChanged -= _metricsCollectionChangedHandler;
            }
            catch { }

            try
            {
                if (_panelHeader != null)
                {
                    if (_panelHeaderRefreshHandler != null)
                        _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                    if (_panelHeaderCloseHandler != null)
                        _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                }
            }
            catch { }

            // Dispose controls using SafeDispose pattern
            try { _metricsGrid?.SafeClearDataSource(); } catch { }
            try { _metricsGrid?.SafeDispose(); } catch { }
            try { _panelHeader?.Dispose(); } catch { }
            try { _loadingOverlay?.Dispose(); } catch { }
            try { _noDataOverlay?.Dispose(); } catch { }
            try { _summaryCardsPanel?.Dispose(); } catch { }
            try { _summaryPanel?.Dispose(); } catch { }
        }

        base.Dispose(disposing);
    }
}
