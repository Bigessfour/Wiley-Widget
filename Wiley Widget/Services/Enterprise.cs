using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Wiley_Widget
{
    /// <summary>
    /// Simplified enterprise model used in legacy or test contexts.
    /// Prefer using WileyWidget.Models.Enterprise for full domain behavior.
    /// </summary>
    public class Enterprise : IValidatableObject
    {
        /// <summary>Current rate value (must be positive).</summary>
        [Range(0.01, 1000, ErrorMessage = "Rates can't be zero—citizens ain't free!")]
        public decimal CurrentRate { get; set; }

        /// <summary>Computed or assigned monthly revenue.</summary>
        public decimal MonthlyRevenue { get; set; }
        /// <summary>Total monthly expenses.</summary>
        public decimal MonthlyExpenses { get; set; }
    /// <summary>
    /// Performs custom validation for the enterprise financial state. Emits a warning when
    /// expenses exceed revenue and ensures the current rate is positive.
    /// </summary>
    /// <param name="context">Validation context supplied by the runtime.</param>
    /// <returns>Enumeration of <see cref="ValidationResult"/> detailing any validation issues.</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (MonthlyExpenses > MonthlyRevenue)
                yield return new ValidationResult("Warning: Deficit alert!", new[] { nameof(MonthlyExpenses) });
            if (CurrentRate <= 0)
                yield return new ValidationResult("Rate must be positive!", new[] { nameof(CurrentRate) });
        }
    }
}
