using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid.Styles;
using System;
using System.Threading.Tasks;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Enhanced Budget View Form with advanced filtering, analysis, and visualizations
    /// </summary>
    public partial class BudgetViewFormEnhanced : Form
    {
        private readonly BudgetViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BudgetViewFormEnhanced> _logger;

        // Main Controls
        private SfDataGrid _dataGrid = null!;
        private ChartControl _trendChart = null!;
        private ChartControl _varianceChart = null!;
        private TabControl _tabControl = null!;
        private Panel _filterPanel = null!;
        private Panel _analysisPanel = null!;
        private StatusStrip _statusStrip = null!;

        // Filter Controls
        private TextBox _searchBox = null!;
        private ComboBox _departmentFilter = null!;
        private ComboBox _fundTypeFilter = null!;
        private NumericUpDown _varianceThresholdFilter = null!;
        private CheckBox _showOverBudgetOnly = null!;
        private CheckBox _showUnderBudgetOnly = null!;
        private ComboBox _groupByCombo = null!;
        private CheckBox _showHierarchyCheck = null!;

        // Analysis Controls
        private Label _totalBudgetedLabel = null!;
        private Label _totalActualLabel = null!;
        private Label _totalVarianceLabel = null!;
        private Label _percentUsedLabel = null!;
        private Label _overBudgetCountLabel = null!;
        private Label _underBudgetCountLabel = null!;
        private ProgressBar _budgetProgressBar = null!;

        // Toolbar Buttons
        private ToolStripButton _addBtn = null!;
        private ToolStripButton _editBtn = null!;
        private ToolStripButton _deleteBtn = null!;
        private ToolStripButton _refreshBtn = null!;
        private ToolStripButton _importBtn = null!;
        private ToolStripButton _exportBtn = null!;
        private ToolStripButton _copyToNextYearBtn = null!;
        private ToolStripButton _bulkAdjustBtn = null!;
        private ToolStripButton _calculateVariancesBtn = null!;

        private BindingSource _bindingSource = null!;

        public BudgetViewFormEnhanced(
            BudgetViewModel viewModel,
            IServiceProvider serviceProvider,
            ILogger<BudgetViewFormEnhanced> logger)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            SetupBindings();
            SetupFilters();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Main Form Setup
            this.Text = "Budget Management - Enhanced";
            this.Size = new Size(1400, 900);
            this.MinimumSize = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9F);
            this.KeyPreview = true;

            // Status Strip
            _statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(240, 240, 240),
                Font = new Font("Segoe UI", 9F)
            };
            var statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            var recordCountLabel = new ToolStripStatusLabel("Records: 0") { Name = "recordCount" };
            var filterStatusLabel = new ToolStripStatusLabel("No filters") { Name = "filterStatus" };
            _statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, recordCountLabel, filterStatusLabel });
            this.Controls.Add(_statusStrip);

            // Toolbar
            var toolStrip = CreateToolbar();
            this.Controls.Add(toolStrip);

            // Main Layout: Filter Panel (left) | Tab Control (center-right)
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 280,
                Panel1MinSize = 200,
                Panel2MinSize = 600
            };

            // Left Panel - Filters and Analysis
            var leftPanel = new Panel { Dock = DockStyle.Fill };
            var leftSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 350
            };

            _filterPanel = CreateFilterPanel();
            _analysisPanel = CreateAnalysisPanel();

            leftSplit.Panel1.Controls.Add(_filterPanel);
            leftSplit.Panel2.Controls.Add(_analysisPanel);
            leftPanel.Controls.Add(leftSplit);
            mainSplit.Panel1.Controls.Add(leftPanel);

            // Right Panel - Tab Control with Data Grid and Charts
            _tabControl = CreateTabControl();
            mainSplit.Panel2.Controls.Add(_tabControl);

            this.Controls.Add(mainSplit);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private ToolStrip CreateToolbar()
        {
            var toolStrip = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(24, 24),
                Padding = new Padding(4)
            };

            _addBtn = new ToolStripButton("Add")
            {
                ToolTipText = "Add new budget entry (Ctrl+N)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            _editBtn = new ToolStripButton("Edit")
            {
                ToolTipText = "Edit selected entry (Ctrl+E)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                Enabled = false
            };
            _deleteBtn = new ToolStripButton("Delete")
            {
                ToolTipText = "Delete selected entry (Delete)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                Enabled = false
            };
            _refreshBtn = new ToolStripButton("Refresh")
            {
                ToolTipText = "Refresh data (F5)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            _importBtn = new ToolStripButton("Import")
            {
                ToolTipText = "Import from CSV/Excel (Ctrl+I)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            _exportBtn = new ToolStripButton("Export")
            {
                ToolTipText = "Export to CSV/Excel/PDF (Ctrl+Shift+E)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            _copyToNextYearBtn = new ToolStripButton("Copy to Next Year")
            {
                ToolTipText = "Copy selected entry to next fiscal year",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                Enabled = false
            };
            _bulkAdjustBtn = new ToolStripButton("Bulk Adjust")
            {
                ToolTipText = "Adjust filtered entries by percentage",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            _calculateVariancesBtn = new ToolStripButton("Calculate Variances")
            {
                ToolTipText = "Recalculate all variances",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };

            var yearLabel = new ToolStripLabel("Fiscal Year: ");
            var nudYear = new NumericUpDown { Minimum = 2000, Maximum = 3000, Value = _viewModel.SelectedFiscalYear, Width = 70 };
            var yearHost = new ToolStripControlHost(nudYear);
            var loadYearBtn = new ToolStripButton("Load");

            toolStrip.Items.AddRange(new ToolStripItem[]
            {
                _addBtn, _editBtn, _deleteBtn,
                new ToolStripSeparator(),
                _refreshBtn, _importBtn, _exportBtn,
                new ToolStripSeparator(),
                yearLabel, yearHost, loadYearBtn,
                new ToolStripSeparator(),
                _copyToNextYearBtn, _bulkAdjustBtn, _calculateVariancesBtn
            });

            // Wire up events
            _addBtn.Click += async (s, e) => await AddEntryAsync();
            _editBtn.Click += async (s, e) => await EditEntryAsync();
            _deleteBtn.Click += async (s, e) => await DeleteEntryAsync();
            _refreshBtn.Click += async (s, e) => await _viewModel.LoadByYearCommand.ExecuteAsync(null);
            _importBtn.Click += async (s, e) => await ImportDataAsync();
            _exportBtn.Click += async (s, e) => await ExportDataAsync();
            _copyToNextYearBtn.Click += async (s, e) => await CopyToNextYearAsync();
            _bulkAdjustBtn.Click += async (s, e) => await BulkAdjustAsync();
            _calculateVariancesBtn.Click += async (s, e) => await _viewModel.CalculateVariancesCommand.ExecuteAsync(null);

            loadYearBtn.Click += async (s, e) =>
            {
                _viewModel.SelectedFiscalYear = (int)nudYear.Value;
                await _viewModel.LoadByYearCommand.ExecuteAsync(null);
            };

            return toolStrip;
        }

        private Panel CreateFilterPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(245, 245, 245)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                Padding = new Padding(5)
            };

            var titleLabel = new Label
            {
                Text = "📋 FILTERS",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(0, 120, 212),
                Padding = new Padding(0, 5, 0, 5)
            };

            _searchBox = new TextBox
            {
                Dock = DockStyle.Top,
                PlaceholderText = "🔍 Search account or description...",
                Height = 25,
                Font = new Font("Segoe UI", 9F)
            };
            _departmentFilter = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Height = 25,
                Font = new Font("Segoe UI", 9F),
                FlatStyle = FlatStyle.Flat
            };
            _fundTypeFilter = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Height = 25,
                Font = new Font("Segoe UI", 9F),
                FlatStyle = FlatStyle.Flat
            };
            _varianceThresholdFilter = new NumericUpDown
            {
                Dock = DockStyle.Top,
                DecimalPlaces = 2,
                Increment = 100,
                Height = 25,
                Font = new Font("Segoe UI", 9F),
                ThousandsSeparator = true
            };
            _showOverBudgetOnly = new CheckBox
            {
                Text = "🔴 Show only over budget",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI", 9F)
            };
            _showUnderBudgetOnly = new CheckBox
            {
                Text = "🟢 Show only under budget",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI", 9F)
            };
            _groupByCombo = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Height = 25,
                Font = new Font("Segoe UI", 9F),
                FlatStyle = FlatStyle.Flat
            };
            _showHierarchyCheck = new CheckBox
            {
                Text = "📊 Show hierarchy",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI", 9F)
            };

            var applyFiltersBtn = new Button
            {
                Text = "✓ Apply Filters",
                Dock = DockStyle.Top,
                Height = 35,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            applyFiltersBtn.FlatAppearance.BorderSize = 0;

            var clearFiltersBtn = new Button
            {
                Text = "✗ Clear Filters",
                Dock = DockStyle.Top,
                Height = 35,
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            clearFiltersBtn.FlatAppearance.BorderSize = 1;
            clearFiltersBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);

            layout.Controls.Add(titleLabel);
            layout.Controls.Add(new Label { Text = "Search:", Dock = DockStyle.Top });
            layout.Controls.Add(_searchBox);
            layout.Controls.Add(new Label { Text = "Department:", Dock = DockStyle.Top });
            layout.Controls.Add(_departmentFilter);
            layout.Controls.Add(new Label { Text = "Fund Type:", Dock = DockStyle.Top });
            layout.Controls.Add(_fundTypeFilter);
            layout.Controls.Add(new Label { Text = "Variance Threshold:", Dock = DockStyle.Top });
            layout.Controls.Add(_varianceThresholdFilter);
            layout.Controls.Add(_showOverBudgetOnly);
            layout.Controls.Add(_showUnderBudgetOnly);
            layout.Controls.Add(new Label { Text = "Group By:", Dock = DockStyle.Top });
            layout.Controls.Add(_groupByCombo);
            layout.Controls.Add(_showHierarchyCheck);
            layout.Controls.Add(applyFiltersBtn);
            layout.Controls.Add(clearFiltersBtn);

            applyFiltersBtn.Click += async (s, e) => await _viewModel.ApplyFiltersCommand.ExecuteAsync(null);
            clearFiltersBtn.Click += async (s, e) => await _viewModel.ClearFiltersCommand.ExecuteAsync(null);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateAnalysisPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(250, 250, 250)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                Padding = new Padding(5)
            };

            var titleLabel = new Label
            {
                Text = "📊 ANALYSIS",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(16, 124, 16),
                Padding = new Padding(0, 5, 0, 5)
            };

            _totalBudgetedLabel = CreateMetricLabel("Total Budgeted:", "$0.00");
            _totalActualLabel = CreateMetricLabel("Total Actual:", "$0.00");
            _totalVarianceLabel = CreateMetricLabel("Variance:", "$0.00");
            _percentUsedLabel = CreateMetricLabel("% Used:", "0.00%");
            _overBudgetCountLabel = CreateMetricLabel("Over Budget:", "0");
            _underBudgetCountLabel = CreateMetricLabel("Under Budget:", "0");

            _budgetProgressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 25,
                Style = ProgressBarStyle.Continuous,
                ForeColor = Color.FromArgb(0, 120, 212)
            };

            layout.Controls.Add(titleLabel);
            layout.Controls.Add(_totalBudgetedLabel);
            layout.Controls.Add(_totalActualLabel);
            layout.Controls.Add(_totalVarianceLabel);
            layout.Controls.Add(_percentUsedLabel);
            layout.Controls.Add(_budgetProgressBar);
            layout.Controls.Add(_overBudgetCountLabel);
            layout.Controls.Add(_underBudgetCountLabel);

            panel.Controls.Add(layout);
            return panel;
        }

        private Label CreateMetricLabel(string title, string value)
        {
            var label = new Label
            {
                Text = $"{title} {value}",
                Font = new Font("Segoe UI", 9),
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleLeft
            };
            return label;
        }

        private TabControl CreateTabControl()
        {
            var tabControl = new TabControl { Dock = DockStyle.Fill };

            // Data Grid Tab
            var gridTab = new TabPage("Data View");
            _dataGrid = CreateDataGrid();
            gridTab.Controls.Add(_dataGrid);

            // Trend Chart Tab
            var trendTab = new TabPage("Trend Analysis");
            _trendChart = new ChartControl { Dock = DockStyle.Fill };
            trendTab.Controls.Add(_trendChart);

            // Variance Chart Tab
            var varianceTab = new TabPage("Variance Analysis");
            _varianceChart = new ChartControl { Dock = DockStyle.Fill };
            varianceTab.Controls.Add(_varianceChart);

            tabControl.TabPages.AddRange(new[] { gridTab, trendTab, varianceTab });
            tabControl.SelectedIndexChanged += (s, e) => UpdateCharts();

            return tabControl;
        }

        private SfDataGrid CreateDataGrid()
        {
            var grid = new SfDataGrid
            {
                Dock = DockStyle.Fill,
                AllowEditing = false,
                AllowFiltering = true,
                AllowSorting = true,
                AllowGrouping = true,
                AllowResizingColumns = true,
                AllowResizingHiddenColumns = true,
                SelectionMode = GridSelectionMode.Single,
                AutoGenerateColumns = false,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.White
            };

            // Apply modern styling to the grid
            grid.Style.HeaderStyle.BackColor = Color.FromArgb(240, 240, 240);
            grid.Style.HeaderStyle.Font.Bold = true;
            grid.Style.HeaderStyle.Font.Size = 9F;

            // Define columns
            grid.Columns.Add(new GridTextColumn { MappingName = nameof(BudgetEntry.AccountNumber), HeaderText = "Account #", Width = 100 });
            grid.Columns.Add(new GridTextColumn { MappingName = nameof(BudgetEntry.Description), HeaderText = "Description", Width = 250 });
            grid.Columns.Add(new GridNumericColumn { MappingName = nameof(BudgetEntry.BudgetedAmount), HeaderText = "Budgeted", Format = "C2", Width = 120 });
            grid.Columns.Add(new GridNumericColumn { MappingName = nameof(BudgetEntry.ActualAmount), HeaderText = "Actual", Format = "C2", Width = 120 });
            grid.Columns.Add(new GridNumericColumn { MappingName = nameof(BudgetEntry.Variance), HeaderText = "Variance", Format = "C2", Width = 120 });
            grid.Columns.Add(new GridNumericColumn { MappingName = nameof(BudgetEntry.EncumbranceAmount), HeaderText = "Encumbrance", Format = "C2", Width = 120 });
            grid.Columns.Add(new GridTextColumn { MappingName = nameof(BudgetEntry.FiscalYear), HeaderText = "FY", Width = 70 });
            grid.Columns.Add(new GridTextColumn { MappingName = nameof(BudgetEntry.FundType), HeaderText = "Fund Type", Width = 100 });

            // Conditional formatting for variance
            grid.DrawCell += (s, e) =>
            {
                if (e.Column.MappingName == nameof(BudgetEntry.Variance) && e.DataRow != null)
                {
                    var entry = e.DataRow.RowData as BudgetEntry;
                    if (entry != null)
                    {
                        if (entry.Variance < 0)
                        {
                            e.Style.BackColor = Color.FromArgb(255, 230, 230); // Light red for over budget
                            e.Style.TextColor = Color.DarkRed;
                        }
                        else if (entry.Variance > 0)
                        {
                            e.Style.BackColor = Color.FromArgb(230, 255, 230); // Light green for under budget
                            e.Style.TextColor = Color.DarkGreen;
                        }
                    }
                }
            };

            // Add double-click to edit
            grid.CellDoubleClick += async (s, e) =>
            {
                if (e.DataRow != null)
                {
                    await EditEntryAsync();
                }
            };

            // Add selection changed event to update toolbar state
            grid.SelectionChanged += (s, e) =>
            {
                bool hasSelection = grid.SelectedItem != null;
                _editBtn.Enabled = hasSelection;
                _deleteBtn.Enabled = hasSelection;
                _copyToNextYearBtn.Enabled = hasSelection;
            };

            return grid;
        }

        private void SetupBindings()
        {
            _bindingSource = new BindingSource();

            // Add keyboard shortcuts
            this.KeyDown += async (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.N)
                {
                    e.Handled = true;
                    await AddEntryAsync();
                }
                else if (e.Control && e.KeyCode == Keys.E)
                {
                    e.Handled = true;
                    await EditEntryAsync();
                }
                else if (e.KeyCode == Keys.Delete)
                {
                    e.Handled = true;
                    await DeleteEntryAsync();
                }
                else if (e.KeyCode == Keys.F5)
                {
                    e.Handled = true;
                    await _viewModel.LoadByYearCommand.ExecuteAsync(null);
                }
                else if (e.Control && e.KeyCode == Keys.I)
                {
                    e.Handled = true;
                    await ImportDataAsync();
                }
                else if (e.Control && e.Shift && e.KeyCode == Keys.E)
                {
                    e.Handled = true;
                    await ExportDataAsync();
                }
            };

            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(BudgetViewModel.FilteredBudgetEntries))
                {
                    _bindingSource.DataSource = _viewModel.FilteredBudgetEntries;
                    _dataGrid.DataSource = _bindingSource;
                    UpdateCharts();
                    UpdateStatusBar();
                }
                else if (e.PropertyName == nameof(BudgetViewModel.BudgetEntries))
                {
                    _bindingSource.DataSource = _viewModel.BudgetEntries;
                    _dataGrid.DataSource = _bindingSource;
                    UpdateCharts();
                    UpdateStatusBar();
                }

                UpdateAnalysisPanel();
            };

            Load += async (s, e) =>
            {
                try
                {
                    await _viewModel.LoadBudgetsCommand.ExecuteAsync(null);
                    _bindingSource.DataSource = _viewModel.BudgetEntries;
                    _dataGrid.DataSource = _bindingSource;
                    UpdateCharts();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize");
                    MessageBox.Show($"Error loading budget data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
        }

        private void SetupFilters()
        {
            // Populate filter dropdowns
            _departmentFilter.Items.Add("All Departments");
            _fundTypeFilter.Items.Add("All Funds");
            _fundTypeFilter.Items.AddRange(Enum.GetNames(typeof(FundType)));
            _groupByCombo.Items.AddRange(new[] { "None", "Department", "Fund Type", "Fiscal Year" });

            _departmentFilter.SelectedIndex = 0;
            _fundTypeFilter.SelectedIndex = 0;
            _groupByCombo.SelectedIndex = 0;

            // Wire up filter change events
            _searchBox.TextChanged += (s, e) => _viewModel.SearchText = _searchBox.Text;
            _showOverBudgetOnly.CheckedChanged += (s, e) => _viewModel.ShowOnlyOverBudget = _showOverBudgetOnly.Checked;
            _showUnderBudgetOnly.CheckedChanged += (s, e) => _viewModel.ShowOnlyUnderBudget = _showUnderBudgetOnly.Checked;
            _varianceThresholdFilter.ValueChanged += (s, e) => _viewModel.VarianceThreshold = _varianceThresholdFilter.Value;
        }

        private void UpdateAnalysisPanel()
        {
            _totalBudgetedLabel.Text = $"💰 Total Budgeted: {_viewModel.TotalBudgeted:C2}";
            _totalActualLabel.Text = $"💵 Total Actual: {_viewModel.TotalActual:C2}";
            _totalVarianceLabel.Text = $"📊 Variance: {_viewModel.TotalVariance:C2}";
            _percentUsedLabel.Text = $"📈 % Used: {_viewModel.PercentUsed:F2}%";
            _overBudgetCountLabel.Text = $"🔴 Over Budget: {_viewModel.EntriesOverBudget}";
            _underBudgetCountLabel.Text = $"🟢 Under Budget: {_viewModel.EntriesUnderBudget}";

            var percentUsed = Math.Min(100, (int)_viewModel.PercentUsed);
            _budgetProgressBar.Value = percentUsed;

            // Color code the variance label and progress bar
            if (_viewModel.TotalVariance < 0)
            {
                _totalVarianceLabel.ForeColor = Color.DarkRed;
                _totalVarianceLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }
            else
            {
                _totalVarianceLabel.ForeColor = Color.DarkGreen;
                _totalVarianceLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }

            // Color code progress bar based on usage
            if (percentUsed >= 90)
                _budgetProgressBar.ForeColor = Color.Red;
            else if (percentUsed >= 75)
                _budgetProgressBar.ForeColor = Color.Orange;
            else
                _budgetProgressBar.ForeColor = Color.FromArgb(0, 120, 212);
        }

        private void UpdateStatusBar()
        {
            var recordCount = _viewModel.FilteredBudgetEntries.Any()
                ? _viewModel.FilteredBudgetEntries.Count
                : _viewModel.BudgetEntries.Count;

            if (_statusStrip.Items["recordCount"] is ToolStripStatusLabel recordLabel)
            {
                recordLabel.Text = $"📋 Records: {recordCount}";
            }

            if (_statusStrip.Items["filterStatus"] is ToolStripStatusLabel filterLabel)
            {
                var hasFilters = _viewModel.FilteredBudgetEntries.Any() &&
                                _viewModel.FilteredBudgetEntries.Count != _viewModel.BudgetEntries.Count;
                filterLabel.Text = hasFilters ? "🔍 Filters active" : "No filters";
                filterLabel.ForeColor = hasFilters ? Color.FromArgb(0, 120, 212) : Color.Gray;
            }
        }

        private void UpdateCharts()
        {
            if (_tabControl.SelectedIndex == 1)
                UpdateTrendChart();
            else if (_tabControl.SelectedIndex == 2)
                UpdateVarianceChart();
        }

        private void UpdateTrendChart()
        {
            if (_trendChart == null || _viewModel == null) return;

            try
            {
                _trendChart.Series.Clear();
                var entries = _viewModel.FilteredBudgetEntries.Any() ? _viewModel.FilteredBudgetEntries : _viewModel.BudgetEntries;

                var budgetSeries = new ChartSeries("Budgeted", ChartSeriesType.Line);
                var actualSeries = new ChartSeries("Actual", ChartSeriesType.Line);

                int idx = 0;
                foreach (var entry in entries.OrderBy(e => e.AccountNumber))
                {
                    idx++;
                    budgetSeries.Points.Add(new ChartPoint(idx, (double)entry.BudgetedAmount));
                    actualSeries.Points.Add(new ChartPoint(idx, (double)entry.ActualAmount));
                }

                _trendChart.Series.Add(budgetSeries);
                _trendChart.Series.Add(actualSeries);
                _trendChart.PrimaryXAxis.Title = "Budget Entries";
                _trendChart.PrimaryYAxis.Title = "Amount ($)";
                _trendChart.Title.Text = "Budget vs Actual Trend";
                _trendChart.ShowLegend = true;
                _trendChart.Refresh();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UpdateTrendChart failed");
            }
        }

        private void UpdateVarianceChart()
        {
            if (_varianceChart == null || _viewModel == null) return;

            try
            {
                _varianceChart.Series.Clear();
                var entries = _viewModel.FilteredBudgetEntries.Any() ? _viewModel.FilteredBudgetEntries : _viewModel.BudgetEntries;

                var varianceSeries = new ChartSeries("Variance", ChartSeriesType.Column);

                int idx = 0;
                foreach (var entry in entries.OrderBy(e => e.Variance))
                {
                    idx++;
                    varianceSeries.Points.Add(new ChartPoint(idx, (double)entry.Variance));
                }

                _varianceChart.Series.Add(varianceSeries);
                _varianceChart.PrimaryXAxis.Title = "Budget Entries";
                _varianceChart.PrimaryYAxis.Title = "Variance ($)";
                _varianceChart.Title.Text = "Budget Variance Analysis";
                _varianceChart.ShowLegend = true;
                _varianceChart.Refresh();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UpdateVarianceChart failed");
            }
        }

        // CRUD Operations
        private async Task AddEntryAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var deptRepo = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IDepartmentRepository>(scope.ServiceProvider);
                var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scope.ServiceProvider);
                var dlgLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<BudgetEntryDialog>>(scope.ServiceProvider);

                using var dlg = new BudgetEntryDialog(deptRepo, scopeFactory, dlgLogger);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    await _viewModel.AddEntryAsync(dlg.Entry);
                    _bindingSource.ResetBindings(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Add entry failed");
                MessageBox.Show($"Failed to add entry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task EditEntryAsync()
        {
            var selected = _dataGrid.SelectedItem as BudgetEntry;
            if (selected == null)
            {
                MessageBox.Show("Please select a budget entry to edit.", "Edit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var deptRepo = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IDepartmentRepository>(scope.ServiceProvider);
                var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scope.ServiceProvider);
                var dlgLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<BudgetEntryDialog>>(scope.ServiceProvider);

                using var dlg = new BudgetEntryDialog(deptRepo, scopeFactory, dlgLogger, selected);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    await _viewModel.UpdateEntryAsync(dlg.Entry);
                    _bindingSource.ResetBindings(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Edit entry failed");
                MessageBox.Show($"Failed to edit entry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DeleteEntryAsync()
        {
            var selected = _dataGrid.SelectedItem as BudgetEntry;
            if (selected == null)
            {
                MessageBox.Show("Please select a budget entry to delete.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to delete budget entry {selected.AccountNumber}?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm == DialogResult.Yes)
            {
                try
                {
                    await _viewModel.DeleteEntryAsync(selected.Id);
                    _bindingSource.ResetBindings(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Delete entry failed");
                    MessageBox.Show($"Failed to delete entry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task ImportDataAsync()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "CSV Files|*.csv|Excel Files|*.xlsx;*.xls|All Files|*.*",
                Title = "Import Budget Data"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    await _viewModel.ImportFromCsvCommand.ExecuteAsync(dlg.FileName);
                    MessageBox.Show("Import completed successfully.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Import failed");
                    MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task ExportDataAsync()
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "CSV Files|*.csv|Excel Files|*.xlsx|PDF Files|*.pdf",
                Title = "Export Budget Data",
                FileName = $"BudgetData_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var ext = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    if (ext == ".csv")
                        await _viewModel.ExportToCsvCommand.ExecuteAsync(dlg.FileName);
                    else if (ext == ".xlsx" || ext == ".xls")
                        await _viewModel.ExportToExcelCommand.ExecuteAsync(dlg.FileName);
                    else if (ext == ".pdf")
                        await _viewModel.ExportToPdfCommand.ExecuteAsync(dlg.FileName);

                    MessageBox.Show("Export completed successfully.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Export failed");
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task CopyToNextYearAsync()
        {
            var selected = _dataGrid.SelectedItem as BudgetEntry;
            if (selected == null)
            {
                MessageBox.Show("Please select a budget entry to copy.", "Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                await _viewModel.CopyToNextYearCommand.ExecuteAsync(selected);
                MessageBox.Show($"Entry copied to FY {selected.FiscalYear + 1}.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Copy to next year failed");
                MessageBox.Show($"Failed to copy entry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task BulkAdjustAsync()
        {
            using var inputDlg = new Form
            {
                Text = "Bulk Adjustment",
                Size = new Size(300, 150),
                StartPosition = FormStartPosition.CenterParent
            };

            var label = new Label { Text = "Adjustment Percentage:", Left = 10, Top = 10, Width = 260 };
            var numericUpDown = new NumericUpDown { Left = 10, Top = 40, Width = 260, DecimalPlaces = 2, Increment = 1, Minimum = -100, Maximum = 100 };
            var okButton = new Button { Text = "Apply", Left = 100, Top = 70, DialogResult = DialogResult.OK };
            var cancelButton = new Button { Text = "Cancel", Left = 180, Top = 70, DialogResult = DialogResult.Cancel };

            inputDlg.Controls.AddRange(new Control[] { label, numericUpDown, okButton, cancelButton });
            inputDlg.AcceptButton = okButton;
            inputDlg.CancelButton = cancelButton;

            if (inputDlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    await _viewModel.BulkAdjustCommand.ExecuteAsync(numericUpDown.Value);
                    MessageBox.Show($"Applied {numericUpDown.Value}% adjustment.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bulk adjust failed");
                    MessageBox.Show($"Failed to adjust entries: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _bindingSource?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
