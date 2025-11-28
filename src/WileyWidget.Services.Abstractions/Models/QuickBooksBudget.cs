using System;

namespace WileyWidget.Services.Abstractions.Models
{
    /// <summary>
    /// Minimal DTO representing a QuickBooks budget for use by service interfaces.
    /// This type lives in the Abstractions project so the service contract remains independent
    /// from the concrete QuickBooks SDK types.
    /// </summary>
    public sealed class QuickBooksBudget
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public decimal? TotalAmount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Notes { get; set; }
    }
}
