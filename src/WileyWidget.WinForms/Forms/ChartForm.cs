using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Business.Interfaces;
using Syncfusion.Windows.Forms.Chart;
using SfChartSeries = Syncfusion.Windows.Forms.Chart.ChartSeries;
using SyncPdf = Syncfusion.Pdf;
using SyncPdfGraphics = Syncfusion.Pdf.Graphics;
using SyncPdfGrid = Syncfusion.Pdf.Grid;
using WileyWidget.Services;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Chart form displaying budget analytics with custom-drawn charts.
    /// Displays both bar charts and pie charts for data visualization with modern styling.
    /// </summary>
    internal static class ChartFormResources
    {
        public const string FormTitle = "Budget Analytics";
        public const string RefreshButton = "Refresh Data";
        public const string ExportButton = "Export to PDF";
        public const string PrintButton = "Print";
        public const string MonthlyTrendTitle = "Monthly Budget Trend";
        public const string CategoryBreakdownTitle = "Category Breakdown";
        public const string SummaryTitle = "Budget Summary";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class ChartForm : Form
    {
        private readonly ChartViewModel _vm;
        private readonly ILogger<ChartForm> _logger;
        private readonly IChartService _chartService;
        private readonly IPrintingService _printingService;
        private ChartControl? revenueChart;
        private ChartControl? expenditureChart;
        private ChartControl? cumulativeChart;
        private ChartControl? proportionChart;
        private ComboBox? _yearSelector;
        private ComboBox? _categoryFilter;
        private ToolStripComboBox? _chartTypeCombo;
        private Label? _trendLabel;

        // Concurrency control for UpdateSummaryValues
        private readonly object _updateSummaryLock = new object();
        private bool _isUpdatingSummary = false;

        // Track which series we've attached PrepareStyle handlers for to avoid repeated -/+ operations
        private readonly HashSet<SfChartSeries> _attachedPrepareStyle = new HashSet<SfChartSeries>();
        private readonly object _attachedPrepareStyleLock = new object();

        // Cancellation token source for async operations
        private CancellationTokenSource? _cts;

        // Color palette for charts
        private static readonly Color[] ChartColors = new[]
        {
            Color.FromArgb(66, 133, 244),   // Blue
            Color.FromArgb(52, 168, 83),    // Green
            Color.FromArgb(251, 188, 4),    // Yellow
            Color.FromArgb(234, 67, 53),    // Red
            Color.FromArgb(154, 160, 166),  // Gray
            Color.FromArgb(171, 71, 188),   // Purple
            Color.FromArgb(0, 172, 193)     // Cyan
        };

        public ChartForm(ChartViewModel vm, ILogger<ChartForm> logger, IChartService chartService, IPrintingService printingService)
        {
            _vm = vm;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _chartService = chartService ?? throw new ArgumentNullException(nameof(chartService));
            _printingService = printingService ?? throw new ArgumentNullException(nameof(printingService));

            try
            {
                InitializeComponent();
                _logger.LogInformation("ChartForm initialized successfully");

                // Initialize cancellation token source
                _cts = new CancellationTokenSource();

                Load += async (s, e) =>
                {
                    await Utilities.AsyncEventHelper.ExecuteAsync(
                        async ct =>
                        {
                            await _vm.LoadChartsAsync(null, null, ct);
                            DrawCharts();
                        },
                        _cts,
                        this,
                        _logger,
                        "Loading chart data");
                };

                FormClosing += (s, e) =>
                {
                    Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize ChartForm");
                throw;
            }
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // Declare valueLabels at the beginning for UpdateSummaryValues method
            Label[] valueLabels = null!;

            // Define UpdateSummaryValues method first
            async Task UpdateSummaryValuesAsync()
            {
                // Prevent concurrent execution of UpdateSummaryValues
                if (Interlocked.Exchange(ref _isUpdatingSummary, true))
                    return; // Another call is already running

                if (valueLabels == null)
                {
                    Interlocked.Exchange(ref _isUpdatingSummary, false);
                    return; // Guard against null reference
                }

                try
                {
                    var start = new DateTime(_vm.SelectedYear, 1, 1);
                    var end = new DateTime(_vm.SelectedYear, 12, 31);

                    // Total transactions count
                    var txCount = await _chartService.GetTransactionCountAsync(start, end, _vm.SelectedCategory);

                    // Calculate other values (CPU/bound work ok off-UI thread)
                    var totalYear = _vm.LineChartData.Sum(d => d.Value);
                    var topCategory = _vm.PieChartData.OrderByDescending(p => p.Value).FirstOrDefault();
                    var varianceTask = _chartService.GetBudgetVarianceAsync(_vm.SelectedYear);
                    var currentMonth = DateTime.UtcNow.Month;
                    var ytd = _vm.LineChartData.Take(currentMonth).Sum(d => d.Value);
                    var budgetedTask = _chartService.GetBudgetedAmountAsync(_vm.SelectedYear);
                    var trendTask = _chartService.GetTrendAsync(_vm.SelectedYear, DateTime.UtcNow.Month);

                    await Task.WhenAll(varianceTask, budgetedTask, trendTask).ConfigureAwait(false);

                    var variance = varianceTask.Result;
                    var budgeted = budgetedTask.Result;
                    var trend = trendTask.Result;

                    // Prepare UI update as a single action to marshal to UI thread
                    Action uiUpdate = () =>
                    {
                        try
                        {
                            if (valueLabels == null) return;

                            valueLabels[0].Text = txCount.ToString(CultureInfo.InvariantCulture);
                            valueLabels[1].Text = totalYear.ToString("C0", CultureInfo.CurrentCulture);
                            valueLabels[2].Text = topCategory != null ? $"{topCategory.Category} ({topCategory.Value.ToString("C0", CultureInfo.CurrentCulture)})" : "-";
                            valueLabels[3].Text = variance.ToString("C0", CultureInfo.CurrentCulture);
                            valueLabels[4].Text = ytd.ToString("C0", CultureInfo.CurrentCulture);
                            valueLabels[5].Text = ((double)budgeted - _vm.LineChartData.Sum(d => d.Value)).ToString("C0", CultureInfo.CurrentCulture);

                            var trendText = trend >= 0 ? "📈 Trending Up" : "📉 Trending Down";
                            if (_trendLabel != null)
                            {
                                _trendLabel.Text = $"{trendText}\nRevenue changed {trend:F1}% compared to last month";
                                _trendLabel.ForeColor = trend >= 0 ? Color.FromArgb(52, 168, 83) : Color.FromArgb(234, 67, 53);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed applying summary UI updates");
                        }
                    };

                    if (this != null && this.IsHandleCreated && this.InvokeRequired)
                    {
                        try { this.BeginInvoke(uiUpdate); }
                        catch { uiUpdate(); }
                    }
                    else
                    {
                        uiUpdate();
                    }
                }
                catch { /* best-effort: do not crash UI */ }
                finally
                {
                    Interlocked.Exchange(ref _isUpdatingSummary, false);
                }
            }

            // === Toolbar ===
            var toolStrip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            // Year selector
            var yearLabel = new ToolStripLabel("Year: ");
            _yearSelector = new ComboBox { Width = 80 };
            _yearSelector.Items.AddRange(new object[] { "2025", "2024", "2023", "2022" });
            _yearSelector.SelectedItem = _vm.SelectedYear.ToString(CultureInfo.InvariantCulture);
            _yearSelector.SelectedIndexChanged += async (s, e) =>
            {
                await Utilities.AsyncEventHelper.ExecuteAsync(
                    async ct =>
                    {
                        _vm.SelectedYear = int.Parse(_yearSelector.SelectedItem?.ToString() ?? DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                        await _vm.LoadChartsAsync(_vm.SelectedYear, null, ct);
                        DrawCharts();
                        await UpdateSummaryValuesAsync();
                    },
                    _cts,
                    this,
                    _logger,
                    "Updating chart year",
                    showErrorDialog: false);
            };
            var yearHost = new ToolStripControlHost(_yearSelector);

            // Category filter
            var categoryLabel = new ToolStripLabel("  Category: ");
            _categoryFilter = new ComboBox { Width = 150 };
            _categoryFilter.Items.AddRange(new object[] { "All Categories", "Revenue", "Expenses", "Capital", "Operations" });
            _categoryFilter.SelectedItem = _vm.SelectedCategory;
            _categoryFilter.SelectedIndexChanged += async (s, e) =>
            {
                await Utilities.AsyncEventHelper.ExecuteAsync(
                    async ct =>
                    {
                        _vm.SelectedCategory = _categoryFilter.SelectedItem?.ToString() ?? "All Categories";
                        await _vm.LoadChartsAsync(null, _vm.SelectedCategory, ct);
                        DrawCharts();
                        await UpdateSummaryValuesAsync();
                    },
                    _cts,
                    this,
                    _logger,
                    "Updating chart category",
                    showErrorDialog: false);
            };
            var categoryHost = new ToolStripControlHost(_categoryFilter);

            // Chart type selector
            var chartTypeLabel = new ToolStripLabel("  Chart: ");
            _chartTypeCombo = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            _chartTypeCombo.Items.AddRange(new object[] { "Bar", "Line", "Area" });
            _chartTypeCombo.SelectedIndex = 0;
            _chartTypeCombo.SelectedIndexChanged += (s, e) => DrawCharts();

            var refreshBtn = new ToolStripButton(ChartFormResources.RefreshButton, null, async (s, e) =>
            {
                await Utilities.AsyncEventHelper.ExecuteAsync(
                    async ct =>
                    {
                        await _vm.LoadChartsAsync(null, null, ct);
                        DrawCharts();
                        await UpdateSummaryValuesAsync();
                    },
                    _cts,
                    this,
                    _logger,
                    "Refreshing chart data");
            });

            var exportBtn = new ToolStripButton(ChartFormResources.ExportButton, null, (s, e) =>
            {
                _logger.LogInformation("Chart export button clicked");
                using var saveDialog = new SaveFileDialog
                {
                    Filter = "PDF Files|*.pdf|PNG Images|*.png|All Files|*.*",
                    DefaultExt = "pdf",
                    FileName = $"BudgetReport_{DateTime.Now:yyyyMMdd}"
                };
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (Path.GetExtension(saveDialog.FileName).ToLower(CultureInfo.InvariantCulture) == ".pdf")
                        {
                            // TODO: PDF export temporarily disabled due to Syncfusion version conflict
                            // Will be re-enabled once version conflict is resolved
                            MessageBox.Show("PDF export is temporarily unavailable", "Feature Disabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            // For PNG, perhaps export chart image, but placeholder
                            _logger.LogWarning("Export to {Extension} not implemented yet", Path.GetExtension(saveDialog.FileName));
                        }
                        _logger.LogInformation("Report exported to: {FileName}", saveDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Export failed");
                    }
                }
            });

            var printBtn = new ToolStripButton(ChartFormResources.PrintButton, null, async (s, e) =>
            {
                _logger.LogInformation("Print preview requested for chart data");
                try
                {
                    var pdfPath = await _printingService.GeneratePdfAsync(_vm);
                    await _printingService.PreviewAsync(pdfPath);
                    _logger.LogInformation("Print preview completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Print preview failed");
                }
            });

            toolStrip.Items.AddRange(new ToolStripItem[]
            {
                yearLabel, yearHost,
                categoryLabel, categoryHost,
                chartTypeLabel, _chartTypeCombo,
                new ToolStripSeparator(),
                refreshBtn,
                new ToolStripSeparator(),
                exportBtn,
                printBtn
            });

            // === Main Layout ===
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 800,
                BackColor = Color.FromArgb(245, 245, 250)
            };

            // === Left: Charts ===
            var chartTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.FromArgb(245, 245, 250),
                Padding = new Padding(10)
            };
            chartTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            chartTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            chartTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            chartTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            // Revenue Chart (Line)
            var revenueGroup = new GroupBox
            {
                Text = "Revenue Trends",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Padding = new Padding(10),
                Margin = new Padding(5)
            };
            revenueChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                ShowLegend = true,
                LegendsPlacement = ChartPlacement.Outside
            };
            revenueChart.ChartArea.BackInterior = new Syncfusion.Drawing.BrushInfo(Color.White);
            revenueChart.PrimaryXAxis.Title = "Time";
            revenueChart.PrimaryXAxis.TitleColor = Color.FromArgb(108, 117, 125);
            revenueChart.PrimaryYAxis.Title = "Revenue ($)";
            // Apply recommended defaults
            ConfigureChartControl(revenueChart, ChartFormResources.MonthlyTrendTitle, ChartValueType.Category, "Time", "Revenue ($)");
            revenueGroup.Controls.Add(revenueChart);
            chartTable.Controls.Add(revenueGroup, 0, 0);

            // Expenditure Chart (Column)
            var expenditureGroup = new GroupBox
            {
                Text = "Expenditure Breakdown",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Padding = new Padding(10),
                Margin = new Padding(5)
            };
            expenditureChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                ShowLegend = true,
                LegendsPlacement = ChartPlacement.Outside
            };
            expenditureChart.ChartArea.BackInterior = new Syncfusion.Drawing.BrushInfo(Color.White);
            expenditureChart.PrimaryXAxis.Title = "Department";
            expenditureChart.PrimaryXAxis.TitleColor = Color.FromArgb(108, 117, 125);
            expenditureChart.PrimaryYAxis.TitleColor = Color.FromArgb(108, 117, 125);
            // Apply recommended defaults
            ConfigureChartControl(expenditureChart, ChartFormResources.CategoryBreakdownTitle, ChartValueType.Category, "Department", "Amount");
            expenditureGroup.Controls.Add(expenditureChart);
            chartTable.Controls.Add(expenditureGroup, 1, 0);

            // Cumulative Chart (Stacked Column)
            var cumulativeGroup = new GroupBox
            {
                Text = "Cumulative Budget",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Padding = new Padding(10),
                Margin = new Padding(5)
            };
            cumulativeChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                ShowLegend = true,
                LegendsPlacement = ChartPlacement.Outside
            };
            cumulativeChart.ChartArea.BackInterior = new Syncfusion.Drawing.BrushInfo(Color.White);
            cumulativeChart.PrimaryXAxis.Title = "Month";
            cumulativeChart.PrimaryYAxis.TitleColor = Color.FromArgb(108, 117, 125);
            // Apply recommended defaults
            ConfigureChartControl(cumulativeChart, "Cumulative Budget", ChartValueType.Category, "Month", "Amount");
            cumulativeGroup.Controls.Add(cumulativeChart);
            // ToolTip property removed as it is invalid
            chartTable.Controls.Add(cumulativeGroup, 0, 1);

            // Proportion Chart (Pie)
            var proportionGroup = new GroupBox
            {
                Text = "Budget Proportions",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Padding = new Padding(10),
                Margin = new Padding(5)
            };
            proportionChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                ShowLegend = true,
                LegendsPlacement = ChartPlacement.Outside
            };
            proportionChart.ChartArea.BackInterior = new Syncfusion.Drawing.BrushInfo(Color.White);
            // Apply recommended defaults
            ConfigureChartControl(proportionChart, ChartFormResources.CategoryBreakdownTitle);
            proportionGroup.Controls.Add(proportionChart);
            chartTable.Controls.Add(proportionGroup, 1, 1);

            mainSplit.Panel1.Controls.Add(chartTable);

            // === Right: Summary Panel ===
            var summaryPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(15)
            };

            var summaryHeader = new Label
            {
                Text = "📊 " + ChartFormResources.SummaryTitle,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Dock = DockStyle.Top,
                Height = 40
            };

            var summaryContent = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(0, 10, 0, 0)
            };
            summaryContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            summaryContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            // Summary metrics
            // Build the summary metric rows and keep the value labels in fields for dynamic updates
            var metricDefinitions = new[]
            {
                ("Total Transactions", "-", Color.FromArgb(33, 37, 41)),
                ("Total (Last 12m)", "-", Color.FromArgb(52, 168, 83)),
                ("Top Category", "-", Color.FromArgb(66, 133, 244)),
                ("Budget Variance", "-", Color.FromArgb(52, 168, 83)),
                ("YTD Actuals", "-", Color.FromArgb(33, 37, 41)),
                ("Remaining Budget", "-", Color.FromArgb(251, 188, 4))
            };

            // Keep references to the value labels so we can update them when real data loads
            valueLabels = new Label[metricDefinitions.Length];

            for (int i = 0; i < metricDefinitions.Length; i++)
            {
                var (label, value, color) = metricDefinitions[i];
                var labelCtrl = new Label
                {
                    Text = label,
                    Font = new Font("Segoe UI", 10),
                    ForeColor = Color.FromArgb(108, 117, 125),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var valueCtrl = new Label
                {
                    Text = value,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    ForeColor = color,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight
                };
                valueLabels[i] = valueCtrl;
                summaryContent.Controls.Add(labelCtrl, 0, i);
                summaryContent.Controls.Add(valueCtrl, 1, i);
            }

            // Apply initial values (likely placeholders). We'll also refresh after chart drawing completes
            _ = UpdateSummaryValuesAsync();

            // Ensure charts redraw when the view model content changes
            _vm.RevenueTrendSeries.CollectionChanged += (s, e) =>
            {
                Action invoke = () => { DrawCharts(); _ = UpdateSummaryValuesAsync(); };
                if (this != null && this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(invoke);
                else invoke();
            };
            _vm.ExpenditureColumnSeries.CollectionChanged += (s, e) =>
            {
                Action invoke = () => { DrawCharts(); _ = UpdateSummaryValuesAsync(); };
                if (this != null && this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(invoke);
                else invoke();
            };
            _vm.BudgetStackedSeries.CollectionChanged += (s, e) =>
            {
                Action invoke = () => { DrawCharts(); _ = UpdateSummaryValuesAsync(); };
                if (this != null && this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(invoke);
                else invoke();
            };
            _vm.ProportionPieSeries.CollectionChanged += (s, e) =>
            {
                Action invoke = () => { DrawCharts(); _ = UpdateSummaryValuesAsync(); };
                if (this != null && this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(invoke);
                else invoke();
            };

            // Make UpdateSummaryValues available later when data changes
            // When the charts invalidate, refresh summary metrics
            if (revenueChart != null)
            {
                revenueChart.SizeChanged += (s, e) =>
                {
                    Action invoke = () => _ = UpdateSummaryValuesAsync();
                    if (this != null && this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(invoke);
                    else invoke();
                };
            }
            if (expenditureChart != null)
            {
                expenditureChart.SizeChanged += (s, e) =>
                {
                    Action invoke = () => _ = UpdateSummaryValuesAsync();
                    if (this != null && this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(invoke);
                    else invoke();
                };
            }
            if (cumulativeChart != null)
            {
                cumulativeChart.SizeChanged += (s, e) =>
                {
                    Action invoke = () => _ = UpdateSummaryValuesAsync();
                    if (this != null && this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(invoke);
                    else invoke();
                };
            }
            if (proportionChart != null)
            {
                proportionChart.SizeChanged += (s, e) =>
                {
                    Action invoke = () => _ = UpdateSummaryValuesAsync();
                    if (this != null && this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(invoke);
                    else invoke();
                };
            }

            // Trend indicator
            var trendPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(10)
            };
            _trendLabel = new Label
            {
                Text = "📈 Trending Up\nRevenue increased 12% compared to last month",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(52, 168, 83),
                Dock = DockStyle.Fill
            };
            trendPanel.Controls.Add(_trendLabel);

            summaryPanel.Controls.Add(summaryContent);
            summaryPanel.Controls.Add(trendPanel);
            summaryPanel.Controls.Add(summaryHeader);

            mainSplit.Panel2.Controls.Add(summaryPanel);

            // === Status Strip ===
            var statusStrip = new StatusStrip();
            var statusLabel = new ToolStripStatusLabel("Chart data loaded successfully") { Spring = true };
            var dataPointsLabel = new ToolStripStatusLabel("Data Points: 10") { Alignment = ToolStripItemAlignment.Right };
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, dataPointsLabel });

            // === Add Controls ===
            Controls.Add(mainSplit);
            Controls.Add(toolStrip);
            Controls.Add(statusStrip);

            Size = new Size(1200, 850);
            MinimumSize = new Size(900, 650);
            StartPosition = FormStartPosition.CenterParent;

            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>
        /// Event handler used to prepare the per-point style safely (invoked by Syncfusion during rendering).
        /// This is preferred to directly assigning to series.Style which can be null or read-only in some versions.
        /// </summary>
        private void Series_PrepareStyle(object? sender, ChartPrepareStyleInfoEventArgs args)
        {
            try
            {
                var series = sender as SfChartSeries;
                if (series == null || args.Style == null)
                    return;

                // Read configuration we stored on the series.Tag (displayText flag and text color)
                var displayText = true;
                var textColor = Color.FromArgb(33, 37, 41);
                if (series.Tag is ValueTuple<bool, Color> settings)
                {
                    displayText = settings.Item1;
                    textColor = settings.Item2;
                }

                if (displayText)
                {
                    args.Style.DisplayText = true;
                }

                args.Style.TextColor = textColor;

                // Friendly tooltip per-point
                try
                {
                    var idx = args.Index;
                    if (idx >= 0 && series.Points != null && idx < series.Points.Count)
                    {
                        var pt = series.Points[idx];
                        double y = double.NaN;
                        if (pt != null && pt.YValues != null && pt.YValues.Length > 0)
                        {
                            y = pt.YValues[0];
                        }

                        args.Style.ToolTip = $"{series.Name}: {y.ToString("C0", CultureInfo.CurrentCulture)}";
                    }
                }
                catch { /* non-fatal for tooltip */ }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Series PrepareStyle handler failed");
            }
        }

        private void EnsureSeriesStyleSafe(SfChartSeries series, Color textColor, bool displayText = true)
        {

            // Store desired settings on the series so the PrepareStyle handler can use them
            series.Tag = (displayText, textColor);

            // Attach PrepareStyle exactly once per series to avoid triggering Syncfusion internal re-computation
            lock (_attachedPrepareStyleLock)
            {
                if (!_attachedPrepareStyle.Contains(series))
                {
                    try
                    {
                        series.PrepareStyle += Series_PrepareStyle;
                        _attachedPrepareStyle.Add(series);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to attach PrepareStyle to series {Series}", series?.Name);
                    }
                }
            }
        }

        /// <summary>
        /// Apply recommended control-level configuration for a ChartControl to match Syncfusion examples
        /// (tooltips, legend placement, skins, axis value types and friendly title/axis labels)
        /// </summary>
        private void ConfigureChartControl(ChartControl chart, string? title = null, ChartValueType? xAxisType = null, string? xAxisTitle = null, string? yAxisTitle = null)
        {
            if (chart == null) return;

            try
            {
                chart.ShowToolTips = true;
                chart.Tooltip.BackgroundColor = new Syncfusion.Drawing.BrushInfo(Color.White);
                chart.Tooltip.BorderStyle = BorderStyle.FixedSingle;
                chart.Tooltip.Font = new Font("Segoe UI", 10);

                chart.LegendAlignment = ChartAlignment.Center;
                chart.Legend.Position = ChartDock.Top;
                chart.LegendsPlacement = ChartPlacement.Outside;

                // Prefer a modern skin by default
                try { chart.Skins = Skins.Metro; } catch { /* not all releases expose Skin APIs identically */ }

                if (xAxisType.HasValue)
                {
                    chart.PrimaryXAxis.ValueType = xAxisType.Value;
                }

                if (!string.IsNullOrEmpty(xAxisTitle))
                {
                    chart.PrimaryXAxis.Title = xAxisTitle;
                    chart.PrimaryXAxis.TitleColor = Color.FromArgb(108, 117, 125);
                }

                if (!string.IsNullOrEmpty(yAxisTitle))
                {
                    chart.PrimaryYAxis.Title = yAxisTitle;
                    chart.PrimaryYAxis.TitleColor = Color.FromArgb(108, 117, 125);
                }

                if (!string.IsNullOrEmpty(title))
                {
                    chart.Titles.Clear();
                    var ct = new ChartTitle { Text = title };
                    chart.Titles.Add(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ConfigureChartControl failed for chart");
            }
        }

        public void DrawCharts()
        {
            // Ensure DrawCharts runs on the UI thread to avoid cross-thread control access
            if (this != null && this.IsHandleCreated && this.InvokeRequired)
            {
                try { this.BeginInvoke(new Action(DrawCharts)); }
                catch { DrawCharts(); }
                return;
            }
            try
            {
                // === Revenue Chart (Line) ===
                if (revenueChart != null && _vm.RevenueTrendSeries.Count > 0)
                {
                    revenueChart.Series.Clear();
                    foreach (var series in _vm.RevenueTrendSeries)
                    {
                        series.Type = ChartSeriesType.Line;

                        // Apply style safely (guard against null Style objects in syncfusion runtime)
                        EnsureSeriesStyleSafe(series, Color.FromArgb(33, 37, 41));

                        revenueChart.Series.Add(series);
                    }

                    try { revenueChart.Refresh(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Non-fatal failure refreshing revenueChart"); }

                    _logger.LogDebug("Revenue chart rendered with RevenueTrendSeries");
                }

                // === Expenditure Chart (Column) ===
                if (expenditureChart != null && _vm.ExpenditureColumnSeries.Count > 0)
                {
                    expenditureChart.Series.Clear();
                    foreach (var series in _vm.ExpenditureColumnSeries)
                    {
                        series.Type = ChartSeriesType.Column;

                        EnsureSeriesStyleSafe(series, Color.FromArgb(33, 37, 41));

                        expenditureChart.Series.Add(series);
                    }

                    try { expenditureChart.Refresh(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Non-fatal failure refreshing expenditureChart"); }

                    _logger.LogDebug("Expenditure chart rendered with ExpenditureColumnSeries");
                }

                // === Cumulative Chart (Stacked Column) ===
                if (cumulativeChart != null && _vm.BudgetStackedSeries.Count > 0)
                {
                    cumulativeChart.Series.Clear();
                    foreach (var series in _vm.BudgetStackedSeries)
                    {
                        series.Type = ChartSeriesType.StackingColumn;

                        EnsureSeriesStyleSafe(series, Color.White);

                        cumulativeChart.Series.Add(series);
                    }

                    try { cumulativeChart.Refresh(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Non-fatal failure refreshing cumulativeChart"); }

                    _logger.LogDebug("Cumulative chart rendered with BudgetStackedSeries");
                }

                // === Proportion Chart (Pie) ===
                if (proportionChart != null && _vm.ProportionPieSeries.Count > 0)
                {
                    proportionChart.Series.Clear();
                    foreach (var series in _vm.ProportionPieSeries)
                    {
                        EnsureSeriesStyleSafe(series, Color.FromArgb(33, 37, 41));
                        proportionChart.Series.Add(series);
                    }

                    try { proportionChart.Refresh(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Non-fatal failure refreshing proportionChart"); }

                    _logger.LogDebug("Proportion chart rendered with ProportionPieSeries");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering charts");
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DrawCharts();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Detach PrepareStyle handlers we attached earlier
                lock (_attachedPrepareStyleLock)
                {
                    try
                    {
                        foreach (var s in _attachedPrepareStyle.ToList())
                        {
                            try { s.PrepareStyle -= Series_PrepareStyle; }
                            catch { /* ignore */ }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error detaching series PrepareStyle handlers during dispose");
                    }

                    _attachedPrepareStyle.Clear();
                }

                revenueChart?.Dispose();
                expenditureChart?.Dispose();
                cumulativeChart?.Dispose();
                proportionChart?.Dispose();
                _yearSelector?.Dispose();
                _categoryFilter?.Dispose();
                _chartTypeCombo?.Dispose();
                _trendLabel?.Dispose();

                // Cancel and dispose async operations
                Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
            }

            base.Dispose(disposing);
        }
    }
}
