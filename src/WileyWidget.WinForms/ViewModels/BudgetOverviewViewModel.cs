using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using System.Globalization;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyWidget.ViewModels
{
    /// <summary>
    /// Represents a financial metric line item for budget overview display.
    /// </summary>
    public class FinancialMetric
    {
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal BudgetedAmount { get; set; }
        public decimal Variance => Amount - BudgetedAmount;
        public double VariancePercent => BudgetedAmount != 0 ? (double)(Variance / BudgetedAmount * 100) : 0;
        public bool IsOverBudget => Amount > BudgetedAmount;
        public string DepartmentName { get; set; } = string.Empty;
        public int FiscalYear { get; set; }
    }

    /// <summary>
    /// ViewModel for the Budget Overview providing comprehensive budget vs actual analysis,
    /// variance tracking, and fiscal year comparisons for municipal financial management.
    /// Implements full MVVM with async loading, filtering, and export capabilities.
    /// </summary>
    public partial class BudgetOverviewViewModel : ObservableRecipient, IDisposable
    {
        private readonly ILogger<BudgetOverviewViewModel>? _logger;
        private readonly IDbContextFactory<AppDbContext>? _dbContextFactory;
        private readonly ISettingsService? _settingsService;
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly CancellationTokenSource _disposalCts = new();
        private bool _disposed;

        /// <summary>
        /// Indicates whether the ViewModel is currently displaying demo/sample data.
        /// UI can bind to this to show a visual indicator.
        /// </summary>
        [ObservableProperty]
        private bool isUsingDemoData;

        /// <summary>
        /// Event raised when export file path needs to be selected by the UI.
        /// Handler should set ExportFilePath property with selected path.
        /// </summary>
        public event EventHandler<ExportRequestEventArgs>? ExportPathRequested;

        [ObservableProperty]
        private string? exportFilePath;

        [ObservableProperty]
        private string title = "Budget Overview";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private ObservableCollection<FinancialMetric> metrics = new();

        [ObservableProperty]
        private ObservableCollection<int> availableFiscalYears = new();

        [ObservableProperty]
        private int selectedFiscalYear = DateTime.Now.Year;

        [ObservableProperty]
        private decimal totalBudgeted;

        [ObservableProperty]
        private decimal totalActual;

        [ObservableProperty]
        private decimal totalVariance;

        [ObservableProperty]
        private double overallVariancePercent;

        [ObservableProperty]
        private int overBudgetCount;

        [ObservableProperty]
        private int underBudgetCount;

        [ObservableProperty]
        private DateTime lastUpdated = DateTime.Now;

        /// <summary>
        /// Initializes a new instance with optional DI dependencies.
        /// </summary>
        public BudgetOverviewViewModel(
            ILogger<BudgetOverviewViewModel>? logger = null,
            IDbContextFactory<AppDbContext>? dbContextFactory = null,
            ISettingsService? settingsService = null)
        {
            _logger = logger;
            _dbContextFactory = dbContextFactory;
            _settingsService = settingsService;

            LoadBudgetOverviewCommand = new AsyncRelayCommand(LoadBudgetOverviewAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            ExportCommand = new AsyncRelayCommand(ExportToCsvAsync);
            ExportToPdfCommand = new AsyncRelayCommand(ExportToPdfAsync);

            // Initialize fiscal years
            for (int year = DateTime.Now.Year - 5; year <= DateTime.Now.Year + 1; year++)
            {
                AvailableFiscalYears.Add(year);
            }

            // Fire initial load
            _ = LoadBudgetOverviewAsync();
        }

        /// <summary>Command to load budget overview data.</summary>
        public IAsyncRelayCommand LoadBudgetOverviewCommand { get; }

        /// <summary>Command to refresh budget data.</summary>
        public IAsyncRelayCommand RefreshCommand { get; }

        /// <summary>Command to export data to CSV.</summary>
        public IAsyncRelayCommand ExportCommand { get; }

        /// <summary>Command to export data to PDF.</summary>
        public IAsyncRelayCommand ExportToPdfCommand { get; }

        /// <summary>
        /// Loads budget overview metrics from database for the selected fiscal year.
        /// </summary>
        private async Task LoadBudgetOverviewAsync(CancellationToken ct = default)
        {
            // Use linked token to respect both caller cancellation and disposal
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposalCts.Token);
            var token = linkedCts.Token;

            // Try to acquire lock with timeout (5 seconds)
            if (!await _loadLock.WaitAsync(TimeSpan.FromSeconds(5), token))
            {
                _logger?.LogWarning("LoadBudgetOverviewAsync: Could not acquire lock within timeout");
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = null;
                _logger?.LogInformation("Loading budget overview for FY {Year}", SelectedFiscalYear);

                // Check if demo mode is enabled via settings
                var useDemoMode = _settingsService?.Current?.UseDemoData ?? false;
                if (useDemoMode || _dbContextFactory == null)
                {
                    _logger?.LogInformation("Using demo data (demo mode: {DemoMode}, factory null: {FactoryNull})",
                        useDemoMode, _dbContextFactory == null);
                    LoadSampleData();
                    IsUsingDemoData = true;
                    return;
                }

                IsUsingDemoData = false;
                token.ThrowIfCancellationRequested();
                await using var db = await _dbContextFactory.CreateDbContextAsync(token);

                // Query budget data grouped by department (read-only, no tracking needed)
                var budgetData = await db.MunicipalAccounts
                    .AsNoTracking()
                    .Include(a => a.Department)
                    .GroupBy(a => a.Department!.Name)
                    .Select(g => new FinancialMetric
                    {
                        Category = g.Key ?? "Unassigned",
                        DepartmentName = g.Key ?? "Unassigned",
                        BudgetedAmount = g.Sum(a => a.BudgetAmount),
                        Amount = g.Sum(a => a.Balance),
                        FiscalYear = SelectedFiscalYear
                    })
                    .OrderByDescending(m => m.BudgetedAmount)
                    .ToListAsync(token);

                Metrics = new ObservableCollection<FinancialMetric>(budgetData);

                // Calculate totals
                TotalBudgeted = Metrics.Sum(m => m.BudgetedAmount);
                TotalActual = Metrics.Sum(m => m.Amount);
                TotalVariance = TotalActual - TotalBudgeted;
                OverallVariancePercent = TotalBudgeted != 0 ? (double)(TotalVariance / TotalBudgeted * 100) : 0;

                OverBudgetCount = Metrics.Count(m => m.IsOverBudget);
                UnderBudgetCount = Metrics.Count(m => !m.IsOverBudget);

                LastUpdated = DateTime.Now;
                _logger?.LogInformation("Budget overview loaded: {Count} departments, variance {Variance:C}",
                    Metrics.Count, TotalVariance);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("Budget overview load cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load budget overview");
                ErrorMessage = $"Unable to load budget data: {ex.Message}";
                LoadSampleData();
                IsUsingDemoData = true;
            }
            finally
            {
                IsLoading = false;
                try { _loadLock.Release(); } catch { }
            }
        }

        /// <summary>
        /// Refreshes budget data for the current fiscal year.
        /// </summary>
        private async Task RefreshAsync(CancellationToken ct = default)
        {
            _logger?.LogInformation("Refreshing budget overview");
            await LoadBudgetOverviewAsync(ct);
        }

        /// <summary>
        /// Exports current budget data to CSV format using ClosedXML.
        /// </summary>
        private async Task ExportToCsvAsync(CancellationToken ct = default)
        {
            if (Metrics == null || Metrics.Count == 0)
            {
                ErrorMessage = "No data to export.";
                return;
            }

            try
            {
                // Request file path from UI
                var args = new ExportRequestEventArgs("csv", $"BudgetOverview_FY{SelectedFiscalYear}.csv");
                ExportPathRequested?.Invoke(this, args);

                if (string.IsNullOrEmpty(args.SelectedPath))
                {
                    _logger?.LogDebug("CSV export cancelled by user");
                    return;
                }

                _logger?.LogInformation("Exporting budget overview to CSV: {Path}", args.SelectedPath);

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposalCts.Token);
                var token = linkedCts.Token;

                await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add($"Budget FY{SelectedFiscalYear}");

                    // Header row
                    worksheet.Cell(1, 1).Value = "Department";
                    worksheet.Cell(1, 2).Value = "Category";
                    worksheet.Cell(1, 3).Value = "Budgeted Amount";
                    worksheet.Cell(1, 4).Value = "Actual Amount";
                    worksheet.Cell(1, 5).Value = "Variance";
                    worksheet.Cell(1, 6).Value = "Variance %";
                    worksheet.Cell(1, 7).Value = "Status";

                    // Style header
                    var headerRange = worksheet.Range(1, 1, 1, 7);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                    // Data rows
                    int row = 2;
                    foreach (var metric in Metrics)
                    {
                        token.ThrowIfCancellationRequested();
                        worksheet.Cell(row, 1).Value = metric.DepartmentName;
                        worksheet.Cell(row, 2).Value = metric.Category;
                        worksheet.Cell(row, 3).Value = metric.BudgetedAmount;
                        worksheet.Cell(row, 4).Value = metric.Amount;
                        worksheet.Cell(row, 5).Value = metric.Variance;
                        worksheet.Cell(row, 6).Value = metric.VariancePercent;
                        worksheet.Cell(row, 7).Value = metric.IsOverBudget ? "Over Budget" : "Under Budget";
                        row++;
                    }

                    // Summary row
                    row++;
                    worksheet.Cell(row, 1).Value = "TOTALS";
                    worksheet.Cell(row, 1).Style.Font.Bold = true;
                    worksheet.Cell(row, 3).Value = TotalBudgeted;
                    worksheet.Cell(row, 4).Value = TotalActual;
                    worksheet.Cell(row, 5).Value = TotalVariance;
                    worksheet.Cell(row, 6).Value = OverallVariancePercent;

                    // Format currency columns
                    worksheet.Column(3).Style.NumberFormat.Format = "$#,##0.00";
                    worksheet.Column(4).Style.NumberFormat.Format = "$#,##0.00";
                    worksheet.Column(5).Style.NumberFormat.Format = "$#,##0.00";
                    worksheet.Column(6).Style.NumberFormat.Format = "0.00%";

                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(args.SelectedPath);
                }, token).ConfigureAwait(false);

                _logger?.LogInformation("Budget overview exported successfully to {Path}", args.SelectedPath);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("CSV export cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to export budget overview to CSV");
                ErrorMessage = $"Export failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Exports current budget data to PDF format using Syncfusion PDF.
        /// </summary>
        public async Task ExportToPdfAsync(CancellationToken ct = default)
        {
            if (Metrics == null || Metrics.Count == 0)
            {
                ErrorMessage = "No data to export.";
                return;
            }

            try
            {
                // Request file path from UI
                var args = new ExportRequestEventArgs("pdf", $"BudgetOverview_FY{SelectedFiscalYear}.pdf");
                ExportPathRequested?.Invoke(this, args);

                if (string.IsNullOrEmpty(args.SelectedPath))
                {
                    _logger?.LogDebug("PDF export cancelled by user");
                    return;
                }

                _logger?.LogInformation("Exporting budget overview to PDF: {Path}", args.SelectedPath);

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposalCts.Token);
                var token = linkedCts.Token;

                await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    using var document = new PdfDocument();
                    var page = document.Pages.Add();
                    var graphics = page.Graphics;

                    // Title
                    var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold);
                    graphics.DrawString($"Budget Overview - Fiscal Year {SelectedFiscalYear}",
                        titleFont, PdfBrushes.DarkBlue, new System.Drawing.PointF(10, 10));

                    // Summary section
                    var regularFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
                    var boldFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
                    float yPos = 50;

                    graphics.DrawString($"Generated: {DateTime.Now:g}", regularFont, PdfBrushes.Gray, new System.Drawing.PointF(10, yPos));
                    yPos += 20;

                    graphics.DrawString($"Total Budgeted: {TotalBudgeted:C}", boldFont, PdfBrushes.Black, new System.Drawing.PointF(10, yPos));
                    yPos += 15;
                    graphics.DrawString($"Total Actual: {TotalActual:C}", boldFont, PdfBrushes.Black, new System.Drawing.PointF(10, yPos));
                    yPos += 15;

                    var varianceColor = TotalVariance >= 0 ? PdfBrushes.Green : PdfBrushes.Red;
                    graphics.DrawString($"Variance: {TotalVariance:C} ({OverallVariancePercent:F2}%)", boldFont, varianceColor, new System.Drawing.PointF(10, yPos));
                    yPos += 30;

                    // Create PDF grid for data
                    var pdfGrid = new PdfGrid();
                    pdfGrid.Columns.Add(7);

                    // Header row
                    var headerRow = pdfGrid.Headers.Add(1)[0];
                    headerRow.Cells[0].Value = "Department";
                    headerRow.Cells[1].Value = "Category";
                    headerRow.Cells[2].Value = "Budgeted";
                    headerRow.Cells[3].Value = "Actual";
                    headerRow.Cells[4].Value = "Variance";
                    headerRow.Cells[5].Value = "Variance %";
                    headerRow.Cells[6].Value = "Status";

                    // Style header
                    foreach (PdfGridCell cell in headerRow.Cells)
                    {
                        cell.Style.BackgroundBrush = PdfBrushes.LightGray;
                        cell.Style.Font = boldFont;
                    }

                    // Data rows
                    foreach (var metric in Metrics)
                    {
                        token.ThrowIfCancellationRequested();
                        var row = pdfGrid.Rows.Add();
                        row.Cells[0].Value = metric.DepartmentName;
                        row.Cells[1].Value = metric.Category;
                        row.Cells[2].Value = metric.BudgetedAmount.ToString("C", CultureInfo.CurrentCulture);
                        row.Cells[3].Value = metric.Amount.ToString("C", CultureInfo.CurrentCulture);
                        row.Cells[4].Value = metric.Variance.ToString("C", CultureInfo.CurrentCulture);
                        row.Cells[5].Value = metric.VariancePercent.ToString("F2", CultureInfo.CurrentCulture) + "%";
                        row.Cells[6].Value = metric.IsOverBudget ? "Over Budget" : "Under Budget";

                        // Highlight over budget rows
                        if (metric.IsOverBudget)
                        {
                            row.Cells[6].Style.TextBrush = PdfBrushes.Red;
                        }
                    }

                    pdfGrid.Draw(page, new System.Drawing.PointF(10, yPos));

                    using var stream = new FileStream(args.SelectedPath, FileMode.Create, FileAccess.Write);
                    document.Save(stream);
                    document.Close(true);
                }, token).ConfigureAwait(false);

                _logger?.LogInformation("Budget overview exported successfully to PDF: {Path}", args.SelectedPath);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("PDF export cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to export budget overview to PDF");
                ErrorMessage = $"PDF export failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Loads sample data when database is unavailable.
        /// </summary>
        private void LoadSampleData()
        {
            Metrics = new ObservableCollection<FinancialMetric>
            {
                new() { Category = "General Fund", DepartmentName = "Administration", BudgetedAmount = 500_000m, Amount = 485_000m, FiscalYear = SelectedFiscalYear },
                new() { Category = "Public Works", DepartmentName = "Public Works", BudgetedAmount = 350_000m, Amount = 372_000m, FiscalYear = SelectedFiscalYear },
                new() { Category = "Public Safety", DepartmentName = "Police", BudgetedAmount = 420_000m, Amount = 415_000m, FiscalYear = SelectedFiscalYear },
                new() { Category = "Parks & Rec", DepartmentName = "Parks", BudgetedAmount = 180_000m, Amount = 175_000m, FiscalYear = SelectedFiscalYear },
                new() { Category = "Utilities", DepartmentName = "Water/Sewer", BudgetedAmount = 290_000m, Amount = 310_000m, FiscalYear = SelectedFiscalYear }
            };

            TotalBudgeted = Metrics.Sum(m => m.BudgetedAmount);
            TotalActual = Metrics.Sum(m => m.Amount);
            TotalVariance = TotalActual - TotalBudgeted;
            OverallVariancePercent = TotalBudgeted != 0 ? (double)(TotalVariance / TotalBudgeted * 100) : 0;
            OverBudgetCount = Metrics.Count(m => m.IsOverBudget);
            UnderBudgetCount = Metrics.Count(m => !m.IsOverBudget);
            LastUpdated = DateTime.Now;
        }

        /// <summary>
        /// Called when SelectedFiscalYear changes to reload data.
        /// </summary>
        partial void OnSelectedFiscalYearChanged(int value)
        {
            _ = LoadBudgetOverviewAsync();
        }

        /// <summary>
        /// Disposes resources and cancels pending operations.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose pattern implementation.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                try { _disposalCts.Cancel(); } catch { }
                try { _disposalCts.Dispose(); } catch { }
                try { _loadLock.Dispose(); } catch { }
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Event arguments for export path requests.
    /// </summary>
    public class ExportRequestEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the export format type (csv, pdf, xlsx).
        /// </summary>
        public string Format { get; }

        /// <summary>
        /// Gets the suggested default filename.
        /// </summary>
        public string SuggestedFileName { get; }

        /// <summary>
        /// Gets or sets the selected file path. Set by the UI event handler.
        /// </summary>
        public string? SelectedPath { get; set; }

        public ExportRequestEventArgs(string format, string suggestedFileName)
        {
            Format = format;
            SuggestedFileName = suggestedFileName;
        }
    }
}
