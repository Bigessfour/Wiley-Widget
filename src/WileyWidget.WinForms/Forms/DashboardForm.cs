using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Styles;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms
{
    internal static class DashboardResources
    {
        public const string FormTitle = "Dashboard - Wiley Widget";
        public const string LoadButton = "Load Dashboard";
        public const string RefreshButton = "Refresh";
        public const string ExportButton = "Export";
        public const string LoadingText = "Loading dashboard...";
        public const string MunicipalityLabel = "Municipality:";
        public const string FiscalYearLabel = "Fiscal Year:";
        public const string LastUpdatedLabel = "Last Updated:";
        public const string ErrorTitle = "Error";
        public const string LoadErrorMessage = "Error loading dashboard: {0}";
        public const string MetricsGridTitle = "Key Performance Metrics";
        public const string RevenueTrendTitle = "Revenue Trend";
        public const string StatusReady = "Ready";
        public const string StatusExported = "Dashboard exported";
        public const string StatusRefreshed = "Dashboard refreshed";
        public const string StatusAutoRefresh = "Auto-refresh: {0}";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class DashboardForm : Form
    {
        private readonly DashboardViewModel _viewModel;
        private TableLayoutPanel? _mainLayout;
        private SfDataGrid? _metricsGrid;
        private ChartControl? _revenueChart;
        private RadialGauge? _budgetGauge;
        private RadialGauge? _revenueGauge;
        private RadialGauge? _expensesGauge;
        private RadialGauge? _netPositionGauge;
        private ToolStripEx? _toolbar;
        private StatusBarAdv? _statusBar;
        private StatusBarAdvPanel? _statusPanel;
        private StatusBarAdvPanel? _countsPanel;
        private StatusBarAdvPanel? _updatedPanel;
        private Label? _municipalityLabel;
        private Label? _fiscalYearLabel;
        private Label? _lastUpdatedLabel;
        private System.Windows.Forms.Timer? _refreshTimer;
        private CheckBox? _autoRefreshCheckbox;

        private const int RefreshIntervalMs = 30000;

        public DashboardForm(DashboardViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            SetupUI();
            SfSkinManager.SetVisualStyle(this, "Office2019Colorful");
            ThemeColors.ApplyTheme(this);
            BindViewModel();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            LoadDashboard();
#pragma warning restore CS4014
        }

        [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "System.Windows.Forms.Form.set_Text")]
        private void InitializeComponent()
        {
            Text = DashboardResources.FormTitle;
            Size = new Size(1400, 900);
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void SetupUI()
        {
            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(10)
            };

            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));  // Toolbar
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));   // Header info
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));  // KPI Gauges
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));    // Chart
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));    // Metrics Grid
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));   // Status bar

            BuildToolbar();

            _refreshTimer = new System.Windows.Forms.Timer { Interval = RefreshIntervalMs };
            _refreshTimer.Tick += async (s, e) =>
            {
                await _viewModel.RefreshCommand.ExecuteAsync(null);
                UpdateStatus(DashboardResources.StatusRefreshed);
            };
            _refreshTimer.Start();

            // Header panel
            var headerPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = ThemeColors.Background
            };

            _municipalityLabel = new Label { Name = "MunicipalityLabel", Text = $"{DashboardResources.MunicipalityLabel} Loading...", AutoSize = true, Margin = new Padding(10, 5, 20, 5) };
            _fiscalYearLabel = new Label { Name = "FiscalYearLabel", Text = $"{DashboardResources.FiscalYearLabel} Loading...", AutoSize = true, Margin = new Padding(0, 5, 20, 5) };
            _lastUpdatedLabel = new Label { Name = "LastUpdatedLabel", Text = $"{DashboardResources.LastUpdatedLabel} Loading...", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };

            headerPanel.Controls.AddRange(new Control[] { _municipalityLabel, _fiscalYearLabel, _lastUpdatedLabel });
            _mainLayout.Controls.Add(headerPanel, 0, 1);

            // KPI Gauges Panel
            var gaugePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(10)
            };

            _budgetGauge = CreateGauge("Total Budget", ThemeColors.PrimaryAccent);
            _revenueGauge = CreateGauge("Revenue", ThemeColors.Success);
            _expensesGauge = CreateGauge("Expenses", ThemeColors.Error);
            _netPositionGauge = CreateGauge("Net Position", ThemeColors.Warning);

            gaugePanel.Controls.AddRange(new Control[] { _budgetGauge, _revenueGauge, _expensesGauge, _netPositionGauge });
            _mainLayout.Controls.Add(gaugePanel, 0, 2);

            // Revenue Trend Chart
            _revenueChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                Text = DashboardResources.RevenueTrendTitle
            };
            _revenueChart.PrimaryXAxis.Title = "Month";
            _revenueChart.PrimaryYAxis.Title = "Amount ($)";
            _revenueChart.Series.Add(new ChartSeries("Revenue", ChartSeriesType.Line));

            _mainLayout.Controls.Add(_revenueChart, 0, 3);

            // Metrics Grid using Syncfusion SfDataGrid with performance optimizations
            _metricsGrid = new SfDataGrid
            {
                Name = "MetricsGrid",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = false,
                AllowFiltering = true,
                AllowSorting = true,
                AllowResizingColumns = true,
                AllowResizingHiddenColumns = true,
                SelectionMode = GridSelectionMode.Single,
                NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Row,
                AllowGrouping = true,
                ShowGroupDropArea = false,
                // Performance optimizations
                EnableDataVirtualization = true,
                AutoSizeColumnsMode = AutoSizeColumnsMode.None,
                // Enable sorting with initial configuration
                ShowBusyIndicator = true
                // Theme inherited from form's SfSkinManager.SetVisualStyle
            };

            SfSkinManager.SetVisualStyle(_metricsGrid, ThemeColors.DefaultTheme);

            // Configure columns with proper formatting
            _metricsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Name",
                HeaderText = "Metric",
                Width = 200,
                AllowSorting = true
            });

            var valueColumn = new GridNumericColumn
            {
                MappingName = "Value",
                HeaderText = "Value",
                Width = 120,
                NumberFormatInfo = new System.Globalization.NumberFormatInfo { NumberDecimalDigits = 2 },
                AllowSorting = true
            };
            _metricsGrid.Columns.Add(valueColumn);

            _metricsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Unit",
                HeaderText = "Unit",
                Width = 80
            });

            _metricsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Unit",
                HeaderText = "Unit",
                Width = 80,
                AllowSorting = false
                // TextAlignment property not available in this version
            });

            var changeColumn = new GridNumericColumn
            {
                MappingName = "ChangePercent",
                HeaderText = "Change %",
                Width = 100,
                NumberFormatInfo = new System.Globalization.NumberFormatInfo { NumberDecimalDigits = 1 },
                AllowSorting = true
            };
            _metricsGrid.Columns.Add(changeColumn);

            _metricsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Description",
                HeaderText = "Description",
                Width = 300,
                AllowFiltering = true
            });

            // Configure initial sorting (descending by Value)
            _metricsGrid.SortColumnDescriptions.Add(new SortColumnDescription
            {
                ColumnName = "Value",
                SortDirection = ListSortDirection.Descending
            });

            // Apply theme to the metrics grid
            ThemeColors.ApplySfDataGridTheme(_metricsGrid);

            // Note: Styling is now handled by ThemeName property.
            // All colors, fonts, and styles are managed by SkinManager dynamically.

            var metricsPanel = new Panel { Dock = DockStyle.Fill };
            var metricsLabel = new Label { Text = DashboardResources.MetricsGridTitle, Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
            metricsPanel.Controls.Add(_metricsGrid);
            metricsPanel.Controls.Add(metricsLabel);

            _mainLayout.Controls.Add(metricsPanel, 0, 4);

            BuildStatusBar();
            if (_statusBar != null)
            {
                _mainLayout.Controls.Add(_statusBar, 0, 5);
            }

            Controls.Add(_mainLayout);
        }

        private void BuildToolbar()
        {
            _toolbar = new ToolStripEx
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(20, 20),
                Padding = new Padding(8, 4, 8, 4),
                Office12Mode = false
            };
            try { SfSkinManager.SetVisualStyle(_toolbar, ThemeColors.DefaultTheme); } catch { }

            var loadButton = new ToolStripButton
            {
                Text = DashboardResources.LoadButton,
                Name = "Toolbar_LoadButton",
                AutoSize = false,
                Width = 120,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            loadButton.Click += async (s, e) => await LoadDashboard();

            var refreshButton = new ToolStripButton
            {
                Text = DashboardResources.RefreshButton,
                Name = "Toolbar_RefreshButton",
                AutoSize = false,
                Width = 100,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            refreshButton.Click += async (s, e) => await _viewModel.RefreshCommand.ExecuteAsync(null);

            var exportButton = new ToolStripButton
            {
                Text = DashboardResources.ExportButton,
                Name = "Toolbar_ExportButton",
                AutoSize = false,
                Width = 90,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            exportButton.Click += async (s, e) => await ExportDashboard();

            _autoRefreshCheckbox = new CheckBox
            {
                Name = "AutoRefreshCheckbox",
                Text = "Auto-refresh (30s)",
                Checked = true,
                Padding = new Padding(5, 0, 5, 0),
                AutoSize = true
            };
            _autoRefreshCheckbox.CheckedChanged += (s, e) =>
            {
                ToggleAutoRefresh(_autoRefreshCheckbox.Checked);
                UpdateStatus(string.Format(CultureInfo.CurrentCulture, DashboardResources.StatusAutoRefresh, _autoRefreshCheckbox.Checked ? "On" : "Off"));
            };
            var autoRefreshHost = new ToolStripControlHost(_autoRefreshCheckbox)
            {
                Margin = new Padding(6, 0, 0, 0)
            };

            _toolbar.Items.Add(loadButton);
            _toolbar.Items.Add(refreshButton);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(exportButton);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(autoRefreshHost);

            _mainLayout?.Controls.Add(_toolbar, 0, 0);
        }

        private void BuildStatusBar()
        {
            try
            {
                _statusBar = new StatusBarAdv
                {
                    Dock = DockStyle.Fill,
                    BeforeTouchSize = new Size(0, 28),
                    SizingGrip = false
                };
                try { SfSkinManager.SetVisualStyle(_statusBar, ThemeColors.DefaultTheme); } catch { }

                _statusPanel = new StatusBarAdvPanel
                {
                    Text = DashboardResources.StatusReady,
                    BorderStyle = BorderStyle.None,
                    Width = 450
                };

                _countsPanel = new StatusBarAdvPanel
                {
                    Text = "0 metrics",
                    BorderStyle = BorderStyle.None,
                    Width = 220
                };

                _updatedPanel = new StatusBarAdvPanel
                {
                    Text = "Updated: --",
                    BorderStyle = BorderStyle.None,
                    Width = 220,
                    Alignment = HorizontalAlignment.Left
                };

                _statusBar.Panels = new StatusBarAdvPanel[] { _statusPanel, _countsPanel, _updatedPanel };
            }
            catch (Exception ex)
            {
                // Log error if logger is available
                var logger = Program.Services.GetService<ILogger<DashboardForm>>();
                logger?.LogError(ex, "Failed to build dashboard status bar");

                // Fallback: Create basic status bar
                try
                {
                    _statusBar = new StatusBarAdv { Dock = DockStyle.Fill };
                    _statusPanel = new StatusBarAdvPanel { Text = "Status bar initialization failed" };
                    _statusBar.Panels = new StatusBarAdvPanel[] { _statusPanel };
                }
                catch { }
            }
        }        private RadialGauge CreateGauge(string label, Color needleColor)
        {
            var gauge = new RadialGauge
            {
                Width = 180,
                Height = 180,
                Margin = new Padding(5),
                // Theme inherited from form's SfSkinManager.SetVisualStyle
                MinimumValue = 0F,
                MaximumValue = 100F,
                MajorDifference = 20F,
                MinorDifference = 5F,
                MinorTickMarkHeight = 5,
                MajorTickMarkHeight = 10,
                NeedleStyle = NeedleStyle.Advanced,
                ShowScaleLabel = true,
                LabelPlacement = Syncfusion.Windows.Forms.Gauge.LabelPlacement.Inside,
                NeedleColor = needleColor,
                Value = 0F,
                GaugeLabel = label,
                ShowNeedle = true,
                EnableCustomNeedles = false,
                GaugeArcColor = ThemeColors.GaugeArc,
                ShowBackgroundFrame = true
            };

            return gauge;
        }



        private void BindViewModel()
        {
            _viewModel.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(_viewModel.MunicipalityName):
                        if (_municipalityLabel != null)
                            _municipalityLabel.Text = $"{DashboardResources.MunicipalityLabel} {_viewModel.MunicipalityName}";
                        break;
                    case nameof(_viewModel.FiscalYear):
                        if (_fiscalYearLabel != null)
                            _fiscalYearLabel.Text = $"{DashboardResources.FiscalYearLabel} {_viewModel.FiscalYear}";
                        break;
                    case nameof(_viewModel.LastUpdated):
                        var lastUpdatedText = _viewModel.LastUpdated == default ? "--" : _viewModel.LastUpdated.ToString("g", CultureInfo.CurrentCulture);
                        if (_lastUpdatedLabel != null)
                            _lastUpdatedLabel.Text = $"{DashboardResources.LastUpdatedLabel} {lastUpdatedText}";
                        if (_updatedPanel != null)
                            _updatedPanel.Text = $"Updated: {lastUpdatedText}";
                        break;
                    case nameof(_viewModel.IsLoading):
                        UpdateStatus(_viewModel.IsLoading ? DashboardResources.LoadingText : DashboardResources.StatusReady);
                        break;
                    case nameof(_viewModel.ErrorMessage):
                        if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
                        {
                            UpdateStatus(_viewModel.ErrorMessage);
                        }
                        break;
                    case nameof(_viewModel.Metrics):
                        if (_metricsGrid != null)
                            _metricsGrid.DataSource = _viewModel.Metrics;
                        if (_countsPanel != null)
                            _countsPanel.Text = $"{_viewModel.Metrics.Count} metrics";
                        break;
                    case nameof(_viewModel.TotalBudgetGauge):
                        AnimateGaugeValue(_budgetGauge, _viewModel.TotalBudgetGauge);
                        break;
                    case nameof(_viewModel.RevenueGauge):
                        AnimateGaugeValue(_revenueGauge, _viewModel.RevenueGauge);
                        break;
                    case nameof(_viewModel.ExpensesGauge):
                        AnimateGaugeValue(_expensesGauge, _viewModel.ExpensesGauge);
                        break;
                    case nameof(_viewModel.NetPositionGauge):
                        AnimateGaugeValue(_netPositionGauge, _viewModel.NetPositionGauge);
                        break;
                    case nameof(_viewModel.StatusText):
                        UpdateStatus(_viewModel.StatusText);
                        break;
                    case nameof(_viewModel.MonthlyRevenueData):
                        UpdateRevenueChart();
                        break;
                }
            };
        }

        private void UpdateRevenueChart()
        {
            if (_revenueChart == null || _viewModel.MonthlyRevenueData.Count == 0)
                return;

            _revenueChart.Series.Clear();
            var series = new ChartSeries("Revenue", ChartSeriesType.Line);
            series.Style.Interior = new BrushInfo(ThemeColors.PrimaryAccent);

            foreach (var data in _viewModel.MonthlyRevenueData)
            {
                series.Points.Add(data.MonthNumber, (double)data.Amount);
            }

            _revenueChart.Series.Add(series);
            _revenueChart.Refresh();
        }

        private void AnimateGaugeValue(RadialGauge? gauge, float value)
        {
            if (gauge == null)
            {
                return;
            }

            var target = Math.Max(gauge.MinimumValue, Math.Min(value, gauge.MaximumValue));
            var start = gauge.Value;
            var steps = 15;
            var stepValue = (target - start) / steps;
            var timer = new System.Windows.Forms.Timer { Interval = 16 };
            var currentStep = 0;

            timer.Tick += (s, e) =>
            {
                if (gauge.IsDisposed)
                {
                    timer.Stop();
                    timer.Dispose();
                    return;
                }

                currentStep++;
                var next = start + (stepValue * currentStep);
                gauge.Value = currentStep >= steps ? target : next;

                if (currentStep >= steps)
                {
                    timer.Stop();
                    timer.Dispose();
                }
            };

            timer.Start();
        }

        private async Task LoadDashboard()
        {
            try
            {
                await _viewModel.LoadCommand.ExecuteAsync(null);

                UpdateStatus(DashboardResources.StatusReady);
            }
            catch (Exception ex)
            {
                UpdateStatus(string.Format(CultureInfo.CurrentCulture, DashboardResources.LoadErrorMessage, ex.Message));

                MessageBox.Show(string.Format(CultureInfo.CurrentCulture, DashboardResources.LoadErrorMessage, ex.Message),
                    DashboardResources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ExportDashboard()
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx",
                DefaultExt = "csv",
                FileName = $"Dashboard_{_viewModel.MunicipalityName.Replace(" ", "_", StringComparison.Ordinal)}_{_viewModel.FiscalYear.Replace(" ", "_", StringComparison.Ordinal)}.csv"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        if (saveDialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        {
                            // Export to CSV
                            var csv = new System.Text.StringBuilder();
                            csv.AppendLine("Metric,Value,Unit,Trend,Change %,Description");

                            foreach (var metric in _viewModel.Metrics)
                            {
                                string line = string.Format(CultureInfo.InvariantCulture,
                                "{0},{1},{2},{3},{4},{5}",
                                EscapeCsv(metric.Name),
                                metric.Value,
                                EscapeCsv(metric.Unit),
                                EscapeCsv(metric.Trend),
                                metric.ChangePercent,
                                EscapeCsv(metric.Description));
                            csv.AppendLine(line);
                            }

                            System.IO.File.WriteAllText(saveDialog.FileName, csv.ToString());
                        }
                        else
                        {
                            // Excel export requires Syncfusion.XlsIO - placeholder for now
                            MessageBox.Show("Excel export requires Syncfusion.XlsIO package. Please use CSV format.",
                                "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    });

                    MessageBox.Show($"Dashboard exported successfully to {saveDialog.FileName}",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    UpdateStatus(DashboardResources.StatusExported);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
            {
                return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
            }
            return value;
        }

        private void ToggleAutoRefresh(bool enabled)
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Enabled = enabled;
            }
        }

        private void UpdateStatus(string text)
        {
            if (_statusPanel != null)
            {
                _statusPanel.Text = text;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Dispose();
                _revenueChart?.Dispose();
                _metricsGrid?.Dispose();
                _budgetGauge?.Dispose();
                _revenueGauge?.Dispose();
                _expensesGauge?.Dispose();
                _netPositionGauge?.Dispose();
                _toolbar?.Dispose();
                _statusBar?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
