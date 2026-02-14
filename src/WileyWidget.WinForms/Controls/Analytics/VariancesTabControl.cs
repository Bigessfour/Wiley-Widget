using System;
using System.Drawing;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Events;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Services.Abstractions;

using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;

namespace WileyWidget.WinForms.Controls.Analytics;

/// <summary>
/// Control for the Variances tab in Analytics Hub.
/// Displays detailed variance analysis with filtering and drill-down capabilities.
/// </summary>
public partial class VariancesTabControl : UserControl
{
    private readonly VariancesTabViewModel? _viewModel;

    // UI Controls
    private SfDataGrid? _variancesGrid;
    private LegacyGradientPanel? _filterPanel;
    private LoadingOverlay? _loadingOverlay;

    // Filter controls
    private TextBox? _searchTextBox;
    private ComboBox? _departmentFilter;
    private Button? _exportButton;

    public bool IsLoaded { get; private set; }

    public VariancesTabControl(VariancesTabViewModel? viewModel)
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

        // Main layout - filters on top, grid below
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 60
        };

        // Top: Filter panel
        InitializeFilterPanel();
        mainSplit.Panel1.Controls.Add(_filterPanel);

        // Bottom: Variances grid
        InitializeVariancesGrid();
        mainSplit.Panel2.Controls.Add(_variancesGrid);

        // Loading overlay
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading variance data...",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        Controls.Add(mainSplit);
    }

    private void InitializeFilterPanel()
    {
        _filterPanel = new LegacyGradientPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        SfSkinManager.SetVisualStyle(_filterPanel, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));  // Search label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));   // Search box
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Dept label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));   // Dept combo
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Export button

        var searchLabel = new Label { Text = "Search:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _searchTextBox = new TextBox { Dock = DockStyle.Fill };
        _searchTextBox.TextChanged += (s, e) => ApplyFilters();

        var deptLabel = new Label { Text = "Department:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _departmentFilter = new ComboBox { Dock = DockStyle.Fill };
        _departmentFilter.Items.AddRange(new[] { "All", "Sales", "Marketing", "Operations", "HR", "Finance" });
        _departmentFilter.SelectedIndex = 0;
        _departmentFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

        _exportButton = new Button { Text = "Export CSV", Dock = DockStyle.Fill };
        _exportButton.Click += (s, e) => ExportVariances();

        layout.Controls.Add(searchLabel, 0, 0);
        layout.Controls.Add(_searchTextBox, 1, 0);
        layout.Controls.Add(deptLabel, 2, 0);
        layout.Controls.Add(_departmentFilter, 3, 0);
        layout.Controls.Add(_exportButton, 4, 0);

        _filterPanel.Controls.Add(layout);
    }

    private void InitializeVariancesGrid()
    {
        _variancesGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AllowGrouping = true,
            AllowSorting = true,
            AllowFiltering = true,
            AutoGenerateColumns = false
        }.PreventStringRelationalFilters(null, "Department", "Account");

        // Configure columns for variance data
        var columns = new GridColumn[]
        {
            new GridTextColumn { MappingName = "Department", HeaderText = "Department" },
            new GridTextColumn { MappingName = "Account", HeaderText = "Account" },
            new GridNumericColumn { MappingName = "Budget", HeaderText = "Budget", Format = "C2" },
            new GridNumericColumn { MappingName = "Actual", HeaderText = "Actual", Format = "C2" },
            new GridNumericColumn { MappingName = "Variance", HeaderText = "Variance", Format = "C2" },
            new GridNumericColumn { MappingName = "VariancePercent", HeaderText = "Variance %", Format = "P2" }
        };

        foreach (var column in columns)
        {
            _variancesGrid.Columns.Add(column);
        }

        // Configure conditional formatting for variances
        _variancesGrid.QueryCellStyle += VariancesGrid_QueryCellStyle;
    }

    private void VariancesGrid_QueryCellStyle(object sender, QueryCellStyleEventArgs e)
    {
        if (e.Column.MappingName == "Variance" || e.Column.MappingName == "VariancePercent")
        {
            if (e.DataRow.RowData is VarianceRecord record)
            {
                if (record.Variance < 0)
                {
                    // Removed manual BackColor to respect SfSkinManager theme cascade.
                    // Keep semantic text color for status clarity.
                    e.Style.TextColor = Color.DarkRed;
                }
                else if (record.Variance > 0)
                {
                    // Removed manual BackColor to respect SfSkinManager theme cascade.
                    // Keep semantic text color for status clarity.
                    e.Style.TextColor = Color.DarkGreen;
                }
            }
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
            else if (e.PropertyName == nameof(_viewModel.Variances))
            {
                UpdateVariancesGrid();
            }
        };

        UpdateVariancesGrid();
    }

    private void UpdateLoadingState()
    {
        if (_loadingOverlay != null)
        {
            _loadingOverlay.Visible = _viewModel?.IsLoading ?? false;
        }
    }

    private void UpdateVariancesGrid()
    {
        if (_variancesGrid != null && _viewModel?.Variances != null)
        {
            _variancesGrid.DataSource = _viewModel.Variances;
        }
    }

    private void ApplyFilters()
    {
        if (_variancesGrid?.View == null) return;

        string searchText = _searchTextBox?.Text ?? "";
        string departmentFilter = _departmentFilter?.SelectedItem?.ToString() ?? "All";

        // Apply filtering logic
        _variancesGrid.View.Filter = item =>
        {
            if (item is VarianceRecord record)
            {
                bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                    record.Department.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    record.Account.Contains(searchText, StringComparison.OrdinalIgnoreCase);

                bool matchesDepartment = departmentFilter == "All" ||
                    record.Department.Equals(departmentFilter, StringComparison.OrdinalIgnoreCase);

                return matchesSearch && matchesDepartment;
            }
            return true;
        };

        _variancesGrid.View.RefreshFilter();
    }

    private void ExportVariances()
    {
        // TODO: Implement CSV export functionality
        MessageBox.Show("CSV export will be implemented", "Variances",
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
                UpdateVariancesGrid();
            }

            IsLoaded = true;
        }
        catch (Exception ex)
        {
            // Handle error
            System.Diagnostics.Debug.WriteLine($"Failed to load variances tab: {ex.Message}");
        }
    }
}
