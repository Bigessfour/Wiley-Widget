#nullable enable

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Mvvm;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;

namespace WileyWidget.ViewModels.Main;

/// <summary>
/// ViewModel for budget analysis functionality
/// </summary>
public partial class BudgetAnalysisViewModel : AsyncViewModelBase, IDataErrorInfo
{
    /// <summary>
    /// Represents a compliance issue found during validation
    /// </summary>
    public class ComplianceIssue
    {
        public string Rule { get; set; } = string.Empty;
        public string Severity { get; set; } = "Warning"; // Warning, Error, Info
        public string Description { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public string GasbReference { get; set; } = string.Empty; // GASB standard reference
    }
    /// <summary>
    /// Available budget periods for analysis
    /// </summary>
    public ObservableCollection<string> AvailableBudgetPeriods { get; } = new()
    {
        "Current Year",
        "Last Year",
        "Year to Date",
        "Custom Period"
    };

    /// <summary>
    /// Selected budget period
    /// </summary>
    private string? _selectedBudgetPeriod = "Current Year";
    public string? SelectedBudgetPeriod
    {
        get => _selectedBudgetPeriod;
        set => SetProperty(ref _selectedBudgetPeriod, value);
    }

    /// <summary>
    /// Available analysis types
    /// </summary>
    public ObservableCollection<string> AnalysisTypes { get; } = new()
    {
        "Budget vs Actual",
        "Variance Analysis",
        "Trend Analysis",
        "Fund Analysis"
    };

    /// <summary>
    /// Selected analysis type
    /// </summary>
    private string? _selectedAnalysisType = "Budget vs Actual";
    public string? SelectedAnalysisType
    {
        get => _selectedAnalysisType;
        set => SetProperty(ref _selectedAnalysisType, value);
    }

    /// <summary>
    /// Generate analysis command
    /// </summary>
    public DelegateCommand GenerateAnalysisCommand { get; private set; } = null!;

    /// <summary>
    /// Whether analysis can be generated
    /// </summary>
    private bool _canGenerateAnalysis = true;
    public bool CanGenerateAnalysis
    {
        get => _canGenerateAnalysis;
        set
        {
            if (SetProperty(ref _canGenerateAnalysis, value))
            {
                GenerateAnalysisCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Fund sort options
    /// </summary>
    public ObservableCollection<string> FundSortOptions { get; } = new()
    {
        "Fund Name",
        "Budget Amount",
        "Actual Amount",
        "Variance"
    };

    /// <summary>
    /// Selected fund sort option
    /// </summary>
    private string? _selectedFundSortOption = "Fund Name";
    public string? SelectedFundSortOption
    {
        get => _selectedFundSortOption;
        set => SetProperty(ref _selectedFundSortOption, value);
    }

    /// <summary>
    /// Fund filter text
    /// </summary>
    private string _fundFilterText = string.Empty;
    public string FundFilterText
    {
        get => _fundFilterText;
        set => SetProperty(ref _fundFilterText, value);
    }

    /// <summary>
    /// Fund grid data
    /// </summary>
    public ObservableCollection<object> FundGridData { get; } = new();

    /// <summary>
    /// Export fund data command
    /// </summary>
    public DelegateCommand ExportFundDataCommand { get; private set; } = null!;

    /// <summary>
    /// Department sort options
    /// </summary>
    public ObservableCollection<string> DepartmentSortOptions { get; } = new()
    {
        "Department Name",
        "Budget Amount",
        "Actual Amount",
        "Variance"
    };

    /// <summary>
    /// Selected department sort option
    /// </summary>
    private string? _selectedDepartmentSortOption = "Department Name";
    public string? SelectedDepartmentSortOption
    {
        get => _selectedDepartmentSortOption;
        set => SetProperty(ref _selectedDepartmentSortOption, value);
    }

    /// <summary>
    /// Department filter text
    /// </summary>
    private string _departmentFilterText = string.Empty;
    public string DepartmentFilterText
    {
        get => _departmentFilterText;
        set => SetProperty(ref _departmentFilterText, value);
    }

    /// <summary>
    /// Department grid data
    /// </summary>
    public ObservableCollection<object> DepartmentGridData { get; } = new();

    /// <summary>
    /// Export department data command
    /// </summary>
    public DelegateCommand ExportDepartmentDataCommand { get; private set; } = null!;

    /// <summary>
    /// Compliance check command
    /// </summary>
    public DelegateCommand CheckComplianceCommand { get; private set; } = null!;

    /// <summary>
    /// Whether compliance check can be performed
    /// </summary>
    private bool _canCheckCompliance = true;
    public bool CanCheckCompliance
    {
        get => _canCheckCompliance;
        set
        {
            if (SetProperty(ref _canCheckCompliance, value))
            {
                CheckComplianceCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Compliance check results
    /// </summary>
    private ObservableCollection<ComplianceIssue> _complianceIssues = new();
    public ObservableCollection<ComplianceIssue> ComplianceIssues
    {
        get => _complianceIssues;
        set => SetProperty(ref _complianceIssues, value);
    }

    /// <summary>
    /// Compliance summary text
    /// </summary>
    private string _complianceSummary = "Click 'Check Compliance' to validate budget against GASB best practices";
    public string ComplianceSummary
    {
        get => _complianceSummary;
        set => SetProperty(ref _complianceSummary, value);
    }

    /// <summary>
    /// Variance threshold
    /// </summary>
    private decimal _varianceThreshold = 0.05m;
    public decimal VarianceThreshold
    {
        get => _varianceThreshold;
        set => SetProperty(ref _varianceThreshold, value);
    }

    /// <summary>
    /// Error message for display
    /// </summary>
    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// Variance sort options
    /// </summary>
    public ObservableCollection<string> VarianceSortOptions { get; } = new()
    {
        "Account Number",
        "Variance Amount",
        "Variance Percent"
    };

    /// <summary>
    /// Selected variance sort option
    /// </summary>
    private string? _selectedVarianceSortOption = "Variance Amount";
    public string? SelectedVarianceSortOption
    {
        get => _selectedVarianceSortOption;
        set => SetProperty(ref _selectedVarianceSortOption, value);
    }

    /// <summary>
    /// Variance filter text
    /// </summary>
    private string _varianceFilterText = string.Empty;
    public string VarianceFilterText
    {
        get => _varianceFilterText;
        set => SetProperty(ref _varianceFilterText, value);
    }

    /// <summary>
    /// Variance hierarchy data
    /// </summary>
    public ObservableCollection<object> VarianceHierarchy { get; } = new();

    /// <summary>
    /// Variance chart data
    /// </summary>
    public ObservableCollection<object> VarianceChartData { get; } = new();

    /// <summary>
    /// Budget distribution data for charts
    /// </summary>
    public ObservableCollection<BudgetDistributionData> BudgetDistributionData { get; } = new();

    /// <summary>
    /// Budget comparison data for charts
    /// </summary>
    public ObservableCollection<BudgetComparisonData> BudgetComparisonData { get; } = new();

    /// <summary>
    /// Account variance for editing
    /// </summary>
    private decimal _accountVariance;
    public decimal AccountVariance
    {
        get => _accountVariance;
        set => SetProperty(ref _accountVariance, value);
    }

    /// <summary>
    /// Key for editing
    /// </summary>
    private string _key = string.Empty;
    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }

    /// <summary>
    /// Value for editing
    /// </summary>
    private decimal _value;
    public decimal Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <summary>
    /// Collection of budgets
    /// </summary>
    public ObservableCollection<OverallBudget> Budgets { get; } = new();

    /// <summary>
    /// Selected budget
    /// </summary>
    private OverallBudget? _selectedBudget;
    public OverallBudget? SelectedBudget
    {
        get => _selectedBudget;
        set => SetProperty(ref _selectedBudget, value);
    }

    /// <summary>
    /// Collection of budget periods
    /// </summary>
    public ObservableCollection<BudgetPeriod> BudgetPeriods { get; } = new();

    /// <summary>
    /// Collection of budget entries
    /// </summary>
    public ObservableCollection<BudgetEntry> BudgetEntries { get; } = new();

    /// <summary>
    /// Trend data for budget analysis
    /// </summary>
    public ObservableCollection<WileyWidget.Models.BudgetTrendItem> TrendData { get; } = new();

    /// <summary>
    /// Analysis results
    /// </summary>
    private BudgetAnalysisResult? _analysis;
    public BudgetAnalysisResult? Analysis
    {
        get => _analysis;
        set => SetProperty(ref _analysis, value);
    }

    /// <summary>
    /// Report export service
    /// </summary>
    private readonly IReportExportService _reportExportService;

    /// <summary>
    /// Budget repository for data access
    /// </summary>
    private readonly IBudgetRepository _budgetRepository;

    /// <summary>
    /// Constructor
    /// </summary>
    public BudgetAnalysisViewModel(IDispatcherHelper dispatcherHelper, Microsoft.Extensions.Logging.ILogger<BudgetAnalysisViewModel> logger, IReportExportService reportExportService, IBudgetRepository budgetRepository)
        : base(dispatcherHelper, logger)
    {
        _reportExportService = reportExportService ?? throw new ArgumentNullException(nameof(reportExportService));
        _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
        InitializeCommands();
    }

    private void InitializeCommands()
    {
        GenerateAnalysisCommand = new DelegateCommand(ExecuteGenerateAnalysis, () => CanGenerateAnalysis);
        ExportFundDataCommand = new DelegateCommand(ExecuteExportFundData);
        ExportDepartmentDataCommand = new DelegateCommand(ExecuteExportDepartmentData);
        CheckComplianceCommand = new DelegateCommand(ExecuteCheckCompliance, () => CanCheckCompliance);
    }

    private async void ExecuteGenerateAnalysis()
    {
        try
        {
            IsBusy = true;
            BusyMessage = "Generating budget analysis...";

            // Generate comprehensive budget analysis
            var result = new BudgetAnalysisResult();

            // Load real budget data for the current fiscal year
            var currentYear = DateTime.Now.Year;
            var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(currentYear);

            // Convert to array for analysis
            var budgetData = budgetEntries.ToArray();

            if (budgetData.Length == 0)
            {
                // Fallback to mock data if no real data available
                budgetData = new[]
                {
                    new BudgetEntry { BudgetedAmount = 100000m, ActualAmount = 95000m, AccountNumber = "101", Description = "Mock Account 1", FiscalYear = currentYear, DepartmentId = 1 },
                    new BudgetEntry { BudgetedAmount = 50000m, ActualAmount = 52000m, AccountNumber = "102", Description = "Mock Account 2", FiscalYear = currentYear, DepartmentId = 1 },
                    new BudgetEntry { BudgetedAmount = 75000m, ActualAmount = 70000m, AccountNumber = "103", Description = "Mock Account 3", FiscalYear = currentYear, DepartmentId = 1 },
                    new BudgetEntry { BudgetedAmount = 25000m, ActualAmount = 24000m, AccountNumber = "104", Description = "Mock Account 4", FiscalYear = currentYear, DepartmentId = 1 }
                };
                Logger.LogWarning("No budget data found for fiscal year {Year}, using mock data", currentYear);
            }

            // Populate overview
            result.Overview.TotalBudget = budgetData.Sum(b => b.BudgetedAmount);
            result.Overview.TotalBalance = budgetData.Sum(b => b.ActualAmount);
            result.Overview.Variance = result.Overview.TotalBudget - result.Overview.TotalBalance;
            result.Overview.TotalAccounts = budgetData.Length;

            // Calculate key ratios
            result.Overview.KeyRatios.Add(new KeyValuePair<string, decimal>("Budget Utilization",
                result.Overview.TotalBudget > 0 ? (result.Overview.TotalBalance / result.Overview.TotalBudget) * 100 : 0));
            result.Overview.KeyRatios.Add(new KeyValuePair<string, decimal>("Average per Account",
                result.Overview.TotalAccounts > 0 ? result.Overview.TotalBudget / result.Overview.TotalAccounts : 0));

            // Populate variance analysis
            var variances = budgetData.Select(b =>
                b.BudgetedAmount > 0 ? ((b.ActualAmount - b.BudgetedAmount) / b.BudgetedAmount) * 100 : 0).ToList();

            result.Variance.AccountsOverThreshold = variances.Count(v => Math.Abs(v) > 10); // Over 10% variance
            result.Variance.AverageVariancePercent = variances.Any() ? variances.Average() : 0;

            Analysis = result;

            // Populate new collections
            Budgets.Clear();
            // Note: OverallBudget loading would need to be implemented based on your data model

            BudgetPeriods.Clear();
            BudgetPeriods.Add(new BudgetPeriod { Year = currentYear, Name = $"FY {currentYear}", Status = BudgetStatus.Adopted });

            BudgetEntries.Clear();
            foreach (var entry in budgetData)
            {
                BudgetEntries.Add(entry);
            }

            // Generate trend data
            TrendData.Clear();
            var trendItem = new WileyWidget.Models.BudgetTrendItem
            {
                Period = $"FY {currentYear}",
                Amount = result.Overview.TotalBudget,
                ProjectedAmount = result.Overview.TotalBalance,
                Category = "Budget vs Actual"
            };
            TrendData.Add(trendItem);

            // Group by fund type for fund analysis
            var fundGroups = budgetData.GroupBy(b => b.FundType.ToString() ?? "Unknown");
            foreach (var group in fundGroups)
            {
                var fundData = new
                {
                    Fund = group.Key,
                    AccountCount = group.Count(),
                    Budget = group.Sum(b => b.BudgetedAmount),
                    Actual = group.Sum(b => b.ActualAmount),
                    Variance = group.Sum(b => b.ActualAmount) - group.Sum(b => b.BudgetedAmount)
                };
                FundGridData.Add(fundData);
            }

            // Group by department for department analysis
            var departmentGroups = budgetData.GroupBy(b => b.DepartmentId.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Unknown");
            foreach (var group in departmentGroups)
            {
                var deptData = new
                {
                    Department = $"Department {group.Key}",
                    Code = group.Key,
                    AccountCount = group.Count(),
                    Budget = group.Sum(b => b.BudgetedAmount),
                    Actual = group.Sum(b => b.ActualAmount),
                    Variance = group.Sum(b => b.ActualAmount) - group.Sum(b => b.BudgetedAmount),
                    Utilization = group.Sum(b => b.BudgetedAmount) > 0 ?
                        (group.Sum(b => b.ActualAmount) / group.Sum(b => b.BudgetedAmount)) * 100 : 0
                };
                DepartmentGridData.Add(deptData);
            }

            // Populate variance chart data
            foreach (var entry in budgetData.Take(10)) // Limit to first 10 for chart readability
            {
                var chartData = new
                {
                    AccountName = entry.AccountNumber ?? "Unknown",
                    Budget = entry.BudgetedAmount,
                    Actual = entry.ActualAmount
                };
                VarianceChartData.Add(chartData);
            }

            // Populate budget distribution data for pie chart
            foreach (var group in fundGroups)
            {
                var totalBudget = budgetData.Sum(b => b.BudgetedAmount);
                var distributionData = new BudgetDistributionData
                {
                    FundType = group.Key,
                    Amount = group.Sum(b => b.BudgetedAmount),
                    Percentage = totalBudget > 0 ? (double)((group.Sum(b => b.BudgetedAmount) / totalBudget) * 100) : 0
                };
                BudgetDistributionData.Add(distributionData);
            }

            // Populate budget comparison data for bar chart
            var categories = new[] { "GeneralFund", "SpecialRevenue", "CapitalProjects", "DebtService" };
            foreach (var category in categories)
            {
                var categoryData = budgetData.Where(b => b.FundType.ToString().Contains(category, StringComparison.OrdinalIgnoreCase));
                var comparisonData = new BudgetComparisonData
                {
                    Category = category,
                    BudgetAmount = categoryData.Sum(b => b.BudgetedAmount),
                    ActualAmount = categoryData.Sum(b => b.ActualAmount)
                };
                BudgetComparisonData.Add(comparisonData);
            }

            Logger.LogInformation("Analysis generated: {AccountCount} accounts analyzed for fiscal year {Year}", result.Overview.TotalAccounts, currentYear);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating budget analysis");
            ErrorMessage = $"Error generating analysis: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async void ExecuteExportFundData()
    {
        try
        {
            if (!FundGridData.Any())
            {
                Logger.LogWarning("No fund data available for export");
                return;
            }

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Fund Data",
                Filter = "Excel files (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = $"Fund_Analysis_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;

                // Export data using report export service
                await _reportExportService.ExportToExcelAsync(FundGridData, filePath);

                Logger.LogInformation("Fund data exported to {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export fund data");
        }
    }

    private async void ExecuteExportDepartmentData()
    {
        try
        {
            if (!DepartmentGridData.Any())
            {
                Logger.LogWarning("No department data available for export");
                return;
            }

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Department Data",
                Filter = "Excel files (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = $"Department_Analysis_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;

                // Export data using report export service
                await _reportExportService.ExportToExcelAsync(DepartmentGridData, filePath);

                Logger.LogInformation("Department data exported to {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export department data");
        }
    }

    private async void ExecuteCheckCompliance()
    {
        try
        {
            IsBusy = true;
            BusyMessage = "Checking budget compliance...";

            // Clear previous results
            ComplianceIssues.Clear();

            // Get budget entries for the current fiscal year
            var currentYear = DateTime.Now.Year;
            var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(currentYear);

            if (!budgetEntries.Any())
            {
                Logger.LogWarning("No budget entries available for compliance check");
                ComplianceSummary = "No budget data available for compliance check";
                return;
            }

            // Perform GASB compliance checks
            var issues = new List<ComplianceIssue>();

            // Check 1: Budget entries must have valid account codes
            var invalidAccountCodes = budgetEntries.Where(e => string.IsNullOrWhiteSpace(e.AccountNumber) || e.AccountNumber.Length < 4);
            foreach (var entry in invalidAccountCodes)
            {
                issues.Add(new ComplianceIssue
                {
                    Rule = "GASB-001",
                    Severity = "Error",
                    Description = $"Invalid account code: '{entry.AccountNumber}'",
                    AccountNumber = entry.AccountNumber,
                    Recommendation = "Account codes must be at least 4 characters long and follow municipal accounting standards",
                    GasbReference = "GASB Statement No. 34"
                });
            }

            // Check 2: Budget amounts must be non-negative
            var negativeAmounts = budgetEntries.Where(e => e.BudgetedAmount < 0);
            foreach (var entry in negativeAmounts)
            {
                issues.Add(new ComplianceIssue
                {
                    Rule = "GASB-002",
                    Severity = "Error",
                    Description = $"Negative budgeted amount: {entry.BudgetedAmount:C}",
                    AccountNumber = entry.AccountNumber,
                    Recommendation = "Budget amounts must be non-negative",
                    GasbReference = "GASB Statement No. 34"
                });
            }

            // Check 3: Department budgets should not exceed fund allocations (simplified check)
            var departmentTotals = budgetEntries.GroupBy(e => e.DepartmentId)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.BudgetedAmount));

            foreach (var dept in departmentTotals.Where(d => d.Value > 1000000m)) // Simplified threshold
            {
                issues.Add(new ComplianceIssue
                {
                    Rule = "GASB-003",
                    Severity = "Warning",
                    Description = $"Department budget ({dept.Value:C}) exceeds recommended threshold",
                    AccountNumber = "Multiple",
                    Recommendation = "Review department budgets to ensure they align with fund allocations",
                    GasbReference = "GASB Statement No. 34"
                });
            }

            // Check 4: Budget entries should have descriptions
            var missingDescriptions = budgetEntries.Where(e => string.IsNullOrWhiteSpace(e.Description));
            foreach (var entry in missingDescriptions)
            {
                issues.Add(new ComplianceIssue
                {
                    Rule = "GASB-004",
                    Severity = "Warning",
                    Description = "Missing budget description",
                    AccountNumber = entry.AccountNumber,
                    Recommendation = "Add descriptive text to explain the purpose of this budget entry",
                    GasbReference = "GASB Statement No. 34"
                });
            }

            // Add all issues to the collection
            foreach (var issue in issues)
            {
                ComplianceIssues.Add(issue);
            }

            // Update compliance summary
            var errorCount = issues.Count(i => i.Severity == "Error");
            var warningCount = issues.Count(i => i.Severity == "Warning");
            ComplianceSummary = $"Found {issues.Count} compliance issue(s): {errorCount} errors, {warningCount} warnings";

            Logger.LogInformation("Compliance check completed with {IssueCount} issues found", issues.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to perform compliance check");
            ComplianceSummary = "Compliance check failed due to an error";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    // IDataErrorInfo implementation
    public string Error => string.Empty;

    public string this[string columnName]
    {
        get
        {
            return columnName switch
            {
                nameof(VarianceThreshold) when VarianceThreshold < 0 => "Variance threshold cannot be negative",
                _ => string.Empty
            };
        }
    }
}

/// <summary>
/// Budget analysis result
/// </summary>
public class BudgetAnalysisResult
{
    /// <summary>
    /// Analysis overview
    /// </summary>
    public BudgetAnalysisOverview Overview { get; } = new();

    /// <summary>
    /// Variance analysis
    /// </summary>
    public BudgetVarianceAnalysis Variance { get; } = new();
}

/// <summary>
/// Budget analysis overview
/// </summary>
public class BudgetAnalysisOverview
{
    /// <summary>
    /// Total budget amount
    /// </summary>
    public decimal TotalBudget { get; set; }

    /// <summary>
    /// Total balance
    /// </summary>
    public decimal TotalBalance { get; set; }

    /// <summary>
    /// Variance amount
    /// </summary>
    public decimal Variance { get; set; }

    /// <summary>
    /// Total accounts
    /// </summary>
    public int TotalAccounts { get; set; }

    /// <summary>
    /// Key ratios
    /// </summary>
    public ObservableCollection<KeyValuePair<string, decimal>> KeyRatios { get; } = new();
}

/// <summary>
/// Budget variance analysis
/// </summary>
public class BudgetVarianceAnalysis
{
    /// <summary>
    /// Number of accounts over threshold
    /// </summary>
    public int AccountsOverThreshold { get; set; }

    /// <summary>
    /// Average variance percent
    /// </summary>
    public decimal AverageVariancePercent { get; set; }
}
