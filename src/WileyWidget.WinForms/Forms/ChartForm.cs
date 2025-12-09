extern alias sync31pdf;
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
using SyncPdf = sync31pdf::Syncfusion.Pdf;
using SyncPdfGraphics = sync31pdf::Syncfusion.Pdf.Graphics;
using SyncPdfGrid = sync31pdf::Syncfusion.Pdf.Grid;
using WileyWidget.Services;
using Syncfusion.Windows.Forms;

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
        public const string ExportButton = "Export";
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
        private ComboBox? _categoryFilter;
        private ToolStripComboBox? _chartTypeCombo;
        private Label? _trendLabel;
        private Label? _emptyStateLabel;
        private DateTimePicker? _startDatePicker;
        private DateTimePicker? _endDatePicker;

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
            InitializeComponent();

            _vm = vm;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _chartService = chartService ?? throw new ArgumentNullException(nameof(chartService));
            _printingService = printingService ?? throw new ArgumentNullException(nameof(printingService));

            try
            {

                // Prefer SkinManager when available, otherwise apply manual dark theme as fallback
                if (Syncfusion.Windows.Forms.SkinManager.ContainsSkinManager)
                {
                    try { Syncfusion.Windows.Forms.SkinManager.SetVisualStyle(this, "Office2019DarkGray"); } catch { }
                }
                else
                {
                    BackColor = Color.FromArgb(45, 45, 48);
                    ForeColor = Color.White;
                }

                _logger.LogInformation("Applied Office2019DarkGray theme to ChartForm");

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
                            UpdateEmptyState();
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
                    var start = _vm.SelectedStartDate;
                    var end = _vm.SelectedEndDate;

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

                    // Wait for all tasks to complete in parallel (removed ConfigureAwait for UI context)
                    await Task.WhenAll(varianceTask, budgetedTask, trendTask);

                    // Get results using await (tasks already complete, no blocking)
                    var variance = await varianceTask;
                    var budgeted = await budgetedTask;
                    var trend = await trendTask;

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
                GripStyle = ToolStripGripStyle.Hidden
            };
            if (!Syncfusion.Windows.Forms.SkinManager.ContainsSkinManager)
            {
                toolStrip.BackColor = Color.FromArgb(248, 249, 250);
            }

            // Date range pickers
            var dateRangeLabel = new ToolStripLabel("Date Range: ");
            _startDatePicker = new DateTimePicker { Width = 120, Format = DateTimePickerFormat.Short, Value = _vm.SelectedStartDate };
            var toLabel = new ToolStripLabel(" to ");
            _endDatePicker = new DateTimePicker { Width = 120, Format = DateTimePickerFormat.Short, Value = _vm.SelectedEndDate };
            var updateDateRangeBtn = new Button { Text = "Update", Width = 60, Height = 23 };

            updateDateRangeBtn.Click += async (s, e) =>
            {
                await Utilities.AsyncEventHelper.ExecuteAsync(
                    async ct =>
                    {
                        if (_startDatePicker.Value >= _endDatePicker.Value)
                        {
                            MessageBox.Show("Start date must be before end date.", "Invalid Date Range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        _vm.SelectedStartDate = _startDatePicker.Value;
                        _vm.SelectedEndDate = _endDatePicker.Value;
                        await _vm.LoadChartsAsync(null, null, ct);
                        DrawCharts();
                        UpdateEmptyState();
                        await UpdateSummaryValuesAsync();
                    },
                    _cts,
                    this,
                    _logger,
                    "Updating chart date range",
                    showErrorDialog: false);
            };
            var startDateHost = new ToolStripControlHost(_startDatePicker);
            var endDateHost = new ToolStripControlHost(_endDatePicker);
            var updateBtnHost = new ToolStripControlHost(updateDateRangeBtn);

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
                        UpdateEmptyState();
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
            var chartTypeLabel = new ToolStripLabel("  Type: ");
            _chartTypeCombo = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            _chartTypeCombo.Items.AddRange(new object[] { "Line", "Column", "Bar", "Area" });
            _chartTypeCombo.SelectedIndex = 0;
            _chartTypeCombo.SelectedIndexChanged += (s, e) => DrawCharts();

            var refreshBtn = new ToolStripButton(ChartFormResources.RefreshButton, null, async (s, e) =>
            {
                await Utilities.AsyncEventHelper.ExecuteAsync(
                    async ct =>
                    {
                        await _vm.LoadChartsAsync(null, null, ct);
                        DrawCharts();
                        UpdateEmptyState();
                        await UpdateSummaryValuesAsync();
                    },
                    _cts,
                    this,
                    _logger,
                    "Refreshing chart data");
            });

            var exportBtn = new ToolStripButton(ChartFormResources.ExportButton, null, async (s, e) =>
            {
                _logger.LogInformation("Chart export button clicked");
                using var saveDialog = new SaveFileDialog
                {
                    Filter = "PDF Files|*.pdf|PNG Images|*.png|JPEG Images|*.jpg|All Files|*.*",
                    DefaultExt = "pdf",
                    FileName = $"BudgetReport_{DateTime.Now:yyyyMMdd}"
                };
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var ext = Path.GetExtension(saveDialog.FileName).ToLower(CultureInfo.InvariantCulture);
                        if (ext == ".pdf")
                        {
                            // Export via PrintingService which handles PDF generation
                            var pdfPath = await _printingService.GeneratePdfAsync(_vm);
                            File.Copy(pdfPath, saveDialog.FileName, overwrite: true);
                            _logger.LogInformation("PDF exported successfully to: {FileName}", saveDialog.FileName);
                            MessageBox.Show($"Report exported successfully to:\n{saveDialog.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                        {
                            // Export chart image using Syncfusion's ExportToImage
                            ExportChartToImage(saveDialog.FileName, ext);
                            _logger.LogInformation("Image exported successfully to: {FileName}", saveDialog.FileName);
                            MessageBox.Show($"Chart exported successfully to:\n{saveDialog.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Unsupported file format. Please choose PDF, PNG, or JPEG.", "Invalid Format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Export failed");
                        MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            // Zoom buttons
            var zoomInBtn = new ToolStripButton("Zoom In", null, (s, e) => ZoomCharts(1.2));
            var zoomOutBtn = new ToolStripButton("Zoom Out", null, (s, e) => ZoomCharts(0.8));
            var zoomResetBtn = new ToolStripButton("Reset Zoom", null, (s, e) => ResetChartZoom());

            // AI Insights button
            var aiInsightsBtn = new ToolStripButton("AI Insights", null, async (s, e) =>
            {
                await ShowAIInsightsAsync();
            });

            toolStrip.Items.AddRange(new ToolStripItem[]
            {
                dateRangeLabel, startDateHost, toLabel, endDateHost, updateBtnHost,
                new ToolStripSeparator(),
                categoryLabel, categoryHost,
                chartTypeLabel, _chartTypeCombo,
                new ToolStripSeparator(),
                new ToolStripLabel("Zoom: "),
                zoomInBtn, zoomOutBtn, zoomResetBtn,
                new ToolStripSeparator(),
                refreshBtn,
                new ToolStripSeparator(),
                exportBtn,
                printBtn,
                aiInsightsBtn
            });

            // === Main Layout ===
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 800
            };
            if (!Syncfusion.Windows.Forms.SkinManager.ContainsSkinManager)
            {
                mainSplit.BackColor = Color.FromArgb(45, 45, 48);
            }

            // === Left: Charts ===
            var chartTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.FromArgb(45, 45, 48),
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
            ConfigureChartControl(cumulativeChart, "Cumulative Budget", ChartValueType.Category, "Month", "Amount");
            cumulativeGroup.Controls.Add(cumulativeChart);
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
            ConfigureChartControl(proportionChart, ChartFormResources.CategoryBreakdownTitle);
            proportionGroup.Controls.Add(proportionChart);
            chartTable.Controls.Add(proportionGroup, 1, 1);

            // Empty state overlay
            _emptyStateLabel = new Label
            {
                Text = "No data available\n\nAdjust filters and click 'Update' or 'Refresh Data'",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(45, 45, 48),
                AutoSize = false,
                Dock = DockStyle.Fill,
                Visible = false
            };
            chartTable.Controls.Add(_emptyStateLabel, 0, 0);

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
            var metricDefinitions = new[]
            {
                ("Total Transactions", "-", Color.FromArgb(33, 37, 41)),
                ("Total (Last 12m)", "-", Color.FromArgb(52, 168, 83)),
                ("Top Category", "-", Color.FromArgb(66, 133, 244)),
                ("Budget Variance", "-", Color.FromArgb(52, 168, 83)),
                ("YTD Actuals", "-", Color.FromArgb(33, 37, 41)),
                ("Remaining Budget", "-", Color.FromArgb(251, 188, 4))
            };

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

            Size = new Size(1400, 850);
            MinimumSize = new Size(1000, 650);
            StartPosition = FormStartPosition.CenterParent;

            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>
        /// Updates visibility of empty state label based on data availability.
        /// </summary>
        private void UpdateEmptyState()
        {
            try
            {
                var hasData = _vm.RevenueTrendSeries.Count > 0 || _vm.ExpenditureColumnSeries.Count > 0 ||
                              _vm.BudgetStackedSeries.Count > 0 || _vm.ProportionPieSeries.Count > 0;

                if (_emptyStateLabel != null)
                {
                    _emptyStateLabel.Visible = !hasData;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update empty state");
            }
        }

        /// <summary>
        /// Event handler used to prepare the per-point style safely (invoked by Syncfusion during rendering).
        /// </summary>
        private void Series_PrepareStyle(object? sender, ChartPrepareStyleInfoEventArgs args)
        {
            try
            {
                var series = sender as SfChartSeries;
                if (series == null || args.Style == null)
                    return;

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
            series.Tag = (displayText, textColor);

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
        /// Apply recommended control-level configuration for a ChartControl.
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

        /// <summary>
        /// Exports the specified chart to an image file.
        /// </summary>
        private void ExportChartToImage(string filePath, string extension)
        {
            try
            {
                // Prefer capturing the control to a bitmap which works across Syncfusion versions
                var chart = revenueChart ?? expenditureChart ?? cumulativeChart ?? proportionChart;
                if (chart == null)
                    throw new InvalidOperationException("No chart available to export");

                // Ensure a reasonable size for rendering
                var size = chart.ClientSize;
                if (size.Width <= 0 || size.Height <= 0)
                {
                    size = new Size(Math.Max(800, size.Width), Math.Max(600, size.Height));
                }

                using var bmp = new Bitmap(size.Width, size.Height);
                try
                {
                    if (chart.InvokeRequired)
                    {
                        chart.Invoke((Action)(() => chart.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size))));
                    }
                    else
                    {
                        chart.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size));
                    }
                }
                catch
                {
                    // If DrawToBitmap still fails, attempt a best-effort render by refreshing and copying the control's image
                    try
                    {
                        if (chart.InvokeRequired)
                        {
                            chart.Invoke((Action)(() => chart.Refresh()));
                        }
                        else
                        {
                            chart.Refresh();
                        }

                        using var g = Graphics.FromImage(bmp);
                        var rect = new Rectangle(Point.Empty, bmp.Size);
                        chart.Invoke((Action)(() => chart.DrawToBitmap(bmp, rect)));
                    }
                    catch
                    {
                        // give up and save empty bitmap
                    }
                }

                // Choose image format from extension
                var ext = extension?.ToLowerInvariant() ?? Path.GetExtension(filePath).ToLowerInvariant();
                var format = ext switch
                {
                    ".png" => System.Drawing.Imaging.ImageFormat.Png,
                    ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                    ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
                    _ => System.Drawing.Imaging.ImageFormat.Png
                };

                bmp.Save(filePath, format);
                _logger.LogInformation("Chart image exported to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export chart image");
                throw;
            }
        }

        /// <summary>
        /// Applies zoom to all charts.
        /// </summary>
        private void ZoomCharts(double zoomFactor)
        {
            try
            {
                // Use reflection to adapt to different Syncfusion range types (MinMaxInfo vs DoubleRange etc.)
                void TryAdjust(ChartControl? chart)
                {
                    if (chart == null) return;
                    try
                    {
                        var axis = chart.PrimaryYAxis;
                        var rangeProp = axis?.GetType().GetProperty("Range");
                        if (rangeProp == null) return;
                        var rangeVal = rangeProp.GetValue(axis);
                        if (rangeVal == null) return;

                        var startProp = rangeVal.GetType().GetProperty("Start") ?? rangeVal.GetType().GetProperty("Min") ?? rangeVal.GetType().GetProperty("Minimum") ?? rangeVal.GetType().GetProperty("Lower");
                        var endProp = rangeVal.GetType().GetProperty("End") ?? rangeVal.GetType().GetProperty("Max") ?? rangeVal.GetType().GetProperty("Maximum") ?? rangeVal.GetType().GetProperty("Upper");
                        if (startProp == null || endProp == null) return;

                        var startObj = startProp.GetValue(rangeVal);
                        var endObj = endProp.GetValue(rangeVal);
                        if (startObj == null || endObj == null) return;

                        var start = Convert.ToDouble(startObj, CultureInfo.InvariantCulture);
                        var end = Convert.ToDouble(endObj, CultureInfo.InvariantCulture);
                        start *= zoomFactor;
                        end *= zoomFactor;

                        startProp.SetValue(rangeVal, Convert.ChangeType(start, startProp.PropertyType));
                        endProp.SetValue(rangeVal, Convert.ChangeType(end, endProp.PropertyType));

                        // Reassign back in case the property is a struct/value type
                        rangeProp.SetValue(axis, rangeVal);
                        chart.Refresh();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Non-fatal: failed to adjust chart range via reflection");
                    }
                }

                TryAdjust(revenueChart);
                TryAdjust(expenditureChart);
                TryAdjust(cumulativeChart);
                _logger.LogDebug("Charts zoomed by factor {ZoomFactor}", zoomFactor);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Zoom operation failed");
            }
        }

        /// <summary>
        /// Resets zoom on all charts to default.
        /// </summary>
        private void ResetChartZoom()
        {
            try
            {
                void TryReset(ChartControl? chart)
                {
                    if (chart == null) return;
                    try
                    {
                        var axis = chart.PrimaryYAxis;
                        var rangeProp = axis?.GetType().GetProperty("Range");
                        if (rangeProp == null) return;
                        var rangeType = rangeProp.PropertyType;
                        object? newRange = null;
                        try { newRange = Activator.CreateInstance(rangeType); } catch { newRange = null; }
                        if (newRange != null)
                        {
                            rangeProp.SetValue(axis, newRange);
                        }
                        else
                        {
                            // Fallback: try to set numeric start/end to reasonable defaults
                            var current = rangeProp.GetValue(axis);
                            if (current != null)
                            {
                                var startProp = current.GetType().GetProperty("Start") ?? current.GetType().GetProperty("Min") ?? current.GetType().GetProperty("Minimum") ?? current.GetType().GetProperty("Lower");
                                var endProp = current.GetType().GetProperty("End") ?? current.GetType().GetProperty("Max") ?? current.GetType().GetProperty("Maximum") ?? current.GetType().GetProperty("Upper");
                                if (startProp != null && endProp != null)
                                {
                                    startProp.SetValue(current, Convert.ChangeType(0.0, startProp.PropertyType));
                                    endProp.SetValue(current, Convert.ChangeType(100.0, endProp.PropertyType));
                                    rangeProp.SetValue(axis, current);
                                }
                            }
                        }

                        chart.Refresh();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Non-fatal: failed to reset chart range via reflection");
                    }
                }

                TryReset(revenueChart);
                TryReset(expenditureChart);
                TryReset(cumulativeChart);
                _logger.LogDebug("Chart zoom reset to default");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reset zoom failed");
            }
        }

        /// <summary>
        /// Shows AI-generated insights about the current chart data.
        /// </summary>
        private async Task ShowAIInsightsAsync()
        {
            try
            {
                // Placeholder for AI insights integration
                // TODO: Integrate IAIService to generate insights based on chart data
                var insights = "AI Insights feature will be available in the next release.\n\n" +
                    "This feature will analyze current budget trends and provide recommendations.";
                MessageBox.Show(insights, "AI Insights", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _logger.LogInformation("AI Insights requested");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve AI insights");
                MessageBox.Show($"Failed to retrieve insights: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                var chartType = _chartTypeCombo?.SelectedItem?.ToString() ?? "Line";

                // === Revenue Chart (Line) ===
                if (revenueChart != null && _vm.RevenueTrendSeries.Count > 0)
                {
                    revenueChart.Series.Clear();
                    foreach (var series in _vm.RevenueTrendSeries)
                    {
                        series.Type = chartType switch
                        {
                            "Column" => ChartSeriesType.Column,
                            "Bar" => ChartSeriesType.Bar,
                            "Area" => ChartSeriesType.Area,
                            _ => ChartSeriesType.Line
                        };

                        EnsureSeriesStyleSafe(series, Color.FromArgb(33, 37, 41));
                        revenueChart.Series.Add(series);
                    }

                    try { revenueChart.Refresh(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Non-fatal failure refreshing revenueChart"); }

                    _logger.LogDebug("Revenue chart rendered with type {ChartType}", chartType);
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

                    _logger.LogDebug("Expenditure chart rendered");
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

                    _logger.LogDebug("Cumulative chart rendered");
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

                    _logger.LogDebug("Proportion chart rendered");
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
                _categoryFilter?.Dispose();
                _chartTypeCombo?.Dispose();
                _trendLabel?.Dispose();
                _emptyStateLabel?.Dispose();
                _startDatePicker?.Dispose();
                _endDatePicker?.Dispose();

                Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
            }

            base.Dispose(disposing);
        }
    }
}
