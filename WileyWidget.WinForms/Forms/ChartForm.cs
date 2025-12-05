using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Business.Interfaces;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using SyncDrawing = Syncfusion.Drawing;
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

                Load += async (s, e) =>
                {
                    try
                    {
                        await _vm.LoadChartsAsync();
                        DrawCharts();
                    }
                    catch (OperationCanceledException oce)
                    {
                        _logger.LogWarning(oce, "Chart load was canceled or timed out");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed loading chart data");
                        throw;
                    }
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
                    valueLabels[0].Text = txCount.ToString(CultureInfo.InvariantCulture);

                    // Total for selected year amount (sum of monthly)
                    var totalYear = _vm.LineChartData.Sum(d => d.Value);
                    valueLabels[1].Text = totalYear.ToString("C0", CultureInfo.CurrentCulture);

                    // Top category
                    var topCategory = _vm.PieChartData.OrderByDescending(p => p.Value).FirstOrDefault();
                    valueLabels[2].Text = topCategory != null ? $"{topCategory.Category} ({topCategory.Value.ToString("C0", CultureInfo.CurrentCulture)})" : "-";

                    // Budget variance
                    var variance = await _chartService.GetBudgetVarianceAsync(_vm.SelectedYear);
                    valueLabels[3].Text = variance.ToString("C0", CultureInfo.CurrentCulture);

                    // YTD Actuals (up to current month)
                    var currentMonth = DateTime.UtcNow.Month;
                    var ytd = _vm.LineChartData.Take(currentMonth).Sum(d => d.Value);
                    valueLabels[4].Text = ytd.ToString("C0", CultureInfo.CurrentCulture);

                    // Remaining budget
                    var budgeted = await _chartService.GetBudgetedAmountAsync(_vm.SelectedYear);
                    var actual = _vm.LineChartData.Sum(d => d.Value);
                    valueLabels[5].Text = ((double)budgeted - actual).ToString("C0", CultureInfo.CurrentCulture);
                    _trendLabel?.Dispose();
                    // Update trend
                    var trend = await _chartService.GetTrendAsync(_vm.SelectedYear, DateTime.UtcNow.Month);
                    var trendText = trend >= 0 ? "📈 Trending Up" : "📉 Trending Down";
                    _trendLabel!.Text = $"{trendText}\nRevenue changed {trend:F1}% compared to last month";
                    _trendLabel!.ForeColor = trend >= 0 ? Color.FromArgb(52, 168, 83) : Color.FromArgb(234, 67, 53);
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
                _vm.SelectedYear = int.Parse(_yearSelector.SelectedItem?.ToString() ?? DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                await _vm.LoadChartsAsync();
                DrawCharts();
                await UpdateSummaryValuesAsync();
            };
            var yearHost = new ToolStripControlHost(_yearSelector);

            // Category filter
            var categoryLabel = new ToolStripLabel("  Category: ");
            _categoryFilter = new ComboBox { Width = 150 };
            _categoryFilter.Items.AddRange(new object[] { "All Categories", "Revenue", "Expenses", "Capital", "Operations" });
            _categoryFilter.SelectedItem = _vm.SelectedCategory;
            _categoryFilter.SelectedIndexChanged += async (s, e) =>
            {
                _vm.SelectedCategory = _categoryFilter.SelectedItem?.ToString() ?? "All Categories";
                await _vm.LoadChartsAsync();
                DrawCharts();
                await UpdateSummaryValuesAsync();
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
                await _vm.LoadChartsAsync();
                DrawCharts();
                await UpdateSummaryValuesAsync();
            });

            var exportBtn = new ToolStripButton(ChartFormResources.ExportButton, null, (s, e) =>
            {
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
                            // Generate PDF report using Syncfusion
                            using var document = new PdfDocument();
                            var page = document.Pages.Add();
                            var graphics = page.Graphics;

                            // Draw header
                            var headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 20, PdfFontStyle.Bold);
                            graphics.DrawString($"Budget Analytics Report - {_vm.SelectedYear}", headerFont,
                                PdfBrushes.Black, new SyncDrawing.PointF(10, 10));

                            var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
                            var yPosition = 50f;

                            // Draw metadata
                            graphics.DrawString($"Year: {_vm.SelectedYear}, Category: {_vm.SelectedCategory}",
                                bodyFont, PdfBrushes.Black, new SyncDrawing.PointF(10, yPosition));
                            yPosition += 30;

                            // Monthly Trends section
                            var sectionFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold);
                            graphics.DrawString("Monthly Trends:", sectionFont, PdfBrushes.Black, new SyncDrawing.PointF(10, yPosition));
                            yPosition += 20;

                            // Create PdfGrid for monthly data
                            var monthlyGrid = new PdfGrid();
                            var monthlyTable = new System.Data.DataTable();
                            monthlyTable.Columns.Add("Month");
                            monthlyTable.Columns.Add("Amount");

                            foreach (var data in _vm.LineChartData)
                            {
                                monthlyTable.Rows.Add(data.Category, data.Value.ToString("C", CultureInfo.CurrentCulture));
                            }

                            monthlyGrid.DataSource = monthlyTable;
                            monthlyGrid.Draw(page, new SyncDrawing.PointF(10, yPosition));
                            yPosition += monthlyGrid.Headers.Count * 20 + _vm.LineChartData.Count * 15 + 30;

                            // Category Breakdown section
                            if (yPosition > page.GetClientSize().Height - 150)
                            {
                                page = document.Pages.Add();
                                graphics = page.Graphics;
                                yPosition = 10;
                            }

                            graphics.DrawString("Category Breakdown:", sectionFont, PdfBrushes.Black, new SyncDrawing.PointF(10, yPosition));
                            yPosition += 20;

                            // Create PdfGrid for category data
                            var categoryGrid = new PdfGrid();
                            var categoryTable = new System.Data.DataTable();
                            categoryTable.Columns.Add("Category");
                            categoryTable.Columns.Add("Amount");

                            foreach (var data in _vm.PieChartData)
                            {
                                categoryTable.Rows.Add(data.Category, data.Value.ToString("C", CultureInfo.CurrentCulture));
                            }

                            categoryGrid.DataSource = categoryTable;
                            categoryGrid.Draw(page, new SyncDrawing.PointF(10, yPosition));

                            // Save the document
                            document.Save(saveDialog.FileName);
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
            _vm.RevenueTrendSeries.CollectionChanged += (s, e) => { DrawCharts(); _ = UpdateSummaryValuesAsync(); };
            _vm.ExpenditureColumnSeries.CollectionChanged += (s, e) => { DrawCharts(); _ = UpdateSummaryValuesAsync(); };
            _vm.BudgetStackedSeries.CollectionChanged += (s, e) => { DrawCharts(); _ = UpdateSummaryValuesAsync(); };
            _vm.ProportionPieSeries.CollectionChanged += (s, e) => { DrawCharts(); _ = UpdateSummaryValuesAsync(); };

            // Make UpdateSummaryValues available later when data changes
            // When the charts invalidate, refresh summary metrics
            if (revenueChart != null)
            {
                revenueChart.SizeChanged += (s, e) => _ = UpdateSummaryValuesAsync();
            }
            if (expenditureChart != null)
            {
                expenditureChart.SizeChanged += (s, e) => _ = UpdateSummaryValuesAsync();
            }
            if (cumulativeChart != null)
            {
                cumulativeChart.SizeChanged += (s, e) => _ = UpdateSummaryValuesAsync();
            }
            if (proportionChart != null)
            {
                proportionChart.SizeChanged += (s, e) => _ = UpdateSummaryValuesAsync();
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

        public void DrawCharts()
        {
            try
            {
                // === Revenue Chart (Line) ===
                if (revenueChart != null && _vm.RevenueTrendSeries.Count > 0)
                {
                    revenueChart.Series.Clear();
                    foreach (var series in _vm.RevenueTrendSeries)
                    {
                        series.Type = ChartSeriesType.Line;
                        // Enable data labels for better visualization
                        series.Style.DisplayText = true;
                        series.Style.TextColor = Color.FromArgb(33, 37, 41);
                        revenueChart.Series.Add(series);
                    }
                    revenueChart.Refresh();
                    _logger.LogDebug("Revenue chart rendered with RevenueTrendSeries");
                }

                // === Expenditure Chart (Column) ===
                if (expenditureChart != null && _vm.ExpenditureColumnSeries.Count > 0)
                {
                    expenditureChart.Series.Clear();
                    foreach (var series in _vm.ExpenditureColumnSeries)
                    {
                        series.Type = ChartSeriesType.Column;
                        // Enable data labels
                        series.Style.DisplayText = true;
                        series.Style.TextColor = Color.FromArgb(33, 37, 41);
                        expenditureChart.Series.Add(series);
                    }
                    expenditureChart.Refresh();
                    _logger.LogDebug("Expenditure chart rendered with ExpenditureColumnSeries");
                }

                // === Cumulative Chart (Stacked Column) ===
                if (cumulativeChart != null && _vm.BudgetStackedSeries.Count > 0)
                {
                    cumulativeChart.Series.Clear();
                    foreach (var series in _vm.BudgetStackedSeries)
                    {
                        series.Type = ChartSeriesType.StackingColumn;
                        // Enable data labels
                        series.Style.DisplayText = true;
                        series.Style.TextColor = Color.White;
                        cumulativeChart.Series.Add(series);
                    }
                    cumulativeChart.Refresh();
                    _logger.LogDebug("Cumulative chart rendered with BudgetStackedSeries");
                }

                // === Proportion Chart (Pie) ===
                if (proportionChart != null && _vm.ProportionPieSeries.Count > 0)
                {
                    proportionChart.Series.Clear();
                    foreach (var series in _vm.ProportionPieSeries)
                    {
                        // Enable data labels for pie chart
                        series.Style.DisplayText = true;
                        series.Style.TextColor = Color.FromArgb(33, 37, 41);
                        proportionChart.Series.Add(series);
                    }
                    proportionChart.Refresh();
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
                revenueChart?.Dispose();
                expenditureChart?.Dispose();
                cumulativeChart?.Dispose();
                proportionChart?.Dispose();
                _yearSelector?.Dispose();
                _categoryFilter?.Dispose();
                _chartTypeCombo?.Dispose();
                _trendLabel?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
