using System;

namespace WileyWidget.Models
{
    /// <summary>
    /// Represents budget data for municipal utility financial analysis.
    /// Contains financial information including allocations, expenditures, and projections.
    /// </summary>
    /// <summary>
    /// Represents a class for budgetdata.
    /// </summary>
    public class BudgetData
    {
        /// <summary>
        /// Gets or sets the enterprise identifier.
        /// </summary>
        /// <summary>
        /// Gets or sets the enterpriseid.
        /// </summary>
        public int EnterpriseId { get; set; }

        /// <summary>
        /// Gets or sets the fiscal year.
        /// </summary>
        /// <summary>
        /// Gets or sets the fiscalyear.
        /// </summary>
        public int FiscalYear { get; set; }

        /// <summary>
        /// Gets or sets the total budgeted amount.
        /// </summary>
        /// <summary>
        /// Gets or sets the totalbudget.
        /// </summary>
        public decimal TotalBudget { get; set; }

        /// <summary>
        /// Gets or sets the total expenditures.
        /// </summary>
        /// <summary>
        /// Gets or sets the totalexpenditures.
        /// </summary>
        public decimal TotalExpenditures { get; set; }

        /// <summary>
        /// Gets or sets the remaining budget.
        /// </summary>
        /// <summary>
        /// Gets or sets the remainingbudget.
        /// </summary>
        public decimal RemainingBudget { get; set; }

        public FundType FundType { get; set; }

        public decimal BudgetedAmount { get; set; }
    }
}
