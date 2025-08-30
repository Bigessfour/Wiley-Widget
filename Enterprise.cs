using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Wiley_Widget
{
    public class Enterprise : IValidatableObject
    {
                [Range(0.01, 1000, ErrorMessage = "Rates can't be zero—citizens ain't free!")]
        public decimal CurrentRate { get; set; }

        public decimal MonthlyRevenue { get; set; }
        public decimal MonthlyExpenses { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (MonthlyExpenses > MonthlyRevenue)
                yield return new ValidationResult("Warning: Deficit alert!", new[] { nameof(MonthlyExpenses) });
            if (CurrentRate <= 0)
                yield return new ValidationResult("Rate must be positive!", new[] { nameof(CurrentRate) });
        }
    }
}
