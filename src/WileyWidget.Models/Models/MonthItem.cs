namespace WileyWidget.Models
{
    /// <summary>
    /// Represents a month item for fiscal year selection
    /// </summary>
    /// <summary>
    /// Represents a class for monthitem.
    /// </summary>
    public class MonthItem
    {
        /// <summary>
        /// Gets or sets the display name of the month
        /// </summary>
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the numeric value of the month (1-12)
        /// </summary>
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public int Value { get; set; }
    }
}
