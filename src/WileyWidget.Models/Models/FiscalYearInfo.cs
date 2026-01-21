namespace WileyWidget.Models
{
    /// <summary>
    /// Represents information about a fiscal year.
    /// Uses init-only properties for immutable initialization pattern.
    /// </summary>
    public class FiscalYearInfo
    {
        /// <summary>
        /// Gets the fiscal year number
        /// </summary>
        public int Year { get; init; }

        /// <summary>
        /// Gets the start date of the fiscal year
        /// </summary>
        public DateTime StartDate { get; init; }

        /// <summary>
        /// Gets the end date of the fiscal year
        /// </summary>
        public DateTime EndDate { get; init; }

        /// <summary>
        /// Gets the display name for the fiscal year
        /// </summary>
        public string DisplayName => $"FY {Year}";

        /// <summary>
        /// Creates a FiscalYearInfo from a given date.
        /// Assumes fiscal year starts July 1st (adjust if needed for your municipality).
        /// </summary>
        public static FiscalYearInfo FromDateTime(DateTime date)
        {
            // Most municipalities use July 1 - June 30 fiscal year
            // If the date is before July, it's in the fiscal year that started last year
            // If the date is July or later, it's in the fiscal year that started this year
            int fiscalYear;
            DateTime fiscalYearStart;
            DateTime fiscalYearEnd;

            if (date.Month < 7)
            {
                // January-June: FY started last year
                fiscalYear = date.Year;
                fiscalYearStart = new DateTime(date.Year - 1, 7, 1);
                fiscalYearEnd = new DateTime(date.Year, 6, 30);
            }
            else
            {
                // July-December: FY started this year
                fiscalYear = date.Year + 1;
                fiscalYearStart = new DateTime(date.Year, 7, 1);
                fiscalYearEnd = new DateTime(date.Year + 1, 6, 30);
            }

            return new FiscalYearInfo
            {
                Year = fiscalYear,
                StartDate = fiscalYearStart,
                EndDate = fiscalYearEnd
            };
        }
    }
}
