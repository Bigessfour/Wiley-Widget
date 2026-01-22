using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using FastReport;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using System.Data;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for the Reports form, managing FastReport Open Source report generation and export.
/// Uses CommunityToolkit.Mvvm for MVVM pattern with source generators.
/// Delegates report operations to IReportService.
/// </summary>
public partial class ReportsViewModel : ObservableObject, IDisposable
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportsViewModel> _logger;
    private readonly IAuditService _auditService;
        private readonly IReportExportService? _exportService;
        private readonly IBudgetRepository _budgetRepository;

    /// <summary>
    /// Available report types for the dropdown.
    /// </summary>
    public static readonly string[] AvailableReportTypes =
    [
        "Budget Summary",
        "Budget Comparison",
        "Account List",
        "Monthly Transactions",
        "Category Breakdown",
        "Variance Analysis"
    ];

    [ObservableProperty]
    private string selectedReportType = "Budget Summary";

    [ObservableProperty]
    private DateTime fromDate = new(DateTime.Now.Year, 1, 1);

    [ObservableProperty]
    private DateTime toDate = DateTime.Now;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasReportLoaded;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private ObservableCollection<ReportDataItem> previewData = [];

    [ObservableProperty]
    private Dictionary<string, object> parameters = new();

    [ObservableProperty]
    private ObservableCollection<string> reportTemplates = new();

    /// <summary>
    /// Display names for templates (friendly names shown in the UI). Keys map to actual file names.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> reportTemplateDisplayNames = new();

    // Map from display name -> file name (with extension)
    private readonly Dictionary<string, string> _displayNameToFile = new();

    [ObservableProperty]
    private int pageSize = 25;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int zoomPercent = 100;

    [ObservableProperty]
    private string? searchText;

    [ObservableProperty]
    private bool showParametersPanel = false;

    /// <summary>
    /// Reference to the ReportViewer control (set by the Form).
    /// </summary>
    /// Reference to the ReportViewer control (set by the Form).
    /// Use object here to avoid tight coupling to WinForms control in the view model and ease unit testing.
    public object? ReportViewer { get; set; }

    /// <summary>
    /// Reference to the PreviewControl (set by the Form).
    /// </summary>
    public object? PreviewControl { get; set; }

    /// <summary>
    /// Path to the reports folder.
    /// </summary>
    [ObservableProperty]
    private string reportsFolder = string.Empty;

    /// <summary>
    /// List of supported export formats provided by IReportExportService when available.
    /// </summary>
    public IEnumerable<string> SupportedExportFormats => _exportService?.GetSupportedFormats() ?? new[] { "PDF", "Excel", "CSV" };

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsViewModel"/> class.
    /// </summary>
    /// <param name="reportService">Service for report operations.</param>
    /// <param name="logger">Logger instance for the ViewModel.</param>
    /// <param name="auditService">Service for audit logging.</param>
    /// <param name="exportService">Optional service for report export operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    public ReportsViewModel(IReportService reportService, ILogger<ReportsViewModel> logger, IAuditService auditService, IBudgetRepository budgetRepository, IReportExportService? exportService = null)
    {
        ArgumentNullException.ThrowIfNull(reportService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(auditService);
        ArgumentNullException.ThrowIfNull(budgetRepository);

        _reportService = reportService;
        _logger = logger;
        _auditService = auditService;
        _exportService = exportService;
        _budgetRepository = budgetRepository;

        // Set reports folder path relative to application
        ReportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");

        // Populate available templates from disk if present
        try
        {
            if (Directory.Exists(ReportsFolder))
            {
                // Build a normalized map of available friendly names for matching
                var normalizedFriendly = AvailableReportTypes.ToDictionary(x => Normalize(x), x => x);

                foreach (var file in Directory.EnumerateFiles(ReportsFolder, "*.frx", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileName(file); // with extension
                    var nameNoExt = Path.GetFileNameWithoutExtension(file);

                    if (!ReportTemplates.Contains(nameNoExt)) ReportTemplates.Add(nameNoExt);

                    // Determine a friendly display name if possible
                    var normalized = Normalize(nameNoExt);
                    string displayName;
                    if (normalizedFriendly.TryGetValue(normalized, out var friendly))
                    {
                        displayName = friendly; // use known friendly name (e.g., 'Budget Summary')
                    }
                    else
                    {
                        // Fallback: insert spaces in PascalCase or use the raw name
                        displayName = SplitCamelCase(nameNoExt);
                    }

                    if (!ReportTemplateDisplayNames.Contains(displayName))
                    {
                        ReportTemplateDisplayNames.Add(displayName);
                        _displayNameToFile[displayName] = fileName;
                    }
                }

                // If we found templates and SelectedReportType is empty, pick the first friendly name
                if (ReportTemplateDisplayNames.Count > 0 && string.IsNullOrWhiteSpace(SelectedReportType))
                {
                    SelectedReportType = ReportTemplateDisplayNames.First();
                }
            }
            else
            {
                _logger.LogWarning("Reports folder does not exist: {ReportsFolder}", ReportsFolder);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enumerating report templates in {ReportsFolder}", ReportsFolder);
        }

        _logger.LogInformation("ReportsViewModel initialized. Reports folder: {ReportsFolder}", ReportsFolder);
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var chars = input.Where(c => char.IsLetterOrDigit(c)).Select(char.ToLowerInvariant).ToArray();
        return new string(chars);
    }

    private static string SplitCamelCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(input[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    [RelayCommand]
    public async Task SetZoomAsync(int percent, CancellationToken cancellationToken = default)
    {
        if (ReportViewer == null)
        {
            ErrorMessage = "Report viewer not initialized.";
            return;
        }

        try
        {
            ZoomPercent = percent;
            // ConfigureViewerAsync not available in FastReport.OpenSource
            // Zoom configuration may need to be handled differently
            await Task.Yield(); // Ensure async execution for UI thread safety
            StatusMessage = $"Zoom set to {percent}%";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to set zoom: {ex.Message}";
            _logger.LogError(ex, "Failed to set zoom to {Percent}%", percent);
        }
    }

    [RelayCommand]
    public async Task PrintAsync(CancellationToken cancellationToken = default)
    {
        if (ReportViewer == null)
        {
            ErrorMessage = "Report viewer not initialized.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Printing report...";

            await _reportService.PrintAsync(ReportViewer, cancellationToken);

            StatusMessage = "Report sent to printer.";
            _logger.LogDebug("Report printed");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to print report: {ex.Message}";
            _logger.LogError(ex, "Failed to print report");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task FindAsync(CancellationToken cancellationToken = default)
    {
        // NOTE: BoldReports WPF does not expose find/search API
        // Search is available through the control's built-in toolbar
        StatusMessage = "Search is available through the viewer toolbar";
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task ToggleParametersPanelAsync(CancellationToken cancellationToken = default)
    {
        // NOTE: BoldReports WPF parameters panel is controlled by the control itself
        StatusMessage = "Parameters panel is controlled by the viewer";
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the RDL file path for the selected report type.
    /// </summary>
    private string GetReportPath()
    {
        // If a display name maps to an actual file, use that
        if (!string.IsNullOrWhiteSpace(SelectedReportType) && _displayNameToFile.TryGetValue(SelectedReportType, out var mappedFile))
        {
            return Path.Combine(ReportsFolder, mappedFile);
        }

        // If the SelectedReportType maps directly to a template filename (no spaces), prefer that.
        if (!string.IsNullOrWhiteSpace(SelectedReportType) && ReportTemplates.Contains(SelectedReportType))
        {
            return Path.Combine(ReportsFolder, SelectedReportType + ".frx");
        }

        var reportFileName = SelectedReportType switch
        {
            "Budget Summary" => "BudgetSummary.frx",
            "Budget Comparison" => "BudgetComparison.frx",
            "Account List" => "AccountList.frx",
            "Monthly Transactions" => "MonthlyTransactions.frx",
            "Category Breakdown" => "CategoryBreakdown.frx",
            "Variance Analysis" => "VarianceAnalysis.frx",
            _ => "BudgetSummary.frx"
        };

        return Path.Combine(ReportsFolder, reportFileName);
    }

    /// <summary>
    /// Public helper: return the report path if the file exists, otherwise null.
    /// Used by the WinForms layer to provide a clearer error message when templates are missing.
    /// </summary>
    public string? GetReportPathIfExists()
    {
        var path = GetReportPath();
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Generate and load the selected report into the viewer.
    /// </summary>
    [RelayCommand]
    public async Task GenerateReportAsync(CancellationToken cancellationToken = default)
    {
        if (ReportViewer == null)
        {
            ErrorMessage = "Report viewer not initialized.";
            _logger.LogWarning("GenerateReportAsync called but ReportViewer is null");
            return;
        }

        // Validate incoming parameters first
        if (!ValidateParameters())
        {
            _logger.LogWarning("Report parameters failed validation: {Error}", ErrorMessage);
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = "Generating report...";

            var reportPath = GetReportPath();
            _logger.LogInformation("Generating report: {ReportType} from {ReportPath}", SelectedReportType, reportPath);

            // Prepare data sources based on report type
            StatusMessage = "Preparing data sources...";
            var dataSources = await PrepareDataSourcesAsync(cancellationToken);

            // Honor cancellation after heavy work
            cancellationToken.ThrowIfCancellationRequested();

            // Load report into viewer
            StatusMessage = "Loading report into viewer...";

            var progress = new Progress<double>(p => StatusMessage = $"Loading report... {p:P0}");
            await _reportService.LoadReportAsync(ReportViewer, reportPath, dataSources, progress, cancellationToken);

            // Honor cancellation again
            cancellationToken.ThrowIfCancellationRequested();

            // NOTE: BoldReports WPF does not expose SetReportParametersAsync API
            // Parameters are configured in the RDL file or through the control's UI
            // await _reportService.SetReportParametersAsync(ReportViewer, new Dictionary<string, object>(Parameters), cancellationToken);

            StatusMessage = $"Report generated: {SelectedReportType}";
            _logger.LogInformation("Report generated successfully: {ReportType}", SelectedReportType);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Report generation cancelled.";
            _logger.LogDebug("Report generation cancelled");
        }
        catch (FileNotFoundException ex)
        {
            ErrorMessage = $"Report template not found: {ex.FileName}";
            _logger.LogError(ex, "Report template not found");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to generate report: {ex.Message}";
            _logger.LogError(ex, "Failed to generate report: {ReportType}", SelectedReportType);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Load a specific report file into the viewer.
    /// </summary>
    public async Task LoadReportAsync(string reportPath, CancellationToken cancellationToken = default)
    {
        if (ReportViewer == null)
        {
            ErrorMessage = "Report viewer not initialized.";
            _logger.LogWarning("LoadReportAsync called but ReportViewer is null");
            return;
        }

        if (string.IsNullOrWhiteSpace(reportPath))
        {
            ErrorMessage = "Report path is required.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = "Loading report...";

            _logger.LogInformation("Loading report from path: {ReportPath}", reportPath);

            // Prepare data sources (use default for now)
            var dataSources = await PrepareDataSourcesAsync(cancellationToken);

            // Honor cancellation after heavy work
            cancellationToken.ThrowIfCancellationRequested();

            // Load report into viewer
            var progress = new Progress<double>(p => StatusMessage = $"Loading report... {p:P0}");
            await _reportService.LoadReportAsync(ReportViewer, reportPath, dataSources, progress, cancellationToken);

            // Show the prepared report in the preview control
            if (ReportViewer is FastReport.Report report)
            {
                // PreviewControl not available in Open Source
                // previewControl.Report = report;
                _logger.LogDebug("Report loaded (preview not available in Open Source)");
            }
            else
            {
                _logger.LogWarning("ReportViewer is not of expected type. ReportViewer: {ReportViewerType}",
                    ReportViewer?.GetType().Name);
            }

            // Honor cancellation again
            cancellationToken.ThrowIfCancellationRequested();

            StatusMessage = $"Report loaded: {Path.GetFileName(reportPath)}";
            HasReportLoaded = true;
            _logger.LogInformation("Report loaded successfully from: {ReportPath}", reportPath);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Report loading cancelled.";
            _logger.LogDebug("Report loading cancelled");
        }
        catch (FileNotFoundException ex)
        {
            ErrorMessage = $"Report file not found: {ex.FileName}";
            _logger.LogError(ex, "Report file not found: {FileName}", ex.FileName);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load report: {ex.Message}";
            _logger.LogError(ex, "Failed to load report from {ReportPath}: {Message}", reportPath, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Export the current report to PDF.
    /// </summary>
    [RelayCommand]
    public async Task ExportToPdfAsync(CancellationToken cancellationToken = default)
    {
        if (ReportViewer == null)
        {
            ErrorMessage = "Report viewer not initialized.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = "Exporting to PDF...";

            // Generate default file path
            var fileName = $"{SelectedReportType.Replace(" ", "", StringComparison.Ordinal)}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "WileyWidget",
                "Reports",
                fileName
            );

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var progress = new Progress<double>(p => StatusMessage = $"Exporting PDF... {p:P0}");
            await _reportService.ExportToPdfAsync(ReportViewer, filePath, progress, cancellationToken);

            StatusMessage = $"Exported to: {filePath}";
            _logger.LogInformation("Report exported to PDF: {FilePath}", filePath);

            // Audit the successful export
            try
            {
                await _auditService.AuditAsync("ReportGenerated", new { Report = SelectedReportType, Path = filePath, Format = "PDF", Timestamp = DateTime.UtcNow });
            }
            catch (Exception ax)
            {
                _logger.LogWarning(ax, "Failed to write audit event for report export (PDF)");
            }

            // Open the file location
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to export PDF: {ex.Message}";
            _logger.LogError(ex, "Failed to export report to PDF");

            // Audit the failure
            try
            {
                await _auditService.AuditAsync("ReportExportFailed", new { Report = SelectedReportType, FilePath = (string?)null, Format = "PDF", Error = ex.Message, Timestamp = DateTime.UtcNow });
            }
            catch (Exception ax)
            {
                _logger.LogWarning(ax, "Failed to write audit event for failed PDF export");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Export the current report to Excel.
    /// </summary>
    [RelayCommand]
    public async Task ExportToExcelAsync(CancellationToken cancellationToken = default)
    {
        if (ReportViewer == null)
        {
            ErrorMessage = "Report viewer not initialized.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = "Exporting to Excel...";

            // Generate default file path
            var fileName = $"{SelectedReportType.Replace(" ", "", StringComparison.Ordinal)}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "WileyWidget",
                "Reports",
                fileName
            );

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var progress = new Progress<double>(p => StatusMessage = $"Exporting Excel... {p:P0}");
            await _reportService.ExportToExcelAsync(ReportViewer, filePath, progress, cancellationToken);

            StatusMessage = $"Exported to: {filePath}";
            _logger.LogInformation("Report exported to Excel: {FilePath}", filePath);

            // Audit the successful export
            try
            {
                await _auditService.AuditAsync("ReportGenerated", new { Report = SelectedReportType, Path = filePath, Format = "Excel", Timestamp = DateTime.UtcNow });
            }
            catch (Exception ax)
            {
                _logger.LogWarning(ax, "Failed to write audit event for report export (Excel)");
            }

            // Open the file location
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to export Excel: {ex.Message}";
            _logger.LogError(ex, "Failed to export report to Excel");

            // Audit the failure
            try
            {
                await _auditService.AuditAsync("ReportExportFailed", new { Report = SelectedReportType, FilePath = (string?)null, Format = "Excel", Error = ex.Message, Timestamp = DateTime.UtcNow });
            }
            catch (Exception ax)
            {
                _logger.LogWarning(ax, "Failed to write audit event for failed Excel export");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Refresh the current report.
    /// </summary>
    [RelayCommand]
    public async Task RefreshReportAsync(CancellationToken cancellationToken = default)
    {
        if (ReportViewer == null) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Refreshing report...";

            await _reportService.RefreshReportAsync(ReportViewer);

            StatusMessage = "Report refreshed.";
            _logger.LogDebug("Report refreshed");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to refresh report: {ex.Message}";
            _logger.LogError(ex, "Failed to refresh report");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Validate selected parameters prior to running or exporting a report.
    /// </summary>
    private bool ValidateParameters()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(SelectedReportType))
        {
            ErrorMessage = "Please select a report type.";
            return false;
        }

        if (FromDate > ToDate)
        {
            ErrorMessage = "From date must be earlier than or equal to To date.";
            return false;
        }

        // Merge into parameters dictionary for downstream services
        Parameters["FromDate"] = FromDate;
        Parameters["ToDate"] = ToDate;
        Parameters["ReportTitle"] = SelectedReportType;

        return true;
    }

    /// <summary>
    /// Prepare data sources for the selected report type.
    /// </summary>
    private async Task<Dictionary<string, object>> PrepareDataSourcesAsync(CancellationToken cancellationToken)
    {
        var dataSources = new Dictionary<string, object>();

        // Generate sample data based on report type
        // In production, this would come from real services
        List<ReportDataItem> previewTx = new();
        if (SelectedReportType == "Budget Comparison")
        {
            try
            {
                var entries = (await _budgetRepository.GetByDateRangeAsync(FromDate, ToDate, cancellationToken)).ToList();
                var bcDs = BuildBudgetComparisonDataSet(entries);
                dataSources["BudgetComparison"] = bcDs;

                // populate light preview rows
                previewTx.AddRange(entries.Take(PageSize).Select(e => new ReportDataItem(e.AccountNumber ?? string.Empty, e.Description ?? string.Empty, e.FundType.ToString())).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to prepare Budget Comparison data sources");
            }
        }
        else
        {
            await Task.Run(() =>
            {
                switch (SelectedReportType)
                {
                    case "Budget Summary":
                        dataSources["BudgetData"] = GenerateSampleBudgetData();
                        break;
                    case "Account List":
                        dataSources["AccountData"] = GenerateSampleAccountData();
                        break;
                    case "Monthly Transactions":
                        // Simulate large dataset and update preview with pagination
                        var allTx = GenerateSampleTransactionData(500); // larger set for pagination
                        dataSources["TransactionData"] = allTx;

                        // set preview page
                        var start = (CurrentPage - 1) * PageSize;
                        previewTx.AddRange(allTx.Skip(start).Take(PageSize)
                                .Select(t => new ReportDataItem(t.Date.ToShortDateString(), t.TransactionId, t.Category))
                                .ToList());

                        break;
                    case "Category Breakdown":
                        dataSources["CategoryData"] = GenerateSampleCategoryData();
                        break;
                    case "Variance Analysis":
                        dataSources["VarianceData"] = GenerateSampleVarianceData();
                        break;
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        // Apply preview data to UI-bound collection on the calling context (UI thread)
        // Note: ConfigureAwait(false) means we may not be on UI thread, so don't touch PreviewData here
        // Instead, only update previewTx list in the Task.Run above and clear it now
        try
        {
            PreviewData.Clear();
            foreach (var p in previewTx) PreviewData.Add(p);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply preview data to UI-bound collection");
        }

        _logger.LogDebug("Prepared {Count} data sources for {ReportType}", dataSources.Count, SelectedReportType);
        return dataSources;
    }

    [RelayCommand]
    private async Task LoadPreviewAsync(CancellationToken cancellationToken = default)
    {
        // A lightweight preview load that prepares data sources and populates PreviewData
        try
        {
            IsBusy = true;
            StatusMessage = "Preparing preview...";

            await PrepareDataSourcesAsync(cancellationToken);

            StatusMessage = "Preview ready";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Preview cancelled";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        CurrentPage++;
        // fire-and-forget preview reload; callers (UI) may await if they use ExecuteAsync
        _ = LoadPreviewAsync();
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            _ = LoadPreviewAsync();
        }
    }

    #region Sample Data Generation (Replace with real service calls in production)

    private DataSet BuildBudgetComparisonDataSet(IEnumerable<BudgetEntry> entries)
    {
        var revenues = new DataTable("Revenues");
        var expenses = new DataTable("Expenses");

        // Columns required by the FRX template
        Action<DataTable> addColumns = dt =>
        {
            dt.Columns.Add("Account", typeof(string));
            dt.Columns.Add("Description", typeof(string));
            dt.Columns.Add("ProposedBudget", typeof(decimal));
            dt.Columns.Add("Actual_11_2025", typeof(decimal));
            dt.Columns.Add("Remaining", typeof(decimal));
            dt.Columns.Add("PercentOfBudget", typeof(decimal)); // fractional 0..1
        };

        addColumns(revenues);
        addColumns(expenses);

        foreach (var e in entries ?? Enumerable.Empty<BudgetEntry>())
        {
            try
            {
                var account = e.AccountNumber ?? string.Empty;
                var description = e.Description ?? string.Empty;
                var proposed = e.BudgetedAmount;
                var actual = e.ActualAmount;
                var remaining = e.Remaining;
                var percent = e.PercentOfBudgetFraction; // 0..1

                // Classify revenue vs expense
                var acctType = e.MunicipalAccount?.Type;
                var isRevenue = acctType.HasValue
                    ? acctType.Value == AccountType.Revenue
                    : (!string.IsNullOrWhiteSpace(account) && account.TrimStart().StartsWith("4"));

                if (isRevenue)
                {
                    revenues.Rows.Add(account, description, proposed, actual, remaining, percent);
                }
                else
                {
                    expenses.Rows.Add(account, description, proposed, actual, remaining, percent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping budget entry while building BudgetComparison dataset");
            }
        }

        var ds = new DataSet("BudgetComparison");
        ds.Tables.Add(revenues);
        ds.Tables.Add(expenses);
        return ds;
    }

    private List<BudgetSummaryItem> GenerateSampleBudgetData()
    {
        return
        [
            new("General Fund", 1500000m, 1200000m),
            new("Water Fund", 800000m, 750000m),
            new("Sewer Fund", 600000m, 580000m),
            new("Streets Fund", 400000m, 420000m),
            new("Parks Fund", 200000m, 180000m)
        ];
    }

    private List<AccountItem> GenerateSampleAccountData()
    {
        return
        [
            new("1000", "Cash - General", "Asset", 250000m),
            new("1100", "Accounts Receivable", "Asset", 75000m),
            new("2000", "Accounts Payable", "Liability", 45000m),
            new("3000", "Fund Balance", "Equity", 500000m),
            new("4000", "Property Tax Revenue", "Revenue", 1200000m),
            new("5000", "Personnel Services", "Expense", 800000m)
        ];
    }

    private List<TransactionItem> GenerateSampleTransactionData(int count = 50)
    {
        var transactions = new List<TransactionItem>();
        var random = new Random(42);
        var categories = new[] { "Payroll", "Utilities", "Supplies", "Maintenance", "Revenue" };

        // Prevent invalid range
        var daysRange = Math.Max(1, (ToDate - FromDate).Days);

        for (int i = 0; i < count; i++)
        {
            var date = FromDate.AddDays(random.Next(daysRange));
            var category = categories[random.Next(categories.Length)];
            var amount = (decimal)(random.NextDouble() * 10000);
            transactions.Add(new(date, $"TXN-{i + 1000}", category, amount));
        }

        return transactions.OrderBy(t => t.Date).ToList();
    }

    private List<CategoryBreakdownItem> GenerateSampleCategoryData()
    {
        return
        [
            new("Personnel", 800000m, 53.3m),
            new("Operations", 300000m, 20.0m),
            new("Capital", 200000m, 13.3m),
            new("Debt Service", 150000m, 10.0m),
            new("Other", 50000m, 3.3m)
        ];
    }

    private List<VarianceItem> GenerateSampleVarianceData()
    {
        return
        [
            new("General Fund", 1500000m, 1200000m, 300000m, 20.0m),
            new("Water Fund", 800000m, 750000m, 50000m, 6.3m),
            new("Sewer Fund", 600000m, 580000m, 20000m, 3.3m),
            new("Streets Fund", 400000m, 420000m, -20000m, -5.0m),
            new("Parks Fund", 200000m, 180000m, 20000m, 10.0m)
        ];
    }

    /// <summary>
    /// Alias for PrintAsync to match panel expectations
    /// </summary>
    public async Task PrintReportAsync(CancellationToken cancellationToken = default)
    {
        await PrintAsync(cancellationToken);
    }

    /// <summary>
    /// Disposes of resources used by the ViewModel.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of resources used by the ViewModel.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up managed resources if needed
        }
        // Clean up unmanaged resources if any
        _logger.LogDebug("ReportsViewModel disposed");
    }

    #endregion
}

#region Report Data Models

/// <summary>
/// Generic report data item for preview grid.
/// </summary>
public record ReportDataItem(string Name, string Value, string Category);

/// <summary>
/// Budget summary report data.
/// </summary>
public record BudgetSummaryItem(string FundName, decimal BudgetAmount, decimal ActualAmount)
{
    public decimal Variance => BudgetAmount - ActualAmount;
    public decimal PercentUsed => BudgetAmount > 0 ? (ActualAmount / BudgetAmount) * 100 : 0;
}

/// <summary>
/// Account list report data.
/// </summary>
public record AccountItem(string AccountNumber, string AccountName, string AccountType, decimal Balance);

/// <summary>
/// Transaction report data.
/// </summary>
public record TransactionItem(DateTime Date, string TransactionId, string Category, decimal Amount);

/// <summary>
/// Category breakdown report data.
/// </summary>
public record CategoryBreakdownItem(string Category, decimal Amount, decimal Percentage);

/// <summary>
/// Variance analysis report data.
/// </summary>
public record VarianceItem(string FundName, decimal Budget, decimal Actual, decimal Variance, decimal VariancePercent);

#endregion
