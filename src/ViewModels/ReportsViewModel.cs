#nullable enable

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for the Reports section of the application
/// </summary>
public class ReportsViewModel : AsyncViewModelBase
{
    private string? _selectedReportType;
    private string? _selectedFormat;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private bool _includeCharts;
    private int _enterpriseId;
    private ObservableCollection<Enterprise> _enterprises = new();
    private string _filter = string.Empty;
    private double _progressPercentage;
    private ObservableCollection<object> _reportItems = new();
    private string _statusMessage = "Ready";
    private readonly ISettingsService _settingsService;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IAuditRepository _auditRepository;
    private ReportData? _currentReportData;

    /// <summary>
    /// Gets the collection of available report types
    /// </summary>
    public ObservableCollection<string> ReportTypes { get; } = new()
    {
        "Budget Summary",
        "Variance Analysis",
        "Department Report",
        "Fund Report",
        "Audit Trail",
        "Year-End Summary"
    };

    /// <summary>
    /// Gets the collection of available export formats
    /// </summary>
    public ObservableCollection<string> ExportFormats { get; } = new()
    {
        "PDF",
        "Excel",
        "CSV",
        "Word"
    };

    /// <summary>
    /// Gets or sets the selected report type
    /// </summary>
    public string? SelectedReportType
    {
        get => _selectedReportType;
        set => SetProperty(ref _selectedReportType, value);
    }

    /// <summary>
    /// Gets or sets the selected export format
    /// </summary>
    public string? SelectedFormat
    {
        get => _selectedFormat;
        set => SetProperty(ref _selectedFormat, value);
    }

    /// <summary>
    /// Gets or sets the start date for the report
    /// </summary>
    public DateTime? StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    /// <summary>
    /// Gets or sets the end date for the report
    /// </summary>
    public DateTime? EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to include charts in the report
    /// </summary>
    public bool IncludeCharts
    {
        get => _includeCharts;
        set => SetProperty(ref _includeCharts, value);
    }

    /// <summary>
    /// Gets the command to generate the selected report
    /// </summary>
    public IAsyncRelayCommand GenerateReportCommand { get; }

    /// <summary>
    /// Gets the command to preview the report
    /// </summary>
    public IAsyncRelayCommand PreviewReportCommand { get; }

    /// <summary>
    /// Gets the command to save report settings as default
    /// </summary>
    public ICommand SaveSettingsCommand { get; }

    /// <summary>
    /// Gets the command to export reports
    /// </summary>
    public IAsyncRelayCommand<string> ExportCommand { get; }

    /// <summary>
    /// Gets or sets the selected enterprise ID for filtering reports
    /// </summary>
    public int EnterpriseId
    {
        get => _enterpriseId;
        set => SetProperty(ref _enterpriseId, value);
    }

    /// <summary>
    /// Gets the collection of available enterprises for filtering
    /// </summary>
    public ObservableCollection<Enterprise> Enterprises => _enterprises;

    /// <summary>
    /// Gets or sets the filter text for reports
    /// </summary>
    public string Filter
    {
        get => _filter;
        set => SetProperty(ref _filter, value);
    }

    /// <summary>
    /// Gets the current report data
    /// </summary>
    public ReportData? CurrentReportData
    {
        get => _currentReportData;
        private set => SetProperty(ref _currentReportData, value);
    }

    /// <summary>
    /// Gets or sets the progress percentage for report generation
    /// </summary>
    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    /// <summary>
    /// Gets the collection of report items for display
    /// </summary>
    public ObservableCollection<object> ReportItems => _reportItems;

    /// <summary>
    /// Gets or sets the status message for report operations
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Event raised when report data has been loaded
    /// </summary>
    public event EventHandler<ReportDataEventArgs>? DataLoaded;

    /// <summary>
    /// Event raised when report export has completed
    /// </summary>
    public event EventHandler<ReportExportCompletedEventArgs>? ExportCompleted;

    /// <summary>
    /// Initializes a new instance of the ReportsViewModel class
    /// </summary>
    /// <param name="dispatcherHelper">The dispatcher helper for UI thread operations</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="settingsService">The settings service for persisting user preferences</param>
    /// <param name="budgetRepository">The budget repository for data access</param>
    /// <param name="auditRepository">The audit repository for audit trail data</param>
    public ReportsViewModel(IDispatcherHelper dispatcherHelper, Microsoft.Extensions.Logging.ILogger<ReportsViewModel> logger, ISettingsService settingsService, IBudgetRepository budgetRepository, IAuditRepository auditRepository)
        : base(dispatcherHelper, logger)
    {
        _settingsService = settingsService;
        _budgetRepository = budgetRepository;
        _auditRepository = auditRepository;
        GenerateReportCommand = new AsyncRelayCommand(GenerateReportAsync, CanGenerateReport);
        PreviewReportCommand = new AsyncRelayCommand(PreviewReportAsync, CanPreviewReport);
        SaveSettingsCommand = new RelayCommand(SaveSettings, CanSaveSettings);
        ExportCommand = new AsyncRelayCommand<string>(ExportReportAsync, CanExportReport);

        // Load saved settings
        LoadSavedSettings();

        // Set default dates if not loaded from settings
        if (!StartDate.HasValue)
            StartDate = DateTime.Today.AddMonths(-1);
        if (!EndDate.HasValue)
            EndDate = DateTime.Today;
    }

    private void LoadSavedSettings()
    {
        try
        {
            var settings = _settingsService.Current;

            // Load saved report preferences
            if (!string.IsNullOrWhiteSpace(settings.LastSelectedReportType))
                SelectedReportType = settings.LastSelectedReportType;

            if (!string.IsNullOrWhiteSpace(settings.LastSelectedFormat))
                SelectedFormat = settings.LastSelectedFormat;

            if (settings.LastReportStartDate.HasValue)
                StartDate = settings.LastReportStartDate.Value;

            if (settings.LastReportEndDate.HasValue)
                EndDate = settings.LastReportEndDate.Value;

            IncludeCharts = settings.IncludeChartsInReports;

            if (settings.LastSelectedEnterpriseId > 0)
                EnterpriseId = settings.LastSelectedEnterpriseId;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load saved report settings, using defaults");
        }
    }

    private bool CanGenerateReport()
    {
        return !IsBusy &&
               !string.IsNullOrWhiteSpace(SelectedReportType) &&
               !string.IsNullOrWhiteSpace(SelectedFormat) &&
               StartDate.HasValue &&
               EndDate.HasValue &&
               StartDate <= EndDate;
    }

    private bool CanPreviewReport()
    {
        return !IsBusy &&
               !string.IsNullOrWhiteSpace(SelectedReportType) &&
               StartDate.HasValue &&
               EndDate.HasValue &&
               StartDate <= EndDate;
    }

    private bool CanSaveSettings()
    {
        return !string.IsNullOrWhiteSpace(SelectedReportType) &&
               !string.IsNullOrWhiteSpace(SelectedFormat);
    }

    private async Task GenerateReportAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(SelectedReportType))
            {
                throw new InvalidOperationException("Please select a report type.");
            }

            if (!StartDate.HasValue || !EndDate.HasValue)
            {
                throw new InvalidOperationException("Please specify both start and end dates.");
            }

            if (StartDate > EndDate)
            {
                throw new InvalidOperationException("Start date cannot be after end date.");
            }

            // Generate report based on selected type
            var data = SelectedReportType switch
            {
                "Budget Summary" => await GenerateBudgetSummaryReportAsync(),
                "Variance Analysis" => await GenerateVarianceAnalysisReportAsync(),
                "Department Report" => await GenerateDepartmentReportAsync(),
                "Fund Report" => await GenerateFundReportAsync(),
                "Audit Trail" => await GenerateAuditTrailReportAsync(),
                "Year-End Summary" => await GenerateYearEndSummaryReportAsync(),
                _ => throw new InvalidOperationException($"Unknown report type: {SelectedReportType}")
            };

            OnReportDataLoaded(data);
        }, $"Generating {SelectedReportType} report in {SelectedFormat} format...");
    }

    private async Task<ReportData> GenerateBudgetSummaryReportAsync()
    {
        try
        {
            var startDate = StartDate ?? DateTime.Today.AddMonths(-1);
            var endDate = EndDate ?? DateTime.Today;
            
            var summary = await _budgetRepository.GetBudgetSummaryAsync(startDate, endDate);

            return new ReportData
            {
                Title = "Budget Summary Report",
                GeneratedAt = DateTime.Now,
                BudgetSummary = summary
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating budget summary report");
            return new ReportData
            {
                Title = "Budget Summary Report - Error",
                GeneratedAt = DateTime.Now
            };
        }
    }

    private async Task<ReportData> GenerateVarianceAnalysisReportAsync()
    {
        try
        {
            var startDate = StartDate ?? DateTime.Today.AddMonths(-1);
            var endDate = EndDate ?? DateTime.Today;
            
            var analysis = await _budgetRepository.GetVarianceAnalysisAsync(startDate, endDate);
            
            return new ReportData
            {
                Title = "Budget Variance Analysis Report",
                GeneratedAt = DateTime.Now,
                VarianceAnalysis = analysis
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating variance analysis report");
            return new ReportData
            {
                Title = "Budget Variance Analysis Report - Error",
                GeneratedAt = DateTime.Now
            };
        }
    }    private async Task<ReportData> GenerateDepartmentReportAsync()
    {
        try
        {
            var startDate = StartDate ?? DateTime.Today.AddMonths(-1);
            var endDate = EndDate ?? DateTime.Today;
            
            var departments = await _budgetRepository.GetDepartmentBreakdownAsync(startDate, endDate);

            return new ReportData
            {
                Title = "Department Budget Report",
                GeneratedAt = DateTime.Now,
                Departments = departments
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating department report");
            return new ReportData
            {
                Title = "Department Budget Report - Error",
                GeneratedAt = DateTime.Now
            };
        }
    }

    private async Task<ReportData> GenerateFundReportAsync()
    {
        var startDate = StartDate ?? DateTime.Today.AddMonths(-1);
        var endDate = EndDate ?? DateTime.Today;
        
        var funds = await _budgetRepository.GetFundAllocationsAsync(startDate, endDate);

        return new ReportData
        {
            Title = "Fund Allocation Report",
            GeneratedAt = DateTime.Now,
            Funds = funds
        };
    }

    private async Task<ReportData> GenerateAuditTrailReportAsync()
    {
        var startDate = StartDate ?? DateTime.Today.AddMonths(-1);
        var endDate = EndDate ?? DateTime.Today;
        
        var auditEntries = await _auditRepository.GetAuditTrailAsync(startDate, endDate);

        return new ReportData
        {
            Title = "Budget Audit Trail Report",
            GeneratedAt = DateTime.Now,
            AuditEntries = auditEntries
        };
    }

    private async Task<ReportData> GenerateYearEndSummaryReportAsync()
    {
        var year = DateTime.Now.Year;
        
        var summary = await _budgetRepository.GetYearEndSummaryAsync(year);

        return new ReportData
        {
            Title = $"Year-End Budget Summary Report - {year}",
            GeneratedAt = DateTime.Now,
            YearEndSummary = summary
        };
    }

    private async Task PreviewReportAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Generate preview data (lighter version of full report)
            var data = SelectedReportType switch
            {
                "Budget Summary" => await GenerateBudgetSummaryPreviewAsync(),
                "Variance Analysis" => await GenerateVarianceAnalysisPreviewAsync(),
                "Department Report" => await GenerateDepartmentReportPreviewAsync(),
                "Fund Report" => await GenerateFundReportPreviewAsync(),
                "Audit Trail" => await GenerateAuditTrailPreviewAsync(),
                "Year-End Summary" => await GenerateYearEndSummaryPreviewAsync(),
                _ => new ReportData
                {
                    Title = "Unknown Report Type Preview",
                    GeneratedAt = DateTime.Now
                }
            };

            OnReportDataLoaded(data);
        }, $"Generating preview for {SelectedReportType} report...");
    }

    private async Task<ReportData> GenerateBudgetSummaryPreviewAsync()
    {
        var startDate = StartDate ?? DateTime.Today.AddMonths(-1);
        var endDate = EndDate ?? DateTime.Today;
        
        var summary = await _budgetRepository.GetBudgetSummaryAsync(startDate, endDate);
        
        return new ReportData
        {
            Title = "Budget Summary Report (Preview)",
            GeneratedAt = DateTime.Now,
            BudgetSummary = summary
        };
    }

    private async Task<ReportData> GenerateVarianceAnalysisPreviewAsync()
    {
        var startDate = StartDate ?? DateTime.Today.AddMonths(-1);
        var endDate = EndDate ?? DateTime.Today;
        
        var analysis = await _budgetRepository.GetVarianceAnalysisAsync(startDate, endDate);
        
        return new ReportData
        {
            Title = "Budget Variance Analysis Report (Preview)",
            GeneratedAt = DateTime.Now,
            VarianceAnalysis = analysis
        };
    }

    private async Task<ReportData> GenerateDepartmentReportPreviewAsync()
    {
        var startDate = StartDate ?? DateTime.Today.AddMonths(-1);
        var endDate = EndDate ?? DateTime.Today;
        
        var departments = await _budgetRepository.GetDepartmentBreakdownAsync(startDate, endDate);
        
        return new ReportData
        {
            Title = "Department Budget Report (Preview)",
            GeneratedAt = DateTime.Now,
            Departments = departments
        };
    }

    private async Task<ReportData> GenerateFundReportPreviewAsync()
    {
        var startDate = StartDate ?? DateTime.Today.AddMonths(-1);
        var endDate = EndDate ?? DateTime.Today;
        
        var funds = await _budgetRepository.GetFundAllocationsAsync(startDate, endDate);
        
        return new ReportData
        {
            Title = "Fund Allocation Report (Preview)",
            GeneratedAt = DateTime.Now,
            Funds = funds
        };
    }

    private async Task<ReportData> GenerateAuditTrailPreviewAsync()
    {
        var startDate = StartDate ?? DateTime.Today.AddMonths(-1);
        var endDate = EndDate ?? DateTime.Today;
        
        var auditEntries = await _auditRepository.GetAuditTrailAsync(startDate, endDate);
        
        return new ReportData
        {
            Title = "Budget Audit Trail Report (Preview)",
            GeneratedAt = DateTime.Now,
            AuditEntries = auditEntries
        };
    }

    private async Task<ReportData> GenerateYearEndSummaryPreviewAsync()
    {
        var year = DateTime.Now.Year;
        
        var summary = await _budgetRepository.GetYearEndSummaryAsync(year);
        
        return new ReportData
        {
            Title = $"Year-End Budget Summary Report (Preview) - {year}",
            GeneratedAt = DateTime.Now,
            YearEndSummary = summary
        };
    }

    private void SaveSettings()
    {
        try
        {
            var settings = _settingsService.Current;

            // Save current report preferences
            settings.LastSelectedReportType = SelectedReportType;
            settings.LastSelectedFormat = SelectedFormat;
            settings.LastReportStartDate = StartDate;
            settings.LastReportEndDate = EndDate;
            settings.IncludeChartsInReports = IncludeCharts;
            settings.LastSelectedEnterpriseId = EnterpriseId;

            // Save to disk
            _settingsService.Save();

            StatusMessage = "Report settings saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving report settings: {ex.Message}";
            Logger.LogError(ex, "Failed to save report settings");
        }
    }

    private bool CanExportReport(string? format)
    {
        return !IsBusy &&
               !string.IsNullOrWhiteSpace(format) &&
               !string.IsNullOrWhiteSpace(SelectedReportType);
    }

    private async Task ExportReportAsync(string? format)
    {
        if (string.IsNullOrWhiteSpace(format) || CurrentReportData == null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "WileyWidget", "Reports", "Exports");
            Directory.CreateDirectory(directory);

            var safeTitle = string.IsNullOrWhiteSpace(SelectedReportType)
                ? "Report"
                : SelectedReportType.Replace(' ', '_');

            var fileName = $"{safeTitle}_{DateTime.Now:yyyyMMddHHmmss}.{format.ToLowerInvariant()}";
            var filePath = Path.Combine(directory, fileName);

            // Generate actual report content based on format
            var content = GenerateReportContent(CurrentReportData, format);
            await File.WriteAllTextAsync(filePath, content);

            OnExportCompleted(filePath, format);
        }, $"Exporting {SelectedReportType} report to {format.ToUpperInvariant()}...");
    }

    private void OnReportDataLoaded(ReportData data)
    {
        CurrentReportData = data;
        DataLoaded?.Invoke(this, new ReportDataEventArgs { Data = data });
    }

    private void OnExportCompleted(string filePath, string format)
    {
        ExportCompleted?.Invoke(this, new ReportExportCompletedEventArgs
        {
            FilePath = filePath,
            Format = format
        });
    }

    private string GenerateReportContent(ReportData reportData, string format)
    {
        var builder = new System.Text.StringBuilder();
        
        builder.AppendLine($"Report: {reportData.Title}");
        builder.AppendLine($"Generated: {reportData.GeneratedAt:O}");
        builder.AppendLine(new string('=', 50));
        builder.AppendLine();

        if (reportData.BudgetSummary != null)
        {
            builder.AppendLine("BUDGET SUMMARY");
            builder.AppendLine($"Period: {reportData.BudgetSummary.BudgetPeriod}");
            builder.AppendLine($"Total Budgeted: {reportData.BudgetSummary.TotalBudgeted:C}");
            builder.AppendLine($"Total Actual: {reportData.BudgetSummary.TotalActual:C}");
            builder.AppendLine($"Total Variance: {reportData.BudgetSummary.TotalVariance:C} ({reportData.BudgetSummary.TotalVariancePercentage:F2}%)");
            builder.AppendLine();
            
            if (reportData.BudgetSummary.FundSummaries.Any())
            {
                builder.AppendLine("FUND BREAKDOWN:");
                foreach (var fund in reportData.BudgetSummary.FundSummaries)
                {
                    builder.AppendLine($"  {fund.FundName}: Budgeted {fund.TotalBudgeted:C}, Actual {fund.TotalActual:C}, Variance {fund.Variance:C}");
                }
                builder.AppendLine();
            }
        }

        if (reportData.VarianceAnalysis != null)
        {
            builder.AppendLine("VARIANCE ANALYSIS");
            builder.AppendLine($"Total Variance: {reportData.VarianceAnalysis.TotalVariance:C}");
            builder.AppendLine($"Variance Percentage: {reportData.VarianceAnalysis.TotalVariancePercentage:F2}%");
            builder.AppendLine();
        }

        if (reportData.Departments != null && reportData.Departments.Any())
        {
            builder.AppendLine("DEPARTMENT BREAKDOWN:");
            foreach (var dept in reportData.Departments)
            {
                builder.AppendLine($"  {dept.DepartmentName}: Budgeted {dept.TotalBudgeted:C}, Actual {dept.TotalActual:C}");
            }
            builder.AppendLine();
        }

        if (reportData.Funds != null && reportData.Funds.Any())
        {
            builder.AppendLine("FUND ALLOCATIONS:");
            foreach (var fund in reportData.Funds)
            {
                builder.AppendLine($"  {fund.FundName}: Budgeted {fund.TotalBudgeted:C}, Actual {fund.TotalActual:C}");
            }
            builder.AppendLine();
        }

        if (reportData.AuditEntries != null && reportData.AuditEntries.Any())
        {
            builder.AppendLine("AUDIT TRAIL:");
            foreach (var entry in reportData.AuditEntries.Take(20)) // Limit to first 20 entries
            {
                builder.AppendLine($"  {entry.Timestamp:yyyy-MM-dd HH:mm:ss} - {entry.Action} on {entry.EntityType} by {entry.User}");
                if (!string.IsNullOrEmpty(entry.Changes))
                {
                    builder.AppendLine($"    Changes: {entry.Changes}");
                }
            }
            builder.AppendLine();
        }

        if (reportData.YearEndSummary != null)
        {
            builder.AppendLine("YEAR-END SUMMARY");
            builder.AppendLine($"Total Budgeted: {reportData.YearEndSummary.TotalBudgeted:C}");
            builder.AppendLine($"Total Actual: {reportData.YearEndSummary.TotalActual:C}");
            builder.AppendLine($"Year-End Variance: {reportData.YearEndSummary.TotalVariance:C}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// Event arguments for report data loaded events
    /// </summary>
    public class ReportDataEventArgs : EventArgs
    {
        public ReportData Data { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for report export completed events
    /// </summary>
    public class ReportExportCompletedEventArgs : EventArgs
    {
        public string FilePath { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data structure for report information
    /// </summary>
    public class ReportData
    {
        public string Title { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        
        // Budget Summary Report
        public Models.BudgetVarianceAnalysis? BudgetSummary { get; set; }
        
        // Variance Analysis Report
        public Models.BudgetVarianceAnalysis? VarianceAnalysis { get; set; }
        
        // Department Breakdown Report
        public List<Models.DepartmentSummary>? Departments { get; set; }
        
        // Fund Allocations Report
        public List<Models.FundSummary>? Funds { get; set; }
        
        // Audit Trail Report
        public IEnumerable<Models.AuditEntry>? AuditEntries { get; set; }
        
        // Year-End Summary Report
        public Models.BudgetVarianceAnalysis? YearEndSummary { get; set; }
    }
}