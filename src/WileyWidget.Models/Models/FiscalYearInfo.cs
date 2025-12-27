namespace WileyWidget.Models
{
    /// <summary>
    /// Represents information about a fiscal year
    /// </summary>
    /// <summary>
    /// Represents a class for fiscalyearinfo.
    /// </summary>
    public class FiscalYearInfo
    {
        /// <summary>
        /// Gets or sets the fiscal year number
        /// </summary>
        /// <summary>
        /// Gets or sets the year.
        /// </summary>
        public int Year { get; set; }

        /// <summary>
        /// Gets or sets the start date of the fiscal year
        /// </summary>
        /// <summary>
        /// Gets or sets the startdate.
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Gets or sets the end date of the fiscal year
        /// </summary>
        /// <summary>
        /// Gets or sets the enddate.
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Gets the display name for the fiscal year
        /// </summary>
        public string DisplayName => $"FY {Year}";
    }
}
