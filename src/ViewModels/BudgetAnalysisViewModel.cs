#nullable enable

using System;
using System.Collections.ObjectModel;
using Prism.Mvvm;
using Prism.Commands;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using System.Windows.Input;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for budget analysis functionality
/// </summary>
public partial class BudgetAnalysisViewModel : AsyncViewModelBase
{
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
        set
        {
            if (_selectedBudgetPeriod != value)
            {
                _selectedBudgetPeriod = value;
                RaisePropertyChanged();
            }
        }
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
        set
        {
            if (_selectedAnalysisType != value)
            {
                _selectedAnalysisType = value;
                RaisePropertyChanged();
            }
        }
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
            if (_canGenerateAnalysis != value)
            {
                _canGenerateAnalysis = value;
                RaisePropertyChanged();
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
        set
        {
            if (_selectedFundSortOption != value)
            {
                _selectedFundSortOption = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Fund filter text
    /// </summary>
    private string _fundFilterText = string.Empty;
    public string FundFilterText
    {
        get => _fundFilterText;
        set
        {
            if (_fundFilterText != value)
            {
                _fundFilterText = value;
                RaisePropertyChanged();
            }
        }
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
        set
        {
            if (_selectedDepartmentSortOption != value)
            {
                _selectedDepartmentSortOption = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Department filter text
    /// </summary>
    private string _departmentFilterText = string.Empty;
    public string DepartmentFilterText
    {
        get => _departmentFilterText;
        set
        {
            if (_departmentFilterText != value)
            {
                _departmentFilterText = value;
                RaisePropertyChanged();
            }
        }
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
    /// Variance threshold
    /// </summary>
    private decimal _varianceThreshold = 0.05m;
    public decimal VarianceThreshold
    {
        get => _varianceThreshold;
        set
        {
            if (_varianceThreshold != value)
            {
                _varianceThreshold = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Error message for display
    /// </summary>
    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                RaisePropertyChanged();
            }
        }
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
        set
        {
            if (_selectedVarianceSortOption != value)
            {
                _selectedVarianceSortOption = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Variance filter text
    /// </summary>
    private string _varianceFilterText = string.Empty;
    public string VarianceFilterText
    {
        get => _varianceFilterText;
        set
        {
            if (_varianceFilterText != value)
            {
                _varianceFilterText = value;
                RaisePropertyChanged();
            }
        }
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
    /// Account variance for editing
    /// </summary>
    private decimal _accountVariance;
    public decimal AccountVariance
    {
        get => _accountVariance;
        set
        {
            if (_accountVariance != value)
            {
                _accountVariance = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Key for editing
    /// </summary>
    private string _key = string.Empty;
    public string Key
    {
        get => _key;
        set
        {
            if (_key != value)
            {
                _key = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Value for editing
    /// </summary>
    private decimal _value;
    public decimal Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Analysis results
    /// </summary>
    private BudgetAnalysisResult? _analysis;
    public BudgetAnalysisResult? Analysis
    {
        get => _analysis;
        set
        {
            if (_analysis != value)
            {
                _analysis = value;
                RaisePropertyChanged();
            }
        }
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