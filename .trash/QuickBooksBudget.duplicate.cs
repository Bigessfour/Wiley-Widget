using System;

namespace WileyWidget.Services.Models
{
    /// <summary>
    /// Lightweight representation of a QuickBooks budget suitable for syncing, display and tests.
    /// Keeps a small, stable surface area so we avoid direct dependency on Intuit SDK types.
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
