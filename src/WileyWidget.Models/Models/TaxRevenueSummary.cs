using System.ComponentModel.DataAnnotations;

namespace WileyWidget.Models
{
    public class TaxRevenueSummary
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public decimal PriorYearLevy { get; set; }
        public decimal PriorYearAmount { get; set; }
        public decimal CurrentYearLevy { get; set; }
        public decimal CurrentYearAmount { get; set; }
        public decimal BudgetYearLevy { get; set; }
        public decimal BudgetYearAmount { get; set; }
        public decimal IncDecLevy { get; set; }
        public decimal IncDecAmount { get; set; }
    }
}
