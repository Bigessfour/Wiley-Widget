namespace WileyWidget.Models
{
    /// <summary>
    /// Represents a month item for fiscal year selection.
    /// Uses init-only properties for immutable initialization pattern.
    /// </summary>
    public class MonthItem
    {
        /// <summary>
        /// Gets the display name of the month
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets the numeric value of the month (1-12)
        /// </summary>
        public int Value { get; init; }
    }
}
