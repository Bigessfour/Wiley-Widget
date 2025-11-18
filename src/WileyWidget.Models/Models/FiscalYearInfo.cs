namespace WileyWidget.Models
{
    /// <summary>
    /// Represents information about a fiscal year
    /// </summary>
    public class FiscalYearInfo
    {
        /// <summary>
        /// Gets or sets the fiscal year number
        /// </summary>
        public int Year { get; set; }

        /// <summary>
        /// Gets or sets the start date of the fiscal year
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Gets or sets the end date of the fiscal year
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Gets the display name for the fiscal year
        /// </summary>
        public string DisplayName => $"FY {Year}";
    }
}
