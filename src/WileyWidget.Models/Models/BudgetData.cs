using System;

namespace WileyWidget.Models
{
    /// <summary>
    /// Represents budget data for municipal utility financial analysis.
    /// Contains financial information including allocations, expenditures, and projections.
    /// </summary>
    public class BudgetData
    {
        /// <summary>
        /// Gets or sets the enterprise identifier.
        /// </summary>
        public int EnterpriseId { get; set; }

        /// <summary>
        /// Gets or sets the fiscal year.
        /// </summary>
        public int FiscalYear { get; set; }

        /// <summary>
        /// Gets or sets the total budgeted amount.
        /// </summary>
        public decimal TotalBudget { get; set; }

        /// <summary>
        /// Gets or sets the total expenditures.
        /// </summary>
        public decimal TotalExpenditures { get; set; }

        /// <summary>
        /// Gets or sets the remaining budget.
        /// </summary>
        public decimal RemainingBudget { get; set; }
    }
}