namespace WileyWidget.Models;

/// <summary>
/// Options for budget import operations
/// </summary>
public class BudgetImportOptions
{
    public bool OverwriteExisting { get; set; }
    public bool ValidateData { get; set; } = true;
    public string? DefaultFundType { get; set; }
    public int? FiscalYear { get; set; }
    
    // Additional properties for GASB compliance and import operations
    public bool ValidateGASBCompliance { get; set; } = true;
    public bool PreviewOnly { get; set; }
    public bool CreateNewBudgetPeriod { get; set; }
    public bool OverwriteExistingAccounts { get; set; }
    public int? BudgetYear { get; set; }
}

/// <summary>
/// Progress tracking for import operations
/// </summary>
public class ImportProgress
{
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int SuccessfulRows { get; set; }
    public int FailedRows { get; set; }
    public string? CurrentOperation { get; set; }
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// Gets the percentage of completion (0-100)
    /// </summary>
    public int PercentComplete => TotalRows > 0 ? (ProcessedRows * 100) / TotalRows : 0;
}
