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

        /// <summary>
        /// Creates a FiscalYearInfo from a given date.
        /// Defaults to July 1st fiscal year start if not specified.
        /// </summary>
        /// <param name="date">The date to determine the fiscal year for</param>
        /// <param name="startMonth">The month the fiscal year starts (default 7 for July)</param>
        /// <returns>A new FiscalYearInfo instance</returns>
        public static FiscalYearInfo FromDateTime(DateTime date, int startMonth = 7)
        {
            // If the date is before the start month, it's in the fiscal year that started last year
            // If the date is the start month or later, it's in the fiscal year that started this year
            int fiscalYear;
            DateTime fiscalYearStart;
            DateTime fiscalYearEnd;

            if (date.Month < startMonth)
            {
                // Before start month: FY started last year
                fiscalYear = date.Year;
                fiscalYearStart = new DateTime(date.Year - 1, startMonth, 1);
                fiscalYearEnd = new DateTime(date.Year, startMonth, 1).AddDays(-1);
            }
            else
            {
                // Start month or later: FY started this year
                fiscalYear = date.Year + 1;
                fiscalYearStart = new DateTime(date.Year, startMonth, 1);
                fiscalYearEnd = new DateTime(date.Year + 1, startMonth, 1).AddDays(-1);
            }

            return new FiscalYearInfo
            {
                Year = fiscalYear,
                StartDate = fiscalYearStart,
                EndDate = fiscalYearEnd
            };
        }

        /// <summary>
        /// Creates a FiscalYearInfo for a specific fiscal year number.
        /// </summary>
        /// <param name="fiscalYear">The fiscal year number (e.g., 2025)</param>
        /// <param name="startMonth">The month the fiscal year starts (default 7 for July)</param>
        /// <returns>A new FiscalYearInfo instance</returns>
        public static FiscalYearInfo ForYear(int fiscalYear, int startMonth = 7)
        {
            var fiscalYearStart = new DateTime(fiscalYear - 1, startMonth, 1);
            var fiscalYearEnd = new DateTime(fiscalYear, startMonth, 1).AddDays(-1);

            return new FiscalYearInfo
            {
                Year = fiscalYear,
                StartDate = fiscalYearStart,
                EndDate = fiscalYearEnd
            };
        }
    }
}
