using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Styles;
using Syncfusion.Windows.Forms.Gauge;
using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Budget Dashboard Form with KPI gauges, trend charts, and detailed budget analysis.
    /// Features DPI-aware layout, responsive design, and full Syncfusion theming integration.
    /// </summary>
    public partial class BudgetDashboardForm : SfForm
    {
        private readonly IThemeService _themeService;
        private readonly IDashboardViewModel _viewModel;
        private readonly ILogger<BudgetDashboardForm>? _logger;

        // Layout components
        private TableLayoutPanel? _rootTable;
        private SplitContainerAdv? _mainSplitContainer;
        private StatusStrip? _statusStrip;

        // Data visualization controls
        private ChartControl? _mainChart;
        private SfDataGrid? _detailsGrid;

        // Gauge controls (RadialGauge instances stored for binding)
        private RadialGauge? _budgetUtilizationGauge;
        private RadialGauge? _revenueGauge;
        private RadialGauge? _expenseGauge;
        private RadialGauge? _varianceGauge;
        private readonly ToolTip _sharedTooltip;
        private ToolStripStatusLabel? _statusLabel;

        public BudgetDashboardForm(IThemeService themeService, IDashboardViewModel viewModel, ILogger<BudgetDashboardForm>? logger = null)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _logger = logger;
            _sharedTooltip = new ToolTip();

            InitializeComponent();
            InitializeControls();

            // Subscribe to view model property changes to update gauges/charts
            try
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
            catch { }

            // Apply theme
            var themeName = _themeService.CurrentTheme;
            SfSkinManager.SetVisualStyle(this, themeName);

            _logger?.LogInformation("BudgetDashboardForm initialized with theme: {ThemeName}", themeName);
        }

        private void InitializeControls()
        {
            this.SuspendLayout();

            _rootTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };

            // Percentage-based rows → responsive height
            _rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(70f)));   // Header + ToolStrip
            _rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));                         // Gauges (now larger)
            _rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));                         // Chart + Grid
            _rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28f)));  // Status

            // Header + ToolStrip
            var headerPanel = new Panel { Dock = DockStyle.Fill };
            var headerLabel = new Label
            {
                Text = "Budget Dashboard",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };

            // Add toolbar for filtering and export actions
            var toolStrip = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(5, 0, 0, 0)
            };
            toolStrip.Items.Add(new ToolStripButton("Refresh") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText });
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(new ToolStripButton("Export to Excel") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText });
            toolStrip.Items.Add(new ToolStripButton("Export to PDF") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText });
            toolStrip.Items.Add(new ToolStripSeparator());

            var fiscalYearCombo = new ToolStripComboBox("FiscalYearFilter")
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(120, 25)
            };
            fiscalYearCombo.Items.AddRange(new object[] { "FY 2026", "FY 2025", "FY 2024" });
            fiscalYearCombo.SelectedIndex = 0;
            toolStrip.Items.Add(new ToolStripLabel("Fiscal Year:"));
            toolStrip.Items.Add(fiscalYearCombo);

            headerPanel.Controls.Add(toolStrip);
            headerPanel.Controls.Add(headerLabel);
            _rootTable.Controls.Add(headerPanel, 0, 0);

            // === Gauges Row – TableLayoutPanel with 4 equal columns ===
            var gaugesTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(20, 10, 20, 10),
                BackColor = Color.Transparent
            };
            for (int i = 0; i < 4; i++)
                gaugesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            // Create and add the four gauge cards
            var budgetCard = CreateGaugeCard("Budget Utilization", "Actual spending vs approved budget YTD", Color.FromArgb(33, 150, 243));
            var revenueCard = CreateGaugeCard("Revenue Collection", "% of projected revenue received YTD", Color.FromArgb(76, 175, 80));
            var expenseCard = CreateGaugeCard("Expense Ratio", "Expenses as % of total budget", Color.FromArgb(255, 152, 0));
            var varianceCard = CreateGaugeCard("Budget Variance", "Positive = under budget (good)", Color.FromArgb(156, 39, 176));

            gaugesTable.Controls.Add(budgetCard, 0, 0);
            gaugesTable.Controls.Add(revenueCard, 1, 0);
            gaugesTable.Controls.Add(expenseCard, 2, 0);
            gaugesTable.Controls.Add(varianceCard, 3, 0);

            _rootTable.Controls.Add(gaugesTable, 0, 1);

            // Chart + Grid split (60/40 default, user-resizable)
            _mainSplitContainer = new SplitContainerAdv
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = (Syncfusion.Windows.Forms.Tools.Enums.FixedPanel)System.Windows.Forms.FixedPanel.None,  // Both panels resize proportionally
                IsSplitterFixed = false,  // User can resize
                SplitterWidth = 5,
                // Min sizes are applied on Load to avoid ArgumentOutOfRange when the form
                // is hosted in a container with an unknown runtime size.
            };

            // Chart
            _mainChart = new ChartControl { Dock = DockStyle.Fill };
            ConfigureChart(_mainChart);
            _mainSplitContainer.Panel1.Controls.Add(_mainChart);

            // Grid
            _detailsGrid = new SfDataGrid { Dock = DockStyle.Fill };
            ConfigureDetailsGrid(_detailsGrid);
            _mainSplitContainer.Panel2.Controls.Add(_detailsGrid);
            // Populate grid/chart with initial sample data so controls show meaningful content
            PopulateSampleData();

            _rootTable.Controls.Add(_mainSplitContainer, 0, 2);

            // Status strip
            _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
            _statusLabel = new ToolStripStatusLabel("Ready");
            _statusStrip.Items.Add(_statusLabel);
            _rootTable.Controls.Add(_statusStrip, 0, 3);

            this.Controls.Add(_rootTable);
            this.ResumeLayout(true);

            // Apply theme to Syncfusion controls
            var themeName = _themeService.CurrentTheme;
            SfSkinManager.SetVisualStyle(_mainChart, themeName);
            SfSkinManager.SetVisualStyle(_detailsGrid, themeName);
            SfSkinManager.SetVisualStyle(_mainSplitContainer, themeName);

            // Set splitter to ~55% chart / 45% grid after first layout
            this.Load += async (s, e) =>
            {
                if (_mainSplitContainer != null && _mainSplitContainer.Height > 100)
                {
                    int chartHeight = (int)(_mainSplitContainer.Height * 0.55);
                    _mainSplitContainer.SplitterDistance = chartHeight;
                }

                // Load data from ViewModel
                try
                {
                    if (_viewModel is DashboardViewModel vm)
                    {
                        await vm.LoadCommand.ExecuteAsync(null);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load dashboard data");
                }

                // Update gauges and chart with real data
                UpdateGaugesFromViewModel();
                UpdateChart();

                // Update status with last updated time and fiscal year
                var fiscalYearCombo = headerPanel.Controls.OfType<ToolStrip>().FirstOrDefault()?.Items.OfType<ToolStripComboBox>().FirstOrDefault(c => c.Name == "FiscalYearFilter");
                if (_statusLabel != null)
                {
                    string fiscalYear = fiscalYearCombo?.SelectedItem?.ToString() ?? "2026";
                    _statusLabel.Text = $"Last updated: {_viewModel.LastUpdated:HH:mm} | FY {fiscalYear}";
                }
            };

            // Re-center gauges on resize to prevent cut-off
            this.Resize += (s, e) =>
            {
                // No longer needed with table layout
            };

            _logger?.LogDebug("BudgetDashboardForm controls initialized - Chart, Grid, and 4 gauge cards created");
        }

        private LegacyGradientPanel CreateGaugeCard(string title, string description, Color accent)
        {
            var card = new LegacyGradientPanel
            {
                Dock = DockStyle.Fill,                  // Important: fill the table cell
                Padding = new Padding(12),
                Margin = new Padding(8),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Title
            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(40),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = accent,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 8, 0, 0)
            };

            // Gauge
            var gauge = new RadialGauge
            {
                Dock = DockStyle.Fill,                  // Gauge now uses all remaining space
                NeedleColor = accent,
                MinimumValue = 0,
                MaximumValue = 100,
                Value = 0,
                ShowTicks = true,
                ShowScaleLabel = true,
                GaugeLabel = "0%"                      // Large centered text
            };

            ConfigureRadialGaugeRanges(gauge, title); // Custom ranges per metric

            // Tooltip on the entire card
            _sharedTooltip?.SetToolTip(card, description);
            _sharedTooltip?.SetToolTip(gauge, description);

            // Store reference for later data binding/updates
            switch (title)
            {
                case "Budget Utilization":
                    _budgetUtilizationGauge = gauge;
                    break;
                case "Revenue Collection":
                    _revenueGauge = gauge;
                    break;
                case "Expense Ratio":
                    _expenseGauge = gauge;
                    break;
                case "Budget Variance":
                    _varianceGauge = gauge;
                    break;
            }

            card.Controls.Add(gauge);
            card.Controls.Add(titleLabel);

            return card;
        }

        /// <summary>
        /// Configures a RadialGauge with consistent styling and ranges.
        /// </summary>
        private void ConfigureRadialGauge(RadialGauge gauge, Color accent)
        {
            gauge.MinimumValue = 0;
            gauge.MaximumValue = 100;
            gauge.Value = 0;
            gauge.ShowTicks = true;
            gauge.GaugeLabel = string.Empty;
            gauge.ShowScaleLabel = true;
            gauge.NeedleColor = accent;

            // Configure color ranges
            try
            {
                gauge.Ranges.Clear();
                gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range
                {
                    StartValue = 0,
                    EndValue = 60,
                    Color = Color.Green,
                    Height = 12,
                    InRange = true
                });
                gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range
                {
                    StartValue = 60,
                    EndValue = 85,
                    Color = Color.Orange,
                    Height = 12,
                    InRange = true
                });
                gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range
                {
                    StartValue = 85,
                    EndValue = 100,
                    Color = Color.Red,
                    Height = 12,
                    InRange = true
                });
            }
            catch { }
        }

        private void ConfigureRadialGaugeRanges(RadialGauge gauge, string title)
        {
            gauge.Ranges.Clear();

            // Different semantics per gauge
            if (title.Contains("Variance") || title.Contains("Utilization") || title.Contains("Expense"))
            {
                // Lower = better (green high at low values)
                gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range { StartValue = 0, EndValue = 60, Color = Color.Green });
                gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range { StartValue = 60, EndValue = 85, Color = Color.Orange });
                gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range { StartValue = 85, EndValue = 100, Color = Color.Red });
            }
            else // Revenue Collection – higher = better
            {
                gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range { StartValue = 0, EndValue = 40, Color = Color.Red });
                gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range { StartValue = 40, EndValue = 75, Color = Color.Orange });
                gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range { StartValue = 75, EndValue = 100, Color = Color.Green });
            }
        }

        private void ConfigureChart(ChartControl chart)
        {
            chart.Title.Text = "Budget Trend";
            chart.PrimaryXAxis.Title = "Month";
            chart.PrimaryYAxis.Title = "Amount ($)";
            chart.Series.Clear();

            // Use category axis for month labels and enable tooltips. Actual data should be
            // supplied via CategoryAxisDataBindModel (see PopulateSampleData) so we configure
            // the axis type to match the binding model.
            try { chart.PrimaryXAxis.ValueType = ChartValueType.Category; } catch { }
            chart.ShowToolTips = true;
            // Enable user interaction features: mouse-wheel zoom, reset on double-click, and toolbar
            try
            {
                chart.ZoomType = ZoomType.MouseWheelZooming;
            }
            catch { }
            try
            {
                chart.ResetOnDoubleClick = true;
            }
            catch { }
            try
            {
                chart.ShowToolbar = true;
            }
            catch { }

            var series = new ChartSeries("Budget vs Actual") { Type = ChartSeriesType.Column };
            chart.Series.Add(series);

            _logger?.LogDebug("ChartControl configured (axes, tooltips) and placeholder series added");
        }

        private void ConfigureDetailsGrid(SfDataGrid grid)
        {
            grid.AutoGenerateColumns = false;
            grid.AllowEditing = false;
            grid.AllowResizingColumns = true;
            grid.AllowSorting = true;
            grid.AllowFiltering = true;

            // CRITICAL FIX: Prevent relational operators on string columns to avoid InvalidOperationException
            grid.FilterChanging += DetailsGrid_FilterChanging;

            // Bind directly to DepartmentSummaries from the view model
            try
            {
                grid.DataSource = _viewModel.DepartmentSummaries;
            }
            catch { /* Best-effort binding; sample population will follow if empty */ }

            // Columns - mapped to DepartmentSummary properties
            grid.Columns.Add(new GridTextColumn { MappingName = "DepartmentName", HeaderText = "Category", Width = 180 });
            grid.Columns.Add(new GridNumericColumn { MappingName = "TotalBudgeted", HeaderText = "Budgeted", Format = "C0", Width = 130 });
            grid.Columns.Add(new GridNumericColumn { MappingName = "TotalActual", HeaderText = "Actual", Format = "C0", Width = 130 });
            grid.Columns.Add(new GridNumericColumn { MappingName = "Variance", HeaderText = "Variance", Format = "C0", Width = 130 });

            // Unbound Status column with expression (assumes Variance = Budgeted - Actual -> positive = under budget)
            try
            {
                var statusColumn = new GridUnboundColumn
                {
                    MappingName = "Status",
                    HeaderText = "Status",
                    Width = 110,
                    Expression = "IIF([Variance] >= 0, IIF(Abs([VariancePercentage]) > 5, 'Under', 'On Target'), 'Over')"
                };
                grid.Columns.Add(statusColumn);
            }
            catch { /* Older Syncfusion versions may not support unbound expressions; ignore */ }

            _logger?.LogDebug("SfDataGrid configured with 5 columns: Category, Budgeted, Actual, Variance, Status");
        }

        /// <summary>
        /// Prevents invalid relational filters on string columns to fix System.InvalidOperationException:
        /// "The binary operator GreaterThan is not defined for the types 'System.String' and 'System.String'"
        /// </summary>
        private void DetailsGrid_FilterChanging(object? sender, Syncfusion.WinForms.DataGrid.Events.FilterChangingEventArgs e)
        {
            if (e?.Column?.MappingName == null)
            {
                return;
            }

            // String columns that should not allow relational comparison operators
            var isStringColumn =
                e.Column.MappingName == "DepartmentName" ||
                e.Column.MappingName == "Status";  // Status is unbound column with string result

            if (!isStringColumn)
            {
                return;
            }

            // Check if any predicate uses relational operators (GreaterThan, LessThan, etc.)
            var hasRelationalPredicate = e.FilterPredicates.Any(p =>
                p.FilterType == Syncfusion.Data.FilterType.GreaterThan ||
                p.FilterType == Syncfusion.Data.FilterType.GreaterThanOrEqual ||
                p.FilterType == Syncfusion.Data.FilterType.LessThan ||
                p.FilterType == Syncfusion.Data.FilterType.LessThanOrEqual);

            if (!hasRelationalPredicate)
            {
                return;
            }

            // Cancel the filter and log the prevention
            e.Cancel = true;
            _logger?.LogDebug("BudgetDashboardForm: Cancelled invalid relational filter on string column {Column}", e.Column.MappingName);
        }

        // Lightweight model used for initial population in the dashboard preview
        private sealed class BudgetRow
        {
            public string Category { get; set; } = string.Empty;
            public decimal Budgeted { get; set; }
            public decimal Actual { get; set; }
            public decimal Variance { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        private sealed class ChartMonth
        {
            public string Month { get; set; } = string.Empty;
            public double Budget { get; set; }
            public double Actual { get; set; }
        }

        /// <summary>
        /// Populate sample data for the grid and a minimal chart series so the UI shows content
        /// and demonstrates correct binding patterns.
        /// </summary>
        private void PopulateSampleData()
        {
            try
            {
                // Populate sample DepartmentSummaries into the ViewModel when empty
                try
                {
                    if (_viewModel.DepartmentSummaries == null || _viewModel.DepartmentSummaries.Count == 0)
                    {
                        _viewModel.DepartmentSummaries.Clear();
                        void addDept(string name, decimal budgeted, decimal actual)
                        {
                            var variance = budgeted - actual; // positive = under budget
                            var pct = budgeted != 0 ? Math.Round((variance / budgeted) * 100m, 1) : 0m;
                            _viewModel.DepartmentSummaries.Add(new DepartmentSummary
                            {
                                DepartmentName = name,
                                TotalBudgeted = budgeted,
                                TotalActual = actual,
                                Variance = variance,
                                VariancePercentage = pct
                            });
                        }

                        addDept("Salaries", 120000m, 115000m);
                        addDept("Office Supplies", 10000m, 12000m);
                        addDept("Travel", 25000m, 20000m);
                        addDept("Consulting", 45000m, 47000m);
                        addDept("Maintenance", 8000m, 6000m);
                    }
                }
                catch { /* best-effort sample population */ }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "PopulateSampleData failed to populate SfDataGrid");
            }

            // Update gauges with sample values
            try
            {
                if (_budgetUtilizationGauge != null)
                {
                    _budgetUtilizationGauge.Value = 54.8f;
                    _budgetUtilizationGauge.GaugeLabel = "55%";
                }
                if (_revenueGauge != null)
                {
                    _revenueGauge.Value = 78.3f;
                    _revenueGauge.GaugeLabel = "78%";
                }
                if (_expenseGauge != null)
                {
                    _expenseGauge.Value = 42.1f;
                    _expenseGauge.GaugeLabel = "42%";
                }
                if (_varianceGauge != null)
                {
                    _varianceGauge.Value = 12.5f;
                    _varianceGauge.GaugeLabel = "13%";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "PopulateSampleData failed to update gauges");
            }

            // Chart population — use CategoryAxisDataBindModel (recommended Syncfusion pattern)
            try
            {
                if (_mainChart == null) return;

                var months = new System.ComponentModel.BindingList<ChartMonth>
                {
                    new ChartMonth { Month = "Jan", Budget = 100000, Actual = 98000 },
                    new ChartMonth { Month = "Feb", Budget = 105000, Actual = 107000 },
                    new ChartMonth { Month = "Mar", Budget = 110000, Actual = 112500 },
                    new ChartMonth { Month = "Apr", Budget = 115000, Actual = 117000 },
                    new ChartMonth { Month = "May", Budget = 120000, Actual = 122500 },
                    new ChartMonth { Month = "Jun", Budget = 125000, Actual = 123000 },
                };

                var bindBudget = new CategoryAxisDataBindModel(months)
                {
                    CategoryName = nameof(ChartMonth.Month),
                    YNames = new[] { nameof(ChartMonth.Budget) }
                };

                var bindActual = new CategoryAxisDataBindModel(months)
                {
                    CategoryName = nameof(ChartMonth.Month),
                    YNames = new[] { nameof(ChartMonth.Actual) }
                };

                _mainChart.BeginUpdate();
                _mainChart.Series.Clear();

                var budgetSeries = new ChartSeries("Budget", ChartSeriesType.Column)
                {
                    CategoryModel = bindBudget
                };

                var actualSeries = new ChartSeries("Actual", ChartSeriesType.Column)
                {
                    CategoryModel = bindActual
                };

                _mainChart.Series.Add(budgetSeries);
                _mainChart.Series.Add(actualSeries);
                _mainChart.EndUpdate();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "PopulateSampleData failed to populate ChartControl");
            }
        }

        private void UpdateGaugesFromViewModel()
        {
            try
            {
                if (_budgetUtilizationGauge != null)
                {
                    _budgetUtilizationGauge.Value = _viewModel.TotalBudgetGauge;
                    _budgetUtilizationGauge.GaugeLabel = $"{_viewModel.TotalBudgetGauge:F0}%";
                }
                if (_revenueGauge != null)
                {
                    _revenueGauge.Value = _viewModel.RevenueGauge;
                    _revenueGauge.GaugeLabel = $"{_viewModel.RevenueGauge:F0}%";
                }
                if (_expenseGauge != null)
                {
                    _expenseGauge.Value = _viewModel.ExpensesGauge;
                    _expenseGauge.GaugeLabel = $"{_viewModel.ExpensesGauge:F0}%";
                }
                if (_varianceGauge != null)
                {
                    _varianceGauge.Value = _viewModel.NetPositionGauge;
                    _varianceGauge.GaugeLabel = $"{_viewModel.NetPositionGauge:F0}%";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to update gauges from ViewModel");
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(e?.PropertyName) ||
                    e.PropertyName.Contains("Gauge") ||
                    e.PropertyName == nameof(IDashboardViewModel.StatusText) ||
                    e.PropertyName == nameof(IDashboardViewModel.LastUpdated))
                {
                    this.BeginInvoke((System.Action)UpdateGaugesFromViewModel);
                }

                if (string.IsNullOrEmpty(e?.PropertyName) || e.PropertyName.Contains("Monthly") || e.PropertyName.Contains("MonthlySummaries"))
                {
                    this.BeginInvoke((System.Action)UpdateChart);
                }
            }
            catch { }
        }

        private void UpdateChart()
        {
            if (_mainChart == null) return;

            try
            {
                if (_viewModel.MonthlySummaries == null || _viewModel.MonthlySummaries.Count == 0)
                {
                    return;
                }

                _mainChart.BeginUpdate();
                _mainChart.Series.Clear();

                var budgetSeries = new ChartSeries("Budgeted", ChartSeriesType.Column)
                {
                    Style = { Interior = new Syncfusion.Drawing.BrushInfo(Color.FromArgb(255, 152, 0)) }
                };

                var actualSeries = new ChartSeries("Actual", ChartSeriesType.Column)
                {
                    Style = { Interior = new Syncfusion.Drawing.BrushInfo(Color.FromArgb(33, 150, 243)) }
                };

                var budgetModel = new CategoryAxisDataBindModel(_viewModel.MonthlySummaries)
                {
                    CategoryName = nameof(MonthlyBudgetSummary.Month),
                    YNames = new[] { nameof(MonthlyBudgetSummary.Budgeted) }
                };

                var actualModel = new CategoryAxisDataBindModel(_viewModel.MonthlySummaries)
                {
                    CategoryName = nameof(MonthlyBudgetSummary.Month),
                    YNames = new[] { nameof(MonthlyBudgetSummary.Actual) }
                };

                budgetSeries.CategoryModel = budgetModel;
                actualSeries.CategoryModel = actualModel;

                _mainChart.Series.Add(budgetSeries);
                _mainChart.Series.Add(actualSeries);

                _mainChart.EndUpdate();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "UpdateChart failed");
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Budget Dashboard";
            this.Size = new Size(1400, 900);
            this.MinimumSize = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.ShowInTaskbar = true;
            this.WindowState = FormWindowState.Normal;

            try
            {
                this.AutoScaleMode = AutoScaleMode.Dpi;
            }
            catch
            {
                // Fallback if DPI scaling not supported
            }

            this.ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _rootTable?.Dispose();
                _mainSplitContainer?.Dispose();
                _statusStrip?.Dispose();
                _mainChart?.Dispose();
                _detailsGrid?.Dispose();
                _budgetUtilizationGauge?.Dispose();
                _revenueGauge?.Dispose();
                _expenseGauge?.Dispose();
                _varianceGauge?.Dispose();
                _sharedTooltip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
