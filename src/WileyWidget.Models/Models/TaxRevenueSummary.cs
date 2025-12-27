using System.ComponentModel.DataAnnotations;

namespace WileyWidget.Models
{
    /// <summary>
    /// Represents a class for taxrevenuesummary.
    /// </summary>
    public class TaxRevenueSummary
    {
        [Key]
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the prioryearlevy.
        /// </summary>

        public decimal PriorYearLevy { get; set; }
        /// <summary>
        /// Gets or sets the prioryearamount.
        /// </summary>
        public decimal PriorYearAmount { get; set; }
        /// <summary>
        /// Gets or sets the currentyearlevy.
        /// </summary>
        public decimal CurrentYearLevy { get; set; }
        /// <summary>
        /// Gets or sets the currentyearamount.
        /// </summary>
        public decimal CurrentYearAmount { get; set; }
        /// <summary>
        /// Gets or sets the budgetyearlevy.
        /// </summary>
        public decimal BudgetYearLevy { get; set; }
        /// <summary>
        /// Gets or sets the budgetyearamount.
        /// </summary>
        public decimal BudgetYearAmount { get; set; }
        /// <summary>
        /// Gets or sets the incdeclevy.
        /// </summary>
        public decimal IncDecLevy { get; set; }
        /// <summary>
        /// Gets or sets the incdecamount.
        /// </summary>
        public decimal IncDecAmount { get; set; }
    }
}
