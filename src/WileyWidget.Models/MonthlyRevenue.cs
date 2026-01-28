namespace WileyWidget.Models
{
    /// <summary>
    /// Represents monthly revenue data for trend analysis and sparkline charts.
    /// </summary>
    public class MonthlyRevenue
    {
        /// <summary>
        /// Month identifier (e.g., "January", "Feb", "Jan 2026").
        /// </summary>
        public string Month { get; set; } = string.Empty;

        /// <summary>
        /// Revenue amount for this month.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Previous month revenue for comparison.
        /// </summary>
        public decimal PreviousMonthAmount { get; set; }

        /// <summary>
        /// Year of this revenue entry.
        /// </summary>
        public int Year { get; set; } = DateTime.Now.Year;

        /// <summary>
        /// Month number (1-12).
        /// </summary>
        public int MonthNumber { get; set; }
    }
}
