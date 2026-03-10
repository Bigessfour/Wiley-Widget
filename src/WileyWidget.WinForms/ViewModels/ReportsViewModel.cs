using System;
using System.Collections;
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
/// Delegates report viewing to IReportService and file export to IReportExportService.
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
    /// Reserved for future preview host integrations.
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

    private static string Normalize(string? input)
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

    /// <summary>
    /// Attempts to synchronize the selected report type from a display name, file name, or report path.
    /// </summary>
    public bool TrySelectReportType(string reportIdentifier)
    {
        if (!TryResolveReportDisplayName(reportIdentifier, out var resolvedReportType))
        {
            return false;
        }

        if (!string.Equals(SelectedReportType, resolvedReportType, StringComparison.Ordinal))
        {
            SelectedReportType = resolvedReportType;
        }

        return true;
    }

    private bool TryResolveReportDisplayName(string reportIdentifier, out string resolvedReportType)
    {
        resolvedReportType = string.Empty;

        var trimmedIdentifier = reportIdentifier?.Trim() ?? string.Empty;
        var normalizedIdentifier = Normalize(Path.GetFileNameWithoutExtension(trimmedIdentifier) ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedIdentifier))
        {
            return false;
        }

        foreach (var mapping in _displayNameToFile)
        {
            if (Normalize(mapping.Key) == normalizedIdentifier ||
                Normalize(Path.GetFileNameWithoutExtension(mapping.Value) ?? string.Empty) == normalizedIdentifier)
            {
                resolvedReportType = mapping.Key;
                return true;
            }
        }

        var knownFriendlyName = AvailableReportTypes.FirstOrDefault(reportType => Normalize(reportType) == normalizedIdentifier);
        if (!string.IsNullOrWhiteSpace(knownFriendlyName))
        {
            resolvedReportType = knownFriendlyName;
            return true;
        }

        var knownTemplate = ReportTemplates.FirstOrDefault(templateName => Normalize(templateName) == normalizedIdentifier);
        if (!string.IsNullOrWhiteSpace(knownTemplate))
        {
            resolvedReportType = SplitCamelCase(knownTemplate);
            return true;
        }

        return false;
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
            await Task.Yield(); // Ensure async execution for UI thread safety
            StatusMessage = $"Zoom preference set to {percent}% for report preview workflows.";
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
        StatusMessage = "Search is available in the opened preview window or exported preview file.";
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task ToggleParametersPanelAsync(CancellationToken cancellationToken = default)
    {
        ShowParametersPanel = !ShowParametersPanel;
        StatusMessage = ShowParametersPanel ? "Parameters panel opened." : "Parameters panel hidden.";
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the FRX file path for the selected report type.
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
            IsLoading = true;
            HasReportLoaded = false;
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

            await _reportService.SetReportParametersAsync(ReportViewer, new Dictionary<string, object>(Parameters), cancellationToken);

            // Honor cancellation again
            cancellationToken.ThrowIfCancellationRequested();

            StatusMessage = $"Report generated: {SelectedReportType}";
            HasReportLoaded = true;
            _logger.LogInformation("Report generated successfully: {ReportType}", SelectedReportType);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Report generation cancelled.";
            HasReportLoaded = false;
            _logger.LogDebug("Report generation cancelled");
        }
        catch (FileNotFoundException ex)
        {
            ErrorMessage = $"Report template not found: {ex.FileName}";
            HasReportLoaded = false;
            _logger.LogError(ex, "Report template not found");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to generate report: {ex.Message}";
            HasReportLoaded = false;
            _logger.LogError(ex, "Failed to generate report: {ReportType}", SelectedReportType);
        }
        finally
        {
            IsLoading = false;
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
            IsLoading = true;
            HasReportLoaded = false;
            ErrorMessage = null;
            StatusMessage = "Loading report...";

            TrySelectReportType(reportPath);

            _logger.LogInformation("Loading report from path: {ReportPath}", reportPath);

            // Prepare data sources (use default for now)
            var dataSources = await PrepareDataSourcesAsync(cancellationToken);

            // Honor cancellation after heavy work
            cancellationToken.ThrowIfCancellationRequested();

            // Load report into viewer
            var progress = new Progress<double>(p => StatusMessage = $"Loading report... {p:P0}");
            await _reportService.LoadReportAsync(ReportViewer, reportPath, dataSources, progress, cancellationToken);

            await _reportService.SetReportParametersAsync(ReportViewer, new Dictionary<string, object>(Parameters), cancellationToken);

            // Honor cancellation again
            cancellationToken.ThrowIfCancellationRequested();

            StatusMessage = $"Report loaded: {Path.GetFileName(reportPath)}";
            HasReportLoaded = true;
            _logger.LogInformation("Report loaded successfully from: {ReportPath}", reportPath);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Report loading cancelled.";
            HasReportLoaded = false;
            _logger.LogDebug("Report loading cancelled");
        }
        catch (FileNotFoundException ex)
        {
            ErrorMessage = $"Report file not found: {ex.FileName}";
            HasReportLoaded = false;
            _logger.LogError(ex, "Report file not found: {FileName}", ex.FileName);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load report: {ex.Message}";
            HasReportLoaded = false;
            _logger.LogError(ex, "Failed to load report from {ReportPath}: {Message}", reportPath, ex.Message);
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
        }
    }

    /// <summary>
    /// Export the current report to PDF.
    /// </summary>
    [RelayCommand]
    public async Task ExportToPdfAsync(CancellationToken cancellationToken = default)
    {
        var fileName = $"{SelectedReportType.Replace(" ", "", StringComparison.Ordinal)}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        var filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "WileyWidget",
            "Reports",
            fileName
        );

        await ExportToPdfFileAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// Export the current report to PDF at the specified file path.
    /// </summary>
    public async Task ExportToPdfFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (_exportService == null)
        {
            ErrorMessage = "Report export service is not initialized.";
            return;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            ErrorMessage = "Export path cannot be empty.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = "Exporting to PDF...";

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var exportDocument = await BuildExportDocumentAsync(cancellationToken);
            await _exportService.ExportToPdfAsync(exportDocument, filePath, cancellationToken);

            StatusMessage = $"Exported to: {filePath}";
            _logger.LogInformation("Report exported to PDF: {FilePath}", filePath);

            try
            {
                await _auditService.AuditAsync("ReportGenerated", new { Report = SelectedReportType, Path = filePath, Format = "PDF", Timestamp = DateTime.UtcNow });
            }
            catch (Exception ax)
            {
                _logger.LogWarning(ax, "Failed to write audit event for report export (PDF)");
            }

            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to export PDF: {ex.Message}";
            _logger.LogError(ex, "Failed to export report to PDF");

            try
            {
                await _auditService.AuditAsync("ReportExportFailed", new { Report = SelectedReportType, FilePath = filePath, Format = "PDF", Error = ex.Message, Timestamp = DateTime.UtcNow });
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
        var fileName = $"{SelectedReportType.Replace(" ", "", StringComparison.Ordinal)}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "WileyWidget",
            "Reports",
            fileName
        );

        await ExportToExcelFileAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// Export the current report to Excel at the specified file path.
    /// </summary>
    public async Task ExportToExcelFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (_exportService == null)
        {
            ErrorMessage = "Report export service is not initialized.";
            return;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            ErrorMessage = "Export path cannot be empty.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = "Exporting to Excel...";

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var exportDocument = await BuildExportDocumentAsync(cancellationToken);
            await _exportService.ExportToExcelAsync(exportDocument, filePath, cancellationToken);

            StatusMessage = $"Exported to: {filePath}";
            _logger.LogInformation("Report exported to Excel: {FilePath}", filePath);

            try
            {
                await _auditService.AuditAsync("ReportGenerated", new { Report = SelectedReportType, Path = filePath, Format = "Excel", Timestamp = DateTime.UtcNow });
            }
            catch (Exception ax)
            {
                _logger.LogWarning(ax, "Failed to write audit event for report export (Excel)");
            }

            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to export Excel: {ex.Message}";
            _logger.LogError(ex, "Failed to export report to Excel");

            try
            {
                await _auditService.AuditAsync("ReportExportFailed", new { Report = SelectedReportType, FilePath = filePath, Format = "Excel", Error = ex.Message, Timestamp = DateTime.UtcNow });
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
            IsLoading = true;
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
            IsLoading = false;
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

    private async Task<ReportExportDocument> BuildExportDocumentAsync(CancellationToken cancellationToken)
    {
        if (!ValidateParameters())
        {
            throw new InvalidOperationException(ErrorMessage ?? "Report parameters are invalid.");
        }

        var dataSources = await PrepareDataSourcesAsync(cancellationToken);
        var sections = CreateExportSections(dataSources);

        if (sections.Count == 0 && PreviewData.Count > 0)
        {
            sections.Add(new ReportExportSection(
                Title: "Preview",
                Columns: ["Name", "Value", "Category"],
                Rows: PreviewData
                    .Select(item => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
                    {
                        ["Name"] = item.Name,
                        ["Value"] = item.Value,
                        ["Category"] = item.Category,
                    })
                    .ToList()));
        }

        if (sections.Count == 0)
        {
            sections.Add(new ReportExportSection(
                Title: "Status",
                Columns: ["Field", "Value"],
                Rows:
                [
                    new Dictionary<string, string>
                    {
                        ["Field"] = "Message",
                        ["Value"] = "No report rows were available for export.",
                    },
                ]));
        }

        var metadata = new Dictionary<string, string>
        {
            ["Period"] = $"{FromDate:yyyy-MM-dd} to {ToDate:yyyy-MM-dd}",
            ["Template"] = Path.GetFileName(GetReportPath()),
            ["Format Source"] = "Wiley Widget branded Syncfusion export",
        };

        return new ReportExportDocument(
            Title: SelectedReportType,
            Subtitle: "Town of Wiley Municipal Finance Report",
            GeneratedAt: DateTime.Now,
            GeneratedBy: "Generated by Wiley Widget and Grok",
            Branding: new ReportBrandingInfo(
                OrganizationName: "Town of Wiley",
                ApplicationName: "Wiley Widget",
                Attribution: "Generated by Wiley Widget and Grok",
                LogoPath: ResolveReportLogoPath()),
            Sections: sections,
            Metadata: metadata);
    }

    private List<ReportExportSection> CreateExportSections(Dictionary<string, object> dataSources)
    {
        var sections = new List<ReportExportSection>();

        foreach (var (sectionName, source) in dataSources)
        {
            switch (source)
            {
                case DataSet dataSet:
                    foreach (DataTable table in dataSet.Tables)
                    {
                        sections.Add(CreateSectionFromTable(string.IsNullOrWhiteSpace(table.TableName) ? sectionName : table.TableName, table));
                    }
                    break;

                case DataTable table:
                    sections.Add(CreateSectionFromTable(sectionName, table));
                    break;

                case IEnumerable enumerable when source is not string:
                    var items = enumerable.Cast<object>().ToList();
                    if (items.Count > 0)
                    {
                        sections.Add(CreateSectionFromObjects(sectionName, items));
                    }
                    break;

                default:
                    if (source != null)
                    {
                        sections.Add(CreateSectionFromObjects(sectionName, [source]));
                    }
                    break;
            }
        }

        return sections;
    }

    private static ReportExportSection CreateSectionFromTable(string sectionName, DataTable table)
    {
        var columns = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToList();
        var rows = table.Rows.Cast<DataRow>()
            .Select(row => (IReadOnlyDictionary<string, string>)columns.ToDictionary(
                column => column,
                column => FormatExportValue(row[column])))
            .ToList();

        return new ReportExportSection(sectionName, columns, rows);
    }

    private static ReportExportSection CreateSectionFromObjects(string sectionName, IReadOnlyList<object> items)
    {
        var properties = items[0].GetType().GetProperties()
            .Where(property => property.CanRead)
            .ToArray();

        var columns = properties.Select(property => property.Name).ToList();
        var rows = items
            .Select(item => (IReadOnlyDictionary<string, string>)properties.ToDictionary(
                property => property.Name,
                property => FormatExportValue(property.GetValue(item))))
            .ToList();

        return new ReportExportSection(sectionName, columns, rows);
    }

    private static string FormatExportValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string? ResolveReportLogoPath()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("WILEYWIDGET_REPORT_LOGO_PATH"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Branding", "wiley-report-logo.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Branding", "wiley-report-logo.jpg"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Branding", "wiley-brand-hero.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Branding", "wiley-brand-hero.jpg"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "WileyWidget.WinForms", "Resources", "Branding", "wiley-report-logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "WileyWidget.WinForms", "Resources", "Branding", "wiley-report-logo.jpg"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "WileyWidget.WinForms", "Resources", "Branding", "wiley-brand-hero.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "WileyWidget.WinForms", "Resources", "Branding", "wiley-brand-hero.jpg"),
        };

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    /// <summary>
    /// Prepare data sources for the selected report type.
    /// </summary>
    private async Task<Dictionary<string, object>> PrepareDataSourcesAsync(CancellationToken cancellationToken)
    {
        var dataSources = new Dictionary<string, object>();

        // Prepare report data based on report type
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
                        dataSources["BudgetData"] = CreateEmptyBudgetData();
                        break;
                    case "Account List":
                        dataSources["AccountData"] = CreateEmptyAccountData();
                        break;
                    case "Monthly Transactions":
                        var allTx = CreateEmptyTransactionData();
                        dataSources["TransactionData"] = allTx;

                        // set preview page
                        var start = (CurrentPage - 1) * PageSize;
                        previewTx.AddRange(allTx.Skip(start).Take(PageSize)
                                .Select(t => new ReportDataItem(t.Date.ToShortDateString(), t.TransactionId, t.Category))
                                .ToList());

                        break;
                    case "Category Breakdown":
                        dataSources["CategoryData"] = CreateEmptyCategoryData();
                        break;
                    case "Variance Analysis":
                        dataSources["VarianceData"] = CreateEmptyVarianceData();
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

    #region Empty Data Helpers

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

    private List<BudgetSummaryItem> CreateEmptyBudgetData()
    {
        return new List<BudgetSummaryItem>();
    }

    private List<AccountItem> CreateEmptyAccountData()
    {
        return new List<AccountItem>();
    }

    private List<TransactionItem> CreateEmptyTransactionData()
    {
        return new List<TransactionItem>();
    }

    private List<CategoryBreakdownItem> CreateEmptyCategoryData()
    {
        return new List<CategoryBreakdownItem>();
    }

    private List<VarianceItem> CreateEmptyVarianceData()
    {
        return new List<VarianceItem>();
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
