using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Control for the Variances tab in Analytics Hub.
/// Thin view: export is handled by the parent AnalyticsHubPanel. No local CSV writer.
/// </summary>
public partial class VariancesTabControl : UserControl
{
    private readonly VariancesTabViewModel? _viewModel;
    private readonly ILogger _logger;

    private SfDataGrid? _variancesGrid;
    private LegacyGradientPanel? _filterPanel;
    private LoadingOverlay? _loadingOverlay;

    private TextBox? _searchTextBox;
    private ComboBox? _departmentFilter;

    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;

    public bool IsLoaded { get; private set; }

    public VariancesTabControl(VariancesTabViewModel? viewModel, ILogger? logger = null)
    {
        _viewModel = viewModel;
        _logger = logger ?? NullLogger.Instance;

        try
        {
            var theme = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
            SfSkinManager.SetVisualStyle(this, theme);
        }
        catch { /* best-effort */ }

        InitializeControls();
        if (_viewModel != null) BindViewModel();
    }

    private void InitializeControls()
    {
        this.Dock = DockStyle.Fill;

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 60
        };

        InitializeFilterPanel();
        mainSplit.Panel1.Controls.Add(_filterPanel);

        InitializeVariancesGrid();
        mainSplit.Panel2.Controls.Add(_variancesGrid);

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
        var themeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";

        _filterPanel = new LegacyGradientPanel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        SfSkinManager.SetVisualStyle(_filterPanel, themeName);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));  // Search label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));   // Search box
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Dept label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));   // Dept combo

        var searchLabel = new Label { Text = "Search:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _searchTextBox = new TextBox { Dock = DockStyle.Fill };
        _searchTextBox.TextChanged += SearchTextBox_TextChanged;

        var deptLabel = new Label { Text = "Department:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _departmentFilter = new ComboBox { Dock = DockStyle.Fill };
        _departmentFilter.Items.AddRange(new[] { "All", "Sales", "Marketing", "Operations", "HR", "Finance" });
        _departmentFilter.SelectedIndex = 0;
        _departmentFilter.SelectedIndexChanged += DepartmentFilter_SelectedIndexChanged;

        layout.Controls.Add(searchLabel, 0, 0);
        layout.Controls.Add(_searchTextBox, 1, 0);
        layout.Controls.Add(deptLabel, 2, 0);
        layout.Controls.Add(_departmentFilter, 3, 0);

        _filterPanel.Controls.Add(layout);
    }

    private void InitializeVariancesGrid()
    {
        var themeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
        _variancesGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AllowGrouping = true,
            AllowSorting = true,
            AllowFiltering = true,
            AutoGenerateColumns = false,
            ThemeName = themeName
        }.PreventStringRelationalFilters(null, "Department", "Account");

        GridColumn[] columns =
        [
            new GridTextColumn    { MappingName = "Department",      HeaderText = "Department"  },
            new GridTextColumn    { MappingName = "Account",         HeaderText = "Account"     },
            new GridNumericColumn { MappingName = "Budget",          HeaderText = "Budget",     Format = "C2" },
            new GridNumericColumn { MappingName = "Actual",          HeaderText = "Actual",     Format = "C2" },
            new GridNumericColumn { MappingName = "Variance",        HeaderText = "Variance",   Format = "C2" },
            new GridNumericColumn { MappingName = "VariancePercent", HeaderText = "Variance %", Format = "P2" }
        ];

        foreach (var col in columns) _variancesGrid.Columns.Add(col);
        _variancesGrid.QueryCellStyle += VariancesGrid_QueryCellStyle;
    }

    private void VariancesGrid_QueryCellStyle(object sender, QueryCellStyleEventArgs e)
    {
        if (e.Column.MappingName is not ("Variance" or "VariancePercent")) return;

        if (e.DataRow.RowData is VarianceRecord record)
        {
            if (record.Variance < 0)
                e.Style.TextColor = Color.DarkRed;
            else if (record.Variance > 0)
                e.Style.TextColor = Color.DarkGreen;
        }
    }

    private void BindViewModel()
    {
        if (_viewModel == null) return;

        _viewModelPropertyChangedHandler ??= ViewModel_PropertyChanged;
        _viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        _viewModel.PropertyChanged += _viewModelPropertyChangedHandler;

        UpdateVariancesGrid();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        if (e.PropertyName == nameof(_viewModel.IsLoading))
            UpdateLoadingState();
        else if (e.PropertyName == nameof(_viewModel.Variances))
            UpdateVariancesGrid();
    }

    private void SearchTextBox_TextChanged(object? sender, EventArgs e) => ApplyFilters();

    private void DepartmentFilter_SelectedIndexChanged(object? sender, EventArgs e) => ApplyFilters();

    private void UpdateLoadingState()
    {
        if (_loadingOverlay != null)
            _loadingOverlay.Visible = _viewModel?.IsLoading ?? false;
    }

    private void UpdateVariancesGrid()
    {
        if (_variancesGrid != null && _viewModel?.Variances != null)
            _variancesGrid.DataSource = _viewModel.Variances;
    }

    private void ApplyFilters()
    {
        if (_variancesGrid?.View == null) return;

        string searchText = _searchTextBox?.Text ?? "";
        string departmentFilter = _departmentFilter?.SelectedItem?.ToString() ?? "All";

        _variancesGrid.View.Filter = item =>
        {
            if (item is VarianceRecord record)
            {
                bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                    record.Department.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    record.Account.Contains(searchText, StringComparison.OrdinalIgnoreCase);

                bool matchesDept = departmentFilter == "All" ||
                    record.Department.Equals(departmentFilter, StringComparison.OrdinalIgnoreCase);

                return matchesSearch && matchesDept;
            }
            return true;
        };

        _variancesGrid.View.RefreshFilter();
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
            System.Diagnostics.Debug.WriteLine($"Variances LoadAsync error: {ex.Message}");
            _logger.LogError(ex, "[VariancesTabControl] LoadAsync failed");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_searchTextBox != null)           _searchTextBox.TextChanged -= SearchTextBox_TextChanged;
            if (_departmentFilter != null)        _departmentFilter.SelectedIndexChanged -= DepartmentFilter_SelectedIndexChanged;
            if (_variancesGrid != null)           _variancesGrid.QueryCellStyle -= VariancesGrid_QueryCellStyle;
            if (_viewModel != null && _viewModelPropertyChangedHandler != null)
                _viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        }
        base.Dispose(disposing);
    }
}