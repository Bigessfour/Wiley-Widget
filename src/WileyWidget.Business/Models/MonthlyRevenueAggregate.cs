using System;

namespace WileyWidget.Business.Models
{
    /// <summary>
    /// Repository-returned aggregate for a single calendar month.
    /// </summary>
    public sealed class MonthlyRevenueAggregate
    {
        /// <summary>
        /// First day of the calendar month represented by this aggregate.
        /// </summary>
        public DateTime Month { get; set; }

        /// <summary>
        /// Total revenue amount for the month.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Number of transactions included in the month's aggregate.
        /// </summary>
        public int TransactionCount { get; set; }
    }
}
