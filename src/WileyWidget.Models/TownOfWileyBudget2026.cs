namespace WileyWidget.Models
{
    /// <summary>
    /// Represents budget data for the Town of Wiley fiscal year 2026.
    /// Stores financial information including prior year actuals, current year estimates,
    /// and year-to-date actual amounts across various funds, departments, and account codes.
    /// </summary>
    public class TownOfWileyBudget2026
    {
        /// <summary>
        /// Primary key identifier for this budget record.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Source file name or reference from which this budget data was imported or generated.
        /// </summary>
        public string? SourceFile { get; set; }

        /// <summary>
        /// Fund or department name/code (e.g., "General Fund", "Water Department").
        /// </summary>
        public string? FundOrDepartment { get; set; }

        /// <summary>
        /// Account code for this budget line (e.g., "4100", "5200").
        /// </summary>
        public string? AccountCode { get; set; }

        /// <summary>
        /// Description of the budget line item.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Actual amount from the prior fiscal year.
        /// </summary>
        public decimal? PriorYearActual { get; set; }

        /// <summary>
        /// Actual amount through seven months of the current fiscal year.
        /// </summary>
        public decimal? SevenMonthActual { get; set; }

        /// <summary>
        /// Estimate for the entire current fiscal year.
        /// </summary>
        public decimal? EstimateCurrentYr { get; set; }

        /// <summary>
        /// Budgeted amount for the target fiscal year (2026).
        /// </summary>
        public decimal? BudgetYear { get; set; }

        /// <summary>
        /// Actual year-to-date amount (used for data from Sanitation image data and other sources).
        /// </summary>
        public decimal? ActualYTD { get; set; }

        /// <summary>
        /// Remaining budget available (typically BudgetYear - ActualYTD).
        /// </summary>
        public decimal? Remaining { get; set; }

        /// <summary>
        /// Percentage of budget utilized (0-100).
        /// </summary>
        public int? PercentOfBudget { get; set; }

        /// <summary>
        /// Budget category classification (e.g., "Revenue", "Expense").
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Intelligently mapped department/fund category based on Description analysis.
        /// Values: "Water", "Sewer", "Trash", "Capital Projects", "Administration", or "Unmapped".
        /// This column is populated by database migration logic.
        /// </summary>
        public string? MappedDepartment { get; set; }
    }
}
