namespace WileyWidget.Models;

/// <summary>
/// Options for budget import operations
/// </summary>
/// <summary>
/// Represents a class for budgetimportoptions.
/// </summary>
public class BudgetImportOptions
{
    /// <summary>
    /// Gets or sets the overwriteexisting.
    /// </summary>
    public bool OverwriteExisting { get; set; }
    /// <summary>
    /// Gets or sets the validatedata.
    /// </summary>
    public bool ValidateData { get; set; } = true;
    public string? DefaultFundType { get; set; }
    public int? FiscalYear { get; set; }

    // Additional properties for GASB compliance and import operations
    /// <summary>
    /// Gets or sets the validategasbcompliance.
    /// </summary>
    public bool ValidateGASBCompliance { get; set; } = true;
    /// <summary>
    /// Gets or sets the previewonly.
    /// </summary>
    public bool PreviewOnly { get; set; }
    /// <summary>
    /// Gets or sets the createnewbudgetperiod.
    /// </summary>
    public bool CreateNewBudgetPeriod { get; set; }
    /// <summary>
    /// Gets or sets the overwriteexistingaccounts.
    /// </summary>
    public bool OverwriteExistingAccounts { get; set; }
    public int? BudgetYear { get; set; }
}

/// <summary>
/// Progress tracking for import operations
/// </summary>
/// <summary>
/// Represents a class for importprogress.
/// </summary>
public class ImportProgress
{
    /// <summary>
    /// Gets or sets the totalrows.
    /// </summary>
    public int TotalRows { get; set; }
    /// <summary>
    /// Gets or sets the processedrows.
    /// </summary>
    public int ProcessedRows { get; set; }
    /// <summary>
    /// Gets or sets the successfulrows.
    /// </summary>
    public int SuccessfulRows { get; set; }
    /// <summary>
    /// Gets or sets the failedrows.
    /// </summary>
    public int FailedRows { get; set; }
    public string? CurrentOperation { get; set; }
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets the percentage of completion (0-100)
    /// </summary>
    public int PercentComplete => TotalRows > 0 ? (ProcessedRows * 100) / TotalRows : 0;
}
