using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for the Reports form, managing BoldReports report generation and export.
/// Uses CommunityToolkit.Mvvm for MVVM pattern with source generators.
/// Delegates report operations to IBoldReportService.
/// </summary>
public partial class ReportsViewModel : ObservableObject
{
    private readonly IBoldReportService _reportService;
    private readonly ILogger<ReportsViewModel> _logger;

    /// <summary>
    /// Available report types for the dropdown.
    /// </summary>
    public static readonly string[] AvailableReportTypes =
    [
        "Budget Summary",
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
    private ObservableCollection<ReportDataItem> previewData = [];

    /// <summary>
    /// Reference to the ReportViewer control (set by the Form).
    /// Using object type to avoid WPF reference in ViewModel.
    /// </summary>
    public object? ReportViewer { get; set; }

    /// <summary>
    /// Path to the reports folder.
    /// </summary>
    public string ReportsFolder { get; }

    public ReportsViewModel(IBoldReportService reportService, ILogger<ReportsViewModel> logger)
    {
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Set reports folder path relative to application
        ReportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");

        _logger.LogInformation("ReportsViewModel initialized. Reports folder: {ReportsFolder}", ReportsFolder);
    }

    /// <summary>
    /// Gets the RDL file path for the selected report type.
    /// </summary>
    private string GetReportPath()
    {
        var reportFileName = SelectedReportType switch
        {
            "Budget Summary" => "BudgetSummary.rdlc",
            "Account List" => "AccountList.rdlc",
            "Monthly Transactions" => "MonthlyTransactions.rdlc",
            "Category Breakdown" => "CategoryBreakdown.rdlc",
            "Variance Analysis" => "VarianceAnalysis.rdlc",
            _ => "BudgetSummary.rdlc"
        };

        return Path.Combine(ReportsFolder, reportFileName);
    }

    /// <summary>
    /// Generate and load the selected report into the viewer.
    /// </summary>
    [RelayCommand]
    private async Task GenerateReportAsync(CancellationToken cancellationToken = default)
    {
        if (ReportViewer == null)
        {
            ErrorMessage = "Report viewer not initialized.";
            _logger.LogWarning("GenerateReportAsync called but ReportViewer is null");
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
            var dataSources = await PrepareDataSourcesAsync(cancellationToken);

            // Load report into viewer
            await _reportService.LoadReportAsync(ReportViewer, reportPath, dataSources);

            // Set report parameters (date range)
            var parameters = new Dictionary<string, object>
            {
                { "FromDate", FromDate },
                { "ToDate", ToDate },
                { "ReportTitle", SelectedReportType }
            };
            await _reportService.SetReportParametersAsync(ReportViewer, parameters);

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
    /// Export the current report to PDF.
    /// </summary>
    [RelayCommand]
    private async Task ExportToPdfAsync(CancellationToken cancellationToken = default)
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

            await _reportService.ExportToPdfAsync(ReportViewer, filePath);

            StatusMessage = $"Exported to: {filePath}";
            _logger.LogInformation("Report exported to PDF: {FilePath}", filePath);

            // Open the file location
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to export PDF: {ex.Message}";
            _logger.LogError(ex, "Failed to export report to PDF");
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
    private async Task ExportToExcelAsync(CancellationToken cancellationToken = default)
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

            await _reportService.ExportToExcelAsync(ReportViewer, filePath);

            StatusMessage = $"Exported to: {filePath}";
            _logger.LogInformation("Report exported to Excel: {FilePath}", filePath);

            // Open the file location
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to export Excel: {ex.Message}";
            _logger.LogError(ex, "Failed to export report to Excel");
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
    private async Task RefreshReportAsync(CancellationToken cancellationToken = default)
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
    /// Prepare data sources for the selected report type.
    /// </summary>
    private async Task<Dictionary<string, object>> PrepareDataSourcesAsync(CancellationToken cancellationToken)
    {
        var dataSources = new Dictionary<string, object>();

        // Generate sample data based on report type
        // In production, this would come from real services
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
                    dataSources["TransactionData"] = GenerateSampleTransactionData();
                    break;
                case "Category Breakdown":
                    dataSources["CategoryData"] = GenerateSampleCategoryData();
                    break;
                case "Variance Analysis":
                    dataSources["VarianceData"] = GenerateSampleVarianceData();
                    break;
            }
        }, cancellationToken);

        _logger.LogDebug("Prepared {Count} data sources for {ReportType}", dataSources.Count, SelectedReportType);
        return dataSources;
    }

    #region Sample Data Generation (Replace with real service calls in production)

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

    private List<TransactionItem> GenerateSampleTransactionData()
    {
        var transactions = new List<TransactionItem>();
        var random = new Random(42);
        var categories = new[] { "Payroll", "Utilities", "Supplies", "Maintenance", "Revenue" };

        for (int i = 0; i < 50; i++)
        {
            var date = FromDate.AddDays(random.Next((ToDate - FromDate).Days));
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
