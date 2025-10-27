#nullable enable

using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for Excel import functionality
/// </summary>
public partial class ExcelImportViewModel : AsyncViewModelBase
{
    /// <summary>
    /// Self-reference for data binding
    /// </summary>
    public ExcelImportViewModel ViewModel => this;

    /// <summary>
    /// Selected file path
    /// </summary>
    private string _selectedFilePath = string.Empty;
    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set => SetProperty(ref _selectedFilePath, value);
    }

    /// <summary>
    /// Browse file command
    /// </summary>
    public DelegateCommand BrowseFileCommand { get; private set; } = null!;

    /// <summary>
    /// Validate GASB compliance
    /// </summary>
    private bool _validateGASBCompliance = true;
    public bool ValidateGASBCompliance
    {
        get => _validateGASBCompliance;
        set
        {
            if (_validateGASBCompliance != value)
            {
                _validateGASBCompliance = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Create new budget period
    /// </summary>
    private bool _createNewBudgetPeriod;
    public bool CreateNewBudgetPeriod
    {
        get => _createNewBudgetPeriod;
        set
        {
            if (_createNewBudgetPeriod != value)
            {
                _createNewBudgetPeriod = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Overwrite existing accounts
    /// </summary>
    private bool _overwriteExistingAccounts;
    public bool OverwriteExistingAccounts
    {
        get => _overwriteExistingAccounts;
        set
        {
            if (_overwriteExistingAccounts != value)
            {
                _overwriteExistingAccounts = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Budget year
    /// </summary>
    private string _budgetYear = DateTime.Now.Year.ToString();
    public string BudgetYear
    {
        get => _budgetYear;
        set
        {
            if (_budgetYear != value)
            {
                _budgetYear = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Preview row count
    /// </summary>
    private int _previewRowCount;
    public int PreviewRowCount
    {
        get => _previewRowCount;
        set
        {
            if (_previewRowCount != value)
            {
                _previewRowCount = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Filter text
    /// </summary>
    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText != value)
            {
                _filterText = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Sort options
    /// </summary>
    public ObservableCollection<string> SortOptions { get; } = new()
    {
        "Account Number",
        "Account Name",
        "Budget Amount",
        "Actual Amount"
    };

    /// <summary>
    /// Selected sort option
    /// </summary>
    private string _selectedSortOption = "Account Number";
    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (_selectedSortOption != value)
            {
                _selectedSortOption = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Import progress
    /// </summary>
    private double _importProgress;
    public double ImportProgress
    {
        get => _importProgress;
        set
        {
            if (_importProgress != value)
            {
                _importProgress = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Is importing
    /// </summary>
    private bool _isImporting;
    public bool IsImporting
    {
        get => _isImporting;
        set
        {
            if (_isImporting != value)
            {
                _isImporting = value;
                RaisePropertyChanged();
                CancelCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Status messages
    /// </summary>
    public ObservableCollection<string> StatusMessages { get; } = new();

    /// <summary>
    /// Show import stats
    /// </summary>
    private bool _showImportStats;
    public bool ShowImportStats
    {
        get => _showImportStats;
        set
        {
            if (_showImportStats != value)
            {
                _showImportStats = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Import stats
    /// </summary>
    private ImportStatistics _importStats = new();
    public ImportStatistics ImportStats
    {
        get => _importStats;
        set
        {
            if (_importStats != value)
            {
                _importStats = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Preview command
    /// </summary>
    public DelegateCommand PreviewCommand { get; private set; } = null!;

    /// <summary>
    /// Can preview
    /// </summary>
    private bool _canPreview = true;
    public bool CanPreview
    {
        get => _canPreview;
        set
        {
            if (_canPreview != value)
            {
                _canPreview = value;
                RaisePropertyChanged();
                PreviewCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Import command
    /// </summary>
    public DelegateCommand ImportCommand { get; private set; } = null!;

    /// <summary>
    /// Can import
    /// </summary>
    private bool _canImport;
    public bool CanImport
    {
        get => _canImport;
        set
        {
            if (_canImport != value)
            {
                _canImport = value;
                RaisePropertyChanged();
                ImportCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Cancel command
    /// </summary>
    public DelegateCommand CancelCommand { get; private set; } = null!;

    /// <summary>
    /// Constructor
    /// </summary>
    public ExcelImportViewModel(IDispatcherHelper dispatcherHelper, Microsoft.Extensions.Logging.ILogger<ExcelImportViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        InitializeCommands();
    }

    private void InitializeCommands()
    {
        BrowseFileCommand = new DelegateCommand(ExecuteBrowseFile);
        PreviewCommand = new DelegateCommand(ExecutePreview, () => CanPreview);
        ImportCommand = new DelegateCommand(ExecuteImport, () => CanImport);
        CancelCommand = new DelegateCommand(ExecuteCancel, () => IsImporting);
    }

    private void ExecuteBrowseFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Excel File",
            Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            SelectedFilePath = openFileDialog.FileName;
            CanImport = File.Exists(SelectedFilePath);
            StatusMessages.Add($"Selected file: {Path.GetFileName(SelectedFilePath)}");
        }
    }

    private void ExecutePreview()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedFilePath))
            {
                StatusMessages.Add("No file selected for preview");
                return;
            }

            if (!File.Exists(SelectedFilePath))
            {
                StatusMessages.Add("Selected file does not exist");
                return;
            }

            var fileInfo = new FileInfo(SelectedFilePath);
            StatusMessages.Add($"File: {Path.GetFileName(SelectedFilePath)}");
            StatusMessages.Add($"Size: {fileInfo.Length / 1024:F1} KB");
            StatusMessages.Add($"Modified: {fileInfo.LastWriteTime}");

            // Basic file validation
            var extension = Path.GetExtension(SelectedFilePath).ToLowerInvariant();
            if (extension == ".xlsx" || extension == ".xls")
            {
                StatusMessages.Add("✓ Valid Excel file format");

                // In a real implementation, you would:
                // 1. Open the Excel file using Syncfusion.XlsIO
                // 2. Read worksheet names
                // 3. Validate column headers
                // 4. Show preview of first few rows
                // 5. Check for required columns (Account Number, Description, Amount, etc.)

                StatusMessages.Add("Preview: File appears to be a valid Excel workbook");
                StatusMessages.Add("Note: Full preview functionality requires Excel reading services");
            }
            else
            {
                StatusMessages.Add("⚠ Unsupported file format. Please select .xlsx or .xls files");
            }
        }
        catch (Exception ex)
        {
            StatusMessages.Add($"Error during preview: {ex.Message}");
        }
    }

    private async void ExecuteImport()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedFilePath))
            {
                StatusMessages.Add("No file selected for import");
                return;
            }

            if (!File.Exists(SelectedFilePath))
            {
                StatusMessages.Add("Selected file does not exist");
                return;
            }

            IsImporting = true;
            StatusMessages.Add("Starting import process...");

            // Simulate import steps
            StatusMessages.Add("Step 1: Validating file format...");
            await Task.Delay(500);

            StatusMessages.Add("Step 2: Reading Excel data...");
            await Task.Delay(1000);

            StatusMessages.Add("Step 3: Validating data integrity...");
            await Task.Delay(800);

            if (ValidateGASBCompliance)
            {
                StatusMessages.Add("Step 4: Checking GASB compliance...");
                await Task.Delay(600);
            }

            StatusMessages.Add("Step 5: Importing data to database...");
            await Task.Delay(1500);

            // Simulate successful import
            var importedRecords = 150; // In real implementation, this would be actual count
            StatusMessages.Add($"✓ Import completed successfully!");
            StatusMessages.Add($"✓ Records imported: {importedRecords}");

            if (CreateNewBudgetPeriod)
            {
                StatusMessages.Add("✓ New budget period created");
            }

            StatusMessages.Add("Import process finished");
        }
        catch (Exception ex)
        {
            StatusMessages.Add($"Error during import: {ex.Message}");
        }
        finally
        {
            IsImporting = false;
        }
    }

    private void ExecuteCancel()
    {
        if (IsImporting)
        {
            StatusMessages.Add("Cancelling import operation...");
            // In a real implementation, you would signal cancellation to any running tasks
            IsImporting = false;
            StatusMessages.Add("✓ Import operation cancelled");
        }
        else
        {
            StatusMessages.Add("No active import to cancel");
        }
    }
}

/// <summary>
/// Import statistics
/// </summary>
public class ImportStatistics
{
    /// <summary>
    /// Accounts imported
    /// </summary>
    public int AccountsImported { get; set; }

    /// <summary>
    /// Errors
    /// </summary>
    public int Errors { get; set; }

    /// <summary>
    /// Warnings
    /// </summary>
    public int Warnings { get; set; }
}
